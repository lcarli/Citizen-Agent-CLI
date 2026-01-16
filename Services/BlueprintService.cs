// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Constants;
using CitizenAgent.Setup.Cli.Exceptions;
using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for Blueprint operations
/// </summary>
public interface IBlueprintService
{
    /// <summary>
    /// Finds an existing application by display name
    /// </summary>
    Task<GraphApplication?> FindAppByDisplayNameAsync(string displayName, CancellationToken ct = default);

    /// <summary>
    /// Creates a new Blueprint application
    /// </summary>
    Task<GraphApplication> CreateBlueprintAppAsync(string displayName, CancellationToken ct = default);

    /// <summary>
    /// Finds an existing service principal by app ID
    /// </summary>
    Task<ServicePrincipal?> FindServicePrincipalByAppIdAsync(string appId, CancellationToken ct = default);

    /// <summary>
    /// Creates a service principal for the Blueprint
    /// </summary>
    Task<ServicePrincipal> CreateBlueprintServicePrincipalAsync(string appId, CancellationToken ct = default);

    /// <summary>
    /// Creates a client secret for the Blueprint application
    /// </summary>
    Task<PasswordCredential> CreateClientSecretAsync(string appObjectId, CancellationToken ct = default);
}

/// <summary>
/// Service for Blueprint operations
/// </summary>
public class BlueprintService : IBlueprintService
{
    private readonly ILogger<BlueprintService> _logger;
    private readonly IGraphApiService _graphService;

    public BlueprintService(ILogger<BlueprintService> logger, IGraphApiService graphService)
    {
        _logger = logger;
        _graphService = graphService;
    }

    /// <inheritdoc />
    public async Task<GraphApplication?> FindAppByDisplayNameAsync(string displayName, CancellationToken ct = default)
    {
        _logger.LogDebug("Searching for application with display name: {DisplayName}", displayName);

        var filter = Uri.EscapeDataString($"displayName eq '{displayName}'");
        var doc = await _graphService.GetAsync($"/applications?$filter={filter}", ct);

        if (doc == null)
        {
            return null;
        }

        if (doc.RootElement.TryGetProperty("value", out var value) && value.GetArrayLength() > 0)
        {
            var appElement = value[0];
            return new GraphApplication
            {
                Id = appElement.GetProperty("id").GetString(),
                AppId = appElement.GetProperty("appId").GetString(),
                DisplayName = appElement.TryGetProperty("displayName", out var dn) ? dn.GetString() : null
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<GraphApplication> CreateBlueprintAppAsync(string displayName, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating Blueprint application: {DisplayName}", displayName);

        // Following the official Microsoft Agent365 DevTools CLI approach:
        // Use @odata.type = "Microsoft.Graph.AgentIdentityBlueprint" for Agent Blueprint creation
        // Reference: microsoft/Agent365-devTools BlueprintSubcommand.cs

        var blueprintPayload = new Dictionary<string, object>
        {
            ["@odata.type"] = "Microsoft.Graph.AgentIdentityBlueprint",
            ["displayName"] = displayName,
            ["signInAudience"] = "AzureADMultipleOrgs" // Multi-tenant
        };

        // Try to get current user for sponsor
        // With app-only auth, /me doesn't work, so we need to get the user via Azure CLI
        string? currentUserId = null;
        try
        {
            // First try /me (works with delegated auth)
            var meDoc = await _graphService.GetAsync("/me", ct);
            if (meDoc != null && meDoc.RootElement.TryGetProperty("id", out var idProp))
            {
                currentUserId = idProp.GetString();
                _logger.LogInformation("Current user ID for sponsor (from /me): {UserId}", currentUserId);
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
                // Use cmd.exe to run az command (ensures PATH is available)
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
                    _logger.LogInformation("Current user ID for sponsor (from Azure CLI): {UserId}", currentUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not get user from Azure CLI: {Message}", ex.Message);
            }
        }

        // Add sponsor - this is REQUIRED for Agent Identity Blueprint
        if (!string.IsNullOrEmpty(currentUserId))
        {
            blueprintPayload["sponsors@odata.bind"] = new[]
            {
                $"https://graph.microsoft.com/v1.0/users/{currentUserId}"
            };
        }
        else
        {
            _logger.LogWarning("No sponsor user ID found - Blueprint creation may fail");
            _logger.LogWarning("Ensure you are signed in with Azure CLI: az login");
        }

        _logger.LogDebug("Creating Agent Blueprint with @odata.type approach...");
        
        // Use beta API with special headers for Agent Blueprint
        var doc = await _graphService.PostWithHeadersAsync(
            "/applications",
            blueprintPayload,
            new Dictionary<string, string>
            {
                ["OData-Version"] = "4.0",
                ["ConsistencyLevel"] = "eventual"
            },
            ct);

        if (doc == null)
        {
            throw new GraphApiException("Failed to create Blueprint application", 500);
        }

        // Log raw response for debugging
        var rawJson = doc.RootElement.GetRawText();
        _logger.LogDebug("Blueprint creation response: {Json}", rawJson);

        var objectId = doc.RootElement.GetProperty("id").GetString();
        var appId = doc.RootElement.TryGetProperty("appId", out var appIdProp) 
            ? appIdProp.GetString() 
            : null;

        // If appId is missing or same as objectId, wait and fetch the app to get correct IDs
        if (string.IsNullOrEmpty(appId) || appId == objectId)
        {
            _logger.LogDebug("appId missing or same as objectId, waiting and fetching app...");
            await Task.Delay(2000, ct); // Wait for propagation
            
            var fetchedApp = await FindAppByDisplayNameAsync(displayName, ct);
            if (fetchedApp != null && !string.IsNullOrEmpty(fetchedApp.AppId))
            {
                appId = fetchedApp.AppId;
                objectId = fetchedApp.Id;
                _logger.LogDebug("Fetched correct IDs - AppId: {AppId}, ObjectId: {ObjectId}", appId, objectId);
            }
        }

        var app = new GraphApplication
        {
            Id = objectId,
            AppId = appId,
            DisplayName = displayName
        };

        _logger.LogInformation("Blueprint application created: {AppId} (Object ID: {ObjectId})", app.AppId, app.Id);

        // Update application with identifier URI
        var identifierUri = $"api://{app.AppId}";
        _logger.LogDebug("Setting identifier URI: {Uri}", identifierUri);
        
        await _graphService.PatchAsync(
            $"/applications/{app.Id}",
            new { identifierUris = new[] { identifierUri } },
            ct);

        return app;
    }

    /// <inheritdoc />
    public async Task<ServicePrincipal?> FindServicePrincipalByAppIdAsync(string appId, CancellationToken ct = default)
    {
        _logger.LogDebug("Searching for service principal with app ID: {AppId}", appId);

        var filter = Uri.EscapeDataString($"appId eq '{appId}'");
        var doc = await _graphService.GetAsync($"/servicePrincipals?$filter={filter}", ct);

        if (doc == null)
        {
            return null;
        }

        if (doc.RootElement.TryGetProperty("value", out var value) && value.GetArrayLength() > 0)
        {
            var spElement = value[0];
            return new ServicePrincipal
            {
                Id = spElement.GetProperty("id").GetString(),
                AppId = spElement.GetProperty("appId").GetString(),
                DisplayName = spElement.TryGetProperty("displayName", out var dn) ? dn.GetString() : null
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ServicePrincipal> CreateBlueprintServicePrincipalAsync(string appId, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating Blueprint service principal for app: {AppId}", appId);

        // Standard service principal creation - no special template needed
        // The Blueprint is already of type AgentIdentityBlueprint from the Application creation
        var doc = await _graphService.PostAsync("/servicePrincipals", new { appId }, ct);

        if (doc == null)
        {
            throw new GraphApiException("Failed to create Blueprint service principal", 500);
        }

        var sp = new ServicePrincipal
        {
            Id = doc.RootElement.GetProperty("id").GetString(),
            AppId = appId
        };

        _logger.LogInformation("Blueprint service principal created: {SpId}", sp.Id);
        return sp;
    }

    /// <inheritdoc />
    public async Task<PasswordCredential> CreateClientSecretAsync(string appObjectId, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating client secret for application: {AppObjectId}", appObjectId);

        var payload = new
        {
            passwordCredential = new
            {
                displayName = $"CitizenAgent-Secret-{DateTime.UtcNow:yyyyMMdd}",
                endDateTime = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
            }
        };

        var doc = await _graphService.PostAsync($"/applications/{appObjectId}/addPassword", payload, ct);

        if (doc == null)
        {
            throw new GraphApiException("Failed to create client secret", 500);
        }

        var credential = new PasswordCredential
        {
            SecretText = doc.RootElement.GetProperty("secretText").GetString(),
            DisplayName = doc.RootElement.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
            EndDateTime = doc.RootElement.TryGetProperty("endDateTime", out var ed) ? ed.GetDateTime() : null
        };

        _logger.LogInformation("Client secret created successfully");
        return credential;
    }
}
