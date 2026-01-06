using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Requests;

public class ProjectTicketNoteRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("internalAnalysisFlag")]
    public bool InternalAnalysisFlag { get; set; } = true;
}

