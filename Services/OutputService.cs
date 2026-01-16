// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for output formatting and saving
/// </summary>
public interface IOutputService
{
    /// <summary>
    /// Displays the setup summary to console
    /// </summary>
    void DisplaySummary(SetupOutput output);

    /// <summary>
    /// Displays next steps to the user
    /// </summary>
    void DisplayNextSteps(SetupOutput output);

    /// <summary>
    /// Saves output to a JSON file
    /// </summary>
    Task SaveOutputAsync(string filePath, SetupOutput output, CancellationToken ct = default);

    /// <summary>
    /// Writes a step message
    /// </summary>
    void WriteStep(string message, StepStatus status = StepStatus.Info);

    /// <summary>
    /// Writes a phase header
    /// </summary>
    void WritePhase(string phaseName);

    /// <summary>
    /// Writes a separator line
    /// </summary>
    void WriteSeparator();
}

/// <summary>
/// Status of a step
/// </summary>
public enum StepStatus
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Service for output formatting
/// </summary>
public class OutputService : IOutputService
{
    private readonly ILogger<OutputService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OutputService(ILogger<OutputService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void WriteStep(string message, StepStatus status = StepStatus.Info)
    {
        var (color, prefix) = status switch
        {
            StepStatus.Info => (ConsoleColor.Cyan, "INFO"),
            StepStatus.Success => (ConsoleColor.Green, "SUCCESS"),
            StepStatus.Warning => (ConsoleColor.Yellow, "WARNING"),
            StepStatus.Error => (ConsoleColor.Red, "ERROR"),
            _ => (ConsoleColor.White, "")
        };

        Console.ForegroundColor = color;
        Console.Write($"[{prefix}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    /// <inheritdoc />
    public void WritePhase(string phaseName)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"═══════════════════════════════════════════════════");
        Console.WriteLine($"  {phaseName}");
        Console.WriteLine($"═══════════════════════════════════════════════════");
        Console.ResetColor();
    }

    /// <inheritdoc />
    public void WriteSeparator()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("───────────────────────────────────────────────────");
        Console.ResetColor();
    }

    /// <inheritdoc />
    public void DisplaySummary(SetupOutput output)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                     SETUP COMPLETE                           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("SUMMARY:");
        Console.ResetColor();
        WriteSeparator();

        Console.WriteLine($"Tenant ID:              {output.TenantId}");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Blueprint App:");
        Console.ResetColor();
        Console.WriteLine($"  App ID:               {output.Blueprint.AppId}");
        Console.WriteLine($"  Object ID:            {output.Blueprint.ObjectId}");
        Console.WriteLine($"  Service Principal:    {output.Blueprint.ServicePrincipalId}");
        
        if (!string.IsNullOrEmpty(output.Blueprint.ClientSecret))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Client Secret:        {output.Blueprint.ClientSecret}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ⚠️  SAVE THIS SECRET - It won't be shown again!");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Agent Identity:");
        Console.ResetColor();
        Console.WriteLine($"  ID:                   {output.AgentIdentity.Id}");

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Agent User:");
        Console.ResetColor();
        Console.WriteLine($"  ID:                   {output.AgentUser.Id}");
        Console.WriteLine($"  UPN:                  {output.AgentUser.Upn}");

        if (!string.IsNullOrEmpty(output.Permissions.OAuth2GrantId))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Permissions:");
            Console.ResetColor();
            Console.WriteLine($"  OAuth2 Grant ID:      {output.Permissions.OAuth2GrantId}");
        }

        Console.WriteLine();
    }

    /// <inheritdoc />
    public void DisplayNextSteps(SetupOutput output)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("NEXT STEPS:");
        Console.ResetColor();
        WriteSeparator();

        Console.WriteLine("1. Assign licenses to Agent User (if not done automatically)");
        Console.WriteLine();
        Console.WriteLine("2. Configure App Settings in Azure Web App:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"   TENANT_ID={output.TenantId}");
        Console.WriteLine($"   CLIENT_ID={output.Blueprint.AppId}");
        Console.WriteLine($"   CLIENT_SECRET=<blueprint_secret>");
        Console.WriteLine($"   AGENT_USER_ID={output.AgentUser.Id}");
        Console.WriteLine($"   AGENT_USER_UPN={output.AgentUser.Upn}");
        Console.WriteLine($"   AGENT_BLUEPRINT_ID={output.Blueprint.AppId}");
        Console.WriteLine($"   AGENT_IDENTITY_ID={output.AgentIdentity.Id}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("3. Deploy your agent application to Azure");
        Console.WriteLine("4. Renew Graph subscription hourly (if using webhooks)");
        Console.WriteLine();
    }

    /// <inheritdoc />
    public async Task SaveOutputAsync(string filePath, SetupOutput output, CancellationToken ct = default)
    {
        _logger.LogDebug("Saving output to: {FilePath}", filePath);

        var json = JsonSerializer.Serialize(output, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);

        WriteStep($"Configuration saved to: {filePath}", StepStatus.Success);
    }
}
