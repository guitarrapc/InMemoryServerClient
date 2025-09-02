using ServiceDiscoveryServer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add ServiceDiscoveryServer services
builder.AddServiceDiscoveryServer(builder.Configuration);

var app = builder.Build();

// Configure ServiceDiscoveryServer application
app.ConfigureServiceDiscoveryServer();

// Log startup information
Console.WriteLine($"ServiceDiscoveryServer Server starting...");

await app.RunAsync();
