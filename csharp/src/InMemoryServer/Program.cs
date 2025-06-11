using InMemoryServer;
using Shared;

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

// Build the app
var app = builder.Build();

// Configure the SignalR endpoint
app.MapHub<InMemoryHub>(Constants.HubRoute);

// Add a basic health check endpoint
app.MapGet("/health", () => "Healthy");

// Create directory for battle replays
Directory.CreateDirectory(Constants.BattleReplayDirectory);

// Start the server
Console.WriteLine($"InMemory Server starting on port {Constants.DefaultServerPort}...");
Console.WriteLine($"Hub available at {Constants.HubRoute}");

// Configure the app to listen on the specified port
app.Urls.Add($"http://0.0.0.0:{Constants.DefaultServerPort}");

// Run the app
await app.RunAsync();
