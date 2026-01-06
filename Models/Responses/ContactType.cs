using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class ContactType
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

