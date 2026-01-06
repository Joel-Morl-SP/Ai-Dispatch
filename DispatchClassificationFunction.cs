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
        bool isNoCompanyBranch = false;
        try
        {
            _logger.LogInformation("DispatchClassificationFunction started - Processing new ticket request");
            
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            var ticketRequest = JsonSerializer.Deserialize<TicketRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (ticketRequest == null)
            {
                _logger.LogWarning("Invalid request body - ticketRequest is null after deserialization");
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badResponse.Headers.Add("Content-Type", "application/json");
                var badResponseBody = JsonSerializer.Serialize(new { message = "Invalid request body" });
                await badResponse.WriteStringAsync(badResponseBody);
                return badResponse;
            }

            _logger.LogInformation("Ticket request received - TicketId: {TicketId}, CompanyId: {CompanyId}, CompanyName: {CompanyName}", 
                ticketRequest.TicketId, ticketRequest.CompanyId, ticketRequest.CompanyName ?? "Unknown");

            _logger.LogInformation("Full ticket request - TicketId: {TicketId}, Summary: {Summary}, InitialDescription: {InitialDescription}, ContactName: {ContactName}, CreatedBy: {CreatedBy}, ServiceTeam: {ServiceTeamName} (Id: {ServiceTeamId}), Type: {Type}, SubType: {SubType}, Item: {Item}, Priority: {Priority}, NotesCount: {NotesCount}",
                ticketRequest.TicketId,
                ticketRequest.Summary ?? "None",
                ticketRequest.InitialDescription ?? "None",
                ticketRequest.ContactName ?? "None",
                ticketRequest.CreatedBy ?? "None",
                ticketRequest.ServiceTeam?.Name ?? "None",
                ticketRequest.ServiceTeam?.Id ?? 0,
                ticketRequest.Type ?? "None",
                ticketRequest.SubType ?? "None",
                ticketRequest.Item ?? "None",
                ticketRequest.Priority ?? "None",
                ticketRequest.Notes?.Count ?? 0);

            ticketRequest = InputBuilderService.CleanSummary(ticketRequest);

            SpamClassificationResponse? spamResponse = null;
            TicketClassificationResponse? ticketResponse = null;
            BoardRoutingResponse? boardResponse = null;
            TSIClassificationResponse? tsiResponse = null;
            SummaryResponse? summaryResponse = null;

            var spamConfidence = 0;
            var boardConfidence = 0;
            string? initialIntent = null; // Capture intent once from first classification and reuse for all steps

            var isNoCompany = ticketRequest.CompanyId == ConnectWiseConstants.NoCompanyId;
            isNoCompanyBranch = isNoCompany;

            if (isNoCompany)
            {
                _logger.LogInformation("No Company detected (CompanyId: {CompanyId}) - Starting SPAM classification - TicketId: {TicketId}", 
                    ticketRequest.CompanyId, ticketRequest.TicketId);
                
                var spamPrompt = SpamPromptService.GetPrompt();
                var spamInput = InputBuilderService.BuildSpamTicketClassificationInput(ticketRequest);
                
                _logger.LogInformation("Calling OpenAI for SPAM classification - TicketId: {TicketId}, Model: {Model}", 
                    ticketRequest.TicketId, _baseModel);
                
                var (result, tokenUsage, model) = await _openAIService.GetCompletionAsync<SpamClassificationResponse>(
                    spamPrompt, spamInput, _baseModel, 0f, 1500);

                spamResponse = result;
                spamConfidence = result.ConfidenceScore;
                initialIntent = result.Intent; // Capture intent from first classification
                
                _logger.LogInformation("SPAM classification completed - TicketId: {TicketId}, Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent}, Model: {Model}, Tokens: {TotalTokens}", 
                    ticketRequest.TicketId, result.Decision, spamConfidence, initialIntent ?? "None", model, tokenUsage.TotalTokens);

                var spamLogData = new Dictionary<string, object?>
                {
                    { nameof(DecisionLogEntity.Intent), initialIntent },
                    { nameof(DecisionLogEntity.Reason), result.Reason },
                    { nameof(DecisionLogEntity.ConfidenceScore), result.ConfidenceScore },
                    { nameof(DecisionLogEntity.Model), model },
                    { nameof(DecisionLogEntity.PromptTokens), tokenUsage.PromptTokens },
                    { nameof(DecisionLogEntity.CompletionTokens), tokenUsage.CompletionTokens },
                    { nameof(DecisionLogEntity.TotalTokens), tokenUsage.TotalTokens },
                    { nameof(DecisionLogEntity.Classification), result.Decision == "Spam" ? "SPAM" : "Ticket" }
                };

                await _loggingService.LogDecisionAsync("Ticket Type", ticketRequest.TicketId, 
                    ticketRequest.CompanyName ?? "No Company", spamLogData);

                if (result.Decision == "Ticket")
                {
                    _logger.LogInformation("SPAM classification - Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent} - TicketId: {TicketId}", 
                        result.Decision, spamConfidence, initialIntent ?? "None", ticketRequest.TicketId);
                    
                    var note = NoteBuilderService.BuildNoCompanyNote();
                    var updateRequest = new TicketUpdateRequest
                    {
                        Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
                    };

                    await _connectWiseService.UpdateTicketAsync(ticketRequest.TicketId, updateRequest);
                    await _connectWiseService.AddNoteToTicketAsync(ticketRequest.TicketId, note);
                    
                    _logger.LogInformation("NoCompany ticket processed - TicketId: {TicketId}, Classification: {Decision}, Status: Triage Review, Note: Added, Actions: Successful", 
                        ticketRequest.TicketId, result.Decision);

                    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - No Company" });
                    await response.WriteStringAsync(responseBody);
                    return response;
                }

                if (result.Decision == "Spam" && spamConfidence == 95)
                {
                    _logger.LogInformation("SPAM classification - Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent} - TicketId: {TicketId}", 
                        result.Decision, spamConfidence, initialIntent ?? "None", ticketRequest.TicketId);
                    
                    var note = NoteBuilderService.BuildPossibleSpamNote(spamResponse, initialIntent);
                    var updateRequest = new TicketUpdateRequest
                    {
                        Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
                    };

                    await _connectWiseService.UpdateTicketAsync(ticketRequest.TicketId, updateRequest);
                    await _connectWiseService.AddNoteToTicketAsync(ticketRequest.TicketId, note);
                    await _connectWiseService.CreateActivitiesForClassificationAsync(ticketRequest.TicketId, spamResponse, null, spamConfidence, 0);
                    
                    _logger.LogInformation("NoCompany spam ticket processed - TicketId: {TicketId}, Classification: {Decision} (Confidence: {Confidence}), Status: Triage Review, Note: Added, Activity: Possible SPAM Classification, Actions: Successful", 
                        ticketRequest.TicketId, result.Decision, spamConfidence);

                    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Possible SPAM" });
                    await response.WriteStringAsync(responseBody);
                    return response;
                }

                if (result.Decision == "Spam" && spamConfidence == 100)
                {
                    _logger.LogInformation("SPAM classification - Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent} - TicketId: {TicketId}", 
                        result.Decision, spamConfidence, initialIntent ?? "None", ticketRequest.TicketId);
                    
                    var note = NoteBuilderService.BuildSpam100Note(spamResponse, initialIntent);
                    var updateRequest = new TicketUpdateRequest
                    {
                        Type = new ActivityTypeReference { Id = ConnectWiseConstants.ContinualServiceImprovementTypeId },
                        Status = new ActivityReference { Id = ConnectWiseConstants.ClosingStatusId }
                    };

                    await _connectWiseService.UpdateTicketAsync(ticketRequest.TicketId, updateRequest);
                    await _connectWiseService.AddNoteToTicketAsync(ticketRequest.TicketId, note);
                    await _connectWiseService.CreateActivitiesForClassificationAsync(ticketRequest.TicketId, spamResponse, null, spamConfidence, 0);
                    
                    _logger.LogInformation("NoCompany spam ticket closed - TicketId: {TicketId}, Classification: {Decision} (Confidence: {Confidence}), Status: Closing, Type: Continual Service Improvement, Note: Added, Activity: SPAM Classification 100%, Actions: Successful", 
                        ticketRequest.TicketId, result.Decision, spamConfidence);

                    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    var responseBody = JsonSerializer.Serialize(new { message = "Ticket closed - SPAM" });
                    await response.WriteStringAsync(responseBody);
                    return response;
                }
            }
            else
            {
                _logger.LogInformation("Company detected (CompanyId: {CompanyId}, CompanyName: {CompanyName}) - Starting Ticket Type classification - TicketId: {TicketId}", 
                    ticketRequest.CompanyId, ticketRequest.CompanyName ?? "Unknown", ticketRequest.TicketId);
                
                var ticketPrompt = TicketClassificationPromptService.GetPrompt();
                var ticketInput = InputBuilderService.BuildSpamTicketClassificationInput(ticketRequest);
                
                _logger.LogInformation("Calling OpenAI for Ticket Type classification - TicketId: {TicketId}, Model: {Model}", 
                    ticketRequest.TicketId, _baseModel);
                
                var (result, tokenUsage, model) = await _openAIService.GetCompletionAsync<TicketClassificationResponse>(
                    ticketPrompt, ticketInput, _baseModel, 0f, 1500);

                ticketResponse = result;
                initialIntent = result.Intent; // Capture intent from first classification
                
                _logger.LogInformation("Ticket Type classification completed - TicketId: {TicketId}, Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent}, Model: {Model}, Tokens: {TotalTokens}", 
                    ticketRequest.TicketId, result.Decision, result.ConfidenceScore, initialIntent ?? "None", model, tokenUsage.TotalTokens);

                var ticketLogData = new Dictionary<string, object?>
                {
                    { nameof(DecisionLogEntity.Intent), initialIntent },
                    { nameof(DecisionLogEntity.Reason), result.Reason },
                    { nameof(DecisionLogEntity.ConfidenceScore), result.ConfidenceScore },
                    { nameof(DecisionLogEntity.Model), model },
                    { nameof(DecisionLogEntity.PromptTokens), tokenUsage.PromptTokens },
                    { nameof(DecisionLogEntity.CompletionTokens), tokenUsage.CompletionTokens },
                    { nameof(DecisionLogEntity.TotalTokens), tokenUsage.TotalTokens },
                    { nameof(DecisionLogEntity.Classification), result.Decision }
                };

                await _loggingService.LogDecisionAsync("Ticket Type", ticketRequest.TicketId, 
                    ticketRequest.CompanyName ?? "Unknown", ticketLogData);

                if (result.Decision == "Info-Alert")
                {
                    _logger.LogInformation("Ticket Type classification decision: Info-Alert - Moving to Triage Review - TicketId: {TicketId}, Confidence: {Confidence}", 
                        ticketRequest.TicketId, result.ConfidenceScore);
                    
                    var note = NoteBuilderService.BuildInfoAlertNote(ticketResponse, initialIntent);
                    var updateRequest = new TicketUpdateRequest
                    {
                        Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
                    };

                    await _connectWiseService.UpdateTicketAsync(ticketRequest.TicketId, updateRequest);
                    await _connectWiseService.AddNoteToTicketAsync(ticketRequest.TicketId, note);
                    
                    _logger.LogInformation("Info-Alert ticket processed - TicketId: {TicketId}, Status: Triage Review, Actions: Successful", 
                        ticketRequest.TicketId);

                    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Info-Alert" });
                    await response.WriteStringAsync(responseBody);
                    return response;
                }
            }

            _logger.LogInformation("Starting Board Routing classification - TicketId: {TicketId}, CompanyId: {CompanyId}, CompanyName: {CompanyName}", 
                ticketRequest.TicketId, ticketRequest.CompanyId, ticketRequest.CompanyName ?? "Unknown");
            
            var boardPrompt = await BoardRoutingPromptService.GetPrompt(
                ticketRequest.CompanyId,
                ConnectWiseConstants.L1BoardId,
                ConnectWiseConstants.L2BoardId,
                ConnectWiseConstants.L3BoardId,
                ConnectWiseConstants.CaduceusBoardId,
                ConnectWiseConstants.SecurityBoardId,
                ConnectWiseConstants.NOCBoardId);

            var boardInput = InputBuilderService.BuildBoardRoutingInput(ticketRequest, initialIntent);
            
            _logger.LogInformation("Calling OpenAI for Board Routing - TicketId: {TicketId}, Intent: {Intent}, Model: {Model}", 
                ticketRequest.TicketId, initialIntent ?? "None", _reasoningModel);
            
            var (boardResult, boardTokenUsage, boardModel) = await _openAIService.GetCompletionAsync<BoardRoutingResponse>(
                boardPrompt, boardInput, _reasoningModel, 0f, 3000);

            boardResponse = boardResult;
            boardConfidence = boardResult.ConfidenceScore;
            
            _logger.LogInformation("Board Routing classification completed - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}, Confidence: {Confidence}, Model: {Model}, Tokens: {TotalTokens}", 
                ticketRequest.TicketId, boardResult.BoardId, boardResult.BoardName ?? "Unknown", boardConfidence, boardModel, boardTokenUsage.TotalTokens);

            var boardLogData = new Dictionary<string, object?>
            {
                { nameof(DecisionLogEntity.Intent), initialIntent },
                { nameof(DecisionLogEntity.Reason), boardResult.Reason },
                { nameof(DecisionLogEntity.ConfidenceScore), boardResult.ConfidenceScore },
                { nameof(DecisionLogEntity.BoardId), boardResult.BoardId },
                { nameof(DecisionLogEntity.BoardName), boardResult.BoardName },
                { nameof(DecisionLogEntity.Model), boardModel },
                { nameof(DecisionLogEntity.PromptTokens), boardTokenUsage.PromptTokens },
                { nameof(DecisionLogEntity.CompletionTokens), boardTokenUsage.CompletionTokens },
                { nameof(DecisionLogEntity.TotalTokens), boardTokenUsage.TotalTokens }
            };

            await _loggingService.LogDecisionAsync("Board Routing", ticketRequest.TicketId, 
                ticketRequest.CompanyName ?? "Unknown", boardLogData);

            var activitiesDescription = await _connectWiseService.CreateActivitiesForClassificationAsync(ticketRequest.TicketId, spamResponse, boardResponse, 
                spamConfidence, boardConfidence);
            
            _logger.LogInformation("Activities created for classification - TicketId: {TicketId}, Activities: {Activities}", 
                ticketRequest.TicketId, activitiesDescription);

            if (boardConfidence < 90)
            {
                _logger.LogInformation("Board confidence below threshold ({Confidence} < 90) - Moving to Triage Review - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}", 
                    boardConfidence, ticketRequest.TicketId, boardResult.BoardId, boardResult.BoardName ?? "Unknown");
                
                var note = NoteBuilderService.BuildLowBoardConfidenceNote(boardResponse, initialIntent);
                var updateRequest = new TicketUpdateRequest
                {
                    Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
                };

                await _connectWiseService.UpdateTicketAsync(ticketRequest.TicketId, updateRequest);
                await _connectWiseService.AddNoteToTicketAsync(ticketRequest.TicketId, note);
                
                _logger.LogInformation("Low board confidence ticket processed - TicketId: {TicketId}, Confidence: {Confidence}, Status: Triage Review, Actions: Successful", 
                    ticketRequest.TicketId, boardConfidence);

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Low Board Confidence" });
                await response.WriteStringAsync(responseBody);
                return response;
            }

            if (boardConfidence >= 90 && NoteBuilderService.IsNonServiceBoard(boardResponse.BoardId))
            {
                _logger.LogInformation("Non-service board detected (BoardId: {BoardId}, BoardName: {BoardName}) - Moving to Triage Review - TicketId: {TicketId}", 
                    boardResponse.BoardId, boardResponse.BoardName ?? "Unknown", ticketRequest.TicketId);
                
                var note = NoteBuilderService.BuildNonServiceBoardNote(boardResponse, initialIntent);
                var updateRequest = new TicketUpdateRequest
                {
                    Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
                };

                await _connectWiseService.UpdateTicketAsync(ticketRequest.TicketId, updateRequest);
                await _connectWiseService.AddNoteToTicketAsync(ticketRequest.TicketId, note);
                
                _logger.LogInformation("Non-service board ticket processed - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}, Status: Triage Review, Actions: Successful", 
                    ticketRequest.TicketId, boardResponse.BoardId, boardResponse.BoardName ?? "Unknown");

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Non-Service Board" });
                await response.WriteStringAsync(responseBody);
                return response;
            }

            _logger.LogInformation("Starting TSI Classification - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}", 
                ticketRequest.TicketId, boardResponse.BoardId, boardResponse.BoardName ?? "Unknown");
            
            var tsiPrompt = await TSIPromptService.GetPrompt(boardResponse.BoardId, boardResponse.BoardName);
            var tsiInput = InputBuilderService.BuildTSIInput(ticketRequest, initialIntent);
            
            _logger.LogInformation("Calling OpenAI for TSI Classification - TicketId: {TicketId}, Intent: {Intent}, Model: {Model}", 
                ticketRequest.TicketId, initialIntent ?? "None", _reasoningModel);
            
            var (tsiResult, tsiTokenUsage, tsiModel) = await _openAIService.GetCompletionAsync<TSIClassificationResponse>(
                tsiPrompt, tsiInput, _reasoningModel, 0f, 15000);

            tsiResponse = tsiResult;
            
            _logger.LogInformation("TSI Classification completed - TicketId: {TicketId}, Type: {Type}, Subtype: {Subtype}, Item: {Item}, Priority: {Priority}, Confidence: {Confidence}, Model: {Model}, Tokens: {TotalTokens}", 
                ticketRequest.TicketId, tsiResult.Type?.Name ?? "None", tsiResult.Subtype?.Name ?? "None", tsiResult.Item?.Name ?? "None", 
                tsiResult.Priority?.Name ?? "None", tsiResult.ConfidenceScore, tsiModel, tsiTokenUsage.TotalTokens);

            var tsiLogData = new Dictionary<string, object?>();
            if (initialIntent != null) tsiLogData[nameof(DecisionLogEntity.Intent)] = initialIntent;
            if (tsiResult.Reason.ValueKind != System.Text.Json.JsonValueKind.Undefined) 
                tsiLogData[nameof(DecisionLogEntity.Reason)] = tsiResult.Reason.ToString();
            tsiLogData[nameof(DecisionLogEntity.ConfidenceScore)] = tsiResult.ConfidenceScore;
            if (tsiResult.Type != null) tsiLogData[nameof(DecisionLogEntity.Type)] = tsiResult.Type.Name;
            if (tsiResult.Subtype != null) tsiLogData[nameof(DecisionLogEntity.Subtype)] = tsiResult.Subtype.Name;
            if (tsiResult.Item != null) tsiLogData[nameof(DecisionLogEntity.Item)] = tsiResult.Item.Name;
            if (tsiResult.Priority != null) tsiLogData[nameof(DecisionLogEntity.Priority)] = tsiResult.Priority.Name;
            tsiLogData[nameof(DecisionLogEntity.Model)] = tsiModel;
            tsiLogData[nameof(DecisionLogEntity.PromptTokens)] = tsiTokenUsage.PromptTokens;
            tsiLogData[nameof(DecisionLogEntity.CompletionTokens)] = tsiTokenUsage.CompletionTokens;
            tsiLogData[nameof(DecisionLogEntity.TotalTokens)] = tsiTokenUsage.TotalTokens;


            await _loggingService.LogDecisionAsync("TSI Classification", ticketRequest.TicketId, 
                ticketRequest.CompanyName ?? "Unknown", tsiLogData);

            _logger.LogInformation("Starting Summary generation - TicketId: {TicketId}", ticketRequest.TicketId);
            
            var summaryPrompt = SummaryPromptService.GetPrompt();
            var summaryInput = InputBuilderService.BuildSummaryInput(ticketRequest, initialIntent);
            
            _logger.LogInformation("Calling OpenAI for Summary generation - TicketId: {TicketId}, Intent: {Intent}, Model: {Model}", 
                ticketRequest.TicketId, initialIntent ?? "None", _baseModel);
            
            var (summaryResult, summaryTokenUsage, summaryModel) = await _openAIService.GetCompletionAsync<SummaryResponse>(
                summaryPrompt, summaryInput, _baseModel, 0f, 500);

            summaryResponse = summaryResult;
            
            _logger.LogInformation("Summary generation completed - TicketId: {TicketId}, SubmittedFor: {SubmittedFor}, Model: {Model}, Tokens: {TotalTokens}", 
                ticketRequest.TicketId, summaryResult.SubmittedFor ?? "None", summaryModel, summaryTokenUsage.TotalTokens);

            var summaryLogData = new Dictionary<string, object?>
            {
                { nameof(DecisionLogEntity.Intent), initialIntent },
                { nameof(DecisionLogEntity.Reason), summaryResult.Reason },
                { nameof(DecisionLogEntity.ConfidenceScore), summaryResult.ConfidenceScore },
                { nameof(DecisionLogEntity.Summary), summaryResult.NewSummary },
                { nameof(DecisionLogEntity.Model), summaryModel },
                { nameof(DecisionLogEntity.PromptTokens), summaryTokenUsage.PromptTokens },
                { nameof(DecisionLogEntity.CompletionTokens), summaryTokenUsage.CompletionTokens },
                { nameof(DecisionLogEntity.TotalTokens), summaryTokenUsage.TotalTokens }
            };

            await _loggingService.LogDecisionAsync("Summary", ticketRequest.TicketId, 
                ticketRequest.CompanyName ?? "Unknown", summaryLogData);

            ContactResponse? contact = null;
            bool isVip = false;

            if (ticketRequest.ContactId.HasValue && ticketRequest.ContactId.Value > 0)
            {
                _logger.LogInformation("Looking up contact by ID - TicketId: {TicketId}, ContactId: {ContactId}", 
                    ticketRequest.TicketId, ticketRequest.ContactId.Value);
                
                try
                {
                    contact = await _connectWiseService.GetContactByIdAsync(ticketRequest.ContactId.Value);
                    isVip = ConnectWiseService.IsVipContact(contact);
                    
                    _logger.LogInformation("Contact lookup completed - TicketId: {TicketId}, ContactId: {ContactId}, IsVip: {IsVip}", 
                        ticketRequest.TicketId, contact?.Id ?? 0, isVip);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get contact by ID {ContactId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                        ticketRequest.ContactId.Value, ex.GetType().Name, ex.Message);
                    // Continue without contact
                }
            }

            var finalSummary = NoteBuilderService.BuildSummary(summaryResult.SubmittedFor, summaryResult.NewSummary ?? string.Empty, isVip);
            var finalNote = NoteBuilderService.BuildNote(spamResponse, boardResponse, tsiResponse, spamConfidence, boardConfidence, initialIntent);

            var ticketUpdate = new TicketUpdateRequest
            {
                Summary = finalSummary,
                Board = boardResponse.BoardId.HasValue ? new ActivityReference { Id = boardResponse.BoardId.Value } : null,
                SkipCallback = true
            };

            if (tsiResult.Type != null && tsiResult.Type.Id.HasValue)
            {
                ticketUpdate.Type = new ActivityTypeReference { Id = tsiResult.Type.Id.Value };
            }

            if (tsiResult.Subtype != null && tsiResult.Subtype.Id.HasValue)
            {
                ticketUpdate.SubType = new ActivityTypeReference { Id = tsiResult.Subtype.Id.Value };
            }

            if (tsiResult.Item != null && tsiResult.Item.Id.HasValue)
            {
                ticketUpdate.Item = new ActivityTypeReference { Id = tsiResult.Item.Id.Value };
            }

            if (tsiResult.Priority != null && tsiResult.Priority.Id.HasValue)
            {
                ticketUpdate.Priority = new ActivityTypeReference { Id = tsiResult.Priority.Id.Value };
            }

            if (contact != null)
            {
                ticketUpdate.Contact = new ActivityReference { Id = contact.Id };
            }

            if (boardResponse?.BoardId == ConnectWiseConstants.NOCBoardId)
            {
                ticketUpdate.Status = new ActivityReference { Id = ConnectWiseConstants.NocInQueueStatusId };
            }

            _logger.LogInformation("Preparing final ticket update - TicketId: {TicketId}, Board: {BoardName} (Id: {BoardId}), Type: {TypeName} (Id: {TypeId}), Subtype: {SubtypeName} (Id: {SubtypeId}), Item: {ItemName} (Id: {ItemId}), Priority: {PriorityName} (Id: {PriorityId}), ContactId: {ContactId}, IsVip: {IsVip}, Status: {StatusId}", 
                ticketRequest.TicketId, 
                boardResponse?.BoardName ?? "None", boardResponse?.BoardId?.ToString() ?? "None",
                tsiResult?.Type?.Name ?? "None", ticketUpdate.Type?.Id.ToString() ?? "None",
                tsiResult?.Subtype?.Name ?? "None", ticketUpdate.SubType?.Id.ToString() ?? "None",
                tsiResult?.Item?.Name ?? "None", ticketUpdate.Item?.Id.ToString() ?? "None",
                tsiResult?.Priority?.Name ?? "None", ticketUpdate.Priority?.Id.ToString() ?? "None",
                ticketUpdate.Contact?.Id.ToString() ?? "None", isVip, ticketUpdate.Status?.Id.ToString() ?? "None");
            
            await _connectWiseService.UpdateTicketWithRetryAsync(ticketRequest.TicketId, ticketUpdate);
            await _connectWiseService.AddNoteToTicketAsync(ticketRequest.TicketId, finalNote);
            
            _logger.LogInformation("Ticket processed successfully - TicketId: {TicketId}, Board: {BoardName}, Type: {TypeName}, Subtype: {SubtypeName}, Item: {ItemName}, Priority: {PriorityName}", 
                ticketRequest.TicketId, 
                boardResponse?.BoardName ?? "None",
                tsiResult?.Type?.Name ?? "None",
                tsiResult?.Subtype?.Name ?? "None",
                tsiResult?.Item?.Name ?? "None",
                tsiResult?.Priority?.Name ?? "None");

            // Send proposed note if conditions are met
            _logger.LogInformation("Checking proposed note conditions - TicketId: {TicketId}, NotStreamlineClient: {NotStreamlineClient}, ServiceTeamNull: {ServiceTeamNull}, ServiceTeamName: {ServiceTeamName}", 
                ticketRequest.TicketId, ticketRequest.NotStreamlineClient, ticketRequest.ServiceTeam == null, ticketRequest.ServiceTeam?.Name ?? "None");
            
            var notStreamlineClientMet = ticketRequest.NotStreamlineClient;
            var serviceTeamNotNull = ticketRequest.ServiceTeam != null;
            var serviceTeamNameMatch = serviceTeamNotNull && (ticketRequest.ServiceTeam!.Name == "Hydra" || ticketRequest.ServiceTeam.Name == "Bootes");
            bool proposedNoteSent = false;
            
            if (notStreamlineClientMet && serviceTeamNotNull && serviceTeamNameMatch)
            {
                _logger.LogInformation("Proposed note conditions met - Sending proposed note - TicketId: {TicketId}, ServiceTeam: {ServiceTeamName}", 
                    ticketRequest.TicketId, ticketRequest.ServiceTeam!.Name);
                
                await _proposedNoteService.SendProposedNoteAsync(
                    ticketRequest.TicketId,
                    initialIntent,
                    finalSummary,
                    ticketRequest.InitialDescription,
                    boardResponse?.BoardName,
                    ticketRequest.CompanyName,
                    ticketRequest.CompanyId,
                    ticketRequest.ItGlueOrgId);
                
                proposedNoteSent = true;
            }
            else
            {
                var reasons = new List<string>();
                if (!notStreamlineClientMet) reasons.Add("NotStreamlineClient is false");
                if (!serviceTeamNotNull) reasons.Add("ServiceTeam is null");
                if (serviceTeamNotNull && !serviceTeamNameMatch) reasons.Add($"ServiceTeam name '{ticketRequest.ServiceTeam!.Name}' is not 'Hydra' or 'Bootes'");
                
                _logger.LogInformation("Proposed note not sent - Conditions not met - TicketId: {TicketId}, Reasons: {Reasons}", 
                    ticketRequest.TicketId, string.Join(", ", reasons));
            }

            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            successResponse.Headers.Add("Content-Type", "application/json");
            
            var successResponseBody = new Dictionary<string, object?>
            {
                { "message", "Ticket processed successfully" },
                { "newBoardName", boardResponse?.BoardName ?? null },
                { "newBoardId", boardResponse?.BoardId ?? null },
                { "newTypeName", tsiResult?.Type?.Name ?? null },
                { "newTypeId", tsiResult?.Type?.Id ?? null },
                { "newSummary", finalSummary }
            };
            
            if (proposedNoteSent)
            {
                successResponseBody["proposedNoteSent"] = true;
            }
            
            var successJson = JsonSerializer.Serialize(successResponseBody);
            await successResponse.WriteStringAsync(successJson);
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ticket classification - ExceptionType: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                ex.GetType().Name, ex.Message, ex.StackTrace);
            
            var failureMessage = isNoCompanyBranch 
                ? "Change Me SPAM Classification Failure" 
                : "Dispatch Triage Failure";
            
            // TODO: Uncomment after testing
            // await _teamsAlertService.SendAlertAsync(failureMessage);
            
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            var errorResponseBody = JsonSerializer.Serialize(new { message = $"Error: {ex.Message}" });
            await errorResponse.WriteStringAsync(errorResponseBody);
            return errorResponse;
        }
    }
}
