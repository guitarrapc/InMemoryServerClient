using ServiceDiscoveryServer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add structured logging
builder.AddServiceDiscoveryLogging();

// Add ServiceDiscoveryServer services
builder.Services.AddServiceDiscoveryServer(builder.Configuration);

// Configure Kestrel for multiple ports
var serviceDiscoveryOptions = builder.Configuration.GetSection(ServiceDiscoveryOptions.SectionName).Get<ServiceDiscoveryOptions>()
    ?? new ServiceDiscoveryOptions();

builder.WebHost.ConfigureKestrel(options =>
{
    // SignalR HTTP/1.1 endpoint
    options.ListenAnyIP(serviceDiscoveryOptions.Server.SignalRPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
    });

    // MagicOnion HTTP/2 endpoint
    options.ListenAnyIP(serviceDiscoveryOptions.Server.MagicOnionPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    // Health check HTTP/1.1 endpoint
    options.ListenAnyIP(serviceDiscoveryOptions.Server.HealthCheckPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
    });
});

var app = builder.Build();

// Configure ServiceDiscoveryServer application
app.ConfigureServiceDiscoveryServer(serviceDiscoveryOptions);

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ServiceDiscoveryServer starting up");
logger.LogInformation("SignalR endpoint: http://localhost:{SignalRPort}/discoveryHub", serviceDiscoveryOptions.Server.SignalRPort);
logger.LogInformation("MagicOnion endpoint: http://localhost:{MagicOnionPort}", serviceDiscoveryOptions.Server.MagicOnionPort);
logger.LogInformation("Health check endpoint: http://localhost:{HealthCheckPort}/health", serviceDiscoveryOptions.Server.HealthCheckPort);

await app.RunAsync();
