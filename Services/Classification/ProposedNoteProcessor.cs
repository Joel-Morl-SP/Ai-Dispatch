using Microsoft.Extensions.Logging;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class ProposedNoteProcessor : IProposedNoteProcessor
{
    private readonly ILogger<ProposedNoteProcessor> _logger;
    private readonly ProposedNoteService _proposedNoteService;

    public ProposedNoteProcessor(ILogger<ProposedNoteProcessor> logger, ProposedNoteService proposedNoteService)
    {
        _logger = logger;
        _proposedNoteService = proposedNoteService;
    }

    public async Task ProcessAsync(DispatchClassificationFunction.TicketClassificationContext context)
    {
        _logger.LogInformation("Checking proposed note conditions - TicketId: {TicketId}, NotStreamlineClient: {NotStreamlineClient}, ServiceTeamNull: {ServiceTeamNull}, ServiceTeamName: {ServiceTeamName}", 
            context.TicketRequest.TicketId, context.TicketRequest.NotStreamlineClient, context.TicketRequest.ServiceTeam == null, context.TicketRequest.ServiceTeam?.Name ?? "None");
        
        var notStreamlineClientMet = context.TicketRequest.NotStreamlineClient;
        var serviceTeamNotNull = context.TicketRequest.ServiceTeam != null;
        var serviceTeamNameMatch = serviceTeamNotNull && (context.TicketRequest.ServiceTeam!.Name == "Hydra" || context.TicketRequest.ServiceTeam.Name == "Bootes");
        
        if (notStreamlineClientMet && serviceTeamNotNull && serviceTeamNameMatch)
        {
            _logger.LogInformation("Proposed note conditions met - Sending proposed note - TicketId: {TicketId}, ServiceTeam: {ServiceTeamName}", 
                context.TicketRequest.TicketId, context.TicketRequest.ServiceTeam!.Name);
            
            await _proposedNoteService.SendProposedNoteAsync(
                context.TicketRequest.TicketId,
                context.InitialIntent,
                context.FinalSummary!,
                context.TicketRequest.InitialDescription,
                context.BoardResponse?.BoardName,
                context.TicketRequest.CompanyName,
                context.TicketRequest.CompanyId,
                context.TicketRequest.ItGlueOrgId);
            
            context.ProposedNoteSent = true;
        }
        else
        {
            var reasons = new List<string>();
            if (!notStreamlineClientMet) reasons.Add("NotStreamlineClient is false");
            if (!serviceTeamNotNull) reasons.Add("ServiceTeam is null");
            if (serviceTeamNotNull && !serviceTeamNameMatch) reasons.Add($"ServiceTeam name '{context.TicketRequest.ServiceTeam!.Name}' is not 'Hydra' or 'Bootes'");
            
            _logger.LogInformation("Proposed note not sent - Conditions not met - TicketId: {TicketId}, Reasons: {Reasons}", 
                context.TicketRequest.TicketId, string.Join(", ", reasons));
        }
    }
}
