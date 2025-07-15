using CliClient;
using CliClient.Clients;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<MultiBattleClientManager>();
    });

// Add commands
app.Add<ConsoleCommand>();

// Run the application
app.Run(args);
