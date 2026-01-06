using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class SummaryResponse
{
    [JsonPropertyName("submitted_for")]
    public string? SubmittedFor { get; set; }

    [JsonPropertyName("new_summary")]
    public string? NewSummary { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("confidence_score")]
    public int ConfidenceScore { get; set; }
}

