using System.Text.Json.Serialization;

namespace Ai_Dispatch.Models.Requests;

public class TicketRequest
{
    [JsonPropertyName("company_id")]
    public int CompanyId { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("contact_id")]
    public int? ContactId { get; set; }

    [JsonPropertyName("contact_name")]
    public string? ContactName { get; set; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("initial_description")]
    public string? InitialDescription { get; set; }

    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("notes")]
    public List<string>? Notes { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("service_team")]
    public ServiceTeam? ServiceTeam { get; set; }

    [JsonPropertyName("not_streamline_client")]
    public bool NotStreamlineClient { get; set; }

    [JsonPropertyName("it_glue_org_id")]
    public int ItGlueOrgId { get; set; }

    [JsonPropertyName("sub_type")]
    public string? SubType { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("ticket_id")]
    public int TicketId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

