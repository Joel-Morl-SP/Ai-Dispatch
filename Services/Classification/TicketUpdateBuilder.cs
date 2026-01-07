using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services.Classification;

public class TicketUpdateBuilder : ITicketUpdateBuilder
{
    public TicketUpdateRequest Build(TicketClassificationContext context)
    {
        var ticketUpdate = new TicketUpdateRequest
        {
            Summary = context.FinalSummary,
            Board = context.BoardResponse != null && context.BoardResponse.BoardId.HasValue ? new ActivityReference { Id = context.BoardResponse.BoardId.Value } : null,
            SkipCallback = true
        };

        if (context.TsiResponse?.Type != null && context.TsiResponse.Type.Id.HasValue)
        {
            ticketUpdate.Type = new ActivityTypeReference { Id = context.TsiResponse.Type.Id.Value };
        }

        if (context.TsiResponse?.Subtype != null && context.TsiResponse.Subtype.Id.HasValue)
        {
            ticketUpdate.SubType = new ActivityTypeReference { Id = context.TsiResponse.Subtype.Id.Value };
        }

        if (context.TsiResponse?.Item != null && context.TsiResponse.Item.Id.HasValue)
        {
            ticketUpdate.Item = new ActivityTypeReference { Id = context.TsiResponse.Item.Id.Value };
        }

        if (context.TsiResponse?.Priority != null && context.TsiResponse.Priority.Id.HasValue)
        {
            ticketUpdate.Priority = new ActivityTypeReference { Id = context.TsiResponse.Priority.Id.Value };
        }

        if (context.Contact != null)
        {
            ticketUpdate.Contact = new ActivityReference { Id = context.Contact.Id };
        }

        if (context.BoardResponse?.BoardId == ConnectWiseConstants.NOCBoardId)
        {
            ticketUpdate.Status = new ActivityReference { Id = ConnectWiseConstants.NocInQueueStatusId };
        }

        return ticketUpdate;
    }
}
