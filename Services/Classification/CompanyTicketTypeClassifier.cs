using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class CompanyTicketTypeClassifier
{
    private readonly ILogger _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LoggingService _loggingService;
    private readonly IConnectWiseService _connectWiseService;
    private readonly string _baseModel;

    public CompanyTicketTypeClassifier(
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

    public async Task<HttpResponseData?> ExecuteAsync(DispatchClassificationFunction.TicketClassificationContext context)
    {
        _logger.LogInformation("Company detected (CompanyId: {CompanyId}, CompanyName: {CompanyName}) - Starting Ticket Type classification - TicketId: {TicketId}", 
            context.TicketRequest.CompanyId, context.TicketRequest.CompanyName ?? "Unknown", context.TicketRequest.TicketId);
        
        var ticketPrompt = TicketClassificationPromptService.GetPrompt();
        var ticketInput = InputBuilderService.BuildSpamTicketClassificationInput(context.TicketRequest);
        
        _logger.LogInformation("Calling OpenAI for Ticket Type classification - TicketId: {TicketId}, Model: {Model}", 
            context.TicketRequest.TicketId, _baseModel);
        
        var (result, tokenUsage, model) = await _openAIService.GetCompletionAsync<TicketClassificationResponse>(
            ticketPrompt, ticketInput, _baseModel, 0f, 1500);

        context.TicketResponse = result;
        context.InitialIntent = result.Intent;
        
        _logger.LogInformation("Ticket Type classification completed - TicketId: {TicketId}, Decision: {Decision}, Confidence: {Confidence}, Intent: {Intent}, Model: {Model}, Tokens: {TotalTokens}", 
            context.TicketRequest.TicketId, result.Decision, result.ConfidenceScore, context.InitialIntent ?? "None", model, tokenUsage.TotalTokens);

        var ticketLogData = new Dictionary<string, object?>
        {
            { nameof(DecisionLogEntity.Intent), context.InitialIntent },
            { nameof(DecisionLogEntity.Reason), result.Reason },
            { nameof(DecisionLogEntity.ConfidenceScore), result.ConfidenceScore },
            { nameof(DecisionLogEntity.Model), model },
            { nameof(DecisionLogEntity.PromptTokens), tokenUsage.PromptTokens },
            { nameof(DecisionLogEntity.CompletionTokens), tokenUsage.CompletionTokens },
            { nameof(DecisionLogEntity.TotalTokens), tokenUsage.TotalTokens },
            { nameof(DecisionLogEntity.Classification), result.Decision }
        };

        await _loggingService.LogDecisionAsync("Ticket Type", context.TicketRequest.TicketId, 
            context.TicketRequest.CompanyName ?? "Unknown", ticketLogData);

        if (result.Decision == "Info-Alert")
        {
            _logger.LogInformation("Ticket Type classification decision: Info-Alert - Moving to Triage Review - TicketId: {TicketId}, Confidence: {Confidence}", 
                context.TicketRequest.TicketId, result.ConfidenceScore);
            
            var note = NoteBuilderService.BuildInfoAlertNote(context.TicketResponse, context.InitialIntent);
            var updateRequest = new TicketUpdateRequest
            {
                Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
            };

            await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
            await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
            
            _logger.LogInformation("Info-Alert ticket processed - TicketId: {TicketId}, Status: Triage Review, Actions: Successful", 
                context.TicketRequest.TicketId);

            var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Info-Alert" });
            await response.WriteStringAsync(responseBody);
            return response;
        }

        return null;
    }
}
