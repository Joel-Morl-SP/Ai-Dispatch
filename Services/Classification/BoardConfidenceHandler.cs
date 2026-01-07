using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class BoardConfidenceHandler : IBoardConfidenceHandler
{
    private readonly ILogger<BoardConfidenceHandler> _logger;
    private readonly IConnectWiseService _connectWiseService;

    public BoardConfidenceHandler(ILogger<BoardConfidenceHandler> logger, IConnectWiseService connectWiseService)
    {
        _logger = logger;
        _connectWiseService = connectWiseService;
    }

    public async Task<HttpResponseData> HandleLowBoardConfidenceAsync(TicketClassificationContext context)
    {
        _logger.LogInformation("Board confidence below threshold ({Confidence} < 90) - Moving to Triage Review - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}", 
            context.BoardResponse!.ConfidenceScore, context.TicketRequest.TicketId, context.BoardResponse.BoardId, context.BoardResponse.BoardName ?? "Unknown");
        
        var note = NoteBuilderService.BuildLowBoardConfidenceNote(context.BoardResponse, context.InitialIntent);
        var updateRequest = new TicketUpdateRequest
        {
            Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
        };

        await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
        await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
        
        _logger.LogInformation("Low board confidence ticket processed - TicketId: {TicketId}, Confidence: {Confidence}, Status: Triage Review, Actions: Successful", 
            context.TicketRequest.TicketId, context.BoardResponse.ConfidenceScore);

        var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Low Board Confidence" });
        await response.WriteStringAsync(responseBody);
        return response;
    }

    public async Task<HttpResponseData> HandleNonServiceBoardAsync(TicketClassificationContext context)
    {
        _logger.LogInformation("Non-service board detected (BoardId: {BoardId}, BoardName: {BoardName}) - Moving to Triage Review - TicketId: {TicketId}", 
            context.BoardResponse!.BoardId, context.BoardResponse.BoardName ?? "Unknown", context.TicketRequest.TicketId);
        
        var note = NoteBuilderService.BuildNonServiceBoardNote(context.BoardResponse, context.InitialIntent);
        var updateRequest = new TicketUpdateRequest
        {
            Status = new ActivityReference { Id = ConnectWiseConstants.AdminReviewStatusId }
        };

        await _connectWiseService.UpdateTicketAsync(context.TicketRequest.TicketId, updateRequest);
        await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, note);
        
        _logger.LogInformation("Non-service board ticket processed - TicketId: {TicketId}, BoardId: {BoardId}, BoardName: {BoardName}, Status: Triage Review, Actions: Successful", 
            context.TicketRequest.TicketId, context.BoardResponse.BoardId, context.BoardResponse.BoardName ?? "Unknown");

        var response = context.Request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        var responseBody = JsonSerializer.Serialize(new { message = "Ticket moved to Triage Review - Non-Service Board" });
        await response.WriteStringAsync(responseBody);
        return response;
    }
}
