using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Responses;

public class ContactResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("types")]
    public List<ContactType> Types { get; set; } = new();
}

