using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models;

public class BoardRoutingData
{
    [JsonPropertyName("board_id")]
    public int BoardId { get; set; }
    
    [JsonPropertyName("board_name")]
    public string BoardName { get; set; } = string.Empty;
    
    [JsonPropertyName("rules")]
    public List<string>? Rules { get; set; }
    
    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }
    
    [JsonPropertyName("tasks")]
    public List<string>? Tasks { get; set; }
}

