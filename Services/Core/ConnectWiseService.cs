using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Services;

public class ConnectWiseService : IConnectWiseService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _subscriptionKey;
    private readonly string _dispatchUser;
    private readonly ILogger<ConnectWiseService> _logger;
    private bool _disposed = false;

    public ConnectWiseService(string baseUrl, string subscriptionKey, string dispatchUser, ILogger<ConnectWiseService> logger)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentNullException(nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(subscriptionKey)) throw new ArgumentNullException(nameof(subscriptionKey));
        if (string.IsNullOrWhiteSpace(dispatchUser)) throw new ArgumentNullException(nameof(dispatchUser));

        _baseUrl = baseUrl.TrimEnd('/');
        _subscriptionKey = subscriptionKey;
        _dispatchUser = dispatchUser;
        _logger = logger;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {_dispatchUser}");
    }

    public async Task<string> UpdateTicketAsync(int ticketId, TicketUpdateRequest ticketUpdate)
    {
        try
        {
            var updateDescription = BuildTicketUpdateDescription(ticketId, ticketUpdate, isRealUpdate: true);
            
            var patchOperations = ConvertToJsonPatch(ticketUpdate);
            await QueryEndpointAsync<object>($"service/tickets/{ticketId}",
                requestBody: patchOperations,
                method: HttpMethod.Patch);
            
            _logger.LogInformation("Ticket updated - TicketId: {TicketId}, UpdateDetails: {UpdateDetails}", 
                ticketId, updateDescription);
            
            return updateDescription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ticket {TicketId}: {Message}", ticketId, ex.Message);
            return $"ERROR: Failed to update ticket {ticketId}: {ex.Message}";
        }
    }

    public async Task<string> AddNoteToTicketAsync(int ticketId, string noteText)
    {
        try
        {
            var noteRequest = new ProjectTicketNoteRequest
            {
                Text = noteText,
                InternalAnalysisFlag = true
            };

            await QueryEndpointAsync<object>($"service/tickets/{ticketId}/notes",
                requestBody: noteRequest,
                method: HttpMethod.Post);
            
            _logger.LogInformation("Note added to ticket - TicketId: {TicketId}, NoteLength: {NoteLength}", 
                ticketId, noteText.Length);
            
            return $"Note added to ticket {ticketId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add note to ticket {TicketId}: {Message}", ticketId, ex.Message);
            return $"ERROR: Failed to add note to ticket {ticketId}: {ex.Message}";
        }
    }

    public async Task<string> CreateSalesActivityAsync(SalesActivityRequest activityRequest)
    {
        try
        {
            await QueryEndpointAsync<object>("sales/activities",
                requestBody: activityRequest,
                method: HttpMethod.Post);
            
            _logger.LogInformation("Sales activity created - ActivityName: {ActivityName}, TypeId: {TypeId}, TicketId: {TicketId}", 
                activityRequest.Name, activityRequest.Type?.Id, activityRequest.Ticket?.Id);
            
            return $"Sales activity '{activityRequest.Name}' created";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create sales activity '{ActivityName}': {Message}", activityRequest.Name, ex.Message);
            return $"ERROR: Failed to create sales activity '{activityRequest.Name}': {ex.Message}";
        }
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

        var result = await QueryEndpointAsync<List<ContactResponse>>("company/contacts",
            queryParams: queryParams,
            method: HttpMethod.Get);
        
        return result ?? new List<ContactResponse>();
    }

    public async Task<ContactResponse> GetContactByIdAsync(int contactId)
    {
        var result = await QueryEndpointAsync<ContactResponse>($"company/contacts/{contactId}",
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

    public async Task<string> UpdateTicketWithRetryAsync(int ticketId, TicketUpdateRequest updateRequest)
    {
        try
        {
            var updateDescription = await UpdateTicketAsync(ticketId, updateRequest);
            return updateDescription;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("500") || ex.Message.Contains("Internal Server Error"))
        {
            _logger.LogWarning("Received 500 error updating ticket {TicketId}, waiting 15 seconds before retry - TicketId: {TicketId}, Error: {Error}", 
                ticketId, ticketId, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(15));
            
            try
            {
                var retryDescription = await UpdateTicketAsync(ticketId, updateRequest);
                _logger.LogInformation("Retry successful - TicketId: {TicketId}, UpdateDetails: {UpdateDetails}", 
                    ticketId, retryDescription);
                return $"RETRY: {retryDescription}";
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Retry failed for ticket {TicketId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                    ticketId, retryEx.GetType().Name, retryEx.Message);
                return $"ERROR: Retry failed for ticket {ticketId}: {retryEx.Message}";
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("400") && ex.Message.Contains("item"))
        {
            _logger.LogWarning("Received 400 error with item mismatch for ticket {TicketId}, trying without item - TicketId: {TicketId}, Error: {Error}", 
                ticketId, ticketId, ex.Message);
            
            var fallbackRequest = new TicketUpdateRequest
            {
                Summary = updateRequest.Summary,
                Board = updateRequest.Board,
                Type = updateRequest.Type,
                SubType = updateRequest.SubType,
                Priority = updateRequest.Priority,
                Contact = updateRequest.Contact,
                Status = updateRequest.Status,
                SkipCallback = updateRequest.SkipCallback
            };

            try
            {
                var fallbackDescription = await UpdateTicketAsync(ticketId, fallbackRequest);
                _logger.LogInformation("Fallback (no item) successful - TicketId: {TicketId}, UpdateDetails: {UpdateDetails}", 
                    ticketId, fallbackDescription);
                return $"FALLBACK (no item): {fallbackDescription}";
            }
            catch (HttpRequestException fallbackEx) when (fallbackEx.Message.Contains("400") && fallbackEx.Message.Contains("subtype"))
            {
                _logger.LogWarning("Received 400 error with subtype mismatch for ticket {TicketId}, trying with type only - TicketId: {TicketId}, Error: {Error}", 
                    ticketId, ticketId, fallbackEx.Message);
                
                var typeOnlyRequest = new TicketUpdateRequest
                {
                    Summary = updateRequest.Summary,
                    Board = updateRequest.Board,
                    Type = updateRequest.Type,
                    Priority = updateRequest.Priority,
                    Contact = updateRequest.Contact,
                    Status = updateRequest.Status,
                    SkipCallback = updateRequest.SkipCallback
                };

                try
                {
                    var typeOnlyDescription = await UpdateTicketAsync(ticketId, typeOnlyRequest);
                    _logger.LogInformation("Fallback (type only) successful - TicketId: {TicketId}, UpdateDetails: {UpdateDetails}", 
                        ticketId, typeOnlyDescription);
                    return $"FALLBACK (type only): {typeOnlyDescription}";
                }
                catch (Exception finalEx)
                {
                    _logger.LogError(finalEx, "All fallback attempts failed for ticket {TicketId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                        ticketId, finalEx.GetType().Name, finalEx.Message);
                    return $"ERROR: All fallback attempts failed for ticket {ticketId}: {finalEx.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ticket {TicketId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                ticketId, ex.GetType().Name, ex.Message);
            return $"ERROR: Failed to update ticket {ticketId}: {ex.Message}";
        }
    }

    public async Task<string> CreateActivitiesForClassificationAsync(
        int ticketId,
        SpamClassificationResponse? spamResponse,
        BoardRoutingResponse? boardResponse,
        int spamConfidence,
        int boardConfidence)
    {
        var activities = new List<SalesActivityRequest>();
        var activityDescriptions = new List<string>();

        if (spamResponse != null && spamResponse.Decision == "Spam" && spamConfidence == 100)
        {
            activities.Add(new SalesActivityRequest
            {
                Name = "SPAM Classification 100%",
                Type = new ActivityTypeReference { Id = ConnectWiseActivityConstants.SpamClassificationType },
                Ticket = new ActivityReference { Id = ticketId },
                DateStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Notes = ""
            });
        }

        if (spamResponse != null && spamResponse.Decision == "Spam" && spamConfidence == 95)
        {
            activities.Add(new SalesActivityRequest
            {
                Name = "Possible SPAM Classification",
                Type = new ActivityTypeReference { Id = ConnectWiseActivityConstants.PossibleSpamClassificationType },
                Ticket = new ActivityReference { Id = ticketId },
                DateStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Notes = ""
            });
        }

        if (boardResponse != null && boardConfidence < 90)
        {
            activities.Add(new SalesActivityRequest
            {
                Name = "Unsuccessful Board Classification",
                Type = new ActivityTypeReference { Id = ConnectWiseActivityConstants.UnsuccessfulBoardClassificationType },
                Ticket = new ActivityReference { Id = ticketId },
                DateStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Notes = ""
            });
        }

        if (boardResponse != null && boardConfidence >= 90)
        {
            var activity = new SalesActivityRequest
            {
                Name = "Successful Board Classification",
                Type = new ActivityTypeReference { Id = ConnectWiseActivityConstants.SuccessfulBoardClassificationType },
                Ticket = new ActivityReference { Id = ticketId },
                DateStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Notes = boardResponse.BoardName != null ? $"Chosen Board: {boardResponse.BoardName}" : ""
            };

            activities.Add(activity);
        }

        _logger.LogInformation("Creating activities for classification - TicketId: {TicketId}, ActivityCount: {ActivityCount}, SpamConfidence: {SpamConfidence}, BoardConfidence: {BoardConfidence}", 
            ticketId, activities.Count, spamConfidence, boardConfidence);

        foreach (var activity in activities)
        {
            try
            {
                var activityDescription = await CreateSalesActivityAsync(activity);
                activityDescriptions.Add(activityDescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create activity {ActivityName} for ticket {TicketId} - ExceptionType: {ExceptionType}, Message: {Message}", 
                    activity.Name, ticketId, ex.GetType().Name, ex.Message);
                activityDescriptions.Add($"ERROR: Failed to create activity '{activity.Name}': {ex.Message}");
            }
        }

        var result = activityDescriptions.Count > 0 
            ? string.Join("; ", activityDescriptions) 
            : "No activities created";
        
        _logger.LogInformation("Activities creation completed - TicketId: {TicketId}, CreatedCount: {CreatedCount}, Result: {Result}", 
            ticketId, activityDescriptions.Count, result);
        
        return result;
    }

    private async Task<T> QueryEndpointAsync<T>(string endpoint, object requestBody = null, HttpMethod method = null, Dictionary<string, string> queryParams = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

        method ??= HttpMethod.Get;
        var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

        if (queryParams != null && queryParams.Count > 0)
        {
            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            url += $"?{queryString}";
        }

        // Serialize request body once if needed
        string json = null;
        if (requestBody != null && (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
        {
            json = JsonSerializer.Serialize(requestBody);
        }

        const int maxRetries = 3;
        const int retryDelayMs = 5000;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            // Create a new request for each attempt (HttpRequestMessage can only be sent once)
            using var request = new HttpRequestMessage(method, url);
            if (json != null)
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request);
            stopwatch.Stop();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("ConnectWise API call succeeded - {Method}, StatusCode: {StatusCode}, Duration: {Duration}ms, Attempt: {Attempt}", 
                    method.Method, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, attempt + 1);
            }
            else
            {
                _logger.LogWarning("ConnectWise API call failed - {Method}, StatusCode: {StatusCode}, Duration: {Duration}ms, Attempt: {Attempt}", 
                    method.Method, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, attempt + 1);
            }
            
            if (response.StatusCode == HttpStatusCode.InternalServerError && attempt < maxRetries)
            {
                _logger.LogWarning("ConnectWise API 500 Error - Method: {Method}, Attempt: {Attempt}/{MaxRetries}, Waiting {DelayMs}ms before retry", 
                    method.Method, attempt + 1, maxRetries + 1, retryDelayMs);
                await Task.Delay(retryDelayMs);
                continue;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ConnectWise API Error - Method: {Method}, StatusCode: {StatusCode}, ErrorResponse: {ErrorResponse}, Attempt: {Attempt}", 
                    method.Method, (int)response.StatusCode, responseContent, attempt + 1);
                throw new HttpRequestException($"API Error {response.StatusCode}: {responseContent}");
            }

            if (attempt > 0)
            {
                _logger.LogInformation("ConnectWise API retry successful - {Method}, Attempt: {Attempt}", 
                    method.Method, attempt + 1);
            }

            return JsonSerializer.Deserialize<T>(responseContent);
        }
        
        // This should never be reached, but compiler needs it
        throw new HttpRequestException($"API Error: Max retries exceeded for endpoint {endpoint}");
    }

    private List<object> ConvertToJsonPatch(TicketUpdateRequest ticketUpdate)
    {
        var patchOperations = new List<object>();

        if (ticketUpdate.Summary != null)
        {
            patchOperations.Add(new { op = "replace", path = "/summary", value = ticketUpdate.Summary });
        }

        if (ticketUpdate.Board != null)
        {
            patchOperations.Add(new { op = "replace", path = "/board", value = ticketUpdate.Board });
        }

        if (ticketUpdate.Type != null)
        {
            patchOperations.Add(new { op = "replace", path = "/type", value = ticketUpdate.Type });
        }

        if (ticketUpdate.SubType != null)
        {
            patchOperations.Add(new { op = "replace", path = "/subType", value = ticketUpdate.SubType });
        }

        if (ticketUpdate.Item != null)
        {
            patchOperations.Add(new { op = "replace", path = "/item", value = ticketUpdate.Item });
        }

        if (ticketUpdate.Priority != null)
        {
            patchOperations.Add(new { op = "replace", path = "/priority", value = ticketUpdate.Priority });
        }

        if (ticketUpdate.Contact != null)
        {
            patchOperations.Add(new { op = "replace", path = "/contact", value = ticketUpdate.Contact });
        }

        if (ticketUpdate.Status != null)
        {
            patchOperations.Add(new { op = "replace", path = "/status", value = ticketUpdate.Status });
        }

        if (ticketUpdate.SkipCallback.HasValue)
        {
            patchOperations.Add(new { op = "replace", path = "/skipCallback", value = ticketUpdate.SkipCallback.Value });
        }

        return patchOperations;
    }

    private string BuildTicketUpdateDescription(int ticketId, TicketUpdateRequest updateRequest, bool isRealUpdate = false)
    {
        var prefix = isRealUpdate ? "Updated ticket" : "Would update ticket";
        var parts = new List<string> { $"{prefix} {ticketId}" };
        
        if (updateRequest.Summary != null)
            parts.Add($"Summary: {updateRequest.Summary}");
        if (updateRequest.Board != null)
            parts.Add($"BoardId: {updateRequest.Board.Id}");
        if (updateRequest.Type != null)
        {
            var typeName = GetTypeName(updateRequest.Type.Id);
            parts.Add($"Type: {typeName} (Id: {updateRequest.Type.Id})");
        }
        if (updateRequest.SubType != null)
            parts.Add($"SubtypeId: {updateRequest.SubType.Id}");
        if (updateRequest.Item != null)
            parts.Add($"ItemId: {updateRequest.Item.Id}");
        if (updateRequest.Priority != null)
            parts.Add($"PriorityId: {updateRequest.Priority.Id}");
        if (updateRequest.Contact != null)
            parts.Add($"ContactId: {updateRequest.Contact.Id}");
        if (updateRequest.Status != null)
        {
            var statusName = GetStatusName(updateRequest.Status.Id);
            parts.Add($"Status: {statusName} (Id: {updateRequest.Status.Id})");
        }
        if (updateRequest.SkipCallback.HasValue)
            parts.Add($"SkipCallback: {updateRequest.SkipCallback.Value}");
        
        return string.Join(", ", parts);
    }

    private string GetTypeName(int typeId)
    {
        return typeId switch
        {
            7864 => "Continual Service Improvement",
            _ => $"TypeId-{typeId}"
        };
    }

    private string GetStatusName(int? statusId)
    {
        return statusId switch
        {
            4084 => "Triage Review",
            163 => "Closing",
            _ => $"StatusId-{statusId}"
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

