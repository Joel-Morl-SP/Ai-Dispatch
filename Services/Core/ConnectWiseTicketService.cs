using System.Net;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models.Requests;

namespace Ai_Dispatch.Services;

public class ConnectWiseTicketService : ITicketService
{
    private readonly IConnectWiseHttpClient _httpClient;
    private readonly ITicketUpdateConverter _converter;
    private readonly ILogger<ConnectWiseTicketService> _logger;

    public ConnectWiseTicketService(
        IConnectWiseHttpClient httpClient,
        ITicketUpdateConverter converter,
        ILogger<ConnectWiseTicketService> logger)
    {
        _httpClient = httpClient;
        _converter = converter;
        _logger = logger;
    }

    public async Task<string> UpdateTicketAsync(int ticketId, TicketUpdateRequest ticketUpdate)
    {
        try
        {
            var updateDescription = TicketUpdateDescriptionBuilder.BuildTicketUpdateDescription(ticketId, ticketUpdate, isRealUpdate: true);
            
            var patchOperations = _converter.ConvertToJsonPatch(ticketUpdate);
            await _httpClient.QueryEndpointAsync<object>($"service/tickets/{ticketId}",
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

            await _httpClient.QueryEndpointAsync<object>($"service/tickets/{ticketId}/notes",
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
}
