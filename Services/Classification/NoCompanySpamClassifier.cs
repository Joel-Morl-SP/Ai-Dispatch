using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class NoCompanySpamClassifier : IClassificationStep
{
    private readonly ILogger _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LoggingService _loggingService;
    private readonly IConnectWiseService _connectWiseService;
    private readonly string _baseModel;

    public NoCompanySpamClassifier(
        ILogger logger,
        AzureOpenAIService openAIService,
        LoggingService loggingService,
        IConnectWiseService connectWiseService,
        string baseModel)
    {
        _logger = logger;
        _openAIService = openAIService;
        _loggingService = loggingService;
        _connectWiseService = connectWiseService;
        _baseModel = baseModel;
    }

    async Task<HttpResponseData?> IClassificationStep.ExecuteAsync(DispatchClassificationFunction.TicketClassificationContext context)
    {
        _logger.LogInformation("No Company detected (CompanyId: {CompanyId}) - Starting SPAM classification - TicketId: {TicketId}", 
            context.TicketRequest.CompanyId, context.TicketRequest.TicketId);
        
        var spamPrompt = SpamPromptService.GetPrompt();
        var spamInput = InputBuilderService.BuildSpamTicketClassificationInput(context.TicketRequest);
        
        _logger.LogInformation("Calling OpenAI for SPAM classification - TicketId: {TicketId}, Model: {Model}", 
            context.TicketRequest.TicketId, _baseModel);
        
        var (result, tokenUsage, model) = await _openAIService.GetCompletionAsync<SpamClassificationResponse>(
            spamPrompt, spamInput, _baseModel, 0f, 1500);

        context.SpamResponse = result;
        context.SpamConfidence = result.ConfidenceScore;
        context.InitialIntent = result.Intent;
        
        _logger.LogInformation("SPAM classification completed - TicketId: {TicketId}, Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent}, Model: {Model}, Tokens: {TotalTokens}", 
            context.TicketRequest.TicketId, result.Decision, context.SpamConfidence, context.InitialIntent ?? "None", model, tokenUsage.TotalTokens);

        var spamLogData = new Dictionary<string, object?>
        {
            { nameof(DecisionLogEntity.Intent), context.InitialIntent },
            { nameof(DecisionLogEntity.Reason), result.Reason },
            { nameof(DecisionLogEntity.ConfidenceScore), result.ConfidenceScore },
            { nameof(DecisionLogEntity.Model), model },
            { nameof(DecisionLogEntity.PromptTokens), tokenUsage.PromptTokens },
            { nameof(DecisionLogEntity.CompletionTokens), tokenUsage.CompletionTokens },
            { nameof(DecisionLogEntity.TotalTokens), tokenUsage.TotalTokens },
            { nameof(DecisionLogEntity.Classification), result.Decision == "Spam" ? "SPAM" : "Ticket" }
        };

        await _loggingService.LogDecisionAsync("Ticket Type", context.TicketRequest.TicketId, 
            context.TicketRequest.CompanyName ?? "No Company", spamLogData);

        if (result.Decision == "Ticket")
        {
            _logger.LogInformation("SPAM classification - Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent} - TicketId: {TicketId}", 
                result.Decision, context.SpamConfidence, context.InitialIntent ?? "None", context.TicketRequest.TicketId);
            
            var note = NoteBuilderService.BuildNoCompanyNote();
            var updateRequest = new TicketUpdateRequest
            {
                Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
            };

            await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
            await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
            
            _logger.LogInformation("NoCompany ticket processed - TicketId: {TicketId}, Classification: {Decision}, Status: Triage Review, Note: Added, Actions: Successful", 
                context.TicketRequest.TicketId, result.Decision);

            var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - No Company" });
            await response.WriteStringAsync(responseBody);
            return response;
        }

        if (result.Decision == "Spam" && context.SpamConfidence == 95)
        {
            _logger.LogInformation("SPAM classification - Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent} - TicketId: {TicketId}", 
                result.Decision, context.SpamConfidence, context.InitialIntent ?? "None", context.TicketRequest.TicketId);
            
            var note = NoteBuilderService.BuildPossibleSpamNote(context.SpamResponse, context.InitialIntent);
            var updateRequest = new TicketUpdateRequest
            {
                Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
            };

            await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
            await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
            await _connectWiseService.CreateActivitiesForClassificationAsync(context.TicketRequest.TicketId, context.SpamResponse, null, context.SpamConfidence, 0);
            
            _logger.LogInformation("NoCompany spam ticket processed - TicketId: {TicketId}, Classification: {Decision} (Confidence: {Confidence}), Status: Triage Review, Note: Added, Activity: Possible SPAM Classification, Actions: Successful", 
                context.TicketRequest.TicketId, result.Decision, context.SpamConfidence);

            var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Possible SPAM" });
            await response.WriteStringAsync(responseBody);
            return response;
        }

        if (result.Decision == "Spam" && context.SpamConfidence == 100)
        {
            _logger.LogInformation("SPAM classification - Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent} - TicketId: {TicketId}", 
                result.Decision, context.SpamConfidence, context.InitialIntent ?? "None", context.TicketRequest.TicketId);
            
            var note = NoteBuilderService.BuildSpam100Note(context.SpamResponse, context.InitialIntent);
            var updateRequest = new TicketUpdateRequest
            {
                Type = new ActivityTypeReference { Id = ConnectWiseConstants.ContinualServiceImprovementTypeId },
                Status = new ActivityReference { Id = ConnectWiseConstants.ClosingStatusId }
            };

            await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
            await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
            await _connectWiseService.CreateActivitiesForClassificationAsync(context.TicketRequest.TicketId, context.SpamResponse, null, context.SpamConfidence, 0);
            
            _logger.LogInformation("NoCompany spam ticket closed - TicketId: {TicketId}, Classification: {Decision} (Confidence: {Confidence}), Status: Closing, Type: Continual Service Improvement, Note: Added, Activity: SPAM Classification 100%, Actions: Successful", 
                context.TicketRequest.TicketId, result.Decision, context.SpamConfidence);

            var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            var responseBody = JsonSerializer.Serialize(new { message = "Ticket closed - SPAM" });
            await response.WriteStringAsync(responseBody);
            return response;
        }

        return null;
    }
}
