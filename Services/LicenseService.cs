// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Constants;
using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for license operations
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Gets available licenses in the tenant
    /// </summary>
    Task<List<SubscribedSku>> GetAvailableLicensesAsync(CancellationToken ct = default);

    /// <summary>
    /// Assigns licenses to a user
    /// </summary>
    Task<bool> AssignLicensesToUserAsync(string userId, IEnumerable<string> skuIds, CancellationToken ct = default);
}

/// <summary>
/// Service for license operations
/// </summary>
public class LicenseService : ILicenseService
{
    private readonly ILogger<LicenseService> _logger;
    private readonly IGraphApiService _graphService;

    public LicenseService(ILogger<LicenseService> logger, IGraphApiService graphService)
    {
        _logger = logger;
        _graphService = graphService;
    }

    /// <inheritdoc />
    public async Task<List<SubscribedSku>> GetAvailableLicensesAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Getting available licenses");

        // Use v1.0 for subscribedSkus
        var originalVersion = _graphService.ApiVersion;
        _graphService.ApiVersion = "v1.0";

        try
        {
            var doc = await _graphService.GetAsync("/subscribedSkus", ct);
            var licenses = new List<SubscribedSku>();

            if (doc == null || !doc.RootElement.TryGetProperty("value", out var value))
            {
                return licenses;
            }

            foreach (var sku in value.EnumerateArray())
            {
                var skuId = sku.TryGetProperty("skuId", out var id) ? id.GetString() : null;
                var skuPartNumber = sku.TryGetProperty("skuPartNumber", out var pn) ? pn.GetString() : null;
                var consumedUnits = sku.TryGetProperty("consumedUnits", out var cu) ? cu.GetInt32() : 0;

                var prepaidUnits = new PrepaidUnits();
                if (sku.TryGetProperty("prepaidUnits", out var pu))
                {
                    prepaidUnits.Enabled = pu.TryGetProperty("enabled", out var en) ? en.GetInt32() : 0;
                    prepaidUnits.Suspended = pu.TryGetProperty("suspended", out var su) ? su.GetInt32() : 0;
                    prepaidUnits.Warning = pu.TryGetProperty("warning", out var wa) ? wa.GetInt32() : 0;
                }

                licenses.Add(new SubscribedSku
                {
                    SkuId = skuId,
                    SkuPartNumber = skuPartNumber,
                    ConsumedUnits = consumedUnits,
                    PrepaidUnits = prepaidUnits
                });
            }

            _logger.LogDebug("Found {Count} licenses", licenses.Count);
            return licenses;
        }
        finally
        {
            _graphService.ApiVersion = originalVersion;
        }
    }

    /// <inheritdoc />
    public async Task<bool> AssignLicensesToUserAsync(string userId, IEnumerable<string> skuIds, CancellationToken ct = default)
    {
        _logger.LogInformation("Assigning licenses to user: {UserId}", userId);

        var addLicenses = skuIds.Select(id => new { skuId = id }).ToArray();

        if (!addLicenses.Any())
        {
            _logger.LogWarning("No licenses to assign");
            return false;
        }

        var payload = new
        {
            addLicenses,
            removeLicenses = Array.Empty<object>()
        };

        // Use v1.0 for license assignment
        var originalVersion = _graphService.ApiVersion;
        _graphService.ApiVersion = "v1.0";

        try
        {
            var doc = await _graphService.PostAsync($"/users/{userId}/assignLicense", payload, ct);
            
            if (doc != null)
            {
                _logger.LogInformation("Licenses assigned successfully");
                return true;
            }

            _logger.LogWarning("License assignment returned no response");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign licenses");
            return false;
        }
        finally
        {
            _graphService.ApiVersion = originalVersion;
        }
    }
}
