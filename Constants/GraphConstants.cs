// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

namespace CitizenAgent.Setup.Cli.Constants;

/// <summary>
/// Constants for Microsoft Graph API
/// </summary>
public static class GraphConstants
{
    /// <summary>
    /// Microsoft Graph API base URL
    /// </summary>
    public const string GraphBaseUrl = "https://graph.microsoft.com";

    /// <summary>
    /// Microsoft Graph API Resource ID (for permissions)
    /// </summary>
    public const string GraphResourceId = "00000003-0000-0000-c000-000000000000";

    /// <summary>
    /// Azure AD token endpoint template
    /// </summary>
    public const string TokenEndpointTemplate = "https://login.microsoftonline.com/{0}/oauth2/v2.0/token";

    /// <summary>
    /// Microsoft Graph default scope
    /// </summary>
    public const string GraphDefaultScope = "https://graph.microsoft.com/.default";
}

/// <summary>
/// Microsoft Graph API permission IDs (Application permissions / Role)
/// </summary>
public static class GraphPermissions
{
    // User permissions
    public const string UserReadAll = "df021288-bdef-4463-88db-98f22de89214";

    // Chat permissions
    public const string ChatReadAll = "b633e1c5-b582-4048-a93e-9f11b44c7e96";
    public const string ChatReadWriteAll = "6e472fd1-ad78-48da-a0f0-97ab2c6b769e";
    public const string ChatCreate = "798ee544-9d2d-430c-a058-570e29e34338";

    // Calendar permissions
    public const string CalendarsRead = "ef54d2bf-783f-4e0f-bca1-3210c0444d99";
    public const string CalendarsReadWrite = "089fe4d0-434a-44c5-8827-41ba8a0b9f3b";

    // Mail permissions
    public const string MailRead = "9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30";
    public const string MailSend = "294ce7c9-31ba-490a-ad7d-97a7d075e4ed";

    // Presence permissions
    public const string PresenceReadAll = "7b2449af-6ccd-4f4d-9f78-e550c193f0d1";

    /// <summary>
    /// Gets all required blueprint permissions
    /// </summary>
    public static IReadOnlyList<PermissionDefinition> BlueprintPermissions => new List<PermissionDefinition>
    {
        new(UserReadAll, "Role", "User.Read.All"),
        new(ChatReadAll, "Role", "Chat.Read.All"),
        new(ChatReadWriteAll, "Role", "Chat.ReadWrite.All"),
        new(ChatCreate, "Role", "Chat.Create"),
        new(CalendarsRead, "Role", "Calendars.Read"),
        new(CalendarsReadWrite, "Role", "Calendars.ReadWrite"),
        new(MailRead, "Role", "Mail.Read"),
        new(MailSend, "Role", "Mail.Send"),
        new(PresenceReadAll, "Role", "Presence.Read.All")
    };
}

/// <summary>
/// Represents a permission definition
/// </summary>
public record PermissionDefinition(string Id, string Type, string Name);

/// <summary>
/// OAuth2 delegated scopes for Agent operations
/// </summary>
public static class DelegatedScopes
{
    public const string ChatReadWrite = "Chat.ReadWrite";
    public const string CalendarsReadWrite = "Calendars.ReadWrite";
    public const string MailReadWrite = "Mail.ReadWrite";
    public const string OnlineMeetingsReadWrite = "OnlineMeetings.ReadWrite";
    public const string PresenceRead = "Presence.Read";
    public const string UserRead = "User.Read";

    /// <summary>
    /// Default scopes for OAuth2 permission grant
    /// </summary>
    public static string DefaultGrantScopes =>
        $"{ChatReadWrite} {CalendarsReadWrite} {MailReadWrite} {OnlineMeetingsReadWrite} {PresenceRead} {UserRead}";
}

/// <summary>
/// License SKU IDs
/// </summary>
public static class LicenseSkuIds
{
    /// <summary>
    /// Microsoft 365 E5 Developer
    /// </summary>
    public const string M365E5Developer = "06ebc4ee-1bb5-47dd-8120-11324bc54e06";

    /// <summary>
    /// Microsoft Teams Enterprise
    /// </summary>
    public const string TeamsEnterprise = "57ff2da0-773e-42df-b2af-ffb7a2317929";
}

/// <summary>
/// Azure role names for Foundry
/// </summary>
public static class FoundryRoles
{
    public const string AzureAiDeveloper = "Azure AI Developer";
    public const string AzureAiUser = "Azure AI User";
    public const string AzureAiAdministrator = "Azure AI Administrator";
    public const string CognitiveServicesUser = "Cognitive Services User";

    /// <summary>
    /// Gets all required Foundry roles
    /// </summary>
    public static IReadOnlyList<string> RequiredRoles => new[]
    {
        AzureAiDeveloper,
        AzureAiUser,
        AzureAiAdministrator,
        CognitiveServicesUser
    };
}
