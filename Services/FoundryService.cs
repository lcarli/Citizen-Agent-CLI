// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Constants;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for Azure AI Foundry operations
/// </summary>
public interface IFoundryService
{
    /// <summary>
    /// Assigns Azure roles to a service principal on a Foundry resource
    /// </summary>
    Task<Dictionary<string, bool>> AssignFoundryRolesAsync(
        string servicePrincipalId,
        string foundryResourceId,
        CancellationToken ct = default);
}

/// <summary>
/// Service for Azure AI Foundry operations
/// </summary>
public class FoundryService : IFoundryService
{
    private readonly ILogger<FoundryService> _logger;

    public FoundryService(ILogger<FoundryService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, bool>> AssignFoundryRolesAsync(
        string servicePrincipalId,
        string foundryResourceId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Assigning Foundry roles to service principal: {SpId}", servicePrincipalId);
        _logger.LogDebug("Foundry Resource: {ResourceId}", foundryResourceId);

        var results = new Dictionary<string, bool>();

        foreach (var role in FoundryRoles.RequiredRoles)
        {
            _logger.LogDebug("Assigning role: {Role}", role);

            try
            {
                var success = await AssignRoleViaAzCliAsync(servicePrincipalId, role, foundryResourceId, ct);
                results[role] = success;

                if (success)
                {
                    _logger.LogInformation("  ✓ {Role} assigned", role);
                }
                else
                {
                    _logger.LogWarning("  ✗ {Role} failed", role);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "  ✗ {Role} error: {Message}", role, ex.Message);
                results[role] = false;
            }
        }

        return results;
    }

    private async Task<bool> AssignRoleViaAzCliAsync(
        string assignee,
        string role,
        string scope,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "az",
            Arguments = $"role assignment create --assignee \"{assignee}\" --role \"{role}\" --scope \"{scope}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start Azure CLI process");
                return false;
            }

            await process.WaitForExitAsync(ct);

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);

            if (process.ExitCode != 0)
            {
                // Check if role is already assigned (common scenario)
                if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                    error.Contains("RoleAssignmentExists", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Role {Role} already assigned", role);
                    return true;
                }

                _logger.LogDebug("Azure CLI error: {Error}", error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Azure CLI");
            return false;
        }
    }
}
