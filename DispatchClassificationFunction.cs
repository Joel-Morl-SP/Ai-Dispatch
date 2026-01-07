using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;
using Ai_Dispatch.Services;
using Ai_Dispatch.Services.Classification;

namespace Ai_Dispatch;

public class DispatchClassificationFunction
{
    private readonly ILogger<DispatchClassificationFunction> _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LoggingService _loggingService;
    private readonly IConnectWiseService _connectWiseService;
    private readonly TeamsAlertService _teamsAlertService;
    private readonly ProposedNoteService _proposedNoteService;
    private readonly string _baseModel;
    private readonly string _reasoningModel;
    private readonly IRequestValidator _requestValidator;
    private readonly ITicketRequestLogger _ticketRequestLogger;
    private readonly IResponseBuilder _responseBuilder;
    private readonly ILoggerFactory _loggerFactory;

    public DispatchClassificationFunction(
        ILogger<DispatchClassificationFunction> logger,
        AzureOpenAIService openAIService,
        LoggingService loggingService,
        IConnectWiseService connectWiseService,
        TeamsAlertService teamsAlertService,
        ProposedNoteService proposedNoteService,
        IConfiguration configuration,
        IRequestValidator requestValidator,
        ITicketRequestLogger ticketRequestLogger,
        IResponseBuilder responseBuilder,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _openAIService = openAIService;
        _loggingService = loggingService;
        _connectWiseService = connectWiseService;
        _teamsAlertService = teamsAlertService;
        _proposedNoteService = proposedNoteService;
        _baseModel = configuration["AZURE_BASE_MODEL"] ?? throw new InvalidOperationException("AZURE_BASE_MODEL is required");
        _reasoningModel = configuration["AZURE_REASONING_MODEL"] ?? throw new InvalidOperationException("AZURE_REASONING_MODEL is required");
        _requestValidator = requestValidator;
        _ticketRequestLogger = ticketRequestLogger;
        _responseBuilder = responseBuilder;
        _loggerFactory = loggerFactory;
    }

    [Function("DispatchClassificationFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var context = new TicketClassificationContext
        {
            Request = req
        };

        try
        {
            _logger.LogInformation("DispatchClassificationFunction started - Processing new ticket request");
            
            var ticketRequest = await _requestValidator.ValidateAndDeserializeRequestAsync(req);
            if (ticketRequest == null)
            {
                return await _requestValidator.BuildBadRequestResponseAsync(req);
            }

            ticketRequest = InputBuilderService.CleanSummary(ticketRequest);
            context.TicketRequest = ticketRequest;
            _ticketRequestLogger.LogTicketRequestDetails(context);

            context.IsNoCompany = ticketRequest.CompanyId == ConnectWiseConstants.NoCompanyId;
            context.IsNoCompanyBranch = context.IsNoCompany;

            if (context.IsNoCompany)
            {
                IClassificationStep noCompanySpamClassifier = new NoCompanySpamClassifier(_logger, _openAIService, _loggingService, _connectWiseService, _baseModel);
                var noCompanyResponse = await noCompanySpamClassifier.ExecuteAsync(context);
                if (noCompanyResponse != null)
                {
                    return noCompanyResponse;
                }
                // No-company branch should always return early - if we reach here, it's an error
                _logger.LogError("No-company branch did not return early - this should never happen - TicketId: {TicketId}", context.TicketRequest.TicketId);
                return await _responseBuilder.HandleErrorAsync(context, new InvalidOperationException("No-company branch did not return early"));
            }
            else
            {
                IClassificationStep companyTicketTypeClassifier = new CompanyTicketTypeClassifier(_logger, _openAIService, _loggingService, _connectWiseService, _baseModel);
                var companyResponse = await companyTicketTypeClassifier.ExecuteAsync(context);
                if (companyResponse != null)
                {
                    return companyResponse;
                }

                IClassificationStep boardRoutingClassifier = new BoardRoutingClassifier(_logger, _openAIService, _loggingService, _connectWiseService, _reasoningModel);
                var boardResponse = await boardRoutingClassifier.ExecuteAsync(context);
                if (boardResponse != null)
                {
                    return boardResponse;
                }

                IClassificationStep tsiClassifier = new TSIClassifier(_logger, _openAIService, _loggingService, _reasoningModel);
                await tsiClassifier.ExecuteAsync(context);

                IClassificationStep summaryGenerator = new SummaryGenerator(_logger, _openAIService, _loggingService, _baseModel);
                await summaryGenerator.ExecuteAsync(context);

                var companyTicketFinalizer = new CompanyTicketFinalizer(
                    _logger,
                    new ContactLookup(_loggerFactory.CreateLogger<ContactLookup>(), _connectWiseService),
                    new TicketUpdateBuilder(),
                    new TicketUpdater(_loggerFactory.CreateLogger<TicketUpdater>(), _connectWiseService),
                    new ProposedNoteProcessor(_loggerFactory.CreateLogger<ProposedNoteProcessor>(), _proposedNoteService),
                    _responseBuilder);
                
                IClassificationStep finalizer = companyTicketFinalizer;
                var finalizerResponse = await finalizer.ExecuteAsync(context);
                if (finalizerResponse != null)
                {
                    return finalizerResponse;
                }
                // This should never happen - CompanyTicketFinalizer always returns a response
                _logger.LogError("CompanyTicketFinalizer returned null - this should never happen - TicketId: {TicketId}", context.TicketRequest.TicketId);
                return await _responseBuilder.HandleErrorAsync(context, new InvalidOperationException("CompanyTicketFinalizer returned null"));
            }
        }
        catch (Exception ex)
        {
            return await _responseBuilder.HandleErrorAsync(context, ex);
        }
    }


    public class TicketClassificationContext
    {
        public TicketRequest TicketRequest { get; set; } = null!;
        public HttpRequestData Request { get; set; } = null!;
        public SpamClassificationResponse? SpamResponse { get; set; }
        public TicketClassificationResponse? TicketResponse { get; set; }
        public BoardRoutingResponse? BoardResponse { get; set; }
        public TSIClassificationResponse? TsiResponse { get; set; }
        public SummaryResponse? SummaryResponse { get; set; }
        public int SpamConfidence { get; set; }
        public int BoardConfidence { get; set; }
        public string? InitialIntent { get; set; }
        public bool IsNoCompany { get; set; }
        public bool IsNoCompanyBranch { get; set; }
        public ContactResponse? Contact { get; set; }
        public bool IsVip { get; set; }
        public string? FinalSummary { get; set; }
        public string? FinalNote { get; set; }
        public TicketUpdateRequest? TicketUpdate { get; set; }
        public bool ProposedNoteSent { get; set; }
    }
}
