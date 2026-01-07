using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class ContactLookup : IContactLookup
{
    private readonly ILogger<ContactLookup> _logger;
    private readonly IContactService _contactService;

    public ContactLookup(ILogger<ContactLookup> logger, IContactService contactService)
    {
        _logger = logger;
        _contactService = contactService;
    }

    public async Task LookupAsync(TicketClassificationContext context)
    {
        if (context.TicketRequest.ContactId.HasValue && context.TicketRequest.ContactId.Value > 0)
        {
            _logger.LogInformation("Looking up contact by ID - TicketId: {TicketId}, ContactId: {ContactId}", 
                context.TicketRequest.TicketId, context.TicketRequest.ContactId.Value);
            
            try
            {
                context.Contact = await _contactService.GetContactByIdAsync(context.TicketRequest.ContactId.Value);
                context.IsVip = ConnectWiseContactService.IsVipContact(context.Contact);
                
                _logger.LogInformation("Contact lookup completed - TicketId: {TicketId}, ContactId: {ContactId}, IsVip: {IsVip}", 
                    context.TicketRequest.TicketId, context.Contact?.Id ?? 0, context.IsVip);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get contact by ID {ContactId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                    context.TicketRequest.ContactId.Value, ex.GetType().Name, ex.Message);
            }
        }
    }
}
