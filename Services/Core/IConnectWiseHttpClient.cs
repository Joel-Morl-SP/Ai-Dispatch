namespace Ai_Dispatch.Services;

public interface IConnectWiseHttpClient
{
    Task<T> QueryEndpointAsync<T>(string endpoint, object? requestBody = null, HttpMethod? method = null, Dictionary<string, string>? queryParams = null);
}
