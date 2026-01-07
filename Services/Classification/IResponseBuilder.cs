using Microsoft.Azure.Functions.Worker.Http;
using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

public interface IResponseBuilder
{
    Task<HttpResponseData> BuildSuccessResponseAsync(TicketClassificationContext context);
    Task<HttpResponseData> HandleErrorAsync(TicketClassificationContext context, Exception ex);
}
