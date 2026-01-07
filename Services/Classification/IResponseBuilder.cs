using Microsoft.Azure.Functions.Worker.Http;

namespace Ai_Dispatch.Services.Classification;

public interface IResponseBuilder
{
    Task<HttpResponseData> BuildSuccessResponseAsync(DispatchClassificationFunction.TicketClassificationContext context);
    Task<HttpResponseData> HandleErrorAsync(DispatchClassificationFunction.TicketClassificationContext context, Exception ex);
}
