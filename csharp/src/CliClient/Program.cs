using CliClient;
using CliClient.Clients;
using CliClient.GameLift;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.GameLift;

// Create ConsoleApp with dependency injection
var app = ConsoleApp.Create()
    .ConfigureDefaultConfiguration()
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
    .ConfigureServices((configuration, services) =>
    {
        // Configure GameLift options
        services.Configure<GameLiftOptions>(configuration.GetSection("GameLift"));

        // Configure GameLift client services
        services.ConfigureGameLiftClientServices();

        services.AddSingleton<MultiBattleClientManager>();
    });

// Add commands
app.Add<ConsoleCommand>();

// Run the application
app.Run(args);
