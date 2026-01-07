using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Services;

public class ConnectWiseService : IConnectWiseService
{
    private readonly ITicketService _ticketService;
    private readonly IContactService _contactService;
    private readonly IActivityService _activityService;

    public ConnectWiseService(
        ITicketService ticketService,
        IContactService contactService,
        IActivityService activityService)
    {
        _ticketService = ticketService;
        _contactService = contactService;
        _activityService = activityService;
    }

    public async Task<string> UpdateTicketAsync(int ticketId, TicketUpdateRequest ticketUpdate)
    {
        return await _ticketService.UpdateTicketAsync(ticketId, ticketUpdate);
    }

    public async Task<string> AddNoteToTicketAsync(int ticketId, string noteText)
    {
        return await _ticketService.AddNoteToTicketAsync(ticketId, noteText);
    }

    public async Task<string> CreateSalesActivityAsync(SalesActivityRequest activityRequest)
    {
        return await _activityService.CreateSalesActivityAsync(activityRequest);
    }

    public async Task<List<ContactResponse>> GetContactsAsync(string firstName, string lastName, int companyId)
    {
        return await _contactService.GetContactsAsync(firstName, lastName, companyId);
    }

    public async Task<ContactResponse> GetContactByIdAsync(int contactId)
    {
        return await _contactService.GetContactByIdAsync(contactId);
    }

    public async Task<ContactResponse?> FindContactBySubmittedForAsync(string? submittedFor, int companyId)
    {
        return await _contactService.FindContactBySubmittedForAsync(submittedFor, companyId);
    }

    public static bool IsVipContact(ContactResponse? contact)
    {
        return ConnectWiseContactService.IsVipContact(contact);
    }

    public async Task<string> UpdateTicketWithRetryAsync(int ticketId, TicketUpdateRequest updateRequest)
    {
        return await _ticketService.UpdateTicketWithRetryAsync(ticketId, updateRequest);
    }

    public async Task<string> CreateActivitiesForClassificationAsync(
        int ticketId,
        SpamClassificationResponse? spamResponse,
        BoardRoutingResponse? boardResponse,
        int spamConfidence,
        int boardConfidence)
    {
        return await _activityService.CreateActivitiesForClassificationAsync(ticketId, spamResponse, boardResponse, spamConfidence, boardConfidence);
    }
}
