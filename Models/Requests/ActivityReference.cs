using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Requests;

public class ActivityReference
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
}

