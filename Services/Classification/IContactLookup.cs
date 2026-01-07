using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

public interface IContactLookup
{
    Task LookupAsync(TicketClassificationContext context);
}
