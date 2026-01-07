using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

public interface ITicketRequestLogger
{
    void LogTicketRequestDetails(TicketClassificationContext context);
}
