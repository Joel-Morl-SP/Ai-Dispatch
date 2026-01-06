using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;

namespace Ai_Dispatch.Services;

public class ProposedNoteService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly ILogger<ProposedNoteService> _logger;

    public ProposedNoteService(IConfiguration configuration, ILogger<ProposedNoteService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _endpoint = configuration["PROPOSED_NOTE_ENDPOINT"] ?? string.Empty;
        
        if (string.IsNullOrEmpty(_endpoint))
        {
            _logger.LogWarning("ProposedNoteService endpoint not configured - PROPOSED_NOTE_ENDPOINT not set");
        }
    }

    public async Task SendProposedNoteAsync(int ticketId, string? intent, string summary, string? initialDescription, string? boardName, string? companyName, int companyId, int itGlueOrgId)
    {
        if (string.IsNullOrEmpty(_endpoint))
        {
            _logger.LogWarning("Proposed note not sent - endpoint not configured");
            return;
        }

        try
        {
            var requestBody = new
            {
                ticket_id = ticketId.ToString(),
                intent = intent ?? string.Empty,
                summary = summary,
                initial_description = initialDescription ?? string.Empty,
                board = boardName ?? string.Empty,
                company_name = companyName ?? string.Empty,
                company_id = companyId.ToString(),
                it_glue_org_id = itGlueOrgId.ToString()
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending proposed note - TicketId: {TicketId}", ticketId);
            var response = await _httpClient.PostAsync(_endpoint, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Proposed note sent successfully - TicketId: {TicketId}", ticketId);
            }
            else
            {
                _logger.LogError("Proposed note failed - TicketId: {TicketId}, StatusCode: {StatusCode}", 
                    ticketId, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending proposed note - TicketId: {TicketId}, Exception: {ExceptionType}", 
                ticketId, ex.GetType().Name);
        }
    }
}
