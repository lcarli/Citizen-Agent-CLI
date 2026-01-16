// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Exceptions;
using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for Agent User operations
/// </summary>
public interface IAgentUserService
{
    /// <summary>
    /// Finds an existing Agent User by UPN
    /// </summary>
    Task<GraphUser?> FindAgentUserAsync(string upn, CancellationToken ct = default);

    /// <summary>
    /// Creates a new Agent User
    /// </summary>
    Task<GraphUser> CreateAgentUserAsync(string upn, string displayName, string agentIdentityId, CancellationToken ct = default);
}

/// <summary>
/// Service for Agent User operations
/// </summary>
public class AgentUserService : IAgentUserService
{
    private readonly ILogger<AgentUserService> _logger;
    private readonly IGraphApiService _graphService;

    public AgentUserService(ILogger<AgentUserService> logger, IGraphApiService graphService)
    {
        _logger = logger;
        _graphService = graphService;
    }

    /// <inheritdoc />
    public async Task<GraphUser?> FindAgentUserAsync(string upn, CancellationToken ct = default)
    {
        _logger.LogDebug("Searching for Agent User: {UPN}", upn);

        try
        {
            var doc = await _graphService.GetAsync($"/users/{Uri.EscapeDataString(upn)}", ct);

            if (doc == null)
            {
                return null;
            }

            return new GraphUser
            {
                Id = doc.RootElement.GetProperty("id").GetString(),
                UserPrincipalName = doc.RootElement.TryGetProperty("userPrincipalName", out var upnProp) ? upnProp.GetString() : upn,
                DisplayName = doc.RootElement.TryGetProperty("displayName", out var dn) ? dn.GetString() : null
            };
        }
        catch (GraphApiException ex) when (ex.HttpStatusCode == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GraphUser> CreateAgentUserAsync(string upn, string displayName, string agentIdentityId, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating Agent User: {UPN}", upn);

        var mailNickname = upn.Split('@')[0];

        // Following the official Microsoft Agent365 DevTools CLI approach:
        // Reference: microsoft/Agent365-devTools A365CreateInstanceRunner.cs CreateAgentUserAsync
        // Use @odata.type = "microsoft.graph.agentUser" and identityParent object
        var payload = new Dictionary<string, object>
        {
            ["@odata.type"] = "microsoft.graph.agentUser",
            ["accountEnabled"] = true,
            ["displayName"] = displayName,
            ["mailNickname"] = mailNickname,
            ["userPrincipalName"] = upn,
            ["usageLocation"] = "US",  // Required for license assignment
            ["identityParent"] = new Dictionary<string, object>
            {
                ["id"] = agentIdentityId
            }
        };

        var doc = await _graphService.PostAsync("/users", payload, ct);

        if (doc == null)
        {
            throw new GraphApiException("Failed to create Agent User", 500);
        }

        var user = new GraphUser
        {
            Id = doc.RootElement.GetProperty("id").GetString(),
            UserPrincipalName = upn,
            DisplayName = displayName,
            MailNickname = mailNickname
        };

        _logger.LogInformation("Agent User created: {Id}", user.Id);
        return user;
    }
}
