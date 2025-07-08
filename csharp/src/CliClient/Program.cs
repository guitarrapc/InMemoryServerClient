using CliClient;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Models;

// Create ConsoleApp with dependency injection
var app = ConsoleApp.Create()
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole(options =>
        {
            options.FormatterName = "customTimestamp";
        });

        // カスタムのタイムスタンプフォーマッタを登録
        logging.AddConsoleFormatter<CustomTimestampConsoleFormatter, CustomTimestampConsoleFormatterOptions>(options =>
        {
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
            options.IncludeScopes = true;
        });
    })
    .ConfigureServices((context, services) =>
    {
        // Protocol-independent client registration
        services.AddSingleton<IBattleClient>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

            // Read connection type from configuration with fallback to SignalR
            var connectionTypeString = "SignalR";
            var connectionType = Enum.Parse<ConnectionType>(connectionTypeString);

            return BattleClientFactory.Create(connectionType, loggerFactory);
        });

        services.AddSingleton<MultiClientManager>();
    });

// Add commands
app.Add<ConsoleCommand>();

// Run the application
app.Run(args);
