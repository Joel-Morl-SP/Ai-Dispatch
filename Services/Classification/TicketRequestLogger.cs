using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

public class TicketRequestLogger : ITicketRequestLogger
{
    private readonly ILogger<TicketRequestLogger> _logger;

    public TicketRequestLogger(ILogger<TicketRequestLogger> logger)
    {
        _logger = logger;
    }

    public void LogTicketRequestDetails(TicketClassificationContext context)
    {
        _logger.LogInformation("Ticket request received - TicketId: {TicketId}, CompanyId: {CompanyId}, CompanyName: {CompanyName}", 
            context.TicketRequest.TicketId, context.TicketRequest.CompanyId, context.TicketRequest.CompanyName ?? "Unknown");

        _logger.LogInformation("Full ticket request - TicketId: {TicketId}, Summary: {Summary}, InitialDescription: {InitialDescription}, ContactName: {ContactName}, CreatedBy: {CreatedBy}, ServiceTeam: {ServiceTeamName} (Id: {ServiceTeamId}), Type: {Type}, SubType: {SubType}, Item: {Item}, Priority: {Priority}, NotesCount: {NotesCount}",
            context.TicketRequest.TicketId,
            context.TicketRequest.Summary ?? "None",
            context.TicketRequest.InitialDescription ?? "None",
            context.TicketRequest.ContactName ?? "None",
            context.TicketRequest.CreatedBy ?? "None",
            context.TicketRequest.ServiceTeam?.Name ?? "None",
            context.TicketRequest.ServiceTeam?.Id ?? 0,
            context.TicketRequest.Type ?? "None",
            context.TicketRequest.SubType ?? "None",
            context.TicketRequest.Item ?? "None",
            context.TicketRequest.Priority ?? "None",
            context.TicketRequest.Notes?.Count ?? 0);
    }
}
