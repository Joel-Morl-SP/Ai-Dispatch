namespace Ai_Dispatch.Services.Classification;

public interface ITicketRequestLogger
{
    void LogTicketRequestDetails(DispatchClassificationFunction.TicketClassificationContext context);
}
