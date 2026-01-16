// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

namespace CitizenAgent.Setup.Cli.Exceptions;

/// <summary>
/// Base exception for Agent 365 CLI operations
/// </summary>
public class CitizenAgentException : Exception
{
    /// <summary>
    /// Exit code for the CLI
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Suggestions for resolving the error
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; }

    /// <summary>
    /// Creates a new CitizenAgentException
    /// </summary>
    public CitizenAgentException(string message, int exitCode = 1, IEnumerable<string>? suggestions = null)
        : base(message)
    {
        ExitCode = exitCode;
        Suggestions = suggestions?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Creates a new CitizenAgentException with inner exception
    /// </summary>
    public CitizenAgentException(string message, Exception innerException, int exitCode = 1, IEnumerable<string>? suggestions = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
        Suggestions = suggestions?.ToList() ?? new List<string>();
    }
}

/// <summary>
/// Exception for authentication failures
/// </summary>
public class AuthenticationException : CitizenAgentException
{
    public AuthenticationException(string message, IEnumerable<string>? suggestions = null)
        : base(message, 10, suggestions ?? GetDefaultSuggestions())
    {
    }

    public AuthenticationException(string message, Exception innerException, IEnumerable<string>? suggestions = null)
        : base(message, innerException, 10, suggestions ?? GetDefaultSuggestions())
    {
    }

    private static IEnumerable<string> GetDefaultSuggestions() => new[]
    {
        "Verify your client credentials are correct",
        "Ensure the management app has required permissions",
        "Check if admin consent has been granted"
    };
}

/// <summary>
/// Exception for Graph API errors
/// </summary>
public class GraphApiException : CitizenAgentException
{
    public int HttpStatusCode { get; }
    public string? ErrorCode { get; }

    public GraphApiException(string message, int httpStatusCode, string? errorCode = null, IEnumerable<string>? suggestions = null)
        : base(message, 20, suggestions)
    {
        HttpStatusCode = httpStatusCode;
        ErrorCode = errorCode;
    }

    public GraphApiException(string message, int httpStatusCode, Exception innerException, string? errorCode = null, IEnumerable<string>? suggestions = null)
        : base(message, innerException, 20, suggestions)
    {
        HttpStatusCode = httpStatusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception for configuration errors
/// </summary>
public class ConfigurationException : CitizenAgentException
{
    public ConfigurationException(string message, IEnumerable<string>? suggestions = null)
        : base(message, 30, suggestions ?? GetDefaultSuggestions())
    {
    }

    private static IEnumerable<string> GetDefaultSuggestions() => new[]
    {
        "Verify all required parameters are provided",
        "Check the configuration file format",
        "Use 'a365-setup config init' to create a new configuration"
    };
}

/// <summary>
/// Exception for resource not found errors
/// </summary>
public class ResourceNotFoundException : CitizenAgentException
{
    public string ResourceType { get; }
    public string ResourceId { get; }

    public ResourceNotFoundException(string resourceType, string resourceId, IEnumerable<string>? suggestions = null)
        : base($"{resourceType} '{resourceId}' not found", 40, suggestions)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

/// <summary>
/// Exception for insufficient permissions
/// </summary>
public class InsufficientPermissionsException : CitizenAgentException
{
    public IReadOnlyList<string> RequiredPermissions { get; }

    public InsufficientPermissionsException(string message, IEnumerable<string> requiredPermissions, IEnumerable<string>? suggestions = null)
        : base(message, 50, suggestions ?? GetDefaultSuggestions(requiredPermissions))
    {
        RequiredPermissions = requiredPermissions.ToList();
    }

    private static IEnumerable<string> GetDefaultSuggestions(IEnumerable<string> permissions) => new[]
    {
        $"Required permissions: {string.Join(", ", permissions)}",
        "Grant admin consent in Azure Portal",
        "Verify you have Application Administrator or Global Administrator role"
    };
}
