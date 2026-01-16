// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CitizenAgent.Setup.Cli.Commands;

/// <summary>
/// Command for Agent Identity operations
/// </summary>
public class IdentityCommand
{
    public static Command CreateCommand(
        ILogger<IdentityCommand> logger,
        IConfigService configService,
        IGraphApiService graphService,
        IAgentIdentityService identityService,
        IOutputService outputService)
    {
        var command = new Command("identity", "Manage Agent Identity");

        var createCommand = new Command("create", "Create a new Agent Identity");

        var tenantIdOption = new Option<string>("--tenant-id", "Azure AD tenant ID") { IsRequired = true };
        var identityNameOption = new Option<string>("--name", "Agent Identity display name") { IsRequired = true };
        var blueprintAppIdOption = new Option<string>("--blueprint-app-id", "Blueprint App ID (client_id)") { IsRequired = true };
        var blueprintSecretOption = new Option<string>("--blueprint-secret", "Blueprint Client Secret") { IsRequired = true };
        var clientIdOption = new Option<string>("--client-id", "Management App Client ID (for initial token)") { IsRequired = true };
        var clientSecretOption = new Option<string>("--client-secret", "Management App Client Secret (for initial token)") { IsRequired = true };

        createCommand.AddOption(tenantIdOption);
        createCommand.AddOption(identityNameOption);
        createCommand.AddOption(blueprintAppIdOption);
        createCommand.AddOption(blueprintSecretOption);
        createCommand.AddOption(clientIdOption);
        createCommand.AddOption(clientSecretOption);

        createCommand.SetHandler(async (context) =>
        {
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption)!;
            var identityName = context.ParseResult.GetValueForOption(identityNameOption)!;
            var blueprintAppId = context.ParseResult.GetValueForOption(blueprintAppIdOption)!;
            var blueprintSecret = context.ParseResult.GetValueForOption(blueprintSecretOption)!;
            var clientId = context.ParseResult.GetValueForOption(clientIdOption)!;
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption)!;
            var ct = context.GetCancellationToken();

            try
            {
                outputService.WritePhase("Agent Identity Creation");

                outputService.WriteStep("Acquiring management token...");
                await graphService.GetAccessTokenAsync(tenantId, clientId, clientSecret, ct);
                outputService.WriteStep("Token acquired", StepStatus.Success);

                var existing = await identityService.FindAgentIdentityAsync(identityName, ct);
                if (existing != null)
                {
                    outputService.WriteStep($"Agent Identity already exists: {existing.Id}", StepStatus.Warning);
                    context.ExitCode = 0;
                    return;
                }

                outputService.WriteStep("Creating Agent Identity...");
                // Use the official Microsoft approach:
                // - Authenticate using Blueprint client credentials
                // - POST to /beta/serviceprincipals/Microsoft.Graph.AgentIdentity
                // - Use agentAppId (Blueprint App ID)
                var identity = await identityService.CreateAgentIdentityAsync(
                    identityName, 
                    blueprintAppId, 
                    blueprintSecret,
                    tenantId,
                    ct);
                outputService.WriteStep($"Agent Identity created", StepStatus.Success);
                outputService.WriteStep($"  ID: {identity.Id}");

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agent Identity creation failed");
                throw;
            }
        });

        command.AddCommand(createCommand);
        return command;
    }
}
