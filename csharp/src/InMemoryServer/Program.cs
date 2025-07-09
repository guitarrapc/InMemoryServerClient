using Shared.Constants;
using BattleLogic.Constans;
using BattleLogic.Infrastructures.BattleReplayWriter;

namespace InMemoryServer;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        // Create a WebApplication builder
        var builder = WebApplication.CreateBuilder(args);

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = "customTimestamp";
        });
        // カスタムのタイムスタンプフォーマッタを登録
        builder.Logging.AddConsoleFormatter<CustomTimestampConsoleFormatter, CustomTimestampConsoleFormatterOptions>(options =>
        {
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
            options.IncludeScopes = true;
        });

        // Add services to the container
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<InMemoryState>();
        builder.Services.AddSingleton<GroupManager>();
        builder.Services.AddSingleton<InMemoryHub>();

        // Register BattleReplayWriterFactory and options
        builder.Services.AddSingleton<BattleReplayWriterFactory>();

        // Build the app
        var app = builder.Build();

        // Configure the SignalR endpoint
        app.MapHub<InMemoryHub>(SystemDefines.HubRoute);

        // Add a basic health check endpoint
        app.MapGet("/health", () => "Healthy");

        // Create directory for battle replays
        Directory.CreateDirectory(BattleSystemDefines.BattleReplayDirectory);

        // Start the server
        Console.WriteLine($"InMemory Server starting on port {SystemDefines.DefaultServerPort}...");
        Console.WriteLine($"Hub available at {SystemDefines.HubRoute}");

        // Configure the app to listen on the specified port
        app.Urls.Add($"http://0.0.0.0:{SystemDefines.DefaultServerPort}");

        // Run the app
        await app.RunAsync();
    }
}
