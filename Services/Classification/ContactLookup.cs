using Microsoft.Extensions.Logging;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class ContactLookup : IContactLookup
{
    private readonly ILogger<ContactLookup> _logger;
    private readonly IConnectWiseService _connectWiseService;

    public ContactLookup(ILogger<ContactLookup> logger, IConnectWiseService connectWiseService)
    {
        _logger = logger;
        _connectWiseService = connectWiseService;
    }

    public async Task LookupAsync(DispatchClassificationFunction.TicketClassificationContext context)
    {
        if (context.TicketRequest.ContactId.HasValue && context.TicketRequest.ContactId.Value > 0)
        {
            _logger.LogInformation("Looking up contact by ID - TicketId: {TicketId}, ContactId: {ContactId}", 
                context.TicketRequest.TicketId, context.TicketRequest.ContactId.Value);
            
            try
            {
                context.Contact = await _connectWiseService.GetContactByIdAsync(context.TicketRequest.ContactId.Value);
                context.IsVip = ConnectWiseService.IsVipContact(context.Contact);
                
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
