using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services.Classification;

public class RequestValidator : IRequestValidator
{
    private readonly ILogger<RequestValidator> _logger;

    public RequestValidator(ILogger<RequestValidator> logger)
    {
        _logger = logger;
    }

    public async Task<TicketRequest?> ValidateAndDeserializeRequestAsync(HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        var ticketRequest = JsonSerializer.Deserialize<TicketRequest>(requestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (ticketRequest == null)
        {
            _logger.LogWarning("Invalid request body - ticketRequest is null after deserialization");
            return null;
        }

        return ticketRequest;
    }

    public async Task<HttpResponseData> BuildBadRequestResponseAsync(HttpRequestData req)
    {
        var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        badResponse.Headers.Add("Content-Type", "application/json");
        var badResponseBody = JsonSerializer.Serialize(new { message = "Invalid request body" });
        await badResponse.WriteStringAsync(badResponseBody);
        return badResponse;
    }
}
