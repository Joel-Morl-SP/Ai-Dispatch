using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class SpecialRule
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("indicators")]
    public List<string> Indicators { get; set; } = new();
}

