using Microsoft.Extensions.Logging;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class ClassificationActivityCreator : IClassificationActivityCreator
{
    private readonly ILogger<ClassificationActivityCreator> _logger;
    private readonly IConnectWiseService _connectWiseService;

    public ClassificationActivityCreator(ILogger<ClassificationActivityCreator> logger, IConnectWiseService connectWiseService)
    {
        _logger = logger;
        _connectWiseService = connectWiseService;
    }

    public async Task CreateAsync(DispatchClassificationFunction.TicketClassificationContext context)
    {
        var activitiesDescription = await _connectWiseService.CreateActivitiesForClassificationAsync(context.TicketRequest.TicketId, context.SpamResponse, context.BoardResponse, 
            context.SpamConfidence, context.BoardConfidence);
        
        _logger.LogInformation("Activities created for classification - TicketId: {TicketId}, Activities: {Activities}", 
            context.TicketRequest.TicketId, activitiesDescription);
    }
}
