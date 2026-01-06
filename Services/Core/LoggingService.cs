using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services;

public class LoggingService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(IConfiguration configuration, ILogger<LoggingService> logger)
    {
        _logger = logger;
        
        var connectionString = configuration["AzureWebJobsStorage"];
        var tableName = configuration["DECISION_LOG_TABLE_NAME"] ?? "DecisionLogs";

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string is missing. Please set AzureWebJobsStorage environment variable.");
        }

        _tableClient = new TableClient(connectionString, tableName);
    }

    public async Task LogDecisionAsync(string decisionType, int ticketId, string companyName, Dictionary<string, object?> logData)
    {
        try
        {
            var entity = new DecisionLogEntity
            {
                PartitionKey = decisionType,
                RowKey = ticketId.ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                Company = companyName
            };

            if (logData.TryGetValue(nameof(DecisionLogEntity.BoardId), out var boardId) && boardId != null)
                entity.BoardId = Convert.ToInt32(boardId);

            if (logData.TryGetValue(nameof(DecisionLogEntity.BoardName), out var boardName) && boardName != null)
                entity.BoardName = boardName.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.CompletionTokens), out var completionTokens) && completionTokens != null)
                entity.CompletionTokens = Convert.ToInt32(completionTokens);

            if (logData.TryGetValue(nameof(DecisionLogEntity.ConfidenceScore), out var confidenceScore) && confidenceScore != null)
                entity.ConfidenceScore = Convert.ToInt32(confidenceScore);

            if (logData.TryGetValue(nameof(DecisionLogEntity.Intent), out var intent) && intent != null)
                entity.Intent = intent.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.Item), out var item) && item != null)
                entity.Item = item.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.Model), out var model) && model != null)
                entity.Model = model.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.Priority), out var priority) && priority != null)
                entity.Priority = priority.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.PromptTokens), out var promptTokens) && promptTokens != null)
                entity.PromptTokens = Convert.ToInt32(promptTokens);

            if (logData.TryGetValue(nameof(DecisionLogEntity.Reason), out var reason) && reason != null)
                entity.Reason = reason.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.Subtype), out var subtype) && subtype != null)
                entity.Subtype = subtype.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.Type), out var type) && type != null)
                entity.Type = type.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.Summary), out var summary) && summary != null)
                entity.Summary = summary.ToString();

            if (logData.TryGetValue(nameof(DecisionLogEntity.TotalTokens), out var totalTokens) && totalTokens != null)
                entity.TotalTokens = Convert.ToInt32(totalTokens);

            if (logData.TryGetValue(nameof(DecisionLogEntity.Classification), out var classification) && classification != null)
                entity.Classification = classification.ToString();

            await _tableClient.UpsertEntityAsync(entity);

            _logger.LogInformation("Logged decision: {DecisionType} for ticket {TicketId}", decisionType, ticketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging decision: {DecisionType} for ticket {TicketId}", decisionType, ticketId);
            throw;
        }
    }
}

