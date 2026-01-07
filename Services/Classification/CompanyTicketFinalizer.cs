using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ai_Dispatch.Models;
using Ai_Dispatch.Services;

namespace Ai_Dispatch.Services.Classification;

public class CompanyTicketFinalizer : IClassificationStep
{
    private readonly ILogger _logger;
    private readonly IContactLookup _contactLookup;
    private readonly ITicketUpdateBuilder _ticketUpdateBuilder;
    private readonly ITicketUpdater _ticketUpdater;
    private readonly IProposedNoteProcessor _proposedNoteProcessor;
    private readonly IResponseBuilder _responseBuilder;

    public CompanyTicketFinalizer(
        ILogger logger,
        IContactLookup contactLookup,
        ITicketUpdateBuilder ticketUpdateBuilder,
        ITicketUpdater ticketUpdater,
        IProposedNoteProcessor proposedNoteProcessor,
        IResponseBuilder responseBuilder)
    {
        _logger = logger;
        _contactLookup = contactLookup;
        _ticketUpdateBuilder = ticketUpdateBuilder;
        _ticketUpdater = ticketUpdater;
        _proposedNoteProcessor = proposedNoteProcessor;
        _responseBuilder = responseBuilder;
    }

    async Task<HttpResponseData?> IClassificationStep.ExecuteAsync(TicketClassificationContext context)
    {
        await _contactLookup.LookupAsync(context);

        context.FinalSummary = NoteBuilderService.BuildSummary(context.SummaryResponse!.SubmittedFor, context.SummaryResponse.NewSummary ?? string.Empty, context.IsVip);
        context.FinalNote = NoteBuilderService.BuildNote(context.SpamResponse, context.BoardResponse, context.TsiResponse, context.SpamConfidence, context.BoardConfidence, context.InitialIntent);

        context.TicketUpdate = _ticketUpdateBuilder.Build(context);

        await _ticketUpdater.UpdateAsync(context);

        await _proposedNoteProcessor.ProcessAsync(context);

        return await _responseBuilder.BuildSuccessResponseAsync(context);
    }
}
