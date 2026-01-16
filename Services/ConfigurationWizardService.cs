// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interactive wizard service for configuring CitizenAgent
/// </summary>
public interface IConfigurationWizardService
{
    /// <summary>
    /// Runs an interactive configuration wizard
    /// </summary>
    Task<SetupConfig?> RunWizardAsync(SetupConfig? existingConfig = null, CancellationToken ct = default);
}

public class ConfigurationWizardService : IConfigurationWizardService
{
    private readonly ILogger<ConfigurationWizardService> _logger;
    private readonly IAzureCliService _azureCliService;

    public ConfigurationWizardService(
        ILogger<ConfigurationWizardService> logger,
        IAzureCliService azureCliService)
    {
        _logger = logger;
        _azureCliService = azureCliService;
    }

    public async Task<SetupConfig?> RunWizardAsync(SetupConfig? existingConfig = null, CancellationToken ct = default)
    {
        try
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           Citizen Agent Configuration Wizard                  ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            if (existingConfig != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Found existing configuration. Press Enter to keep current values.");
                Console.ResetColor();
                Console.WriteLine();
            }

            // ═══════════════════════════════════════════════════════════════
            // Step 0: Verify Azure CLI Login
            // ═══════════════════════════════════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("═══ Step 0: Azure CLI Authentication ═══");
            Console.ResetColor();
            Console.WriteLine();

            Console.Write("Checking Azure CLI login status...");
            
            var isLoggedIn = await _azureCliService.IsLoggedInAsync(ct);
            
            if (!isLoggedIn)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Not logged in to Azure CLI");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Please run 'az login' first, then try again.");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("To login:");
                Console.WriteLine("  az login");
                Console.WriteLine();
                Console.WriteLine("To use a specific tenant:");
                Console.WriteLine("  az login --tenant <tenant-id>");
                Console.ResetColor();
                return null;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" ✓");
            Console.ResetColor();

            // Get Azure account info
            var accountInfo = await _azureCliService.GetCurrentAccountAsync(ct);
            
            if (accountInfo == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to retrieve Azure account information.");
                Console.ResetColor();
                return null;
            }

            // Display detected tenant and user
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Detected from Azure CLI:");
            Console.ResetColor();
            Console.WriteLine($"  Tenant ID:    {accountInfo.TenantId}");
            Console.WriteLine($"  User:         {accountInfo.User?.Name ?? "N/A"}");
            Console.WriteLine();

            // Extract domain from user email for UPN generation
            var userDomain = ExtractDomainFromEmail(accountInfo.User?.Name);

            // Use detected tenant
            var tenantId = existingConfig?.TenantId ?? accountInfo.TenantId;

            // ═══════════════════════════════════════════════════════════════
            // List and select subscription
            // ═══════════════════════════════════════════════════════════════
            Console.Write("Loading available subscriptions...");
            var subscriptions = await _azureCliService.ListSubscriptionsAsync(ct);
            Console.WriteLine(" ✓");
            Console.WriteLine();

            if (subscriptions.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No subscriptions found for this account.");
                Console.ResetColor();
                return null;
            }

            // Filter subscriptions by current tenant
            var tenantSubscriptions = subscriptions
                .Where(s => s.TenantId == tenantId)
                .ToList();

            if (tenantSubscriptions.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"No subscriptions found for tenant {tenantId}. Showing all subscriptions.");
                Console.ResetColor();
                tenantSubscriptions = subscriptions;
            }

            AzureSubscriptionInfo selectedSubscription;

            if (tenantSubscriptions.Count == 1)
            {
                selectedSubscription = tenantSubscriptions[0];
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Using subscription: {selectedSubscription.Name}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Available subscriptions:");
                Console.WriteLine();

                for (int i = 0; i < tenantSubscriptions.Count; i++)
                {
                    var sub = tenantSubscriptions[i];
                    var isDefault = sub.IsDefault ? " (default)" : "";
                    var stateColor = sub.State == "Enabled" ? ConsoleColor.Green : ConsoleColor.Yellow;
                    
                    Console.Write($"  [{i + 1}] ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(sub.Name);
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($" ({sub.Id})");
                    Console.ForegroundColor = stateColor;
                    Console.Write($" [{sub.State}]");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(isDefault);
                    Console.ResetColor();
                }

                Console.WriteLine();
                
                // Find default subscription index
                var defaultIndex = tenantSubscriptions.FindIndex(s => s.IsDefault);
                if (defaultIndex < 0) defaultIndex = 0;

                while (true)
                {
                    Console.Write($"Select subscription [1-{tenantSubscriptions.Count}] (default: {defaultIndex + 1}): ");
                    var input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input))
                    {
                        selectedSubscription = tenantSubscriptions[defaultIndex];
                        break;
                    }

                    if (int.TryParse(input, out var choice) && choice >= 1 && choice <= tenantSubscriptions.Count)
                    {
                        selectedSubscription = tenantSubscriptions[choice - 1];
                        break;
                    }

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Invalid selection. Please enter a number between 1 and {tenantSubscriptions.Count}.");
                    Console.ResetColor();
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Selected: {selectedSubscription.Name}");
                Console.ResetColor();
            }

            Console.WriteLine();

            // ═══════════════════════════════════════════════════════════════
            // Step 1: Management App Credentials
            // ═══════════════════════════════════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("═══ Step 1: Management App Credentials ═══");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("The Management App is used to create resources in your tenant.");
            Console.WriteLine("It needs Application permissions: Application.ReadWrite.All, User.ReadWrite.All");
            Console.WriteLine();

            var clientId = PromptForInput(
                "Management App Client ID",
                existingConfig?.MgmtClientId,
                "Client ID of your Management App Registration",
                ValidateGuid);

            if (string.IsNullOrEmpty(clientId))
            {
                Console.WriteLine("Client ID is required. Configuration cancelled.");
                return null;
            }

            var clientSecret = PromptForSecret(
                "Management App Client Secret",
                existingConfig?.MgmtClientSecret,
                "Client Secret of your Management App");

            if (string.IsNullOrEmpty(clientSecret))
            {
                Console.WriteLine("Client Secret is required. Configuration cancelled.");
                return null;
            }

            // ═══════════════════════════════════════════════════════════════
            // Step 2: Agent Configuration
            // ═══════════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("═══ Step 2: Agent Configuration ═══");
            Console.ResetColor();
            Console.WriteLine();

            var agentName = PromptForInput(
                "Agent Name",
                ExtractAgentName(existingConfig) ?? GenerateDefaultAgentName(accountInfo.User?.Name),
                "A friendly name for your agent (e.g., MyCompanyAgent)",
                ValidateAgentName);

            if (string.IsNullOrEmpty(agentName))
            {
                agentName = $"Agent{DateTime.Now:MMdd}";
            }

            // Generate derived names with detected domain
            var derivedNames = GenerateDerivedNames(agentName, userDomain);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Generated names (press Enter to accept, or type to customize):");
            Console.ResetColor();

            var blueprintName = PromptForInput(
                "Blueprint App Name",
                existingConfig?.BlueprintDisplayName ?? derivedNames.BlueprintName,
                "Display name for the Blueprint App Registration");

            var identityName = PromptForInput(
                "Agent Identity Name",
                existingConfig?.AgentIdentityDisplayName ?? derivedNames.IdentityName,
                "Display name for the Agent Identity (Service Principal)");

            var userUpn = PromptForInput(
                "Agent User UPN",
                existingConfig?.AgentUserUpn ?? derivedNames.UserUpn,
                "User Principal Name for the Agent User",
                ValidateUpn);

            var userDisplayName = PromptForInput(
                "Agent User Display Name",
                existingConfig?.AgentUserDisplayName ?? derivedNames.UserDisplayName,
                "Display name for the Agent User");

            // ═══════════════════════════════════════════════════════════════
            // Step 3: Optional Configuration
            // ═══════════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("═══ Step 3: Optional Configuration ═══");
            Console.ResetColor();
            Console.WriteLine();

            Console.Write("Configure Azure AI Foundry integration? (y/N): ");
            var configureFoundry = Console.ReadLine()?.Trim().ToLowerInvariant();
            string? foundryResourceId = null;

            if (configureFoundry == "y" || configureFoundry == "yes")
            {
                foundryResourceId = PromptForInput(
                    "Foundry Resource ID",
                    existingConfig?.FoundryResourceId,
                    "Azure Resource ID of your AI Foundry instance\n  Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.MachineLearningServices/workspaces/{name}");
            }

            Console.WriteLine();
            Console.Write("Configure Graph Webhook subscription? (y/N): ");
            var configureWebhook = Console.ReadLine()?.Trim().ToLowerInvariant();
            string? webhookUrl = null;

            if (configureWebhook == "y" || configureWebhook == "yes")
            {
                webhookUrl = PromptForInput(
                    "Webhook URL",
                    existingConfig?.WebhookUrl,
                    "URL where your agent receives Teams messages\n  Example: https://myagent.azurewebsites.net/api/messages",
                    ValidateUrl);
            }

            // ═══════════════════════════════════════════════════════════════
            // Step 4: Summary and Confirmation
            // ═══════════════════════════════════════════════════════════════
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("                    Configuration Summary");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"  Tenant ID              : {tenantId}");
            Console.WriteLine($"  Subscription           : {selectedSubscription.Name} ({selectedSubscription.Id})");
            Console.WriteLine($"  Management Client ID   : {clientId}");
            Console.WriteLine($"  Management Secret      : {MaskSecret(clientSecret)}");
            Console.WriteLine();
            Console.WriteLine($"  Blueprint Name         : {blueprintName}");
            Console.WriteLine($"  Identity Name          : {identityName}");
            Console.WriteLine($"  Agent User UPN         : {userUpn}");
            Console.WriteLine($"  Agent User Display     : {userDisplayName}");
            Console.WriteLine();
            Console.WriteLine($"  Foundry Resource ID    : {foundryResourceId ?? "(not configured)"}");
            Console.WriteLine($"  Webhook URL            : {webhookUrl ?? "(not configured)"}");
            Console.WriteLine();

            Console.Write("Save this configuration? (Y/n): ");
            var saveResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (saveResponse == "n" || saveResponse == "no")
            {
                Console.WriteLine("Configuration cancelled.");
                return null;
            }

            // Build config
            var config = new SetupConfig
            {
                TenantId = tenantId,
                SubscriptionId = selectedSubscription.Id,
                SubscriptionName = selectedSubscription.Name,
                MgmtClientId = clientId,
                MgmtClientSecret = clientSecret,
                BlueprintDisplayName = blueprintName ?? derivedNames.BlueprintName,
                AgentIdentityDisplayName = identityName ?? derivedNames.IdentityName,
                AgentUserUpn = userUpn ?? derivedNames.UserUpn,
                AgentUserDisplayName = userDisplayName ?? derivedNames.UserDisplayName,
                FoundryResourceId = foundryResourceId,
                WebhookUrl = webhookUrl
            };

            _logger.LogDebug("Configuration wizard completed successfully");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration wizard failed: {Message}", ex.Message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return null;
        }
    }

    private static string ExtractDomainFromEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            return "yourdomain.com";

        var parts = email.Split('@');
        return parts.Length == 2 ? parts[1] : "yourdomain.com";
    }

    private static string GenerateDefaultAgentName(string? userEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
            return $"Agent{DateTime.Now:MMdd}";

        // Extract username from email
        var username = userEmail.Split('@')[0];
        // Clean it up
        username = System.Text.RegularExpressions.Regex.Replace(username, @"[^a-zA-Z0-9]", "");
        
        if (string.IsNullOrEmpty(username))
            return $"Agent{DateTime.Now:MMdd}";

        return $"{username}Agent";
    }

    private static string? PromptForInput(
        string prompt,
        string? defaultValue,
        string? description = null,
        Func<string, (bool isValid, string error)>? validator = null)
    {
        if (!string.IsNullOrEmpty(description))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {description}");
            Console.ResetColor();
        }

        while (true)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Console.Write($"{prompt} [{defaultValue}]: ");
            }
            else
            {
                Console.Write($"{prompt}: ");
            }

            var input = Console.ReadLine()?.Trim();

            // Use default if empty
            if (string.IsNullOrWhiteSpace(input) && !string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            // Validate if validator provided
            if (validator != null)
            {
                var (isValid, error) = validator(input);
                if (!isValid)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ {error}");
                    Console.ResetColor();
                    continue;
                }
            }

            return input;
        }
    }

    private static string? PromptForSecret(string prompt, string? defaultValue, string? description = null)
    {
        if (!string.IsNullOrEmpty(description))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {description}");
            Console.ResetColor();
        }

        if (!string.IsNullOrEmpty(defaultValue))
        {
            Console.Write($"{prompt} [{MaskSecret(defaultValue)}]: ");
        }
        else
        {
            Console.Write($"{prompt}: ");
        }

        // Read secret with masking
        var secret = ReadSecretLine();

        if (string.IsNullOrWhiteSpace(secret) && !string.IsNullOrEmpty(defaultValue))
        {
            return defaultValue;
        }

        return secret;
    }

    private static string ReadSecretLine()
    {
        var secret = new System.Text.StringBuilder();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Backspace && secret.Length > 0)
            {
                secret.Remove(secret.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return secret.ToString();
    }

    private static string MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return "";
        if (secret.Length <= 8) return new string('*', secret.Length);
        return secret.Substring(0, 4) + new string('*', secret.Length - 8) + secret.Substring(secret.Length - 4);
    }

    private static string? ExtractAgentName(SetupConfig? config)
    {
        if (config == null) return null;

        // Try to extract from blueprint name
        if (!string.IsNullOrEmpty(config.BlueprintDisplayName))
        {
            var name = config.BlueprintDisplayName
                .Replace("-Blueprint", "")
                .Replace(" Blueprint", "")
                .Replace("Blueprint", "")
                .Trim();
            if (!string.IsNullOrEmpty(name)) return name;
        }

        return null;
    }

    private static DerivedNames GenerateDerivedNames(string agentName, string domain)
    {
        var cleanName = System.Text.RegularExpressions.Regex.Replace(agentName, @"[^a-zA-Z0-9]", "");
        var lowerName = cleanName.ToLowerInvariant();

        return new DerivedNames
        {
            BlueprintName = $"{agentName}-Blueprint",
            IdentityName = $"{agentName}-Identity",
            UserUpn = $"agent.{lowerName}@{domain}",
            UserDisplayName = $"{agentName} Agent"
        };
    }

    // Validators
    private static (bool isValid, string error) ValidateGuid(string input)
    {
        if (Guid.TryParse(input, out _))
            return (true, "");
        return (false, "Must be a valid GUID format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)");
    }

    private static (bool isValid, string error) ValidateAgentName(string input)
    {
        if (input.Length < 2 || input.Length > 50)
            return (false, "Agent name must be between 2-50 characters");

        if (!System.Text.RegularExpressions.Regex.IsMatch(input, @"^[a-zA-Z][a-zA-Z0-9\-_ ]*$"))
            return (false, "Agent name must start with a letter and contain only letters, numbers, spaces, hyphens, or underscores");

        return (true, "");
    }

    private static (bool isValid, string error) ValidateUpn(string input)
    {
        if (!input.Contains("@"))
            return (false, "UPN must contain @ symbol (e.g., agent@domain.com)");
        return (true, "");
    }

    private static (bool isValid, string error) ValidateUrl(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return (false, "Must be a valid URL");

        if (uri.Scheme != "http" && uri.Scheme != "https")
            return (false, "URL must use HTTP or HTTPS");

        return (true, "");
    }

    private class DerivedNames
    {
        public string BlueprintName { get; set; } = "";
        public string IdentityName { get; set; } = "";
        public string UserUpn { get; set; } = "";
        public string UserDisplayName { get; set; } = "";
    }
}
