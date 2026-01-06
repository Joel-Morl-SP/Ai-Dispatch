using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class BoardRoutingResponse
{
    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("confidence_score")]
    public int ConfidenceScore { get; set; }

    [JsonPropertyName("board_name")]
    public string? BoardName { get; set; }

    [JsonPropertyName("board_id")]
    public int? BoardId { get; set; }
}

