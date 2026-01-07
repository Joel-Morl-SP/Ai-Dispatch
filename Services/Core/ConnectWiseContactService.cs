using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Services;

public class ConnectWiseContactService : IContactService
{
    private readonly IConnectWiseHttpClient _httpClient;
    private readonly ILogger<ConnectWiseContactService> _logger;

    public ConnectWiseContactService(
        IConnectWiseHttpClient httpClient,
        ILogger<ConnectWiseContactService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ContactResponse>> GetContactsAsync(string firstName, string lastName, int companyId)
    {
        var queryParams = new Dictionary<string, string>
        {
            { "firstName", firstName ?? string.Empty },
            { "lastName", lastName ?? string.Empty },
            { "company/id", companyId.ToString() },
            { "inactiveFlag", "false" }
        };

        _logger.LogInformation("Searching for contacts - FirstName: {FirstName}, LastName: {LastName}, CompanyId: {CompanyId}", 
            firstName, lastName, companyId);

        var result = await _httpClient.QueryEndpointAsync<List<ContactResponse>>("company/contacts",
            queryParams: queryParams,
            method: HttpMethod.Get);
        
        return result ?? new List<ContactResponse>();
    }

    public async Task<ContactResponse> GetContactByIdAsync(int contactId)
    {
        var result = await _httpClient.QueryEndpointAsync<ContactResponse>($"company/contacts/{contactId}",
            method: HttpMethod.Get);
        
        return result;
    }

    public async Task<ContactResponse?> FindContactBySubmittedForAsync(string? submittedFor, int companyId)
    {
        if (string.IsNullOrWhiteSpace(submittedFor) || companyId == ConnectWiseConstants.NoCompanyId)
        {
            _logger.LogInformation("Skipping contact lookup - SubmittedFor: {SubmittedFor}, CompanyId: {CompanyId}", 
                submittedFor ?? "null", companyId);
            return null;
        }

        var nameParts = submittedFor.Trim().Split(' ', 2);
        if (nameParts.Length < 2)
        {
            _logger.LogInformation("Invalid name format for contact lookup - SubmittedFor: {SubmittedFor}, CompanyId: {CompanyId}", 
                submittedFor, companyId);
            return null;
        }

        var firstName = nameParts[0];
        var lastName = nameParts[1];

        try
        {
            _logger.LogInformation("Searching for contact - FirstName: {FirstName}, LastName: {LastName}, CompanyId: {CompanyId}", 
                firstName, lastName, companyId);
            
            var contacts = await GetContactsAsync(firstName, lastName, companyId);
            var contact = contacts.FirstOrDefault();
            
            if (contact != null)
            {
                _logger.LogInformation("Contact found - ContactId: {ContactId}, FirstName: {FirstName}, LastName: {LastName}, CompanyId: {CompanyId}", 
                    contact.Id, contact.FirstName, contact.LastName, companyId);
            }
            else
            {
                _logger.LogInformation("Contact not found - FirstName: {FirstName}, LastName: {LastName}, CompanyId: {CompanyId}", 
                    firstName, lastName, companyId);
            }
            
            return contact;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find contact for {SubmittedFor} in company {CompanyId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                submittedFor, companyId, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    public static bool IsVipContact(ContactResponse? contact)
    {
        if (contact == null) return false;
        return contact.Types?.Any(t => t.Id == ConnectWiseConstants.VipContactTypeId) ?? false;
    }
}
