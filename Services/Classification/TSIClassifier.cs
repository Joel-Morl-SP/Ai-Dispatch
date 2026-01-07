using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class TSIClassifier
{
    private readonly ILogger _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LoggingService _loggingService;
    private readonly string _reasoningModel;

    public TSIClassifier(
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
        _logger.LogInformation("Starting TSI Classification - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}", 
            context.TicketRequest.TicketId, context.BoardResponse!.BoardId, context.BoardResponse.BoardName ?? "Unknown");
        
        var tsiPrompt = await TSIPromptService.GetPrompt(context.BoardResponse.BoardId, context.BoardResponse.BoardName);
        var tsiInput = InputBuilderService.BuildTSIInput(context.TicketRequest, context.InitialIntent);
        
        _logger.LogInformation("Calling OpenAI for TSI Classification - TicketId: {TicketId}, Intent: {Intent}, Model: {Model}", 
            context.TicketRequest.TicketId, context.InitialIntent ?? "None", _reasoningModel);
        
        var (tsiResult, tsiTokenUsage, tsiModel) = await _openAIService.GetCompletionAsync<TSIClassificationResponse>(
            tsiPrompt, tsiInput, _reasoningModel, 0f, 15000);
        
        context.TsiResponse = tsiResult;
        
        _logger.LogInformation("TSI Classification completed - TicketId: {TicketId}, Type: {Type}, Subtype: {Subtype}, Item: {Item}, Priority: {Priority}, Confidence: {Confidence}, Model: {Model}, Tokens: {TotalTokens}", 
            context.TicketRequest.TicketId, tsiResult.Type?.Name ?? "None", tsiResult.Subtype?.Name ?? "None", tsiResult.Item?.Name ?? "None", 
            tsiResult.Priority?.Name ?? "None", tsiResult.ConfidenceScore, tsiModel, tsiTokenUsage.TotalTokens);

        var tsiLogData = new Dictionary<string, object?>();
        if (context.InitialIntent != null) tsiLogData[nameof(DecisionLogEntity.Intent)] = context.InitialIntent;
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

        await _loggingService.LogDecisionAsync("TSI Classification", context.TicketRequest.TicketId, 
            context.TicketRequest.CompanyName ?? "Unknown", tsiLogData);
    }
}
