// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CitizenAgent.Setup.Cli.Commands;

/// <summary>
/// Command for Agent User operations
/// </summary>
public class UserCommand
{
    public static Command CreateCommand(
        ILogger<UserCommand> logger,
        IConfigService configService,
        IGraphApiService graphService,
        IAgentUserService userService,
        IOutputService outputService)
    {
        var command = new Command("user", "Manage Agent User");

        var createCommand = new Command("create", "Create a new Agent User");

        var tenantIdOption = new Option<string>("--tenant-id", "Azure AD tenant ID") { IsRequired = true };
        var userUpnOption = new Option<string>("--upn", "Agent User UPN") { IsRequired = true };
        var displayNameOption = new Option<string>("--display-name", () => "Agent User", "Agent User display name");
        var identityIdOption = new Option<string>("--identity-id", "Agent Identity ID") { IsRequired = true };
        var clientIdOption = new Option<string>("--client-id", "Management App Client ID") { IsRequired = true };
        var clientSecretOption = new Option<string>("--client-secret", "Management App Client Secret") { IsRequired = true };

        createCommand.AddOption(tenantIdOption);
        createCommand.AddOption(userUpnOption);
        createCommand.AddOption(displayNameOption);
        createCommand.AddOption(identityIdOption);
        createCommand.AddOption(clientIdOption);
        createCommand.AddOption(clientSecretOption);

        createCommand.SetHandler(async (context) =>
        {
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption)!;
            var userUpn = context.ParseResult.GetValueForOption(userUpnOption)!;
            var displayName = context.ParseResult.GetValueForOption(displayNameOption)!;
            var identityId = context.ParseResult.GetValueForOption(identityIdOption)!;
            var clientId = context.ParseResult.GetValueForOption(clientIdOption)!;
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption)!;
            var ct = context.GetCancellationToken();

            try
            {
                outputService.WritePhase("Agent User Creation");

                outputService.WriteStep("Acquiring management token...");
                await graphService.GetAccessTokenAsync(tenantId, clientId, clientSecret, ct);
                outputService.WriteStep("Token acquired", StepStatus.Success);

                var existing = await userService.FindAgentUserAsync(userUpn, ct);
                if (existing != null)
                {
                    outputService.WriteStep($"Agent User already exists: {existing.Id}", StepStatus.Warning);
                    outputService.WriteStep($"  UPN: {existing.UserPrincipalName}");
                    context.ExitCode = 0;
                    return;
                }

                outputService.WriteStep($"Creating Agent User: {userUpn}...");
                var user = await userService.CreateAgentUserAsync(userUpn, displayName, identityId, ct);
                outputService.WriteStep("Agent User created", StepStatus.Success);
                outputService.WriteStep($"  ID: {user.Id}");
                outputService.WriteStep($"  UPN: {user.UserPrincipalName}");

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agent User creation failed");
                throw;
            }
        });

        command.AddCommand(createCommand);
        return command;
    }
}
