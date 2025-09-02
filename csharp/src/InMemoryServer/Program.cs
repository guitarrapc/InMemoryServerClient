using Shared.Constants;
using BattleLogic.Constants;
using BattleLogic.Infrastructures.BattleReplayWriter;
using InMemoryServer.Services;
using InMemoryServer.Http1Server;
using InMemoryServer.Extensions;

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
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
            options.StreamBufferCapacity = 50;
        });
        builder.Services.AddMagicOnion();
        builder.Services.AddSingleton<InMemoryState>();
        builder.Services.AddSingleton<ConnectionManager>();

        // Health checks
        builder.Services.AddHealthChecks();

        // Choose Group Manager implementation
        // Use Actor Model Pattern (Recommended)
        builder.Services.AddActorGroupManager();

        builder.Services.AddSingleton<SignalRBattleHub>();
        builder.Services.AddSingleton<MagicOnionGroupService>();
        builder.Services.AddSingleton<CrossProtocolNotificationService>();
        builder.Services.AddSingleton<BattleCompletionService>();

        // Register BattleReplayWriterFactory and options
        builder.Services.AddSingleton<BattleReplayWriterFactory>();

        // Build the app
        var app = builder.Build();

        // Add a basic health check endpoint
        app.MapGet("/health", () => "Healthy");

        // Configure the SignalR endpoint (HTTP/1)
        app.MapHub<SignalRBattleHub>(SystemDefines.BattleHubRoute);

        // Configure MagicOnion endpoint (HTTP/2)
        app.MapMagicOnionService();

        // Create directory for battle replays
        Directory.CreateDirectory(BattleSystemDefines.BattleReplayDirectory);

        // Start the server
        Console.WriteLine($"InMemory Server starting...");
        Console.WriteLine($"SignalR Hub available at {SystemDefines.BattleHubRoute}");

        // Configure the app to listen on the specified ports (Kestrel configuration from appsettings.json will be used)
        // Note: URLs are configured in appsettings.json

        // Run the app
        await app.RunAsync();
    }
}
