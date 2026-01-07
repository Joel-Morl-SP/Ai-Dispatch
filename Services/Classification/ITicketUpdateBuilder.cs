using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services.Classification;

public interface ITicketUpdateBuilder
{
    TicketUpdateRequest Build(DispatchClassificationFunction.TicketClassificationContext context);
}
