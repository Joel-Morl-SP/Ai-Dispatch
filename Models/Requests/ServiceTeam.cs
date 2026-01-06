using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Requests;

public class ServiceTeam
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

