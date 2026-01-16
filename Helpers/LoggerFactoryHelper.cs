// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace CitizenAgent.Setup.Cli.Helpers;

/// <summary>
/// Helper for creating logger factories
/// </summary>
public static class LoggerFactoryHelper
{
    /// <summary>
    /// Creates a logger factory with clean console formatting and optional file logging
    /// </summary>
    public static ILoggerFactory CreateCleanLoggerFactory(LogLevel minimumLevel, string? logFilePath = null)
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(minimumLevel);

            // Add clean console formatter
            builder.AddConsoleFormatter<CleanConsoleFormatter, SimpleConsoleFormatterOptions>();
            builder.AddConsole(options =>
            {
                options.FormatterName = "clean";
            });

            // Add file logging if path provided
            if (!string.IsNullOrEmpty(logFilePath))
            {
                builder.Services.AddSingleton<ILoggerProvider>(provider =>
                    new FileLoggerProvider(logFilePath));
            }
        });

        return factory;
    }
}

/// <summary>
/// Clean console formatter that produces minimal output
/// </summary>
public class CleanConsoleFormatter : ConsoleFormatter
{
    public CleanConsoleFormatter() : base("clean")
    {
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // Color based on log level
        var color = logEntry.LogLevel switch
        {
            LogLevel.Critical => ConsoleColor.Red,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Trace => ConsoleColor.DarkGray,
            _ => ConsoleColor.White
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        textWriter.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}

/// <summary>
/// File logger provider
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use FileShare.ReadWrite to allow multiple processes
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _writer, _lock);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}

/// <summary>
/// File logger implementation
/// </summary>
public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly StreamWriter _writer;
    private readonly object _lock;

    public FileLogger(string categoryName, StreamWriter writer, object lockObject)
    {
        _categoryName = categoryName;
        _writer = writer;
        _lock = lockObject;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel,-11}] [{_categoryName}] {message}";

        lock (_lock)
        {
            _writer.WriteLine(logLine);
            
            if (exception != null)
            {
                _writer.WriteLine(exception.ToString());
            }
        }
    }
}

/// <summary>
/// Helper for log file paths
/// </summary>
public static class LogHelper
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CitizenAgent",
        "Setup",
        "logs");

    /// <summary>
    /// Gets the log file path for a command
    /// </summary>
    public static string GetCommandLogPath(string commandName)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(LogDirectory, $"{commandName}-{timestamp}.log");
    }
}
