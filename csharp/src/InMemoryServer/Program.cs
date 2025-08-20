using Shared.Constants;
using Shared.Contracts;
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

        // Configure GameLift services
        builder.ConfigureGameLiftServices();

        // Add services to the container
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
            options.StreamBufferCapacity = 50;
        });
        builder.Services.AddMagicOnion();
        builder.Services.AddSingleton<InMemoryState>();
        builder.Services.AddSingleton<ConnectionManager>();

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

        // Get GameLift provider from DI (singleton)
        var gameServerProvider = app.Services.GetRequiredService<IGameServerProvider>();

        var initResult = await gameServerProvider.InitializeAsync();
        if (!initResult)
        {
            app.Logger.LogError("Failed to initialize GameLift provider");
            return;
        }

        // Notify GameLift that the process is ready (for Anywhere mode)
        var processParameters = new GameServerProcessParameters(
            Port: 5000, // HTTP/1 port for SignalR
            LogPaths: []);

        var processReadyResult = await gameServerProvider.ProcessReadyAsync(processParameters);
        if (!processReadyResult)
        {
            app.Logger.LogWarning("Failed to notify GameLift that process is ready (this is normal for Direct mode)");
        }

        // Configure the SignalR endpoint (HTTP/1)
        app.MapHub<SignalRBattleHub>(SystemDefines.HubRoute);

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

        // Configure graceful shutdown
        var applicationLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        applicationLifetime.ApplicationStopping.Register(() =>
        {
            app.Logger.LogInformation("Application stopping, shutting down GameLift provider...");
            _ = Task.Run(async () =>
            {
                try
                {
                    // Get provider from DI since gameServerProvider variable might be out of scope
                    var provider = app.Services.GetRequiredService<IGameServerProvider>();
                    await provider.ShutdownAsync();
                }
                catch (Exception ex)
                {
                    app.Logger.LogWarning(ex, "Error during GameLift provider shutdown");
                }
            });
        });

        // Configure the app to listen on the specified ports (Kestrel configuration from appsettings.json will be used)
        // Note: URLs are configured in appsettings.json

        // Run the app
        await app.RunAsync();
    }
}
