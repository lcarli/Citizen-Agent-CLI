// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Models;
using CitizenAgent.Setup.Cli.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CitizenAgent.Setup.Cli.Commands;

/// <summary>
/// Command for Blueprint operations only
/// </summary>
public class BlueprintCommand
{
    public static Command CreateCommand(
        ILogger<BlueprintCommand> logger,
        IConfigService configService,
        IGraphApiService graphService,
        IBlueprintService blueprintService,
        IOutputService outputService)
    {
        var command = new Command("blueprint", "Manage Blueprint App Registration");

        // Create subcommand
        var createCommand = new Command("create", "Create a new Blueprint App");
        
        var tenantIdOption = new Option<string>("--tenant-id", "Azure AD tenant ID") { IsRequired = true };
        var blueprintNameOption = new Option<string>("--name", "Blueprint App display name") { IsRequired = true };
        var clientAppIdOption = new Option<string?>("--client-app-id", "Optional: Client App ID for delegated authentication");
        var createSecretOption = new Option<bool>("--create-secret", () => true, "Create client secret for the blueprint");

        createCommand.AddOption(tenantIdOption);
        createCommand.AddOption(blueprintNameOption);
        createCommand.AddOption(clientAppIdOption);
        createCommand.AddOption(createSecretOption);

        createCommand.SetHandler(async (context) =>
        {
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption)!;
            var blueprintName = context.ParseResult.GetValueForOption(blueprintNameOption)!;
            var clientAppId = context.ParseResult.GetValueForOption(clientAppIdOption);
            var createSecret = context.ParseResult.GetValueForOption(createSecretOption);
            var ct = context.GetCancellationToken();

            try
            {
                outputService.WritePhase("Blueprint App Creation");

                // Authenticate interactively
                outputService.WriteStep("Authenticating via Azure CLI or browser...");
                await graphService.GetAccessTokenInteractiveAsync(tenantId, clientAppId, ct);
                outputService.WriteStep("Authentication successful", StepStatus.Success);

                // Check if exists
                var existing = await blueprintService.FindAppByDisplayNameAsync(blueprintName, ct);
                if (existing != null)
                {
                    outputService.WriteStep($"Blueprint already exists: {existing.AppId}", StepStatus.Warning);
                    outputService.WriteStep($"  Object ID: {existing.Id}");
                    context.ExitCode = 0;
                    return;
                }

                // Create app
                outputService.WriteStep("Creating Blueprint App...");
                var app = await blueprintService.CreateBlueprintAppAsync(blueprintName, ct);
                outputService.WriteStep($"Blueprint App created", StepStatus.Success);
                outputService.WriteStep($"  App ID: {app.AppId}");
                outputService.WriteStep($"  Object ID: {app.Id}");

                // Create SP
                outputService.WriteStep("Creating Service Principal...");
                var sp = await blueprintService.CreateBlueprintServicePrincipalAsync(app.AppId!, ct);
                outputService.WriteStep($"Service Principal created: {sp.Id}", StepStatus.Success);

                // Create secret
                if (createSecret)
                {
                    outputService.WriteStep("Creating Client Secret...");
                    var secret = await blueprintService.CreateClientSecretAsync(app.Id!, ct);
                    outputService.WriteStep("Client Secret created", StepStatus.Success);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  SECRET: {secret.SecretText}");
                    Console.WriteLine("  ⚠️  SAVE THIS - It won't be shown again!");
                    Console.ResetColor();
                }

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Blueprint creation failed");
                throw;
            }
        });

        // List subcommand
        var listCommand = new Command("list", "Find existing Blueprint Apps");
        
        listCommand.AddOption(tenantIdOption);
        listCommand.AddOption(blueprintNameOption);
        listCommand.AddOption(clientAppIdOption);

        listCommand.SetHandler(async (context) =>
        {
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption)!;
            var blueprintName = context.ParseResult.GetValueForOption(blueprintNameOption)!;
            var clientAppId = context.ParseResult.GetValueForOption(clientAppIdOption);
            var ct = context.GetCancellationToken();

            await graphService.GetAccessTokenInteractiveAsync(tenantId, clientAppId, ct);
            
            var app = await blueprintService.FindAppByDisplayNameAsync(blueprintName, ct);
            if (app != null)
            {
                Console.WriteLine($"Found Blueprint: {app.DisplayName}");
                Console.WriteLine($"  App ID: {app.AppId}");
                Console.WriteLine($"  Object ID: {app.Id}");
            }
            else
            {
                Console.WriteLine($"Blueprint '{blueprintName}' not found");
            }

            context.ExitCode = 0;
        });

        command.AddCommand(createCommand);
        command.AddCommand(listCommand);

        return command;
    }
}
