namespace Ai_Dispatch.Services.Classification;

public interface IContactLookup
{
    Task LookupAsync(DispatchClassificationFunction.TicketClassificationContext context);
}
