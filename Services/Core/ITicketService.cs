using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services;

public interface ITicketService
{
    Task<string> UpdateTicketAsync(int ticketId, TicketUpdateRequest ticketUpdate);
    Task<string> UpdateTicketWithRetryAsync(int ticketId, TicketUpdateRequest updateRequest);
    Task<string> AddNoteToTicketAsync(int ticketId, string noteText);
}
