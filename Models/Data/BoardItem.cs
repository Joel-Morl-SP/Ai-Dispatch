using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class BoardItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("parentId")]
    public int? ParentId { get; set; }
}

