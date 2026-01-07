using Microsoft.Azure.Functions.Worker.Http;
using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

public interface IBoardConfidenceHandler
{
    Task<HttpResponseData> HandleLowBoardConfidenceAsync(TicketClassificationContext context);
    Task<HttpResponseData> HandleNonServiceBoardAsync(TicketClassificationContext context);
}
