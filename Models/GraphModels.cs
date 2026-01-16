// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace CitizenAgent.Setup.Cli.Models;

/// <summary>
/// Represents a Microsoft Graph API application
/// </summary>
public class GraphApplication
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("requiredResourceAccess")]
    public List<RequiredResourceAccess>? RequiredResourceAccess { get; set; }
}

/// <summary>
/// Represents required resource access configuration
/// </summary>
public class RequiredResourceAccess
{
    [JsonPropertyName("resourceAppId")]
    public string? ResourceAppId { get; set; }

    [JsonPropertyName("resourceAccess")]
    public List<ResourceAccess>? ResourceAccess { get; set; }
}

/// <summary>
/// Represents a resource access entry
/// </summary>
public class ResourceAccess
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

/// <summary>
/// Represents a service principal
/// </summary>
public class ServicePrincipal
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("servicePrincipalType")]
    public string? ServicePrincipalType { get; set; }
}

/// <summary>
/// Represents a Graph API user
/// </summary>
public class GraphUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("mailNickname")]
    public string? MailNickname { get; set; }

    [JsonPropertyName("accountEnabled")]
    public bool AccountEnabled { get; set; }
}

/// <summary>
/// Represents a password credential (client secret)
/// </summary>
public class PasswordCredential
{
    [JsonPropertyName("secretText")]
    public string? SecretText { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("endDateTime")]
    public DateTime? EndDateTime { get; set; }

    [JsonPropertyName("keyId")]
    public string? KeyId { get; set; }
}

/// <summary>
/// Represents an OAuth2 permission grant
/// </summary>
public class OAuth2PermissionGrant
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    [JsonPropertyName("consentType")]
    public string? ConsentType { get; set; }

    [JsonPropertyName("principalId")]
    public string? PrincipalId { get; set; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

/// <summary>
/// Represents a Graph subscription
/// </summary>
public class GraphSubscription
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    [JsonPropertyName("notificationUrl")]
    public string? NotificationUrl { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("expirationDateTime")]
    public DateTime? ExpirationDateTime { get; set; }

    [JsonPropertyName("clientState")]
    public string? ClientState { get; set; }
}

/// <summary>
/// Represents a subscribed SKU (license)
/// </summary>
public class SubscribedSku
{
    [JsonPropertyName("skuId")]
    public string? SkuId { get; set; }

    [JsonPropertyName("skuPartNumber")]
    public string? SkuPartNumber { get; set; }

    [JsonPropertyName("consumedUnits")]
    public int ConsumedUnits { get; set; }

    [JsonPropertyName("prepaidUnits")]
    public PrepaidUnits? PrepaidUnits { get; set; }
}

/// <summary>
/// Represents prepaid license units
/// </summary>
public class PrepaidUnits
{
    [JsonPropertyName("enabled")]
    public int Enabled { get; set; }

    [JsonPropertyName("suspended")]
    public int Suspended { get; set; }

    [JsonPropertyName("warning")]
    public int Warning { get; set; }
}

/// <summary>
/// Generic Graph API response wrapper
/// </summary>
/// <typeparam name="T">Type of the value</typeparam>
public class GraphResponse<T>
{
    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }
}

/// <summary>
/// Result from Graph API operations
/// </summary>
public record GraphOperationResult
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public string ReasonPhrase { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public System.Text.Json.JsonDocument? Json { get; init; }
}
