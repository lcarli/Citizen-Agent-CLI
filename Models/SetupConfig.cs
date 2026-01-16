// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace CitizenAgent.Setup.Cli.Models;

/// <summary>
/// Configuration for Agent 365 setup
/// </summary>
public class SetupConfig
{
    /// <summary>
    /// Azure AD tenant ID
    /// </summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Azure subscription ID
    /// </summary>
    [JsonPropertyName("subscriptionId")]
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// Azure subscription name (for display purposes)
    /// </summary>
    [JsonPropertyName("subscriptionName")]
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Blueprint App display name
    /// </summary>
    [JsonPropertyName("blueprintDisplayName")]
    public string BlueprintDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Agent Identity display name
    /// </summary>
    [JsonPropertyName("agentIdentityDisplayName")]
    public string AgentIdentityDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Agent User UPN
    /// </summary>
    [JsonPropertyName("agentUserUpn")]
    public string AgentUserUpn { get; set; } = string.Empty;

    /// <summary>
    /// Agent User display name
    /// </summary>
    [JsonPropertyName("agentUserDisplayName")]
    public string AgentUserDisplayName { get; set; } = "Agent User";

    /// <summary>
    /// Management App Client ID
    /// </summary>
    [JsonPropertyName("mgmtClientId")]
    public string MgmtClientId { get; set; } = string.Empty;

    /// <summary>
    /// Management App Client Secret
    /// </summary>
    [JsonPropertyName("mgmtClientSecret")]
    public string MgmtClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Azure AI Foundry Resource ID (optional)
    /// </summary>
    [JsonPropertyName("foundryResourceId")]
    public string? FoundryResourceId { get; set; }

    /// <summary>
    /// Webhook URL for Teams messages (optional)
    /// </summary>
    [JsonPropertyName("webhookUrl")]
    public string? WebhookUrl { get; set; }
}

/// <summary>
/// Output from Agent 365 setup
/// </summary>
public class SetupOutput
{
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("blueprint")]
    public BlueprintOutput Blueprint { get; set; } = new();

    [JsonPropertyName("agentIdentity")]
    public AgentIdentityOutput AgentIdentity { get; set; } = new();

    [JsonPropertyName("agentUser")]
    public AgentUserOutput AgentUser { get; set; } = new();

    [JsonPropertyName("permissions")]
    public PermissionsOutput Permissions { get; set; } = new();
}

/// <summary>
/// Blueprint output details
/// </summary>
public class BlueprintOutput
{
    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; set; }

    [JsonPropertyName("servicePrincipalId")]
    public string? ServicePrincipalId { get; set; }

    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; set; }
}

/// <summary>
/// Agent Identity output details
/// </summary>
public class AgentIdentityOutput
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

/// <summary>
/// Agent User output details
/// </summary>
public class AgentUserOutput
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("upn")]
    public string? Upn { get; set; }
}

/// <summary>
/// Permissions output details
/// </summary>
public class PermissionsOutput
{
    [JsonPropertyName("oauth2GrantId")]
    public string? OAuth2GrantId { get; set; }

    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }
}
