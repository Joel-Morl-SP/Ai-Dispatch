using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class ItemDefinition
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }
    
    [JsonPropertyName("item")]
    public string? Item { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

