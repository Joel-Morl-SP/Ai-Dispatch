using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class TSIClassificationResponse
{
    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("reason")]
    public JsonElement Reason { get; set; }

    [JsonPropertyName("confidence_score")]
    public int ConfidenceScore { get; set; }

    [JsonPropertyName("type")]
    public TSIObject? Type { get; set; }

    [JsonPropertyName("subtype")]
    public TSIObject? Subtype { get; set; }

    [JsonPropertyName("item")]
    public TSIObject? Item { get; set; }

    [JsonPropertyName("priority")]
    public TSIObject? Priority { get; set; }

    [JsonPropertyName("board_name")]
    public string? BoardName { get; set; }
}

