using Microsoft.Azure.Functions.Worker.Http;
using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services.Classification;

/// <summary>
/// Represents a single classification step in the ticket classification pipeline.
/// Returns Task&lt;HttpResponseData?&gt; instead of Task to allow steps that detect early-exit conditions
/// (e.g., spam detection, Info-Alert classification) to return an HTTP response immediately.
/// Steps that should continue processing return null.
/// </summary>
internal interface IClassificationStep
{
    Task<HttpResponseData?> ExecuteAsync(TicketClassificationContext context);
}
