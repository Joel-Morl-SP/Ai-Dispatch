namespace Ai_Dispatch.Services.Classification;

public interface IProposedNoteProcessor
{
    Task ProcessAsync(DispatchClassificationFunction.TicketClassificationContext context);
}
