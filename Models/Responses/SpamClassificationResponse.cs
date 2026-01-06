using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class SpamClassificationResponse
{
    [JsonPropertyName("decision")]
    public string? Decision { get; set; }

    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("confidence_score")]
    public int ConfidenceScore { get; set; }
}

