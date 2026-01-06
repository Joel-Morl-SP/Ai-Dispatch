using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class TSIObject
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
