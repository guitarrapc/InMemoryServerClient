using Shared.Constants;
using BattleLogic.Constants;
using BattleLogic.Infrastructures.BattleReplayWriter;
using InMemoryServer.Services;
using InMemoryServer.Http1Server;
using InMemoryServer.Extensions;
using Shared.Infrastructures.Loggers;

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
            options.IncludeScopes = false; // omit scopes for simplicity
        });

        // Add services to the container
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
            options.StreamBufferCapacity = 50;
        });
        builder.Services.AddMagicOnion();
        builder.Services.AddGrpc(); // for gRPC-web

        // Add CORS for WasmClient
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("WasmClientPolicy", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding"); // Required for SignalR
            });
        });

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

        // Configure CORS (must be before MapHub and MapMagicOnionService)
        app.UseCors("WasmClientPolicy");

        // Configure gRPC-Web (required for browser clients)
        app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

        // Configure the SignalR endpoint (HTTP/1)
        app.MapHub<SignalRBattleHub>(SystemDefines.HubRoute);

        // Configure MagicOnion endpoint (HTTP/2)
        app.MapMagicOnionService()
            .EnableGrpcWeb()
            .RequireCors("WasmClientPolicy");

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
