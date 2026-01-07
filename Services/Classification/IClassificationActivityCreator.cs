using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

public interface IClassificationActivityCreator
{
    Task CreateAsync(TicketClassificationContext context);
}
