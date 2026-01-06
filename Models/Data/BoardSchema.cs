using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class BoardSchema
{
    [JsonPropertyName("board_id")]
    public int BoardId { get; set; }
    
    [JsonPropertyName("board_name")]
    public string BoardName { get; set; } = string.Empty;
    
    [JsonPropertyName("types")]
    public List<TypeDefinition> Types { get; set; } = new();
    
    [JsonPropertyName("sub_types")]
    public List<SubTypeDefinition> SubTypes { get; set; } = new();
    
    [JsonPropertyName("board_items")]
    public List<BoardItem> BoardItems { get; set; } = new();
}

