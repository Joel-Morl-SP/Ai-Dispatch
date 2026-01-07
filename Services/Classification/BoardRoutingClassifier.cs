using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class BoardRoutingClassifier : IClassificationStep
{
    private readonly ILogger _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LoggingService _loggingService;
    private readonly IConnectWiseService _connectWiseService;
    private readonly string _reasoningModel;

    public BoardRoutingClassifier(
        ILogger logger,
        AzureOpenAIService openAIService,
        LoggingService loggingService,
        IConnectWiseService connectWiseService,
        string reasoningModel)
    {
        _logger = logger;
        _openAIService = openAIService;
        _loggingService = loggingService;
        _connectWiseService = connectWiseService;
        _reasoningModel = reasoningModel;
    }

    async Task<HttpResponseData?> IClassificationStep.ExecuteAsync(DispatchClassificationFunction.TicketClassificationContext context)
    {
        _logger.LogInformation("Starting Board Routing classification - TicketId: {TicketId}, CompanyId: {CompanyId}, CompanyName: {CompanyName}", 
            context.TicketRequest.TicketId, context.TicketRequest.CompanyId, context.TicketRequest.CompanyName ?? "Unknown");
        
        var boardPrompt = await BoardRoutingPromptService.GetPrompt(
            context.TicketRequest.CompanyId,
            ConnectWiseConstants.L1BoardId,
            ConnectWiseConstants.L2BoardId,
            ConnectWiseConstants.L3BoardId,
            ConnectWiseConstants.CaduceusBoardId,
            ConnectWiseConstants.SecurityBoardId,
            ConnectWiseConstants.NOCBoardId);

        var boardInput = InputBuilderService.BuildBoardRoutingInput(context.TicketRequest, context.InitialIntent);
        
        _logger.LogInformation("Calling OpenAI for Board Routing - TicketId: {TicketId}, Intent: {Intent}, Model: {Model}", 
            context.TicketRequest.TicketId, context.InitialIntent ?? "None", _reasoningModel);
        
        var (boardResult, boardTokenUsage, boardModel) = await _openAIService.GetCompletionAsync<BoardRoutingResponse>(
            boardPrompt, boardInput, _reasoningModel, 0f, 3000);

        context.BoardResponse = boardResult;
        context.BoardConfidence = boardResult.ConfidenceScore;
        
        _logger.LogInformation("Board Routing classification completed - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}, Confidence: {Confidence}, Model: {Model}, Tokens: {TotalTokens}", 
            context.TicketRequest.TicketId, boardResult.BoardId, boardResult.BoardName ?? "Unknown", context.BoardConfidence, boardModel, boardTokenUsage.TotalTokens);

        var boardLogData = new Dictionary<string, object?>
        {
            { nameof(DecisionLogEntity.Intent), context.InitialIntent },
            { nameof(DecisionLogEntity.Reason), boardResult.Reason },
            { nameof(DecisionLogEntity.ConfidenceScore), boardResult.ConfidenceScore },
            { nameof(DecisionLogEntity.BoardId), boardResult.BoardId },
            { nameof(DecisionLogEntity.BoardName), boardResult.BoardName },
            { nameof(DecisionLogEntity.Model), boardModel },
            { nameof(DecisionLogEntity.PromptTokens), boardTokenUsage.PromptTokens },
            { nameof(DecisionLogEntity.CompletionTokens), boardTokenUsage.CompletionTokens },
            { nameof(DecisionLogEntity.TotalTokens), boardTokenUsage.TotalTokens }
        };

        await _loggingService.LogDecisionAsync("Board Routing", context.TicketRequest.TicketId, 
            context.TicketRequest.CompanyName ?? "Unknown", boardLogData);

        var activitiesDescription = await _connectWiseService.CreateActivitiesForClassificationAsync(context.TicketRequest.TicketId, context.SpamResponse, context.BoardResponse, 
            context.SpamConfidence, context.BoardConfidence);
        
        _logger.LogInformation("Activities created for classification - TicketId: {TicketId}, Activities: {Activities}", 
            context.TicketRequest.TicketId, activitiesDescription);

        if (context.BoardConfidence < 90)
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

        if (context.BoardConfidence >= 90 && NoteBuilderService.IsNonServiceBoard(context.BoardResponse?.BoardId))
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
        
        return null;
    }
}
