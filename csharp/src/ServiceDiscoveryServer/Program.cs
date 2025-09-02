using ServiceDiscoveryServer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add structured logging
builder.AddServiceDiscoveryLogging();

// Add ServiceDiscoveryServer services
builder.Services.AddServiceDiscoveryServer(builder.Configuration);

// Configure Kestrel for multiple ports
var serviceDiscoveryOptions = builder.Configuration.GetSection(ServiceDiscoveryOptions.SectionName).Get<ServiceDiscoveryOptions>()
    ?? new ServiceDiscoveryOptions();

var app = builder.Build();

// Configure ServiceDiscoveryServer application
app.ConfigureServiceDiscoveryServer(serviceDiscoveryOptions);

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ServiceDiscoveryServer starting up");

await app.RunAsync();
