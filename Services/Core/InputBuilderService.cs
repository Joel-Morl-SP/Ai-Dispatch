using System.Text;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services;

public class InputBuilderService
{
    public static TicketRequest CleanSummary(TicketRequest request)
    {
        if (!string.IsNullOrEmpty(request.Summary))
        {
            request.Summary = request.Summary
                .Replace("Software Request: ", "")
                .Replace("My Issue is not listed here.", "");
        }
        return request;
    }

    public static string BuildSpamTicketClassificationInput(TicketRequest request)
    {
        var sb = new StringBuilder();
        
        var summary = request.Summary ?? string.Empty;
        summary = summary.Replace("Software Request: ", "").Replace("My Issue is not listed here.", "");
        
        sb.AppendLine($"Summary: {summary}");
        sb.AppendLine();
        sb.AppendLine($"Initial Description: {request.InitialDescription ?? "None"}");
        sb.AppendLine();
        sb.AppendLine($"Created By: {request.CreatedBy ?? "Unknown"}");
        
        return sb.ToString();
    }

    public static string BuildBoardRoutingInput(TicketRequest request, string? intent = null)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("Initial Incoming Ticket Fields:");
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(intent))
        {
            sb.AppendLine($"Intent: {intent}");
            sb.AppendLine();
        }
        
        sb.AppendLine($"Summary: {request.Summary ?? string.Empty}");
        sb.AppendLine($"Issue Description: {request.InitialDescription ?? string.Empty}");
        sb.AppendLine($"Company: {request.CompanyName ?? string.Empty}");
        sb.AppendLine($"Type: {request.Type ?? string.Empty}");
        sb.AppendLine($"Subtype: {request.SubType ?? string.Empty}");
        sb.AppendLine($"Priority: {request.Priority ?? string.Empty}");
        sb.AppendLine($"Item: {request.Item ?? string.Empty}");
        sb.AppendLine($"Team: {request.ServiceTeam?.Name ?? string.Empty}");
        
        if (request.Notes != null && request.Notes.Count > 0)
        {
            sb.AppendLine($"Notes: {string.Join(" ", request.Notes)}");
        }
        else
        {
            sb.AppendLine("Notes: ");
        }
        
        return sb.ToString();
    }

    public static string BuildTSIInput(TicketRequest request, string? intent = null)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("Incoming Fields");
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(intent))
        {
            sb.AppendLine($"Intent: {intent}");
            sb.AppendLine();
        }
        
        sb.AppendLine($"Summary: {request.Summary ?? string.Empty}");
        sb.AppendLine($"Initial Description: {request.InitialDescription ?? string.Empty}");
        sb.AppendLine($"Type: {request.Type ?? string.Empty}");
        sb.AppendLine($"Subtype: {request.SubType ?? string.Empty}");
        sb.AppendLine($"Item: {request.Item ?? string.Empty}");
        sb.AppendLine($"Priority: {request.Priority ?? string.Empty}");
        
        return sb.ToString();
    }

    public static string BuildSummaryInput(TicketRequest request, string? intent = null)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("Initial Incoming Ticket Fields:");
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(intent))
        {
            sb.AppendLine($"Intent: {intent}");
            sb.AppendLine();
        }
        
        sb.AppendLine($"Ticket ID: {request.TicketId}");
        sb.AppendLine($"Summary: {request.Summary ?? string.Empty}");
        sb.AppendLine($"Issue Description: {request.InitialDescription ?? string.Empty}");
        sb.AppendLine($"Contact Name: {request.ContactName ?? "No Contact Added"}");
        sb.AppendLine();
        sb.AppendLine("-------------");
        sb.AppendLine();
        sb.AppendLine("Ticket Notes:");
        sb.AppendLine();
        
        if (request.Notes != null && request.Notes.Count > 0)
        {
            foreach (var note in request.Notes)
            {
                sb.AppendLine(note);
            }
        }
        
        return sb.ToString();
    }
}

