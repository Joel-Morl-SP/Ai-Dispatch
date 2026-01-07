using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services;

public interface ITicketUpdateConverter
{
    List<object> ConvertToJsonPatch(TicketUpdateRequest ticketUpdate);
}
