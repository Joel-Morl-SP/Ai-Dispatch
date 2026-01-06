using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;

namespace Ai_Dispatch.Services;

public class TeamsAlertService
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private readonly string _teamId;
    private readonly string _channelId;
    private readonly ILogger<TeamsAlertService> _logger;

    public TeamsAlertService(IConfiguration configuration, ILogger<TeamsAlertService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        
        _webhookUrl = configuration["TEAMS_ALERT_WEBHOOK"] ?? string.Empty;
        _teamId = configuration["ALERT_TEAMS_ID"] ?? string.Empty;
        _channelId = configuration["ALERT_CHANNEL_ID"] ?? string.Empty;
        
        if (string.IsNullOrEmpty(_webhookUrl) || string.IsNullOrEmpty(_teamId) || string.IsNullOrEmpty(_channelId))
        {
            _logger.LogWarning("Teams alert configuration incomplete - TEAMS_ALERT_WEBHOOK, ALERT_TEAMS_ID, or ALERT_CHANNEL_ID not set. Teams alerts will not be sent.");
        }
    }

    public async Task SendAlertAsync(string failureMessage)
    {
        if (string.IsNullOrEmpty(_webhookUrl) || string.IsNullOrEmpty(_teamId) || string.IsNullOrEmpty(_channelId))
        {
            _logger.LogWarning("Teams alert not sent - webhook configuration incomplete");
            return;
        }

        try
        {
            var adaptiveCard = new
            {
                type = "AdaptiveCard",
                version = "1.4",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = "Azure App Failure - AI Dispatch",
                        weight = "bolder",
                        color = "attention",
                        horizontalAlignment = "center"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = failureMessage,
                        wrap = true
                    }
                }
            };

            var adaptiveCardJson = JsonSerializer.Serialize(adaptiveCard);
            var requestPayload = new
            {
                adaptive_card = adaptiveCardJson,
                recipient_email = "",
                recipient_name = "",
                team_id = _teamId,
                team_name = "ROC Team",
                channel_id = _channelId,
                channel_name = "Critical Rewst Alerts",
                to_channel = true
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Teams alert sent - Message: {Message}", failureMessage);
            var response = await _httpClient.PostAsync(_webhookUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Teams alert response - StatusCode: {StatusCode}", (int)response.StatusCode);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Teams alert failed - StatusCode: {StatusCode}, Error: {Error}", 
                    (int)response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams alert - Message: {Message}, Exception: {ExceptionType}", 
                failureMessage, ex.GetType().Name);
        }
    }
}

