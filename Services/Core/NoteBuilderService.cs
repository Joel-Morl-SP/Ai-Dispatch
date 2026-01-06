using System.Text;
using System.Text.Json;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Services;

public static class NoteBuilderService
{
    private const int HelpDeskBoardId = 3;
    private const int ClientStrategyBoardId = 54;
    private const int FieldServicesBoardId = 139;
    private const int SecurityEngBoardId = 154;

    public static string BuildSummary(string? submittedFor, string newSummary, bool isVip)
    {
        if (string.IsNullOrEmpty(submittedFor))
        {
            return newSummary.Length > 100 ? newSummary.Substring(0, 100) : newSummary;
        }

        string summary;
        if (isVip)
        {
            summary = $"VIP | {submittedFor} | {newSummary}";
        }
        else
        {
            summary = $"{submittedFor} | {newSummary}";
        }

        return summary.Length > 100 ? summary.Substring(0, 100) : summary;
    }

    public static string BuildNote(
        SpamClassificationResponse? spamResponse,
        BoardRoutingResponse? boardResponse,
        TSIClassificationResponse? tsiResponse,
        int spamConfidence,
        int boardConfidence,
        string? initialIntent)
    {
        if (spamResponse != null && spamResponse.Decision == "Spam" && spamConfidence == 100)
        {
            return BuildSpam100Note(spamResponse, initialIntent);
        }

        if (spamResponse != null && spamResponse.Decision == "Spam" && spamConfidence == 95)
        {
            return BuildPossibleSpamNote(spamResponse, initialIntent);
        }

        if (boardResponse != null && boardConfidence < 90)
        {
            return BuildLowBoardConfidenceNote(boardResponse, initialIntent);
        }

        if (boardResponse != null && boardConfidence >= 90 && IsNonServiceBoard(boardResponse.BoardId))
        {
            return BuildNonServiceBoardNote(boardResponse, initialIntent);
        }

        return BuildSuccessNote(spamResponse, boardResponse, tsiResponse, initialIntent);
    }

    public static string BuildSpam100Note(SpamClassificationResponse spamResponse, string? initialIntent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("This ticket is being closed due to the Confidence Score returned being 100% for a SPAM classification.");
        sb.AppendLine();
        sb.AppendLine($"Intent Found: {initialIntent ?? "None"}");
        sb.AppendLine();
        sb.AppendLine($"Reason for Decision: {spamResponse.Reason}");
        return sb.ToString();
    }

    public static string BuildPossibleSpamNote(SpamClassificationResponse spamResponse, string? initialIntent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("This ticket is being moved to 'Triage Review' for verification due to the Confidence Score returned being 95% for a SPAM classification.");
        sb.AppendLine();
        sb.AppendLine($"Intent Found: {initialIntent ?? "None"}");
        sb.AppendLine();
        sb.AppendLine($"Reason for Decision: {spamResponse.Reason}");
        return sb.ToString();
    }

    public static string BuildLowBoardConfidenceNote(BoardRoutingResponse boardResponse, string? initialIntent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("This ticket is being moved to 'Triage Review' for verification due to the Confidence Score returned being less than 90% for a Board Classification.");
        sb.AppendLine();
        sb.AppendLine($"Board Classification: {boardResponse.BoardName}");
        sb.AppendLine();
        sb.AppendLine($"Intent Found: {initialIntent ?? "None"}");
        sb.AppendLine();
        sb.AppendLine($"Reason for Decision: {boardResponse.Reason}");
        return sb.ToString();
    }

    public static string BuildNonServiceBoardNote(BoardRoutingResponse boardResponse, string? initialIntent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("This ticket is being moved to 'Triage Review' for verification due to the Board Classification resulting in a Non-Service board");
        sb.AppendLine();
        sb.AppendLine($"Board Classification: {boardResponse.BoardName}");
        sb.AppendLine();
        sb.AppendLine($"Intent Found: {initialIntent ?? "None"}");
        sb.AppendLine();
        sb.AppendLine($"Reason for Decision: {boardResponse.Reason}");
        return sb.ToString();
    }

    public static string BuildInfoAlertNote(TicketClassificationResponse ticketResponse, string? initialIntent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("This ticket is being moved to 'Triage Review' for verification due to the Confidence Score returned being 95% for an Info-Alert classification.");
        sb.AppendLine();
        sb.AppendLine($"Intent Found: {initialIntent ?? "None"}");
        sb.AppendLine();
        sb.AppendLine($"Reason for Decision: {ticketResponse.Reason}");
        return sb.ToString();
    }

    public static string BuildNoCompanyNote()
    {
        var sb = new StringBuilder();
        sb.AppendLine("This ticket is being moved to 'Triage Review' for verification due to the Company Name not being able to be resolved.");
        sb.AppendLine();
        sb.AppendLine("**Prior to dispatching a ticket, ensure the following steps are completed**");
        sb.AppendLine();
        sb.AppendLine("**1. Handle Device - Generated Tickets:** If the ticket is generated via an alert from a device, identify the associated company and assign the ticket accordingly.");
        sb.AppendLine();
        sb.AppendLine("    **1.** For the technician who works on this, please update the device to point to noc@sourcepass.com.");
        sb.AppendLine();
        sb.AppendLine("**2. Verify Contact Legitimacy:** If the ticket is generated via an e-mail from a contact, confirm the legitimacy of the contact for the company by coordinating with the designated Point of Contact (POC).");
        sb.AppendLine();
        sb.AppendLine("**3. Update Contact Information:** If the contact is verified and legitimate, add the contact details to ConnectWise Manage.");
        sb.AppendLine();
        sb.AppendLine("**4. Dispatch Ticket:** Proceed with dispatching the ticket only after the contact has been successfully added to ConnectWise Manage and, if applicable, the company has been identified and assigned.");
        return sb.ToString();
    }

    private static string BuildSuccessNote(
        SpamClassificationResponse? spamResponse,
        BoardRoutingResponse? boardResponse,
        TSIClassificationResponse? tsiResponse,
        string? initialIntent)
    {
        var sb = new StringBuilder();

        if (spamResponse != null)
        {
            sb.AppendLine("**--SPAM Classification--**");
            sb.AppendLine();
            sb.AppendLine($"**Decision:** {spamResponse.Decision}");
            sb.AppendLine($"**Intent Found:** {initialIntent ?? "None"}");
            sb.AppendLine($"**Reason for Decision:** {spamResponse.Reason}");
            sb.AppendLine();
        }

        if (boardResponse != null)
        {
            sb.AppendLine("**--Board Classification--**");
            sb.AppendLine();
            sb.AppendLine($"**Decision:** {boardResponse.BoardName}");
            sb.AppendLine($"**Intent Found:** {initialIntent ?? "None"}");
            sb.AppendLine($"**Reason for Decision:** {boardResponse.Reason}");
            sb.AppendLine();
        }

        if (tsiResponse != null)
        {
            sb.AppendLine("**--TSI Classifications--**");
            sb.AppendLine();
            sb.AppendLine($"**Intent Found:** {initialIntent ?? "None"}");

            var reasonObject = GetStructuredReason(tsiResponse.Reason);

            if (tsiResponse.Type != null)
            {
                sb.AppendLine();
                sb.AppendLine("**--Type--**");
                sb.AppendLine($"**Decision:** {tsiResponse.Type.Name}");
                if (reasonObject != null && !string.IsNullOrEmpty(reasonObject.Type))
                {
                    sb.AppendLine($"**Reason for Decision:** {reasonObject.Type}");
                }
            }

            if (tsiResponse.Subtype != null)
            {
                sb.AppendLine();
                sb.AppendLine("**--Subtype--**");
                sb.AppendLine($"**Decision:** {tsiResponse.Subtype.Name}");
                if (reasonObject != null && !string.IsNullOrEmpty(reasonObject.Subtype))
                {
                    sb.AppendLine($"**Reason for Decision:** {reasonObject.Subtype}");
                }
            }

            if (tsiResponse.Item != null)
            {
                sb.AppendLine();
                sb.AppendLine("**--Item--**");
                sb.AppendLine($"**Decision:** {tsiResponse.Item.Name}");
                if (reasonObject != null && !string.IsNullOrEmpty(reasonObject.Item))
                {
                    sb.AppendLine($"**Reason for Decision:** {reasonObject.Item}");
                }
            }

            if (tsiResponse.Priority != null)
            {
                sb.AppendLine();
                sb.AppendLine("**--Priority--**");
                sb.AppendLine($"**Decision:** {tsiResponse.Priority.Name}");
                if (reasonObject != null && !string.IsNullOrEmpty(reasonObject.Priority))
                {
                    sb.AppendLine($"**Reason for Decision:** {reasonObject.Priority}");
                }
            }

            if (reasonObject == null)
            {
                var reasonText = GetSimpleReason(tsiResponse.Reason);
                if (!string.IsNullOrEmpty(reasonText))
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Reason:** {reasonText}");
                }
            }
        }

        return sb.ToString();
    }

    public static bool IsNonServiceBoard(int? boardId)
    {
        if (!boardId.HasValue) return false;

        return boardId.Value == HelpDeskBoardId ||
               boardId.Value == ClientStrategyBoardId ||
               boardId.Value == FieldServicesBoardId ||
               boardId.Value == SecurityEngBoardId;
    }

    private static TSIReason? GetStructuredReason(JsonElement reasonElement)
    {
        if (reasonElement.ValueKind == JsonValueKind.Object && reasonElement.TryGetProperty("type", out _))
        {
            try
            {
                return JsonSerializer.Deserialize<TSIReason>(reasonElement.GetRawText());
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static string GetSimpleReason(JsonElement reasonElement)
    {
        if (reasonElement.ValueKind == JsonValueKind.String)
        {
            return reasonElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }
}

