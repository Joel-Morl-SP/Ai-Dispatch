using Microsoft.Azure.Functions.Worker.Http;
using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services.Classification;

public interface IRequestValidator
{
    Task<TicketRequest?> ValidateAndDeserializeRequestAsync(HttpRequestData req);
    Task<HttpResponseData> BuildBadRequestResponseAsync(HttpRequestData req);
}
