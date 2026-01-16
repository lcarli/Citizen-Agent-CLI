// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for interactive authentication operations
/// </summary>
public interface IInteractiveAuthService
{
    /// <summary>
    /// Acquires an access token using Azure CLI authentication
    /// </summary>
    Task<string?> GetTokenViaAzureCliAsync(string tenantId, string[] scopes, CancellationToken ct = default);

    /// <summary>
    /// Acquires an access token using interactive browser authentication
    /// </summary>
    Task<string?> GetTokenViaBrowserAsync(string tenantId, string[] scopes, string? clientAppId = null, CancellationToken ct = default);

    /// <summary>
    /// Acquires an access token using the best available method (tries Azure CLI first, then browser)
    /// </summary>
    Task<string> GetTokenAsync(string tenantId, string[]? scopes = null, string? clientAppId = null, CancellationToken ct = default);

    /// <summary>
    /// Checks if Azure CLI is authenticated
    /// </summary>
    Task<bool> IsAzureCliAuthenticatedAsync(CancellationToken ct = default);

    /// <summary>
    /// Initiates Azure CLI login
    /// </summary>
    Task<bool> LoginViaAzureCliAsync(string? tenantId = null, CancellationToken ct = default);
}

/// <summary>
/// Service for interactive authentication using Azure CLI or browser
/// </summary>
public class InteractiveAuthService : IInteractiveAuthService
{
    private readonly ILogger<InteractiveAuthService> _logger;
    private static readonly string[] DefaultGraphScopes = new[] { "https://graph.microsoft.com/.default" };

    public InteractiveAuthService(ILogger<InteractiveAuthService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenViaAzureCliAsync(string tenantId, string[] scopes, CancellationToken ct = default)
    {
        _logger.LogDebug("Acquiring token via Azure CLI for tenant {TenantId}", tenantId);

        try
        {
            var credential = new AzureCliCredential(new AzureCliCredentialOptions
            {
                TenantId = tenantId
            });

            var context = new TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(context, ct);

            _logger.LogDebug("Token acquired successfully via Azure CLI");
            return token.Token;
        }
        catch (CredentialUnavailableException ex)
        {
            _logger.LogDebug("Azure CLI credential not available: {Message}", ex.Message);
            return null;
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogDebug("Azure CLI authentication failed: {Message}", ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenViaBrowserAsync(string tenantId, string[] scopes, string? clientAppId = null, CancellationToken ct = default)
    {
        _logger.LogDebug("Acquiring token via interactive browser for tenant {TenantId}", tenantId);

        try
        {
            InteractiveBrowserCredentialOptions options;

            if (!string.IsNullOrEmpty(clientAppId))
            {
                options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = tenantId,
                    ClientId = clientAppId,
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
                };
            }
            else
            {
                // Use Azure CLI's well-known client ID for interactive login
                options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = tenantId,
                    ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46", // Azure CLI client ID
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
                };
            }

            var credential = new InteractiveBrowserCredential(options);
            var context = new TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(context, ct);

            _logger.LogDebug("Token acquired successfully via interactive browser");
            return token.Token;
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning("Interactive browser authentication failed: {Message}", ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetTokenAsync(string tenantId, string[]? scopes = null, string? clientAppId = null, CancellationToken ct = default)
    {
        scopes ??= DefaultGraphScopes;

        // If a custom client app ID is provided, we MUST use browser authentication
        // because Azure CLI doesn't have the required delegated permissions
        if (!string.IsNullOrEmpty(clientAppId))
        {
            _logger.LogInformation("Using interactive browser authentication with custom app: {ClientAppId}", clientAppId);
            _logger.LogInformation("A browser window will open for authentication.");

            var token = await GetTokenViaBrowserAsync(tenantId, scopes, clientAppId, ct);

            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("Authenticated via interactive browser");
                return token;
            }

            throw new InvalidOperationException(
                $"Failed to authenticate with custom app {clientAppId}. " +
                "Ensure the app has the required permissions and admin consent.");
        }

        // No custom app ID - try Azure CLI first (faster, no browser popup)
        var cliToken = await GetTokenViaAzureCliAsync(tenantId, scopes, ct);

        if (!string.IsNullOrEmpty(cliToken))
        {
            _logger.LogInformation("Authenticated via Azure CLI");
            return cliToken;
        }

        _logger.LogInformation("Azure CLI not authenticated. Attempting interactive browser login...");
        _logger.LogInformation("A browser window will open for authentication.");

        // Try interactive browser
        var browserToken = await GetTokenViaBrowserAsync(tenantId, scopes, clientAppId, ct);

        if (!string.IsNullOrEmpty(browserToken))
        {
            _logger.LogInformation("Authenticated via interactive browser");
            return browserToken;
        }

        // If both fail, try to login via Azure CLI
        _logger.LogWarning("Interactive browser authentication failed. Attempting Azure CLI login...");

        if (await LoginViaAzureCliAsync(tenantId, ct))
        {
            cliToken = await GetTokenViaAzureCliAsync(tenantId, scopes, ct);
            if (!string.IsNullOrEmpty(cliToken))
            {
                _logger.LogInformation("Authenticated via Azure CLI after login");
                return cliToken;
            }
        }

        throw new InvalidOperationException(
            "Failed to authenticate. Please ensure you are logged in via Azure CLI ('az login') " +
            "or have access to a browser for interactive authentication.");
    }

    /// <inheritdoc />
    public async Task<bool> IsAzureCliAuthenticatedAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await ExecuteCommandAsync("az", "account show", ct);
            return result.exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> LoginViaAzureCliAsync(string? tenantId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Initiating Azure CLI login...");

        var args = tenantId != null ? $"login --tenant {tenantId}" : "login";

        try
        {
            var result = await ExecuteCommandAsync("az", args, ct);

            if (result.exitCode == 0)
            {
                _logger.LogInformation("Azure CLI login successful");
                return true;
            }

            _logger.LogError("Azure CLI login failed: {Error}", result.error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Azure CLI login");
            return false;
        }
    }

    private static async Task<(int exitCode, string output, string error)> ExecuteCommandAsync(
        string command, string arguments, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // On Windows, use cmd.exe for az commands
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c {command} {arguments}";
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        return (process.ExitCode, output, error);
    }
}
