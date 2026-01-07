using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class TicketUpdater : ITicketUpdater
{
    private readonly ILogger<TicketUpdater> _logger;
    private readonly IConnectWiseService _connectWiseService;

    public TicketUpdater(ILogger<TicketUpdater> logger, IConnectWiseService connectWiseService)
    {
        _logger = logger;
        _connectWiseService = connectWiseService;
    }

    public async Task UpdateAsync(TicketClassificationContext context)
    {
        _logger.LogInformation("Preparing final ticket update - TicketId: {TicketId}, Board: {BoardName} (Id: {BoardId}), Type: {TypeName} (Id: {TypeId}), Subtype: {SubtypeName} (Id: {SubtypeId}), Item: {ItemName} (Id: {ItemId}), Priority: {PriorityName} (Id: {PriorityId}), ContactId: {ContactId}, IsVip: {IsVip}, Status: {StatusId}", 
            context.TicketRequest.TicketId, 
            context.BoardResponse?.BoardName ?? "None", context.BoardResponse?.BoardId?.ToString() ?? "None",
            context.TsiResponse?.Type?.Name ?? "None", context.TicketUpdate!.Type?.Id.ToString() ?? "None",
            context.TsiResponse?.Subtype?.Name ?? "None", context.TicketUpdate.SubType?.Id.ToString() ?? "None",
            context.TsiResponse?.Item?.Name ?? "None", context.TicketUpdate.Item?.Id.ToString() ?? "None",
            context.TsiResponse?.Priority?.Name ?? "None", context.TicketUpdate.Priority?.Id.ToString() ?? "None",
            context.TicketUpdate.Contact?.Id.ToString() ?? "None", context.IsVip, context.TicketUpdate.Status?.Id.ToString() ?? "None");
        
        await _connectWiseService.UpdateTicketWithRetryAsync(context.TicketRequest.TicketId, context.TicketUpdate);
        await _connectWiseService.AddNoteToTicketAsync(context.TicketRequest.TicketId, context.FinalNote!);
        
        _logger.LogInformation("Ticket processed successfully - TicketId: {TicketId}, Board: {BoardName}, Type: {TypeName}, Subtype: {SubtypeName}, Item: {ItemName}, Priority: {PriorityName}", 
            context.TicketRequest.TicketId, 
            context.BoardResponse?.BoardName ?? "None",
            context.TsiResponse?.Type?.Name ?? "None",
            context.TsiResponse?.Subtype?.Name ?? "None",
            context.TsiResponse?.Item?.Name ?? "None",
            context.TsiResponse?.Priority?.Name ?? "None");
    }
}
