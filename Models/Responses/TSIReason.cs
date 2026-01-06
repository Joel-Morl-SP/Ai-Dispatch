using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class TSIReason
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
}

