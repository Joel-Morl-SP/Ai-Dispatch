using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services;

public class TicketUpdateConverter : ITicketUpdateConverter
{
    public List<object> ConvertToJsonPatch(TicketUpdateRequest ticketUpdate)
    {
        var patchOperations = new List<object>();

        if (ticketUpdate.Summary != null)
        {
            patchOperations.Add(new { op = "replace", path = "/summary", value = ticketUpdate.Summary });
        }

        if (ticketUpdate.Board != null)
        {
            patchOperations.Add(new { op = "replace", path = "/board", value = ticketUpdate.Board });
        }

        if (ticketUpdate.Type != null)
        {
            patchOperations.Add(new { op = "replace", path = "/type", value = ticketUpdate.Type });
        }

        if (ticketUpdate.SubType != null)
        {
            patchOperations.Add(new { op = "replace", path = "/subType", value = ticketUpdate.SubType });
        }

        if (ticketUpdate.Item != null)
        {
            patchOperations.Add(new { op = "replace", path = "/item", value = ticketUpdate.Item });
        }

        if (ticketUpdate.Priority != null)
        {
            patchOperations.Add(new { op = "replace", path = "/priority", value = ticketUpdate.Priority });
        }

        if (ticketUpdate.Contact != null)
        {
            patchOperations.Add(new { op = "replace", path = "/contact", value = ticketUpdate.Contact });
        }

        if (ticketUpdate.Status != null)
        {
            patchOperations.Add(new { op = "replace", path = "/status", value = ticketUpdate.Status });
        }

        if (ticketUpdate.SkipCallback.HasValue)
        {
            patchOperations.Add(new { op = "replace", path = "/skipCallback", value = ticketUpdate.SkipCallback.Value });
        }

        return patchOperations;
    }
}
