using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
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

    public DispatchClassificationFunction(
        ILogger<DispatchClassificationFunction> logger,
        AzureOpenAIService openAIService,
        LoggingService loggingService,
        IConnectWiseService connectWiseService,
        TeamsAlertService teamsAlertService,
        ProposedNoteService proposedNoteService,
        IConfiguration configuration)
    {
        _logger = logger;
        _openAIService = openAIService;
        _loggingService = loggingService;
        _connectWiseService = connectWiseService;
        _teamsAlertService = teamsAlertService;
        _proposedNoteService = proposedNoteService;
        _baseModel = configuration["AZURE_BASE_MODEL"] ?? throw new InvalidOperationException("AZURE_BASE_MODEL is required");
        _reasoningModel = configuration["AZURE_REASONING_MODEL"] ?? throw new InvalidOperationException("AZURE_REASONING_MODEL is required");
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
            
            var ticketRequest = await ValidateAndDeserializeRequestAsync(req);
            if (ticketRequest == null)
            {
                return await BuildBadRequestResponseAsync(req);
            }

            ticketRequest = InputBuilderService.CleanSummary(ticketRequest);
            context.TicketRequest = ticketRequest;
            LogTicketRequestDetails(context);

            context.IsNoCompany = ticketRequest.CompanyId == ConnectWiseConstants.NoCompanyId;
            context.IsNoCompanyBranch = context.IsNoCompany;

            if (context.IsNoCompany)
            {
                var noCompanySpamClassifier = new NoCompanySpamClassifier(_logger, _openAIService, _loggingService, _connectWiseService, _baseModel);
                var noCompanyResponse = await noCompanySpamClassifier.ExecuteAsync(context);
                if (noCompanyResponse != null)
                {
                    return noCompanyResponse;
                }
            }
            else
            {
                var companyTicketTypeClassifier = new CompanyTicketTypeClassifier(_logger, _openAIService, _loggingService, _connectWiseService, _baseModel);
                var companyResponse = await companyTicketTypeClassifier.ExecuteAsync(context);
                if (companyResponse != null)
                {
                    return companyResponse;
                }
            }

            var boardRoutingClassifier = new BoardRoutingClassifier(_logger, _openAIService, _loggingService, _reasoningModel);
            await boardRoutingClassifier.ExecuteAsync(context);

            await CreateClassificationActivitiesAsync(context);

            if (context.BoardConfidence < 90)
            {
                return await HandleLowBoardConfidenceAsync(context);
            }

            if (context.BoardConfidence >= 90 && NoteBuilderService.IsNonServiceBoard(context.BoardResponse?.BoardId))
            {
                return await HandleNonServiceBoardAsync(context);
            }

            var tsiClassifier = new TSIClassifier(_logger, _openAIService, _loggingService, _reasoningModel);
            await tsiClassifier.ExecuteAsync(context);

            var summaryGenerator = new SummaryGenerator(_logger, _openAIService, _loggingService, _baseModel);
            await summaryGenerator.ExecuteAsync(context);

            await LookupContactAsync(context);

            context.FinalSummary = NoteBuilderService.BuildSummary(context.SummaryResponse!.SubmittedFor, context.SummaryResponse.NewSummary ?? string.Empty, context.IsVip);
            context.FinalNote = NoteBuilderService.BuildNote(context.SpamResponse, context.BoardResponse, context.TsiResponse, context.SpamConfidence, context.BoardConfidence, context.InitialIntent);

            context.TicketUpdate = BuildTicketUpdateRequest(context);

            await UpdateTicketAndAddNoteAsync(context);

            await ProcessProposedNoteAsync(context);

            return await BuildSuccessResponseAsync(context);
        }
        catch (Exception ex)
        {
            return await HandleErrorAsync(context, ex);
        }
    }

    private async Task<TicketRequest?> ValidateAndDeserializeRequestAsync(HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        var ticketRequest = JsonSerializer.Deserialize<TicketRequest>(requestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (ticketRequest == null)
        {
            _logger.LogWarning("Invalid request body - ticketRequest is null after deserialization");
            return null;
        }

        return ticketRequest;
    }

    private async Task<HttpResponseData> BuildBadRequestResponseAsync(HttpRequestData req)
    {
        var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        badResponse.Headers.Add("Content-Type", "application/json");
        var badResponseBody = JsonSerializer.Serialize(new { message = "Invalid request body" });
        await badResponse.WriteStringAsync(badResponseBody);
        return badResponse;
    }

    private void LogTicketRequestDetails(TicketClassificationContext context)
    {
        _logger.LogInformation("Ticket request received - TicketId: {TicketId}, CompanyId: {CompanyId}, CompanyName: {CompanyName}", 
            context.TicketRequest.TicketId, context.TicketRequest.CompanyId, context.TicketRequest.CompanyName ?? "Unknown");

        _logger.LogInformation("Full ticket request - TicketId: {TicketId}, Summary: {Summary}, InitialDescription: {InitialDescription}, ContactName: {ContactName}, CreatedBy: {CreatedBy}, ServiceTeam: {ServiceTeamName} (Id: {ServiceTeamId}), Type: {Type}, SubType: {SubType}, Item: {Item}, Priority: {Priority}, NotesCount: {NotesCount}",
            context.TicketRequest.TicketId,
            context.TicketRequest.Summary ?? "None",
            context.TicketRequest.InitialDescription ?? "None",
            context.TicketRequest.ContactName ?? "None",
            context.TicketRequest.CreatedBy ?? "None",
            context.TicketRequest.ServiceTeam?.Name ?? "None",
            context.TicketRequest.ServiceTeam?.Id ?? 0,
            context.TicketRequest.Type ?? "None",
            context.TicketRequest.SubType ?? "None",
            context.TicketRequest.Item ?? "None",
            context.TicketRequest.Priority ?? "None",
            context.TicketRequest.Notes?.Count ?? 0);
    }


    private async Task CreateClassificationActivitiesAsync(
        TicketClassificationContext context)
    {
        var activitiesDescription = await _connectWiseService.CreateActivitiesForClassificationAsync(context.TicketRequest.TicketId, context.SpamResponse, context.BoardResponse, 
            context.SpamConfidence, context.BoardConfidence);
        
        _logger.LogInformation("Activities created for classification - TicketId: {TicketId}, Activities: {Activities}", 
            context.TicketRequest.TicketId, activitiesDescription);
    }

    private async Task<HttpResponseData> HandleLowBoardConfidenceAsync(
        TicketClassificationContext context)
    {
        _logger.LogInformation("Board confidence below threshold ({Confidence} < 90) - Moving to Triage Review - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}", 
            context.BoardResponse!.ConfidenceScore, context.TicketRequest.TicketId, context.BoardResponse.BoardId, context.BoardResponse.BoardName ?? "Unknown");
        
        var note = NoteBuilderService.BuildLowBoardConfidenceNote(context.BoardResponse, context.InitialIntent);
        var updateRequest = new TicketUpdateRequest
        {
            Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
        };

        await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
        await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
        
        _logger.LogInformation("Low board confidence ticket processed - TicketId: {TicketId}, Confidence: {Confidence}, Status: Triage Review, Actions: Successful", 
            context.TicketRequest.TicketId, context.BoardResponse.ConfidenceScore);

        var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Low Board Confidence" });
        await response.WriteStringAsync(responseBody);
        return response;
    }

    private async Task<HttpResponseData> HandleNonServiceBoardAsync(
        TicketClassificationContext context)
    {
        _logger.LogInformation("Non-service board detected (BoardId: {BoardId}, BoardName: {BoardName}) - Moving to Triage Review - TicketId: {TicketId}", 
            context.BoardResponse!.BoardId, context.BoardResponse.BoardName ?? "Unknown", context.TicketRequest.TicketId);
        
        var note = NoteBuilderService.BuildNonServiceBoardNote(context.BoardResponse, context.InitialIntent);
        var updateRequest = new TicketUpdateRequest
        {
            Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
        };

        await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
        await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
        
        _logger.LogInformation("Non-service board ticket processed - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}, Status: Triage Review, Actions: Successful", 
            context.TicketRequest.TicketId, context.BoardResponse.BoardId, context.BoardResponse.BoardName ?? "Unknown");

        var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Non-Service Board" });
        await response.WriteStringAsync(responseBody);
        return response;
    }


    private async Task LookupContactAsync(TicketClassificationContext context)
    {
        if (context.TicketRequest.ContactId.HasValue && context.TicketRequest.ContactId.Value > 0)
        {
            _logger.LogInformation("Looking up contact by ID - TicketId: {TicketId}, ContactId: {ContactId}", 
                context.TicketRequest.TicketId, context.TicketRequest.ContactId.Value);
            
            try
            {
                context.Contact = await _connectWiseService.GetContactByIdAsync(context.TicketRequest.ContactId.Value);
                context.IsVip = ConnectWiseService.IsVipContact(context.Contact);
                
                _logger.LogInformation("Contact lookup completed - TicketId: {TicketId}, ContactId: {ContactId}, IsVip: {IsVip}", 
                    context.TicketRequest.TicketId, context.Contact?.Id ?? 0, context.IsVip);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get contact by ID {ContactId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                    context.TicketRequest.ContactId.Value, ex.GetType().Name, ex.Message);
            }
        }
    }

    private TicketUpdateRequest BuildTicketUpdateRequest(
        TicketClassificationContext context)
    {
        var ticketUpdate = new TicketUpdateRequest
        {
            Summary = context.FinalSummary,
            Board = context.BoardResponse != null && context.BoardResponse.BoardId.HasValue ? new ActivityReference { Id = context.BoardResponse.BoardId.Value } : null,
            SkipCallback = true
        };

        if (context.TsiResponse?.Type != null && context.TsiResponse.Type.Id.HasValue)
        {
            ticketUpdate.Type = new ActivityTypeReference { Id = context.TsiResponse.Type.Id.Value };
        }

        if (context.TsiResponse?.Subtype != null && context.TsiResponse.Subtype.Id.HasValue)
        {
            ticketUpdate.SubType = new ActivityTypeReference { Id = context.TsiResponse.Subtype.Id.Value };
        }

        if (context.TsiResponse?.Item != null && context.TsiResponse.Item.Id.HasValue)
        {
            ticketUpdate.Item = new ActivityTypeReference { Id = context.TsiResponse.Item.Id.Value };
        }

        if (context.TsiResponse?.Priority != null && context.TsiResponse.Priority.Id.HasValue)
        {
            ticketUpdate.Priority = new ActivityTypeReference { Id = context.TsiResponse.Priority.Id.Value };
        }

        if (context.Contact != null)
        {
            ticketUpdate.Contact = new ActivityReference { Id = context.Contact.Id };
        }

        if (context.BoardResponse?.BoardId == ConnectWiseConstants.NOCBoardId)
        {
            ticketUpdate.Status = new ActivityReference { Id = ConnectWiseConstants.NocInQueueStatusId };
        }

        return ticketUpdate;
    }

    private async Task UpdateTicketAndAddNoteAsync(
        TicketClassificationContext context)
    {
        _logger.LogInformation("Preparing final ticket update - TicketId: {TicketId}, Board: {BoardName} (Id: {BoardId}), Type: {TypeName} (Id: {TypeId}), Subtype: {SubtypeName} (Id: {SubtypeId}), Item: {ItemName} (Id: {ItemId}), Priority: {PriorityName} (Id: {PriorityId}), ContactId: {ContactId}, IsVip: {IsVip}, Status: {StatusId}", 
            context.TicketRequest.TicketId, 
            context.BoardResponse?.BoardName ?? "None", context.BoardResponse?.BoardId?.ToString() ?? "None",
            context.TsiResponse?.Type?.Name ?? "None", context.TicketUpdate!.Type?.Id.ToString() ?? "None",
            context.TsiResponse?.Subtype?.Name ?? "None", context.TicketUpdate.SubType?.Id.ToString() ?? "None",
            context.TsiResponse?.Item?.Name ?? "None", context.TicketUpdate.Item?.Id.ToString() ?? "None",
            context.TsiResponse?.Priority?.Name ?? "None", context.TicketUpdate.Priority?.Id.ToString() ?? "None",
            context.TicketUpdate.Contact?.Id.ToString() ?? "None", context.IsVip, context.TicketUpdate.Status?.Id.ToString() ?? "None");
        
        await _connectWiseService.UpdateTicketWithRetryAsync(context.TicketRequest.TicketId, context.TicketUpdate);
        await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, context.FinalNote!);
        
        _logger.LogInformation("Ticket processed successfully - TicketId: {TicketId}, Board: {BoardName}, Type: {TypeName}, Subtype: {SubtypeName}, Item: {ItemName}, Priority: {PriorityName}", 
            context.TicketRequest.TicketId, 
            context.BoardResponse?.BoardName ?? "None",
            context.TsiResponse?.Type?.Name ?? "None",
            context.TsiResponse?.Subtype?.Name ?? "None",
            context.TsiResponse?.Item?.Name ?? "None",
            context.TsiResponse?.Priority?.Name ?? "None");
    }

    private async Task ProcessProposedNoteAsync(
        TicketClassificationContext context)
    {
        _logger.LogInformation("Checking proposed note conditions - TicketId: {TicketId}, NotStreamlineClient: {NotStreamlineClient}, ServiceTeamNull: {ServiceTeamNull}, ServiceTeamName: {ServiceTeamName}", 
            context.TicketRequest.TicketId, context.TicketRequest.NotStreamlineClient, context.TicketRequest.ServiceTeam == null, context.TicketRequest.ServiceTeam?.Name ?? "None");
        
        var notStreamlineClientMet = context.TicketRequest.NotStreamlineClient;
        var serviceTeamNotNull = context.TicketRequest.ServiceTeam != null;
        var serviceTeamNameMatch = serviceTeamNotNull && (context.TicketRequest.ServiceTeam!.Name == "Hydra" || context.TicketRequest.ServiceTeam.Name == "Bootes");
        
        if (notStreamlineClientMet && serviceTeamNotNull && serviceTeamNameMatch)
        {
            _logger.LogInformation("Proposed note conditions met - Sending proposed note - TicketId: {TicketId}, ServiceTeam: {ServiceTeamName}", 
                context.TicketRequest.TicketId, context.TicketRequest.ServiceTeam!.Name);
            
            await _proposedNoteService.SendProposedNoteAsync(
                context.TicketRequest.TicketId,
                context.InitialIntent,
                context.FinalSummary!,
                context.TicketRequest.InitialDescription,
                context.BoardResponse?.BoardName,
                context.TicketRequest.CompanyName,
                context.TicketRequest.CompanyId,
                context.TicketRequest.ItGlueOrgId);
            
            context.ProposedNoteSent = true;
        }
        else
        {
            var reasons = new List<string>();
            if (!notStreamlineClientMet) reasons.Add("NotStreamlineClient is false");
            if (!serviceTeamNotNull) reasons.Add("ServiceTeam is null");
            if (serviceTeamNotNull && !serviceTeamNameMatch) reasons.Add($"ServiceTeam name '{context.TicketRequest.ServiceTeam!.Name}' is not 'Hydra' or 'Bootes'");
            
            _logger.LogInformation("Proposed note not sent - Conditions not met - TicketId: {TicketId}, Reasons: {Reasons}", 
                context.TicketRequest.TicketId, string.Join(", ", reasons));
        }
    }

    private async Task<HttpResponseData> BuildSuccessResponseAsync(
        TicketClassificationContext context)
    {
        var successResponse = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
        successResponse.Headers.Add("Content-Type", "application/json");
        
        var successResponseBody = new Dictionary<string, object?>
        {
            { "message", "Ticket processed successfully" },
            { "newBoardName", context.BoardResponse?.BoardName ?? null },
            { "newBoardId", context.BoardResponse?.BoardId ?? null },
            { "newTypeName", context.TsiResponse?.Type?.Name ?? null },
            { "newTypeId", context.TsiResponse?.Type?.Id ?? null },
            { "newSummary", context.FinalSummary }
        };
        
        if (context.ProposedNoteSent)
        {
            successResponseBody["proposedNoteSent"] = true;
        }
        
        var successJson = JsonSerializer.Serialize(successResponseBody);
        await successResponse.WriteStringAsync(successJson);
        return successResponse;
    }

    private async Task<HttpResponseData> HandleErrorAsync(
        TicketClassificationContext context,
        Exception ex)
    {
        _logger.LogError(ex, "Error processing ticket classification - ExceptionType: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
            ex.GetType().Name, ex.Message, ex.StackTrace);
        
        var failureMessage = context.IsNoCompanyBranch 
            ? "Change Me SPAM Classification Failure" 
            : "Dispatch Triage Failure";
        
        // TODO: Uncomment after testing
        // await _teamsAlertService.SendAlertAsync(failureMessage);
        
        var errorResponse = context.Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        errorResponse.Headers.Add("Content-Type", "application/json");
        var errorResponseBody = JsonSerializer.Serialize(new { message = $"Error: {ex.Message}" });
        await errorResponse.WriteStringAsync(errorResponseBody);
        return errorResponse;
    }

    internal class TicketClassificationContext
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
