// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Models;
using CitizenAgent.Setup.Cli.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CitizenAgent.Setup.Cli.Commands;

/// <summary>
/// Command for configuration management
/// </summary>
public class ConfigCommand
{
    public static Command CreateCommand(
        ILogger<ConfigCommand> logger,
        IConfigService configService,
        IConfigurationWizardService wizardService)
    {
        var command = new Command("config", "Manage configuration files");

        // Init subcommand - now with interactive wizard!
        var initCommand = new Command("init", "Interactive wizard to configure CitizenAgent");
        var outputOption = new Option<string>("--output", () => "citizen-agent.config.json", "Output file path");
        var importOption = new Option<string?>(new[] { "-c", "--configfile" }, "Import from existing config file (skip wizard)");
        var templateOption = new Option<bool>("--template", () => false, "Generate template file only (skip wizard)");
        
        initCommand.AddOption(outputOption);
        initCommand.AddOption(importOption);
        initCommand.AddOption(templateOption);
        
        initCommand.SetHandler(async (context) =>
        {
            var outputPath = context.ParseResult.GetValueForOption(outputOption)!;
            var importPath = context.ParseResult.GetValueForOption(importOption);
            var templateOnly = context.ParseResult.GetValueForOption(templateOption);
            var ct = context.GetCancellationToken();

            // Option 1: Import from existing file
            if (!string.IsNullOrEmpty(importPath))
            {
                if (!File.Exists(importPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Config file not found: {importPath}");
                    Console.ResetColor();
                    context.ExitCode = 1;
                    return;
                }

                try
                {
                    var importedConfig = await configService.LoadAsync(importPath, ct);
                    await configService.SaveAsync(outputPath, importedConfig, ct);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Configuration imported to: {outputPath}");
                    Console.ResetColor();
                    context.ExitCode = 0;
                    return;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to import config: {ex.Message}");
                    Console.ResetColor();
                    context.ExitCode = 1;
                    return;
                }
            }

            // Option 2: Generate template only
            if (templateOnly)
            {
                var config = configService.CreateDefault();
                await configService.SaveAsync(outputPath, config, ct);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Configuration template created: {outputPath}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Edit the file with your values, then run:");
                Console.WriteLine("  ca-setup all");
                context.ExitCode = 0;
                return;
            }

            // Option 3: Run interactive wizard (default)
            SetupConfig? existingConfig = null;
            if (File.Exists(outputPath))
            {
                try
                {
                    existingConfig = await configService.LoadAsync(outputPath, ct);
                }
                catch
                {
                    // Ignore - will start fresh
                }
            }

            var wizardConfig = await wizardService.RunWizardAsync(existingConfig, ct);

            if (wizardConfig != null)
            {
                await configService.SaveAsync(outputPath, wizardConfig, ct);

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Configuration saved to: {outputPath}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("You can now run:");
                Console.WriteLine("  ca-setup all");
                context.ExitCode = 0;
            }
            else
            {
                context.ExitCode = 1;
            }
        });

        // Validate subcommand
        var validateCommand = new Command("validate", "Validate a configuration file");
        var configFileOption = new Option<string>("--file", "Configuration file path") { IsRequired = true };
        configFileOption.AddAlias("-f");

        validateCommand.AddOption(configFileOption);
        validateCommand.SetHandler(async (context) =>
        {
            var configFile = context.ParseResult.GetValueForOption(configFileOption)!;
            var ct = context.GetCancellationToken();

            try
            {
                var config = await configService.LoadAsync(configFile, ct);
                var (isValid, errors) = configService.Validate(config);

                if (isValid)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Configuration is valid");
                    Console.ResetColor();
                    context.ExitCode = 0;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Configuration is invalid:");
                    Console.ResetColor();
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    context.ExitCode = 1;
                }
            }
            catch (FileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Configuration file not found: {configFile}");
                Console.ResetColor();
                context.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error reading configuration: {ex.Message}");
                Console.ResetColor();
                context.ExitCode = 1;
            }
        });

        // Show subcommand
        var showCommand = new Command("show", "Display configuration file contents");
        showCommand.AddOption(configFileOption);
        showCommand.SetHandler(async (context) =>
        {
            var configFile = context.ParseResult.GetValueForOption(configFileOption)!;
            var ct = context.GetCancellationToken();

            try
            {
                var config = await configService.LoadAsync(configFile, ct);

                Console.WriteLine("Configuration:");
                Console.WriteLine($"  Tenant ID:           {config.TenantId}");
                Console.WriteLine($"  Blueprint Name:      {config.BlueprintDisplayName}");
                Console.WriteLine($"  Identity Name:       {config.AgentIdentityDisplayName}");
                Console.WriteLine($"  User UPN:            {config.AgentUserUpn}");
                Console.WriteLine($"  User Display Name:   {config.AgentUserDisplayName}");
                Console.WriteLine($"  Client App ID:       {config.ClientAppId ?? "(not set)"}");
                Console.WriteLine($"  Foundry Resource:    {config.FoundryResourceId ?? "(not set)"}");
                Console.WriteLine($"  Webhook URL:         {config.WebhookUrl ?? "(not set)"}");

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                context.ExitCode = 1;
            }
        });

        command.AddCommand(initCommand);
        command.AddCommand(validateCommand);
        command.AddCommand(showCommand);

        return command;
    }
}
