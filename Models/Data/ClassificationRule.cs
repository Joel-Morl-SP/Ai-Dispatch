using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class ClassificationRule
{
    [JsonPropertyName("keyword")]
    public string Keyword { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("subtype")]
    public string Subtype { get; set; } = string.Empty;
    
    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;
}

