namespace Ai_Dispatch.Services.Classification;

public interface ITicketUpdater
{
    Task UpdateAsync(DispatchClassificationFunction.TicketClassificationContext context);
}
