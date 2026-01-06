using System.Text.Json.Serialization;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Models.Requests;

public class SalesActivityRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public ActivityTypeReference Type { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("where")]
    public ActivityReference Where { get; set; } = new() { Id = ConnectWiseActivityConstants.InHouseLocationId };

    [JsonPropertyName("status")]
    public ActivityReference Status { get; set; } = new() { Id = ConnectWiseActivityConstants.ClosedStatusId };

    [JsonPropertyName("ticket")]
    public ActivityReference Ticket { get; set; } = new();

    [JsonPropertyName("assignTo")]
    public ActivityReference AssignTo { get; set; } = new() { Id = ConnectWiseActivityConstants.RewstAssigneeId };

    [JsonPropertyName("dateStart")]
    public string DateStart { get; set; } = string.Empty;
}

