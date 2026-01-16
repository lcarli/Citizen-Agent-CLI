// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Constants;
using CitizenAgent.Setup.Cli.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CitizenAgent.Setup.Cli.Commands;

/// <summary>
/// Command for permission operations
/// </summary>
public class PermissionsCommand
{
    public static Command CreateCommand(
        ILogger<PermissionsCommand> logger,
        IConfigService configService,
        IGraphApiService graphService,
        IPermissionService permissionService,
        IOutputService outputService)
    {
        var command = new Command("permissions", "Manage OAuth2 permissions and grants");

        var grantCommand = new Command("grant", "Create OAuth2 permission grant");

        var tenantIdOption = new Option<string>("--tenant-id", "Azure AD tenant ID") { IsRequired = true };
        var identityIdOption = new Option<string>("--identity-id", "Agent Identity Service Principal ID") { IsRequired = true };
        var userIdOption = new Option<string>("--user-id", "Agent User ID") { IsRequired = true };
        var scopesOption = new Option<string>("--scopes", () => DelegatedScopes.DefaultGrantScopes, "OAuth2 scopes (space-separated)");
        var clientAppIdOption = new Option<string?>("--client-app-id", "Optional: Client App ID for delegated authentication");

        grantCommand.AddOption(tenantIdOption);
        grantCommand.AddOption(identityIdOption);
        grantCommand.AddOption(userIdOption);
        grantCommand.AddOption(scopesOption);
        grantCommand.AddOption(clientAppIdOption);

        grantCommand.SetHandler(async (context) =>
        {
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption)!;
            var identityId = context.ParseResult.GetValueForOption(identityIdOption)!;
            var userId = context.ParseResult.GetValueForOption(userIdOption)!;
            var scopes = context.ParseResult.GetValueForOption(scopesOption)!;
            var clientAppId = context.ParseResult.GetValueForOption(clientAppIdOption);
            var ct = context.GetCancellationToken();

            try
            {
                outputService.WritePhase("OAuth2 Permission Grant");

                outputService.WriteStep("Authenticating via Azure CLI or browser...");
                await graphService.GetAccessTokenInteractiveAsync(tenantId, clientAppId, ct);
                outputService.WriteStep("Authentication successful", StepStatus.Success);

                outputService.WriteStep("Getting Graph Service Principal...");
                var graphSpId = await permissionService.GetGraphServicePrincipalIdAsync(ct);
                outputService.WriteStep($"  Graph SP ID: {graphSpId}");

                var existing = await permissionService.FindOAuth2PermissionGrantAsync(identityId, graphSpId, userId, ct);
                if (existing != null)
                {
                    outputService.WriteStep($"Permission grant already exists: {existing.Id}", StepStatus.Warning);
                    outputService.WriteStep($"  Scopes: {existing.Scope}");
                    context.ExitCode = 0;
                    return;
                }

                outputService.WriteStep("Creating OAuth2 permission grant...");
                var grant = await permissionService.CreateOAuth2PermissionGrantAsync(identityId, graphSpId, userId, scopes, ct);
                outputService.WriteStep("Permission grant created", StepStatus.Success);
                outputService.WriteStep($"  Grant ID: {grant.Id}");
                outputService.WriteStep($"  Scopes: {scopes}");

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Permission grant creation failed");
                throw;
            }
        });

        command.AddCommand(grantCommand);
        return command;
    }
}
