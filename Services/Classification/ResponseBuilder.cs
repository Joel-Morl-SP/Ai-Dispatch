using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class ResponseBuilder : IResponseBuilder
{
    private readonly ILogger<ResponseBuilder> _logger;
    private readonly TeamsAlertService _teamsAlertService;

    public ResponseBuilder(ILogger<ResponseBuilder> logger, TeamsAlertService teamsAlertService)
    {
        _logger = logger;
        _teamsAlertService = teamsAlertService;
    }

    public async Task<HttpResponseData> BuildSuccessResponseAsync(TicketClassificationContext context)
    {
        var successResponse = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
        successResponse.Headers.Add("Content-Type", "application/json");
        
        var successResponseBody = new Dictionary<string, object?>
        {
            { "message", "Ticket processed successfully" },
            { "newBoardName", context.BoardResponse?.BoardName ?? null },
            { "newBoardId", context.BoardResponse?.BoardId ?? null },
            { "newTypeName", context.TsiResponse?.Type?.Name ?? null },
            { "newTypeId", context.TsiResponse?.Type?.Id ?? null },
            { "newSummary", context.FinalSummary }
        };
        
        if (context.ProposedNoteSent)
        {
            successResponseBody["proposedNoteSent"] = true;
        }
        
        var successJson = JsonSerializer.Serialize(successResponseBody);
        await successResponse.WriteStringAsync(successJson);
        return successResponse;
    }

    public async Task<HttpResponseData> HandleErrorAsync(TicketClassificationContext context, Exception ex)
    {
        _logger.LogError(ex, "Error processing ticket classification - ExceptionType: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
            ex.GetType().Name, ex.Message, ex.StackTrace);
        
        var failureMessage = context.IsNoCompanyBranch 
            ? "Change Me SPAM Classification Failure" 
            : "Dispatch Triage Failure";
        
        await _teamsAlertService.SendAlertAsync(failureMessage);
        
        var errorResponse = context.Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        errorResponse.Headers.Add("Content-Type", "application/json");
        var errorResponseBody = JsonSerializer.Serialize(new { message = $"Error: {ex.Message}" });
        await errorResponse.WriteStringAsync(errorResponseBody);
        return errorResponse;
    }
}
