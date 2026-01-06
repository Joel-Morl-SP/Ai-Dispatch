using Azure;
using Azure.Data.Tables;

namespace Ai_Dispatch.Models;

public class DecisionLogEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int? BoardId { get; set; }
    public string? BoardName { get; set; }
    public string? Company { get; set; }
    public int? CompletionTokens { get; set; }
    public int? ConfidenceScore { get; set; }
    public string? Intent { get; set; }
    public string? Item { get; set; }
    public string? Model { get; set; }
    public string? Priority { get; set; }
    public int? PromptTokens { get; set; }
    public string? Reason { get; set; }
    public string? Subtype { get; set; }
    public string? Type { get; set; }
    public string? Summary { get; set; }
    public int? TotalTokens { get; set; }
    public string? Classification { get; set; }
}

