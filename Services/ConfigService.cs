// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for configuration management
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Loads configuration from a file
    /// </summary>
    Task<SetupConfig> LoadAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Saves configuration to a file
    /// </summary>
    Task SaveAsync(string filePath, SetupConfig config, CancellationToken ct = default);

    /// <summary>
    /// Creates a default configuration
    /// </summary>
    SetupConfig CreateDefault();

    /// <summary>
    /// Validates configuration
    /// </summary>
    (bool IsValid, List<string> Errors) Validate(SetupConfig config);
}

/// <summary>
/// Service for configuration management
/// </summary>
public class ConfigService : IConfigService
{
    private readonly ILogger<ConfigService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SetupConfig> LoadAsync(string filePath, CancellationToken ct = default)
    {
        _logger.LogDebug("Loading configuration from: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, ct);
        var config = JsonSerializer.Deserialize<SetupConfig>(json, JsonOptions);

        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize configuration");
        }

        _logger.LogDebug("Configuration loaded successfully");
        return config;
    }

    /// <inheritdoc />
    public async Task SaveAsync(string filePath, SetupConfig config, CancellationToken ct = default)
    {
        _logger.LogDebug("Saving configuration to: {FilePath}", filePath);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);

        _logger.LogDebug("Configuration saved successfully");
    }

    /// <inheritdoc />
    public SetupConfig CreateDefault()
    {
        return new SetupConfig
        {
            TenantId = "<your-tenant-id>",
            BlueprintDisplayName = "MyAgent-Blueprint",
            AgentIdentityDisplayName = "MyAgent-Identity",
            AgentUserUpn = "myagent@yourdomain.com",
            AgentUserDisplayName = "My Agent"
            // clientAppId is optional - leave empty to use Azure CLI authentication
        };
    }

    /// <inheritdoc />
    public (bool IsValid, List<string> Errors) Validate(SetupConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.TenantId))
            errors.Add("TenantId is required");

        if (string.IsNullOrWhiteSpace(config.BlueprintDisplayName))
            errors.Add("BlueprintDisplayName is required");

        if (string.IsNullOrWhiteSpace(config.AgentIdentityDisplayName))
            errors.Add("AgentIdentityDisplayName is required");

        if (string.IsNullOrWhiteSpace(config.AgentUserUpn))
            errors.Add("AgentUserUpn is required");

        // Validate UPN format
        if (!string.IsNullOrWhiteSpace(config.AgentUserUpn) && !config.AgentUserUpn.Contains('@'))
            errors.Add("AgentUserUpn must be a valid email format (user@domain.com)");

        // Validate Tenant ID format (GUID)
        if (!string.IsNullOrWhiteSpace(config.TenantId) && !Guid.TryParse(config.TenantId, out _))
            errors.Add("TenantId must be a valid GUID");

        return (errors.Count == 0, errors);
    }
}
