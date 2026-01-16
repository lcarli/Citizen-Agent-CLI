// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Constants;
using CitizenAgent.Setup.Cli.Exceptions;
using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for permission operations
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Gets the Graph service principal ID
    /// </summary>
    Task<string> GetGraphServicePrincipalIdAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates an OAuth2 permission grant
    /// </summary>
    Task<OAuth2PermissionGrant> CreateOAuth2PermissionGrantAsync(
        string clientSpId,
        string resourceSpId,
        string principalId,
        string scopes,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if an OAuth2 permission grant already exists
    /// </summary>
    Task<OAuth2PermissionGrant?> FindOAuth2PermissionGrantAsync(
        string clientSpId,
        string resourceSpId,
        string? principalId = null,
        CancellationToken ct = default);
}

/// <summary>
/// Service for permission operations
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ILogger<PermissionService> _logger;
    private readonly IGraphApiService _graphService;

    public PermissionService(ILogger<PermissionService> logger, IGraphApiService graphService)
    {
        _logger = logger;
        _graphService = graphService;
    }

    /// <inheritdoc />
    public async Task<string> GetGraphServicePrincipalIdAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Looking up Graph service principal");

        var filter = Uri.EscapeDataString($"appId eq '{GraphConstants.GraphResourceId}'");
        var doc = await _graphService.GetAsync($"/servicePrincipals?$filter={filter}", ct);

        if (doc == null || !doc.RootElement.TryGetProperty("value", out var value) || value.GetArrayLength() == 0)
        {
            throw new ResourceNotFoundException("Service Principal", GraphConstants.GraphResourceId);
        }

        return value[0].GetProperty("id").GetString()!;
    }

    /// <inheritdoc />
    public async Task<OAuth2PermissionGrant?> FindOAuth2PermissionGrantAsync(
        string clientSpId,
        string resourceSpId,
        string? principalId = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Searching for OAuth2 permission grant: Client={ClientSpId}, Resource={ResourceSpId}", clientSpId, resourceSpId);

        var filter = Uri.EscapeDataString($"clientId eq '{clientSpId}'");
        var doc = await _graphService.GetAsync($"/oauth2PermissionGrants?$filter={filter}", ct);

        if (doc == null || !doc.RootElement.TryGetProperty("value", out var value))
        {
            return null;
        }

        foreach (var grant in value.EnumerateArray())
        {
            var grantResourceId = grant.TryGetProperty("resourceId", out var resId) ? resId.GetString() : null;
            var grantPrincipalId = grant.TryGetProperty("principalId", out var prinId) ? prinId.GetString() : null;

            if (grantResourceId == resourceSpId)
            {
                if (principalId == null || grantPrincipalId == principalId)
                {
                    return new OAuth2PermissionGrant
                    {
                        Id = grant.GetProperty("id").GetString(),
                        ClientId = clientSpId,
                        ResourceId = grantResourceId,
                        PrincipalId = grantPrincipalId,
                        Scope = grant.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
                        ConsentType = grant.TryGetProperty("consentType", out var ct2) ? ct2.GetString() : null
                    };
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<OAuth2PermissionGrant> CreateOAuth2PermissionGrantAsync(
        string clientSpId,
        string resourceSpId,
        string principalId,
        string scopes,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating OAuth2 permission grant");
        _logger.LogDebug("  Client: {ClientSpId}", clientSpId);
        _logger.LogDebug("  Resource: {ResourceSpId}", resourceSpId);
        _logger.LogDebug("  Principal: {PrincipalId}", principalId);
        _logger.LogDebug("  Scopes: {Scopes}", scopes);

        var payload = new
        {
            clientId = clientSpId,
            consentType = "Principal",
            principalId,
            resourceId = resourceSpId,
            scope = scopes
        };

        // Use v1.0 for oauth2PermissionGrants
        var originalVersion = _graphService.ApiVersion;
        _graphService.ApiVersion = "v1.0";

        try
        {
            var doc = await _graphService.PostAsync("/oauth2PermissionGrants", payload, ct);

            if (doc == null)
            {
                throw new GraphApiException("Failed to create OAuth2 permission grant", 500);
            }

            var grant = new OAuth2PermissionGrant
            {
                Id = doc.RootElement.GetProperty("id").GetString(),
                ClientId = clientSpId,
                ResourceId = resourceSpId,
                PrincipalId = principalId,
                Scope = scopes,
                ConsentType = "Principal"
            };

            _logger.LogInformation("OAuth2 permission grant created: {Id}", grant.Id);
            return grant;
        }
        finally
        {
            _graphService.ApiVersion = originalVersion;
        }
    }
}
