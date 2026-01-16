// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Service for interacting with Azure CLI
/// </summary>
public interface IAzureCliService
{
    /// <summary>
    /// Check if user is logged in to Azure CLI
    /// </summary>
    Task<bool> IsLoggedInAsync(CancellationToken ct = default);

    /// <summary>
    /// Get current Azure account info
    /// </summary>
    Task<AzureAccountInfo?> GetCurrentAccountAsync(CancellationToken ct = default);

    /// <summary>
    /// Get list of available subscriptions
    /// </summary>
    Task<List<AzureSubscriptionInfo>> ListSubscriptionsAsync(CancellationToken ct = default);
}

public class AzureCliService : IAzureCliService
{
    private readonly ILogger<AzureCliService> _logger;

    public AzureCliService(ILogger<AzureCliService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await RunAzCommandAsync("account show", ct);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AzureAccountInfo?> GetCurrentAccountAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await RunAzCommandAsync("account show -o json", ct);
            
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                _logger.LogDebug("Azure CLI returned non-zero exit code or empty output");
                return null;
            }

            var account = JsonSerializer.Deserialize<AzureAccountInfo>(result.Output, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return account;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get Azure account info");
            return null;
        }
    }

    public async Task<List<AzureSubscriptionInfo>> ListSubscriptionsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await RunAzCommandAsync("account list -o json", ct);
            
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                return new List<AzureSubscriptionInfo>();
            }

            var subscriptions = JsonSerializer.Deserialize<List<AzureSubscriptionInfo>>(result.Output, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return subscriptions ?? new List<AzureSubscriptionInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to list subscriptions");
            return new List<AzureSubscriptionInfo>();
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAzCommandAsync(string arguments, CancellationToken ct)
    {
        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "cmd.exe" : "/bin/bash";
        var args = isWindows ? $"/c az {arguments}" : $"-c \"az {arguments}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }
}

public class AzureAccountInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";  // Subscription ID

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";  // Subscription Name

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = "";

    [JsonPropertyName("user")]
    public AzureUserInfo? User { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}

public class AzureUserInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";  // Usually the email/UPN

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class AzureSubscriptionInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}
