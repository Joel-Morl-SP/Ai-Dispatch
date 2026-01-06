using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class SubTypeDefinition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("typeAssociationIds")]
    public List<int> TypeAssociationIds { get; set; } = new();
    
    [JsonPropertyName("parentId")]
    public int? ParentId { get; set; }
}

