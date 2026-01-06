using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ai_Dispatch.Models;
using OpenAI.Chat;

namespace Ai_Dispatch.Services;

public class AzureOpenAIService
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly Uri _endpoint;
    private readonly AzureKeyCredential _credential;
    private readonly IHttpClientFactory _httpClientFactory;

    public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        
        var endpoint = configuration["AZURE_OPENAI_ENDPOINT"];
        var apiKey = configuration["AZURE_OPENAI_API_KEY"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Azure OpenAI configuration is missing. Please set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_API_KEY environment variables.");
        }

        _endpoint = new Uri(endpoint);
        _credential = new AzureKeyCredential(apiKey);
        _openAIClient = new AzureOpenAIClient(_endpoint, _credential);
    }

    public async Task<(T Result, TokenUsage TokenUsage, string Model)> GetCompletionAsync<T>(string systemPrompt, string userInput, string modelName, float? temperature = null, int? maxTokens = null) where T : class
    {
        try
        {
            (string responseText, TokenUsage tokenUsage) result;
            bool isReasoningModel = modelName.Contains("o3", StringComparison.OrdinalIgnoreCase) || 
                                    modelName.Contains("reasoning", StringComparison.OrdinalIgnoreCase);

            if (isReasoningModel)
            {
                result = await GetReasoningModelCompletionAsync(systemPrompt, userInput, modelName, maxTokens);
            }
            else
            {
                result = await GetStandardCompletionAsync(systemPrompt, userInput, modelName, temperature, maxTokens);
            }

            var rawResponse = result.responseText;
            _logger.LogInformation("AI response received - Model: {Model}, ResponseLength: {ResponseLength}", 
                modelName, rawResponse.Length);

            var responseText = CleanMarkdownCodeBlocks(result.responseText);
            responseText = CleanJsonString(responseText);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var deserializedResult = JsonSerializer.Deserialize<T>(responseText, jsonOptions);
            
            if (deserializedResult == null)
            {
                throw new InvalidOperationException($"Failed to deserialize response to {typeof(T).Name}. Response: {responseText}");
            }

            return (deserializedResult, result.tokenUsage, modelName);
        }
        catch (Azure.RequestFailedException azureEx)
        {
            _logger.LogError(azureEx, "Azure OpenAI error - Model: {Model}, Status: {StatusCode}, ErrorCode: {ErrorCode}, Message: {Message}", 
                modelName, azureEx.Status, azureEx.ErrorCode, azureEx.Message);
            throw;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "JSON parsing error. Response text may be invalid JSON. Message: {Message}", jsonEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion. Exception: {ExceptionType} - {ExceptionMessage}", ex.GetType().Name, ex.Message);
            throw;
        }
    }

    private async Task<(string responseText, TokenUsage tokenUsage)> GetReasoningModelCompletionAsync(string systemPrompt, string userInput, string modelName, int? maxCompletionTokens)
    {
        var endpointStr = _endpoint.ToString().TrimEnd('/');
        if (!endpointStr.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
        {
            endpointStr = $"{endpointStr}/openai";
        }
        var apiUrl = $"{endpointStr}/deployments/{Uri.EscapeDataString(modelName)}/chat/completions?api-version=2025-01-01-preview";
        
        _logger.LogInformation("Calling Azure OpenAI - Model: {Model}", modelName);
        
        var requestBody = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userInput }
            },
            max_completion_tokens = maxCompletionTokens
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("api-key", _credential.Key);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        var response = await httpClient.SendAsync(request, cts.Token);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("REST API call failed. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
            throw new HttpRequestException($"Azure OpenAI REST API call failed with status {response.StatusCode}: {responseContent}");
        }
        
        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var responseText = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "{}";
        
        var tokenUsage = new TokenUsage();
        if (jsonResponse.TryGetProperty("usage", out var usageElement))
        {
            tokenUsage.PromptTokens = usageElement.TryGetProperty("prompt_tokens", out var promptTokens) ? promptTokens.GetInt32() : 0;
            tokenUsage.CompletionTokens = usageElement.TryGetProperty("completion_tokens", out var completionTokens) ? completionTokens.GetInt32() : 0;
            tokenUsage.TotalTokens = usageElement.TryGetProperty("total_tokens", out var totalTokens) ? totalTokens.GetInt32() : 0;
        }
        
        return (responseText, tokenUsage);
    }

    private async Task<(string responseText, TokenUsage tokenUsage)> GetStandardCompletionAsync(string systemPrompt, string userInput, string modelName, float? temperature, int? maxTokens)
    {
        var chatClient = _openAIClient.GetChatClient(modelName);
        
        var options = new ChatCompletionOptions
        {
            Temperature = temperature ?? 0.1f
        };

        if (maxTokens.HasValue)
        {
            options.MaxOutputTokenCount = maxTokens.Value;
        }

        ChatCompletion response = await chatClient.CompleteChatAsync(
            new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userInput)
            },
            options
        );

        var responseText = response.Content[0].Text?.Trim() ?? "{}";
        
        var tokenUsage = new TokenUsage();
        if (response.Usage != null)
        {
            tokenUsage.PromptTokens = response.Usage.InputTokenCount;
            tokenUsage.CompletionTokens = response.Usage.OutputTokenCount;
            tokenUsage.TotalTokens = response.Usage.TotalTokenCount;
        }
        
        return (responseText, tokenUsage);
    }

    private string CleanMarkdownCodeBlocks(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        if (input.StartsWith("```"))
        {
            var firstNewline = input.IndexOf('\n');
            if (firstNewline > 0)
            {
                input = input.Substring(firstNewline + 1);
            }
            
            var lastBacktick = input.LastIndexOf("```");
            if (lastBacktick > 0)
            {
                input = input.Substring(0, lastBacktick).Trim();
            }
        }

        return input;
    }

    private string CleanJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c == '\0' || 
                (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') ||
                (c >= 0x00 && c <= 0x1F && c != '\n' && c != '\r' && c != '\t'))
            {
                continue;
            }
            sb.Append(c);
        }
        
        var cleaned = sb.ToString();
        cleaned = Regex.Replace(cleaned, @"[\x00-\x08\x0B-\x0C\x0E-\x1F]", "");
        
        return cleaned;
    }
}

