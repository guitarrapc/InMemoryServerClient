using Shared.Constants;
using BattleLogic.Constans;
using BattleLogic.Infrastructures.BattleReplayWriter;
using InMemoryServer.Services;
using InMemoryServer.Http1Server;

namespace InMemoryServer;

public class Program
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
        builder.Services.AddMagicOnion();
        builder.Services.AddSingleton<InMemoryState>();
        builder.Services.AddSingleton<GroupManager>();
        builder.Services.AddSingleton<InMemoryHub>();

        // Register BattleReplayWriterFactory and options
        builder.Services.AddSingleton<BattleReplayWriterFactory>();

        // Build the app
        var app = builder.Build();

        // Configure the SignalR endpoint (HTTP/1)
        app.MapHub<InMemoryHub>(SystemDefines.HubRoute);

        // Configure MagicOnion endpoint (HTTP/2)
        app.MapMagicOnionService();

        // Add a basic health check endpoint
        app.MapGet("/health", () => "Healthy");

        // Create directory for battle replays
        Directory.CreateDirectory(BattleSystemDefines.BattleReplayDirectory);

        // Start the server
        Console.WriteLine($"InMemory Server starting...");
        Console.WriteLine($"HTTP/1 (SignalR) available on port {SystemDefines.DefaultServerPort}");
        Console.WriteLine($"HTTP/2 (MagicOnion) available on port {SystemDefines.DefaultHttp2ServerPort}");
        Console.WriteLine($"SignalR Hub available at {SystemDefines.HubRoute}");

        // Configure the app to listen on the specified ports (Kestrel configuration from appsettings.json will be used)
        // Note: URLs are configured in appsettings.json

        // Run the app
        await app.RunAsync();
    }
}
