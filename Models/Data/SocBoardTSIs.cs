using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class SocBoardTSIs
{
    [JsonPropertyName("classification_rules")]
    public List<ClassificationRule> ClassificationRules { get; set; } = new();
    
    [JsonPropertyName("special_rules")]
    public Dictionary<string, SpecialRule>? SpecialRules { get; set; }
}

