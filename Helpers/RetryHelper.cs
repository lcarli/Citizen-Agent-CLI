// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

namespace CitizenAgent.Setup.Cli.Helpers;

/// <summary>
/// Helper for retry operations with interactive console feedback
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Executes an async operation with retries and console feedback
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxAttempts = 5,
        int delaySeconds = 3,
        CancellationToken ct = default)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    // Show retry message with countdown
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"  ⏳ {operationName} - Attempt {attempt}/{maxAttempts} failed. Waiting ");
                    
                    // Countdown timer
                    for (int i = delaySeconds; i > 0; i--)
                    {
                        ct.ThrowIfCancellationRequested();
                        Console.Write($"{i}...");
                        await Task.Delay(1000, ct);
                    }
                    
                    Console.WriteLine(" Retrying...");
                    Console.ResetColor();
                }
            }
        }

        throw new InvalidOperationException(
            $"{operationName} failed after {maxAttempts} attempts: {lastException?.Message}",
            lastException);
    }

    /// <summary>
    /// Waits for a condition to be true with console feedback
    /// </summary>
    public static async Task<T?> WaitForConditionAsync<T>(
        Func<Task<T?>> checkCondition,
        Func<T?, bool> isReady,
        string waitingForMessage,
        int maxAttempts = 10,
        int delaySeconds = 2,
        CancellationToken ct = default) where T : class
    {
        Console.Write($"  ⏳ Waiting for {waitingForMessage}");

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var result = await checkCondition();
            
            if (isReady(result))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" ✓");
                Console.ResetColor();
                return result;
            }

            // Show progress dots
            Console.Write(".");
            await Task.Delay(delaySeconds * 1000, ct);
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(" ✗ (timeout)");
        Console.ResetColor();
        
        return null;
    }

    /// <summary>
    /// Waits for Azure AD propagation after creating a resource
    /// </summary>
    public static async Task WaitForPropagationAsync(
        string resourceType,
        int delaySeconds = 5,
        CancellationToken ct = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  ⏳ Waiting for {resourceType} propagation");
        
        for (int i = 0; i < delaySeconds; i++)
        {
            ct.ThrowIfCancellationRequested();
            Console.Write(".");
            await Task.Delay(1000, ct);
        }
        
        Console.WriteLine(" ✓");
        Console.ResetColor();
    }
}
