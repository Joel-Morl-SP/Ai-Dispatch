using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Services;

public interface IContactService
{
    Task<List<ContactResponse>> GetContactsAsync(string firstName, string lastName, int companyId);
    Task<ContactResponse> GetContactByIdAsync(int contactId);
    Task<ContactResponse?> FindContactBySubmittedForAsync(string? submittedFor, int companyId);
}
