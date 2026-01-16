// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Constants;
using CitizenAgent.Setup.Cli.Helpers;
using CitizenAgent.Setup.Cli.Models;
using CitizenAgent.Setup.Cli.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CitizenAgent.Setup.Cli.Commands;

/// <summary>
/// Main setup command - runs all phases
/// </summary>
public class SetupCommand
{
    public static Command CreateCommand(
        ILogger<SetupCommand> logger,
        IConfigService configService,
        IGraphApiService graphService,
        IBlueprintService blueprintService,
        IAgentIdentityService agentIdentityService,
        IAgentUserService agentUserService,
        IPermissionService permissionService,
        ILicenseService licenseService,
        IFoundryService foundryService,
        IGraphSubscriptionService subscriptionService,
        IOutputService outputService,
        IAzureCliService azureCliService)
    {
        var command = new Command("all", "Set up a complete Agent 365 environment");

        // Options (none are required - can come from config file)
        var tenantIdOption = new Option<string?>("--tenant-id", "Azure AD tenant ID");
        var blueprintNameOption = new Option<string?>("--blueprint-name", "Blueprint App display name");
        var identityNameOption = new Option<string?>("--identity-name", "Agent Identity display name");
        var userUpnOption = new Option<string?>("--user-upn", "Agent User UPN (e.g., agent@contoso.com)");
        var userDisplayNameOption = new Option<string?>("--user-display-name", () => "Agent User", "Agent User display name");
        var clientIdOption = new Option<string?>("--client-id", "Management App Client ID");
        var clientSecretOption = new Option<string?>("--client-secret", "Management App Client Secret");
        var foundryResourceOption = new Option<string?>("--foundry-resource", "Azure AI Foundry Resource ID (for RBAC assignment)");
        var skipFoundryOption = new Option<bool>("--skip-foundry", () => false, "Skip Foundry RBAC assignment phase");
        var webhookUrlOption = new Option<string?>("--webhook-url", "Webhook URL for Teams messages");
        var skipWebhookOption = new Option<bool>("--skip-webhook", () => false, "Skip Graph subscription creation");
        var configFileOption = new Option<string?>("--config", "Path to configuration file");
        var outputFileOption = new Option<string?>("--output", () => "CitizenAgent-output.json", "Output file path");
        var verboseOption = new Option<bool>("--verbose", () => false, "Enable verbose logging");
        verboseOption.AddAlias("-v");

        command.AddOption(tenantIdOption);
        command.AddOption(blueprintNameOption);
        command.AddOption(identityNameOption);
        command.AddOption(userUpnOption);
        command.AddOption(userDisplayNameOption);
        command.AddOption(clientIdOption);
        command.AddOption(clientSecretOption);
        command.AddOption(foundryResourceOption);
        command.AddOption(skipFoundryOption);
        command.AddOption(webhookUrlOption);
        command.AddOption(skipWebhookOption);
        command.AddOption(configFileOption);
        command.AddOption(outputFileOption);
        command.AddOption(verboseOption);

        command.SetHandler(async (context) =>
        {
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption);
            var blueprintName = context.ParseResult.GetValueForOption(blueprintNameOption);
            var identityName = context.ParseResult.GetValueForOption(identityNameOption);
            var userUpn = context.ParseResult.GetValueForOption(userUpnOption);
            var userDisplayName = context.ParseResult.GetValueForOption(userDisplayNameOption);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var foundryResource = context.ParseResult.GetValueForOption(foundryResourceOption);
            var skipFoundry = context.ParseResult.GetValueForOption(skipFoundryOption);
            var webhookUrl = context.ParseResult.GetValueForOption(webhookUrlOption);
            var skipWebhook = context.ParseResult.GetValueForOption(skipWebhookOption);
            var configFile = context.ParseResult.GetValueForOption(configFileOption);
            var outputFile = context.ParseResult.GetValueForOption(outputFileOption) ?? "CitizenAgent-output.json";

            var ct = context.GetCancellationToken();

            // Auto-detect config file if not provided
            const string defaultConfigFile = "citizen-agent.config.json";
            if (string.IsNullOrEmpty(configFile) && File.Exists(defaultConfigFile))
            {
                configFile = defaultConfigFile;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[INFO] Found {defaultConfigFile} - loading configuration...");
                Console.ResetColor();
            }

            // Load from config file (auto-detected or explicit)
            if (!string.IsNullOrEmpty(configFile))
            {
                var config = await configService.LoadAsync(configFile, ct);
                // Config values are used as defaults, CLI args override them
                tenantId ??= config.TenantId;
                blueprintName ??= config.BlueprintDisplayName;
                identityName ??= config.AgentIdentityDisplayName;
                userUpn ??= config.AgentUserUpn;
                userDisplayName ??= config.AgentUserDisplayName;
                clientId ??= config.MgmtClientId;
                clientSecret ??= config.MgmtClientSecret;
                foundryResource ??= config.FoundryResourceId;
                webhookUrl ??= config.WebhookUrl;
            }

            // Validate required fields
            var missingFields = new List<string>();
            if (string.IsNullOrEmpty(tenantId)) missingFields.Add("--tenant-id");
            if (string.IsNullOrEmpty(blueprintName)) missingFields.Add("--blueprint-name");
            if (string.IsNullOrEmpty(identityName)) missingFields.Add("--identity-name");
            if (string.IsNullOrEmpty(userUpn)) missingFields.Add("--user-upn");
            if (string.IsNullOrEmpty(clientId)) missingFields.Add("--client-id");
            if (string.IsNullOrEmpty(clientSecret)) missingFields.Add("--client-secret");

            if (missingFields.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing required parameters:");
                foreach (var field in missingFields)
                {
                    Console.WriteLine($"  - {field}");
                }
                Console.WriteLine();
                Console.WriteLine("Provide values via command line options or create a config file:");
                Console.WriteLine("  ca-setup config init");
                Console.ResetColor();
                context.ExitCode = 1;
                return;
            }

            #pragma warning disable CS8601
            // Initialize output
            var output = new SetupOutput { TenantId = tenantId };
            #pragma warning restore CS8601

            try
            {
                // Display header
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║              Citizen Agent Setup CLI                         ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();

                // ============================================================
                // Phase 0: Authentication
                // ============================================================
                outputService.WritePhase("PHASE 0: Authentication");
                outputService.WriteStep("Acquiring management token...");
                
                #pragma warning disable CS8604
                await graphService.GetAccessTokenAsync(tenantId, clientId, clientSecret, ct);
                #pragma warning restore CS8604
                outputService.WriteStep("Management token acquired", StepStatus.Success);

                // ============================================================
                // Phase 1: Blueprint App Registration
                // ============================================================
                outputService.WritePhase("PHASE 1: Blueprint App Registration");

                #pragma warning disable CS8604
                // Step 1.1: Create or find Blueprint App
                var existingApp = await blueprintService.FindAppByDisplayNameAsync(blueprintName, ct);
                #pragma warning restore CS8604
                GraphApplication blueprintApp;

                if (existingApp != null)
                {
                    outputService.WriteStep($"Blueprint App already exists: {existingApp.Id}", StepStatus.Warning);
                    blueprintApp = existingApp;
                }
                else
                {
                    outputService.WriteStep("Creating Blueprint App...");
                    blueprintApp = await blueprintService.CreateBlueprintAppAsync(blueprintName, ct);
                    outputService.WriteStep($"Blueprint App created: {blueprintApp.Id}", StepStatus.Success);
                }

                output.Blueprint.AppId = blueprintApp.AppId;
                output.Blueprint.ObjectId = blueprintApp.Id;
                outputService.WriteStep($"  App ID: {blueprintApp.AppId}");
                outputService.WriteStep($"  Object ID: {blueprintApp.Id}");

                // Step 1.2: Create Blueprint Service Principal
                outputService.WriteSeparator();
                
                // Wait for App Registration to propagate in Azure AD
                outputService.WriteStep("Verifying App Registration...");
                var verifiedApp = await RetryHelper.WaitForConditionAsync(
                    async () => await blueprintService.FindAppByDisplayNameAsync(blueprintName, ct),
                    app => app != null && !string.IsNullOrEmpty(app.AppId),
                    "App Registration to propagate",
                    maxAttempts: 10,
                    delaySeconds: 2,
                    ct);

                if (verifiedApp == null)
                {
                    throw new InvalidOperationException("App Registration failed to propagate. Please try again.");
                }

                var existingSp = await blueprintService.FindServicePrincipalByAppIdAsync(blueprintApp.AppId!, ct);
                ServicePrincipal blueprintSp;

                if (existingSp != null)
                {
                    outputService.WriteStep($"Blueprint SP already exists: {existingSp.Id}", StepStatus.Warning);
                    blueprintSp = existingSp;
                }
                else
                {
                    outputService.WriteStep("Creating Blueprint Service Principal...");
                    
                    // Use retry with exponential backoff for SP creation
                    blueprintSp = await RetryHelper.ExecuteWithRetryAsync(
                        async () => await blueprintService.CreateBlueprintServicePrincipalAsync(blueprintApp.AppId!, ct),
                        "Service Principal creation",
                        maxAttempts: 5,
                        delaySeconds: 3,
                        ct);
                    
                    outputService.WriteStep($"Blueprint SP created: {blueprintSp.Id}", StepStatus.Success);
                }

                output.Blueprint.ServicePrincipalId = blueprintSp.Id;
                outputService.WriteStep($"  SP ID: {blueprintSp.Id}");

                // Step 1.3: Create Client Secret
                outputService.WriteSeparator();
                outputService.WriteStep("Creating Client Secret...");

                try
                {
                    // Use retry since the app may not be fully propagated yet
                    var secret = await RetryHelper.ExecuteWithRetryAsync(
                        async () => await blueprintService.CreateClientSecretAsync(blueprintApp.Id!, ct),
                        "Client Secret creation",
                        maxAttempts: 5,
                        delaySeconds: 3,
                        ct);
                    
                    output.Blueprint.ClientSecret = secret.SecretText;
                    outputService.WriteStep("Client Secret created", StepStatus.Success);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  SECRET: {secret.SecretText}");
                    Console.ResetColor();
                    
                    // Wait for secret propagation in Azure AD before using it
                    outputService.WriteStep("Waiting for secret propagation (10 seconds)...");
                    await Task.Delay(10000, ct);
                    outputService.WriteStep("Secret ready", StepStatus.Success);
                }
                catch (Exception ex)
                {
                    outputService.WriteStep($"Could not create secret: {ex.Message}", StepStatus.Warning);
                }

                // ============================================================
                // Phase 2: Agent Identity
                // ============================================================
                outputService.WritePhase("PHASE 2: Agent Identity");

                // Agent Identity requires Blueprint client secret for authentication
                if (string.IsNullOrEmpty(output.Blueprint.ClientSecret))
                {
                    outputService.WriteStep("ERROR: Blueprint client secret required for Agent Identity creation", StepStatus.Error);
                    outputService.WriteStep("The Blueprint client secret is needed to authenticate with Microsoft Graph");
                    context.ExitCode = 1;
                    return;
                }

                #pragma warning disable CS8604
                var existingIdentity = await agentIdentityService.FindAgentIdentityAsync(identityName, ct);
                #pragma warning restore CS8604
                ServicePrincipal agentIdentity;

                if (existingIdentity != null)
                {
                    outputService.WriteStep($"Agent Identity already exists: {existingIdentity.Id}", StepStatus.Warning);
                    agentIdentity = existingIdentity;
                }
                else
                {
                    outputService.WriteStep("Creating Agent Identity...");
                    // Use the official Microsoft approach:
                    // - Authenticate using Blueprint client credentials (blueprintAppId + blueprintClientSecret)
                    // - POST to /beta/serviceprincipals/Microsoft.Graph.AgentIdentity
                    // - Use agentAppId (NOT agentIdentityBlueprintId!)
                    agentIdentity = await agentIdentityService.CreateAgentIdentityAsync(
                        identityName, 
                        blueprintApp.AppId!,           // Blueprint App ID (client_id)
                        output.Blueprint.ClientSecret, // Blueprint Client Secret
                        tenantId,                      // Tenant ID
                        ct);
                    outputService.WriteStep($"Agent Identity created: {agentIdentity.Id}", StepStatus.Success);
                }

                output.AgentIdentity.Id = agentIdentity.Id;
                outputService.WriteStep($"  Agent Identity ID: {agentIdentity.Id}");

                // Wait for Agent Identity to propagate before creating Agent User
                if (existingIdentity == null)
                {
                    outputService.WriteStep("Waiting for Agent Identity propagation (15 seconds)...");
                    await Task.Delay(15000, ct);
                    outputService.WriteStep("Agent Identity ready", StepStatus.Success);
                }

                // ============================================================
                // Phase 3: Agent User
                // ============================================================
                outputService.WritePhase("PHASE 3: Agent User");

                #pragma warning disable CS8604
                var existingUser = await agentUserService.FindAgentUserAsync(userUpn, ct);
                #pragma warning restore CS8604
                GraphUser agentUser;

                if (existingUser != null)
                {
                    outputService.WriteStep($"Agent User already exists: {existingUser.Id}", StepStatus.Warning);
                    agentUser = existingUser;
                }
                else
                {
                    outputService.WriteStep($"Creating Agent User: {userUpn}...");
                    #pragma warning disable CS8604
                    agentUser = await agentUserService.CreateAgentUserAsync(userUpn, userDisplayName, agentIdentity.Id!, ct);
                    #pragma warning restore CS8604
                    outputService.WriteStep($"Agent User created: {agentUser.Id}", StepStatus.Success);
                    
                    // Wait for Agent User to propagate before OAuth2 grant
                    outputService.WriteStep("Waiting for Agent User propagation (10 seconds)...");
                    await Task.Delay(10000, ct);
                    outputService.WriteStep("Agent User ready", StepStatus.Success);
                }

                output.AgentUser.Id = agentUser.Id;
                output.AgentUser.Upn = userUpn;
                outputService.WriteStep($"  User ID: {agentUser.Id}");
                outputService.WriteStep($"  UPN: {userUpn}");

                // ============================================================
                // Phase 4: OAuth2 Permission Grant
                // ============================================================
                outputService.WritePhase("PHASE 4: OAuth2 Permission Grant");

                outputService.WriteStep("Getting Graph Service Principal...");
                var graphSpId = await permissionService.GetGraphServicePrincipalIdAsync(ct);

                var existingGrant = await permissionService.FindOAuth2PermissionGrantAsync(
                    agentIdentity.Id!, graphSpId, agentUser.Id, ct);

                if (existingGrant != null)
                {
                    outputService.WriteStep("OAuth2 Permission Grant already exists", StepStatus.Warning);
                    output.Permissions.OAuth2GrantId = existingGrant.Id;
                }
                else
                {
                    outputService.WriteStep("Creating OAuth2 Permission Grant...");
                    var grant = await permissionService.CreateOAuth2PermissionGrantAsync(
                        agentIdentity.Id!,
                        graphSpId,
                        agentUser.Id!,
                        DelegatedScopes.DefaultGrantScopes,
                        ct);
                    output.Permissions.OAuth2GrantId = grant.Id;
                    outputService.WriteStep($"OAuth2 Permission Grant created: {grant.Id}", StepStatus.Success);
                }

                // ============================================================
                // Phase 5: License Assignment
                // ============================================================
                outputService.WritePhase("PHASE 5: License Assignment");

                outputService.WriteStep("Checking available licenses...");
                var licenses = await licenseService.GetAvailableLicensesAsync(ct);

                var licensesToAssign = new List<string>();
                foreach (var lic in licenses)
                {
                    if (lic.SkuId == LicenseSkuIds.M365E5Developer &&
                        lic.ConsumedUnits < (lic.PrepaidUnits?.Enabled ?? 0))
                    {
                        licensesToAssign.Add(lic.SkuId);
                        outputService.WriteStep($"  Found available: {lic.SkuPartNumber}");
                    }
                }

                if (licensesToAssign.Any())
                {
                    var assigned = await licenseService.AssignLicensesToUserAsync(agentUser.Id!, licensesToAssign, ct);
                    if (assigned)
                    {
                        outputService.WriteStep("Licenses assigned successfully", StepStatus.Success);
                    }
                    else
                    {
                        outputService.WriteStep("License assignment failed", StepStatus.Warning);
                    }
                }
                else
                {
                    outputService.WriteStep("No available licenses - assign manually in Azure Portal", StepStatus.Warning);
                }

                // ============================================================
                // Phase 6: Foundry Roles (Optional)
                // ============================================================
                if (skipFoundry)
                {
                    outputService.WritePhase("PHASE 6: Foundry Role Assignment [SKIPPED]");
                    outputService.WriteStep("Skipped by --skip-foundry flag", StepStatus.Warning);
                }
                else if (!string.IsNullOrEmpty(foundryResource))
                {
                    outputService.WritePhase("PHASE 6: Foundry Role Assignment");

                    var roleResults = await foundryService.AssignFoundryRolesAsync(
                        output.Blueprint.ServicePrincipalId!, foundryResource, ct);

                    var successCount = roleResults.Count(r => r.Value);
                    outputService.WriteStep($"Roles assigned: {successCount}/{roleResults.Count}", 
                        successCount == roleResults.Count ? StepStatus.Success : StepStatus.Warning);
                }
                else
                {
                    outputService.WritePhase("PHASE 6: Foundry Role Assignment [SKIPPED]");
                    outputService.WriteStep("No Foundry resource ID provided", StepStatus.Warning);
                }

                // ============================================================
                // Phase 7: Graph Subscription (Optional)
                // ============================================================
                if (skipWebhook)
                {
                    outputService.WritePhase("PHASE 7: Graph Subscription [SKIPPED]");
                    outputService.WriteStep("Skipped by --skip-webhook flag", StepStatus.Warning);
                }
                else if (!string.IsNullOrEmpty(webhookUrl))
                {
                    outputService.WritePhase("PHASE 7: Graph Subscription");

                    try
                    {
                        var subscription = await subscriptionService.CreateSubscriptionAsync(
                            webhookUrl,
                            "/chats/getAllMessages",
                            "created",
                            TimeSpan.FromHours(1),
                            ct: ct);

                        output.Permissions.SubscriptionId = subscription.Id;
                        outputService.WriteStep($"Subscription created: {subscription.Id}", StepStatus.Success);
                        outputService.WriteStep($"  Expires: {subscription.ExpirationDateTime}");
                    }
                    catch (Exception ex)
                    {
                        outputService.WriteStep($"Could not create subscription: {ex.Message}", StepStatus.Warning);
                    }
                }
                else
                {
                    outputService.WritePhase("PHASE 7: Graph Subscription [SKIPPED]");
                    outputService.WriteStep("No webhook URL provided", StepStatus.Warning);
                }

                // ============================================================
                // Output
                // ============================================================
                outputService.DisplaySummary(output);
                await outputService.SaveOutputAsync(outputFile, output, ct);
                outputService.DisplayNextSteps(output);

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Setup failed");
                throw;
            }
        });

        return command;
    }
}
