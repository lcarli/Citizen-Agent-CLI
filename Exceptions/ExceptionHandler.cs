// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

namespace CitizenAgent.Setup.Cli.Exceptions;

/// <summary>
/// Handles CitizenAgent exceptions and formats output
/// </summary>
public static class ExceptionHandler
{
    /// <summary>
    /// Handles an CitizenAgentException and outputs formatted error message
    /// </summary>
    public static void HandleCitizenAgentException(CitizenAgentException exception, string? logFilePath = null)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                           ERROR                              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        // Error type
        var errorType = exception switch
        {
            AuthenticationException => "Authentication Error",
            GraphApiException => "Graph API Error",
            ConfigurationException => "Configuration Error",
            ResourceNotFoundException => "Resource Not Found",
            InsufficientPermissionsException => "Insufficient Permissions",
            _ => "Error"
        };

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Type: {errorType}");
        Console.ResetColor();

        Console.WriteLine($"  Message: {exception.Message}");

        // Additional details for specific exceptions
        if (exception is GraphApiException graphEx)
        {
            Console.WriteLine($"  HTTP Status: {graphEx.HttpStatusCode}");
            if (!string.IsNullOrEmpty(graphEx.ErrorCode))
            {
                Console.WriteLine($"  Error Code: {graphEx.ErrorCode}");
            }
        }

        if (exception is InsufficientPermissionsException permEx)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Required Permissions:");
            Console.ResetColor();
            foreach (var perm in permEx.RequiredPermissions)
            {
                Console.WriteLine($"    • {perm}");
            }
        }

        // Suggestions
        if (exception.Suggestions.Any())
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Suggestions:");
            Console.ResetColor();
            foreach (var suggestion in exception.Suggestions)
            {
                Console.WriteLine($"    → {suggestion}");
            }
        }

        // Inner exception
        if (exception.InnerException != null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Details: {exception.InnerException.Message}");
            Console.ResetColor();
        }

        // Log file reference
        if (!string.IsNullOrEmpty(logFilePath))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  For more details, see: {logFilePath}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}
