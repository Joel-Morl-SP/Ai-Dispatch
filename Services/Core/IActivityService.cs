using Ai_Dispatch.Models.Requests;
using Ai_Dispatch.Models.Responses;

namespace Ai_Dispatch.Services;

public interface IActivityService
{
    Task<string> CreateSalesActivityAsync(SalesActivityRequest activityRequest);
    Task<string> CreateActivitiesForClassificationAsync(int ticketId, SpamClassificationResponse? spamResponse, BoardRoutingResponse? boardResponse, int spamConfidence, int boardConfidence);
}
