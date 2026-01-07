using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

public interface ITicketUpdater
{
    Task UpdateAsync(TicketClassificationContext context);
}
