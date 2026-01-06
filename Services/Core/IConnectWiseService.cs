using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Services;

public interface IConnectWiseService
{
    Task<string> UpdateTicketAsync(int ticketId, TicketUpdateRequest ticketUpdate);
    Task<string> AddNoteToTicketAsync(int ticketId, string noteText);
    Task<string> CreateSalesActivityAsync(SalesActivityRequest activityRequest);
    Task<List<ContactResponse>> GetContactsAsync(string firstName, string lastName, int companyId);
    Task<ContactResponse> GetContactByIdAsync(int contactId);
    Task<ContactResponse?> FindContactBySubmittedForAsync(string? submittedFor, int companyId);
    Task<string> UpdateTicketWithRetryAsync(int ticketId, TicketUpdateRequest updateRequest);
    Task<string> CreateActivitiesForClassificationAsync(int ticketId, SpamClassificationResponse? spamResponse, BoardRoutingResponse? boardResponse, int spamConfidence, int boardConfidence);
}
