using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services;

public class TicketUpdateDescriptionBuilder
{
    public static string BuildTicketUpdateDescription(int ticketId, TicketUpdateRequest updateRequest, bool isRealUpdate = false)
    {
        var prefix = isRealUpdate ? "Updated ticket" : "Would update ticket";
        var parts = new List<string> { $"{prefix} {ticketId}" };
        
        if (updateRequest.Summary != null)
            parts.Add($"Summary: {updateRequest.Summary}");
        if (updateRequest.Board != null)
            parts.Add($"BoardId: {updateRequest.Board.Id}");
        if (updateRequest.Type != null)
        {
            var typeName = GetTypeName(updateRequest.Type.Id);
            parts.Add($"Type: {typeName} (Id: {updateRequest.Type.Id})");
        }
        if (updateRequest.SubType != null)
            parts.Add($"SubtypeId: {updateRequest.SubType.Id}");
        if (updateRequest.Item != null)
            parts.Add($"ItemId: {updateRequest.Item.Id}");
        if (updateRequest.Priority != null)
            parts.Add($"PriorityId: {updateRequest.Priority.Id}");
        if (updateRequest.Contact != null)
            parts.Add($"ContactId: {updateRequest.Contact.Id}");
        if (updateRequest.Status != null)
        {
            var statusName = GetStatusName(updateRequest.Status.Id);
            parts.Add($"Status: {statusName} (Id: {updateRequest.Status.Id})");
        }
        if (updateRequest.SkipCallback.HasValue)
            parts.Add($"SkipCallback: {updateRequest.SkipCallback.Value}");
        
        return string.Join(", ", parts);
    }

    private static string GetTypeName(int typeId)
    {
        return typeId switch
        {
            7864 => "Continual Service Improvement",
            _ => $"TypeId-{typeId}"
        };
    }

    private static string GetStatusName(int? statusId)
    {
        return statusId switch
        {
            4084 => "Triage Review",
            163 => "Closing",
            _ => $"StatusId-{statusId}"
        };
    }
}
