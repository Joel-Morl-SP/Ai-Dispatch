using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Responses;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class SummaryGenerator : IClassificationStep
{
    private readonly ILogger _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LoggingService _loggingService;
    private readonly string _baseModel;

    public SummaryGenerator(
        ILogger logger,
        AzureOpenAIService openAIService,
        LoggingService loggingService,
        string baseModel)
    {
        _logger = logger;
        _openAIService = openAIService;
        _loggingService = loggingService;
        _baseModel = baseModel;
    }

    async Task<HttpResponseData?> IClassificationStep.ExecuteAsync(TicketClassificationContext context)
    {
        _logger.LogInformation("Starting Summary generation - TicketId: {TicketId}", context.TicketRequest.TicketId);
        
        var summaryPrompt = SummaryPromptService.GetPrompt();
        var summaryInput = InputBuilderService.BuildSummaryInput(context.TicketRequest, context.InitialIntent);
        
        _logger.LogInformation("Calling OpenAI for Summary generation - TicketId: {TicketId}, Intent: {Intent}, Model: {Model}", 
            context.TicketRequest.TicketId, context.InitialIntent ?? "None", _baseModel);
        
        var (summaryResult, summaryTokenUsage, summaryModel) = await _openAIService.GetCompletionAsync<SummaryResponse>(
            summaryPrompt, summaryInput, _baseModel, 0f, 500);
        
        context.SummaryResponse = summaryResult;
        
        _logger.LogInformation("Summary generation completed - TicketId: {TicketId}, SubmittedFor: {SubmittedFor}, Model: {Model}, Tokens: {TotalTokens}", 
            context.TicketRequest.TicketId, summaryResult.SubmittedFor ?? "None", summaryModel, summaryTokenUsage.TotalTokens);

        var summaryLogData = new Dictionary<string, object?>
        {
            { nameof(DecisionLogEntity.Intent), context.InitialIntent },
            { nameof(DecisionLogEntity.Reason), summaryResult.Reason },
            { nameof(DecisionLogEntity.ConfidenceScore), summaryResult.ConfidenceScore },
            { nameof(DecisionLogEntity.Summary), summaryResult.NewSummary },
            { nameof(DecisionLogEntity.Model), summaryModel },
            { nameof(DecisionLogEntity.PromptTokens), summaryTokenUsage.PromptTokens },
            { nameof(DecisionLogEntity.CompletionTokens), summaryTokenUsage.CompletionTokens },
            { nameof(DecisionLogEntity.TotalTokens), summaryTokenUsage.TotalTokens }
        };

        await _loggingService.LogDecisionAsync("Summary", context.TicketRequest.TicketId, 
            context.TicketRequest.CompanyName ?? "Unknown", summaryLogData);
        
        return null;
    }
}
