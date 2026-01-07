using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ai_Dispatch.Services;

public class ConnectWiseHttpClient : IConnectWiseHttpClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<ConnectWiseHttpClient> _logger;
    private bool _disposed = false;

    public ConnectWiseHttpClient(string baseUrl, string subscriptionKey, string dispatchUser, ILogger<ConnectWiseHttpClient> logger)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentNullException(nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(subscriptionKey)) throw new ArgumentNullException(nameof(subscriptionKey));
        if (string.IsNullOrWhiteSpace(dispatchUser)) throw new ArgumentNullException(nameof(dispatchUser));

        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {dispatchUser}");
    }

    public async Task<T> QueryEndpointAsync<T>(string endpoint, object? requestBody = null, HttpMethod? method = null, Dictionary<string, string>? queryParams = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

        method ??= HttpMethod.Get;
        var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

        if (queryParams != null && queryParams.Count > 0)
        {
            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            url += $"?{queryString}";
        }

        string? json = null;
        if (requestBody != null && (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
        {
            json = JsonSerializer.Serialize(requestBody);
        }

        const int maxRetries = 3;
        const int retryDelayMs = 5000;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            if (json != null)
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request);
            stopwatch.Stop();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("ConnectWise API call succeeded - {Method}, StatusCode: {StatusCode}, Duration: {Duration}ms, Attempt: {Attempt}", 
                    method.Method, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, attempt + 1);
            }
            else
            {
                _logger.LogWarning("ConnectWise API call failed - {Method}, StatusCode: {StatusCode}, Duration: {Duration}ms, Attempt: {Attempt}", 
                    method.Method, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, attempt + 1);
            }
            
            if (response.StatusCode == HttpStatusCode.InternalServerError && attempt < maxRetries)
            {
                _logger.LogWarning("ConnectWise API 500 Error - Method: {Method}, Attempt: {Attempt}/{MaxRetries}, Waiting {DelayMs}ms before retry", 
                    method.Method, attempt + 1, maxRetries + 1, retryDelayMs);
                await Task.Delay(retryDelayMs);
                continue;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ConnectWise API Error - Method: {Method}, StatusCode: {StatusCode}, ErrorResponse: {ErrorResponse}, Attempt: {Attempt}", 
                    method.Method, (int)response.StatusCode, responseContent, attempt + 1);
                throw new HttpRequestException($"API Error {response.StatusCode}: {responseContent}");
            }

            if (attempt > 0)
            {
                _logger.LogInformation("ConnectWise API retry successful - {Method}, Attempt: {Attempt}", 
                    method.Method, attempt + 1);
            }

            return JsonSerializer.Deserialize<T>(responseContent) ?? throw new InvalidOperationException("Deserialization returned null");
        }
        
        throw new HttpRequestException($"API Error: Max retries exceeded for endpoint {endpoint}");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
