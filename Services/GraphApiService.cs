// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Constants;
using CitizenAgent.Setup.Cli.Exceptions;
using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for Graph API operations
/// </summary>
public interface IGraphApiService
{
    /// <summary>
    /// Acquires an access token using client credentials (legacy - for backwards compatibility)
    /// </summary>
    Task<string> GetAccessTokenAsync(string tenantId, string clientId, string clientSecret, CancellationToken ct = default);

    /// <summary>
    /// Acquires an access token using interactive authentication (Azure CLI or browser)
    /// </summary>
    Task<string> GetAccessTokenInteractiveAsync(string tenantId, string? clientAppId = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a GET request to Microsoft Graph API
    /// </summary>
    Task<JsonDocument?> GetAsync(string endpoint, CancellationToken ct = default);

    /// <summary>
    /// Executes a POST request to Microsoft Graph API
    /// </summary>
    Task<JsonDocument?> PostAsync(string endpoint, object payload, CancellationToken ct = default);

    /// <summary>
    /// Executes a POST request to Microsoft Graph API with custom headers
    /// </summary>
    Task<JsonDocument?> PostWithHeadersAsync(string endpoint, object payload, Dictionary<string, string> headers, CancellationToken ct = default);

    /// <summary>
    /// Executes a PATCH request to Microsoft Graph API
    /// </summary>
    Task<bool> PatchAsync(string endpoint, object payload, CancellationToken ct = default);

    /// <summary>
    /// Executes a DELETE request to Microsoft Graph API
    /// </summary>
    Task<bool> DeleteAsync(string endpoint, bool treatNotFoundAsSuccess = true, CancellationToken ct = default);

    /// <summary>
    /// Sets the access token for subsequent requests
    /// </summary>
    void SetAccessToken(string token);

    /// <summary>
    /// Gets the current API version being used
    /// </summary>
    string ApiVersion { get; set; }
}

/// <summary>
/// Service for Microsoft Graph API operations
/// </summary>
public class GraphApiService : IGraphApiService
{
    private readonly ILogger<GraphApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IInteractiveAuthService _authService;
    private string _accessToken = string.Empty;

    /// <summary>
    /// Gets or sets the API version (default: beta)
    /// </summary>
    public string ApiVersion { get; set; } = "beta";

    public GraphApiService(ILogger<GraphApiService> logger, HttpClient httpClient, IInteractiveAuthService authService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _authService = authService;
        _httpClient.BaseAddress = new Uri(GraphConstants.GraphBaseUrl);
    }

    /// <inheritdoc />
    public void SetAccessToken(string token)
    {
        _accessToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenInteractiveAsync(string tenantId, string? clientAppId = null, CancellationToken ct = default)
    {
        _logger.LogDebug("Acquiring access token interactively for tenant {TenantId}", tenantId);

        var token = await _authService.GetTokenAsync(tenantId, null, clientAppId, ct);
        SetAccessToken(token);

        return token;
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(string tenantId, string clientId, string clientSecret, CancellationToken ct = default)
    {
        _logger.LogDebug("Acquiring access token for tenant {TenantId}", tenantId);

        var tokenEndpoint = string.Format(GraphConstants.TokenEndpointTemplate, tenantId);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = GraphConstants.GraphDefaultScope
        });

        try
        {
            var response = await _httpClient.PostAsync(tokenEndpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to acquire token: {StatusCode} - {Body}", response.StatusCode, responseBody);
                
                var errorDoc = JsonDocument.Parse(responseBody);
                var errorDescription = errorDoc.RootElement.TryGetProperty("error_description", out var desc)
                    ? desc.GetString()
                    : "Unknown error";

                throw new AuthenticationException($"Failed to acquire access token: {errorDescription}");
            }

            var tokenDoc = JsonDocument.Parse(responseBody);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;

            SetAccessToken(accessToken);
            _logger.LogDebug("Access token acquired successfully");

            return accessToken;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while acquiring token");
            throw new AuthenticationException("Network error while acquiring access token", ex);
        }
    }

    /// <inheritdoc />
    public async Task<JsonDocument?> GetAsync(string endpoint, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        var url = BuildUrl(endpoint);
        _logger.LogDebug("GET {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} failed: {StatusCode} - {Body}", url, response.StatusCode, body);
                return null;
            }

            return JsonDocument.Parse(body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during GET {Url}", url);
            throw new GraphApiException($"Network error during GET {endpoint}", 0, ex);
        }
    }

    /// <inheritdoc />
    public async Task<JsonDocument?> PostAsync(string endpoint, object payload, CancellationToken ct = default)
    {
        return await PostWithHeadersAsync(endpoint, payload, null, ct);
    }

    /// <inheritdoc />
    public async Task<JsonDocument?> PostWithHeadersAsync(string endpoint, object payload, Dictionary<string, string>? headers, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        var url = BuildUrl(endpoint);
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        _logger.LogDebug("POST {Url}", url);
        _logger.LogDebug("Payload: {Payload}", jsonPayload);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;

            // Add custom headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("POST {Url} failed: {StatusCode} - {Body}", url, response.StatusCode, body);
                
                var errorMessage = ParseGraphError(body);
                throw new GraphApiException(
                    $"Graph API POST failed: {errorMessage}",
                    (int)response.StatusCode,
                    ParseErrorCode(body));
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return JsonDocument.Parse(body);
        }
        catch (GraphApiException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during POST {Url}", url);
            throw new GraphApiException($"Network error during POST {endpoint}", 0, ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> PatchAsync(string endpoint, object payload, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        var url = BuildUrl(endpoint);
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        _logger.LogDebug("PATCH {Url}", url);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("PATCH {Url} failed: {StatusCode} - {Body}", url, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during PATCH {Url}", url);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string endpoint, bool treatNotFoundAsSuccess = true, CancellationToken ct = default)
    {
        EnsureAuthenticated();

        var url = BuildUrl(endpoint);
        _logger.LogDebug("DELETE {Url}", url);

        try
        {
            var response = await _httpClient.DeleteAsync(url, ct);

            if (treatNotFoundAsSuccess && response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return true;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("DELETE {Url} failed: {StatusCode} - {Body}", url, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during DELETE {Url}", url);
            return false;
        }
    }

    private void EnsureAuthenticated()
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            throw new AuthenticationException("Access token not set. Call GetAccessTokenAsync first.");
        }
    }

    private string BuildUrl(string endpoint)
    {
        if (endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return $"/{ApiVersion}{endpoint}";
    }

    private static string ParseGraphError(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? "Unknown error";
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return body;
    }

    private static string? ParseErrorCode(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("code", out var code))
                {
                    return code.GetString();
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return null;
    }
}
