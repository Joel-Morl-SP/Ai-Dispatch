using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class PriorityDefinition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;
    
    [JsonPropertyName("definition")]
    public string Definition { get; set; } = string.Empty;
    
    [JsonPropertyName("examples")]
    public List<string> Examples { get; set; } = new();
    
    [JsonPropertyName("response_target")]
    public string ResponseTarget { get; set; } = string.Empty;
}

