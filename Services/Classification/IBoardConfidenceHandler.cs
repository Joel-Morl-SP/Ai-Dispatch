using Microsoft.Azure.Functions.Worker.Http;

namespace Ai_Dispatch.Services.Classification;

public interface IBoardConfidenceHandler
{
    Task<HttpResponseData> HandleLowBoardConfidenceAsync(DispatchClassificationFunction.TicketClassificationContext context);
    Task<HttpResponseData> HandleNonServiceBoardAsync(DispatchClassificationFunction.TicketClassificationContext context);
}
