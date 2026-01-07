using Microsoft.Extensions.Logging;
using Ai_Dispatch.Constants;
using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services;

public class ConnectWiseActivityService : IActivityService
{
    private readonly IConnectWiseHttpClient _httpClient;
    private readonly ILogger<ConnectWiseActivityService> _logger;

    public ConnectWiseActivityService(
        IConnectWiseHttpClient httpClient,
        ILogger<ConnectWiseActivityService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> CreateSalesActivityAsync(SalesActivityRequest activityRequest)
    {
        try
        {
            await _httpClient.QueryEndpointAsync<object>("sales/activities",
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
}
