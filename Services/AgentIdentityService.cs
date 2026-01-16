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
/// Interface for Agent Identity operations
/// </summary>
public interface IAgentIdentityService
{
    /// <summary>
    /// Finds an existing Agent Identity by display name
    /// </summary>
    Task<ServicePrincipal?> FindAgentIdentityAsync(string displayName, CancellationToken ct = default);

    /// <summary>
    /// Creates a new Agent Identity using the Blueprint App ID and client credentials
    /// </summary>
    /// <param name="displayName">Display name for the agent identity</param>
    /// <param name="blueprintAppId">The App ID (client_id) of the Blueprint</param>
    /// <param name="blueprintClientSecret">The client secret of the Blueprint</param>
    /// <param name="tenantId">Azure AD tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    Task<ServicePrincipal> CreateAgentIdentityAsync(
        string displayName, 
        string blueprintAppId, 
        string blueprintClientSecret,
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Service for Agent Identity operations following Microsoft Agent365 DevTools CLI approach
/// Reference: microsoft/Agent365-devTools A365CreateInstanceRunner.cs
/// </summary>
public class AgentIdentityService : IAgentIdentityService
{
    private readonly ILogger<AgentIdentityService> _logger;
    private readonly IGraphApiService _graphService;
    private readonly HttpClient _httpClient;

    public AgentIdentityService(
        ILogger<AgentIdentityService> logger, 
        IGraphApiService graphService,
        HttpClient httpClient)
    {
        _logger = logger;
        _graphService = graphService;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<ServicePrincipal?> FindAgentIdentityAsync(string displayName, CancellationToken ct = default)
    {
        _logger.LogDebug("Searching for Agent Identity: {DisplayName}", displayName);

        // Search by displayName only (servicePrincipalType filter is not supported)
        var filter = Uri.EscapeDataString($"displayName eq '{displayName}'");
        var doc = await _graphService.GetAsync($"/servicePrincipals?$filter={filter}", ct);

        if (doc == null)
        {
            return null;
        }

        if (doc.RootElement.TryGetProperty("value", out var value) && value.GetArrayLength() > 0)
        {
            // Look for a ServicePrincipal with servicePrincipalType = 'ServiceIdentity' (Agent Identity)
            foreach (var item in value.EnumerateArray())
            {
                var spType = item.TryGetProperty("servicePrincipalType", out var spTypeProp) 
                    ? spTypeProp.GetString() 
                    : null;

                // Agent Identities have servicePrincipalType = "ServiceIdentity"
                if (spType == "ServiceIdentity")
                {
                    return new ServicePrincipal
                    {
                        Id = item.GetProperty("id").GetString(),
                        DisplayName = item.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
                        ServicePrincipalType = spType
                    };
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ServicePrincipal> CreateAgentIdentityAsync(
        string displayName, 
        string blueprintAppId, 
        string blueprintClientSecret,
        string tenantId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating Agent Identity: {DisplayName}", displayName);
        _logger.LogInformation("  Using Blueprint client credentials authentication...");

        // Step 1: Get access token using Blueprint's client credentials
        // This is the key difference from our previous approach!
        // Reference: microsoft/Agent365-devTools A365CreateInstanceRunner.cs GetBlueprintAccessTokenAsync
        var accessToken = await GetBlueprintAccessTokenAsync(tenantId, blueprintAppId, blueprintClientSecret, ct);

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new AuthenticationException("Failed to acquire Blueprint access token for Agent Identity creation");
        }

        // Step 2: Try to get current user for sponsor (required for Agent Identity)
        string? currentUserId = null;
        
        // First try /me with management token (won't work with app-only, but try anyway)
        try
        {
            var meDoc = await _graphService.GetAsync("/me", ct);
            if (meDoc != null && meDoc.RootElement.TryGetProperty("id", out var idProp))
            {
                currentUserId = idProp.GetString();
                _logger.LogInformation("  Sponsor user ID (from /me): {UserId}", currentUserId);
            }
        }
        catch
        {
            _logger.LogDebug("Could not get current user via /me (app-only auth)");
        }

        // If /me failed, try to get signed-in user from Azure CLI
        if (string.IsNullOrEmpty(currentUserId))
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c az ad signed-in-user show --query id -o tsv",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    currentUserId = output.Trim();
                    _logger.LogInformation("  Sponsor user ID (from Azure CLI): {UserId}", currentUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not get user from Azure CLI: {Message}", ex.Message);
            }
        }

        if (string.IsNullOrEmpty(currentUserId))
        {
            _logger.LogWarning("No sponsor user ID found - Agent Identity creation may fail");
            _logger.LogWarning("Ensure you are signed in with Azure CLI: az login");
        }

        // Step 3: Create Agent Identity using the official Microsoft endpoint
        // Reference: microsoft/Agent365-devTools A365CreateInstanceRunner.cs CreateAgentIdentityAsync
        // Endpoint: POST /beta/serviceprincipals/Microsoft.Graph.AgentIdentity
        // Body: { displayName, agentAppId } (NOT agentIdentityBlueprintId!)
        var createIdentityUrl = $"{GraphConstants.GraphBaseUrl}/beta/serviceprincipals/Microsoft.Graph.AgentIdentity";

        var identityPayload = new Dictionary<string, object>
        {
            ["displayName"] = displayName,
            ["agentAppId"] = blueprintAppId  // CRITICAL: Use agentAppId, NOT agentIdentityBlueprintId!
        };

        // Add sponsor if available
        if (!string.IsNullOrEmpty(currentUserId))
        {
            identityPayload["sponsors@odata.bind"] = new[]
            {
                $"https://graph.microsoft.com/v1.0/users/{currentUserId}"
            };
        }

        _logger.LogInformation("  Creating Agent Identity via Microsoft.Graph.AgentIdentity endpoint...");

        using var request = new HttpRequestMessage(HttpMethod.Post, createIdentityUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(identityPayload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // Handle sponsor error - retry without sponsor if needed
        if (!response.IsSuccessStatusCode && 
            response.StatusCode == System.Net.HttpStatusCode.BadRequest &&
            !string.IsNullOrEmpty(currentUserId))
        {
            _logger.LogWarning("Agent Identity creation with sponsor failed, retrying without sponsor...");
            
            identityPayload.Remove("sponsors@odata.bind");

            using var retryRequest = new HttpRequestMessage(HttpMethod.Post, createIdentityUrl);
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            retryRequest.Content = new StringContent(
                JsonSerializer.Serialize(identityPayload),
                Encoding.UTF8,
                "application/json");

            response = await _httpClient.SendAsync(retryRequest, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create Agent Identity: {StatusCode} - {Body}", response.StatusCode, body);
            
            // Provide helpful error messages
            if (body.Contains("Authorization_RequestDenied", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("");
                _logger.LogError("AUTHORIZATION DENIED - The Blueprint may not have the required permissions.");
                _logger.LogError("Required permissions: Application.ReadWrite.All, AgentIdentity.Create.OwnedBy");
                _logger.LogError("");
            }
            else if (body.Contains("not a compatible application type", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("");
                _logger.LogError("BLUEPRINT NOT COMPATIBLE - The application was not created as an Agent Identity Blueprint.");
                _logger.LogError("The Blueprint must be created with @odata.type = 'Microsoft.Graph.AgentIdentityBlueprint'");
                _logger.LogError("");
            }

            throw new GraphApiException($"Failed to create Agent Identity: {body}", (int)response.StatusCode);
        }

        var doc = JsonDocument.Parse(body);
        var identity = new ServicePrincipal
        {
            Id = doc.RootElement.GetProperty("id").GetString(),
            DisplayName = displayName,
            ServicePrincipalType = "ServiceIdentity"
        };

        _logger.LogInformation("Agent Identity created successfully!");
        _logger.LogInformation("  Agent Identity ID: {Id}", identity.Id);
        return identity;
    }

    /// <summary>
    /// Gets access token using Blueprint's client credentials (OAuth 2.0 Client Credentials Grant)
    /// </summary>
    private async Task<string?> GetBlueprintAccessTokenAsync(
        string tenantId,
        string clientId,
        string clientSecret,
        CancellationToken ct)
    {
        _logger.LogDebug("Acquiring Blueprint access token via client credentials...");

        var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        try
        {
            var response = await _httpClient.PostAsync(tokenEndpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to acquire Blueprint token: {StatusCode} - {Body}", 
                    response.StatusCode, responseBody);

                if (responseBody.Contains("invalid_client", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("");
                    _logger.LogError("INVALID CLIENT - The Blueprint client ID or secret may be incorrect or expired.");
                    _logger.LogError("");
                }

                return null;
            }

            var tokenDoc = JsonDocument.Parse(responseBody);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

            _logger.LogDebug("Blueprint access token acquired successfully");
            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception acquiring Blueprint token: {Message}", ex.Message);
            return null;
        }
    }
}
