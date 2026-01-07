using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Responses;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class BoardRoutingClassifier
{
    private readonly ILogger _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LoggingService _loggingService;
    private readonly string _reasoningModel;

    public BoardRoutingClassifier(
        ILogger logger,
        AzureOpenAIService openAIService,
        LoggingService loggingService,
        string reasoningModel)
    {
        _logger = logger;
        _openAIService = openAIService;
        _loggingService = loggingService;
        _reasoningModel = reasoningModel;
    }

    public async Task ExecuteAsync(DispatchClassificationFunction.TicketClassificationContext context)
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
    }
}
