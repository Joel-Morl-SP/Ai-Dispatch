namespace Ai_Dispatch.Services.Classification;

public interface IClassificationActivityCreator
{
    Task CreateAsync(DispatchClassificationFunction.TicketClassificationContext context);
}
