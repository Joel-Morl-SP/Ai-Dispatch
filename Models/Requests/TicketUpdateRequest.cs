using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Requests;

public class TicketUpdateRequest
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("board")]
    public ActivityReference? Board { get; set; }

    [JsonPropertyName("type")]
    public ActivityTypeReference? Type { get; set; }

    [JsonPropertyName("subType")]
    public ActivityTypeReference? SubType { get; set; }

    [JsonPropertyName("item")]
    public ActivityTypeReference? Item { get; set; }

    [JsonPropertyName("priority")]
    public ActivityTypeReference? Priority { get; set; }

    [JsonPropertyName("contact")]
    public ActivityReference? Contact { get; set; }

    [JsonPropertyName("skipCallback")]
    public bool? SkipCallback { get; set; }

    [JsonPropertyName("status")]
    public ActivityReference? Status { get; set; }
}

