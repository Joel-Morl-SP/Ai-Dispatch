using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class TypeDefinition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    public string? Category { get; set; }
    
    [JsonPropertyName("parentId")]
    public int? ParentId { get; set; }
}

