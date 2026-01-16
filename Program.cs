// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Commands;
using CitizenAgent.Setup.Cli.Exceptions;
using CitizenAgent.Setup.Cli.Helpers;
using CitizenAgent.Setup.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Reflection;

namespace CitizenAgent.Setup.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Detect which command is being run for log file naming
        var commandName = DetectCommandName(args);
        var logFilePath = LogHelper.GetCommandLogPath(commandName);

        // Check if verbose flag is present to adjust logging level
        var isVerbose = args.Contains("--verbose") || args.Contains("-v");
        var logLevel = isVerbose ? LogLevel.Debug : LogLevel.Information;

        // Configure Microsoft.Extensions.Logging with clean console formatter
        var loggerFactory = LoggerFactoryHelper.CreateCleanLoggerFactory(logLevel, logFilePath);
        var startupLogger = loggerFactory.CreateLogger("Program");

        try
        {
            // Log startup info
            startupLogger.LogDebug("==========================================================");
            startupLogger.LogDebug("Citizen Agent Setup CLI - Command: {Command}", commandName);
            startupLogger.LogDebug("Version: {Version}", GetDisplayVersion());
            startupLogger.LogDebug("Log file: {LogFile}", logFilePath);
            startupLogger.LogDebug("Started at: {Time}", DateTime.Now);
            startupLogger.LogDebug("==========================================================");

            var version = GetDisplayVersion();

            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, logLevel, logFilePath);
            var serviceProvider = services.BuildServiceProvider();

            // Create root command
            var rootCommand = new RootCommand($"CitizenAgent Setup CLI v{version} – Create and configure Citizen Agent resources.");

            // Get services
            var graphService = serviceProvider.GetRequiredService<IGraphApiService>();
            var blueprintService = serviceProvider.GetRequiredService<IBlueprintService>();
            var agentIdentityService = serviceProvider.GetRequiredService<IAgentIdentityService>();
            var agentUserService = serviceProvider.GetRequiredService<IAgentUserService>();
            var permissionService = serviceProvider.GetRequiredService<IPermissionService>();
            var licenseService = serviceProvider.GetRequiredService<ILicenseService>();
            var foundryService = serviceProvider.GetRequiredService<IFoundryService>();
            var subscriptionService = serviceProvider.GetRequiredService<IGraphSubscriptionService>();
            var configService = serviceProvider.GetRequiredService<IConfigService>();
            var outputService = serviceProvider.GetRequiredService<IOutputService>();

            // Get loggers
            var setupLogger = serviceProvider.GetRequiredService<ILogger<SetupCommand>>();
            var blueprintLogger = serviceProvider.GetRequiredService<ILogger<BlueprintCommand>>();
            var identityLogger = serviceProvider.GetRequiredService<ILogger<IdentityCommand>>();
            var userLogger = serviceProvider.GetRequiredService<ILogger<UserCommand>>();
            var permissionLogger = serviceProvider.GetRequiredService<ILogger<PermissionsCommand>>();
            var azureCliService = serviceProvider.GetRequiredService<IAzureCliService>();

            // Add commands
            rootCommand.AddCommand(SetupCommand.CreateCommand(
                setupLogger, configService, graphService, blueprintService, 
                agentIdentityService, agentUserService, permissionService, 
                licenseService, foundryService, subscriptionService, outputService, azureCliService));

            rootCommand.AddCommand(BlueprintCommand.CreateCommand(
                blueprintLogger, configService, graphService, blueprintService, outputService));

            rootCommand.AddCommand(IdentityCommand.CreateCommand(
                identityLogger, configService, graphService, agentIdentityService, outputService));

            rootCommand.AddCommand(UserCommand.CreateCommand(
                userLogger, configService, graphService, agentUserService, outputService));

            rootCommand.AddCommand(PermissionsCommand.CreateCommand(
                permissionLogger, configService, graphService, permissionService, outputService));

            var wizardService = serviceProvider.GetRequiredService<IConfigurationWizardService>();
            rootCommand.AddCommand(ConfigCommand.CreateCommand(
                serviceProvider.GetRequiredService<ILogger<ConfigCommand>>(), configService, wizardService));

            // Build with middleware for global exception handling
            var builder = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseExceptionHandler((exception, context) =>
                {
                    if (exception is CitizenAgentException myEx)
                    {
                        ExceptionHandler.HandleCitizenAgentException(myEx, logFilePath);
                        context.ExitCode = myEx.ExitCode;
                    }
                    else
                    {
                        startupLogger.LogCritical(exception, "Application terminated unexpectedly");
                        Console.Error.WriteLine("Unexpected error occurred. This may be a bug in the CLI.");
                        Console.Error.WriteLine();
                        if (!string.IsNullOrEmpty(logFilePath))
                        {
                            Console.Error.WriteLine($"For more details, see the log file at: {logFilePath}");
                            Console.Error.WriteLine();
                        }
                        context.ExitCode = 1;
                    }
                });

            var parser = builder.Build();
            return await parser.InvokeAsync(args);
        }
        finally
        {
            Console.ResetColor();
            loggerFactory.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services, LogLevel minimumLevel, string? logFilePath)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(minimumLevel);

            builder.AddConsole(options =>
            {
                options.FormatterName = "clean";
            });
            builder.AddConsoleFormatter<CleanConsoleFormatter, Microsoft.Extensions.Logging.Console.SimpleConsoleFormatterOptions>();

            if (!string.IsNullOrEmpty(logFilePath))
            {
                builder.Services.AddSingleton<ILoggerProvider>(provider =>
                    new FileLoggerProvider(logFilePath));
            }
        });

        // Add core services
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IAzureCliService, AzureCliService>();
        services.AddSingleton<IConfigurationWizardService, ConfigurationWizardService>();
        services.AddSingleton<IGraphApiService, GraphApiService>();
        services.AddSingleton<IBlueprintService, BlueprintService>();
        services.AddSingleton<IAgentIdentityService, AgentIdentityService>();
        services.AddSingleton<IAgentUserService, AgentUserService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IFoundryService, FoundryService>();
        services.AddSingleton<IGraphSubscriptionService, GraphSubscriptionService>();
        services.AddSingleton<IOutputService, OutputService>();
        services.AddSingleton<HttpClient>();
    }

    public static string GetDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return infoVer ?? asm.GetName().Version?.ToString() ?? "1.0.0";
    }

    private static string DetectCommandName(string[] args)
    {
        if (args.Length == 0)
            return "default";

        var command = args.FirstOrDefault(arg => !arg.StartsWith("-"));

        if (string.IsNullOrWhiteSpace(command))
            return "default";

        return command.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
    }
}
