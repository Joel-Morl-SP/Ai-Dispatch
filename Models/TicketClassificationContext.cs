using Microsoft.Azure.Functions.Worker.Http;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Models;

public class TicketClassificationContext
{
    public TicketRequest TicketRequest { get; set; } = null!;
    public HttpRequestData Request { get; set; } = null!;
    public SpamClassificationResponse? SpamResponse { get; set; }
    public TicketClassificationResponse? TicketResponse { get; set; }
    public BoardRoutingResponse? BoardResponse { get; set; }
    public TSIClassificationResponse? TsiResponse { get; set; }
    public SummaryResponse? SummaryResponse { get; set; }
    public int SpamConfidence { get; set; }
    public int BoardConfidence { get; set; }
    public string? InitialIntent { get; set; }
    public bool IsNoCompany { get; set; }
    public bool IsNoCompanyBranch { get; set; }
    public ContactResponse? Contact { get; set; }
    public bool IsVip { get; set; }
    public string? FinalSummary { get; set; }
    public string? FinalNote { get; set; }
    public TicketUpdateRequest? TicketUpdate { get; set; }
    public bool ProposedNoteSent { get; set; }
}
