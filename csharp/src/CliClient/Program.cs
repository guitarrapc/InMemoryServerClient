using CliClient;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Create ConsoleApp with dependency injection
var app = ConsoleApp.Create()
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<InMemoryClient>();
    });

// Add commands
app.Add<InMemoryCommands>();

// Run the application
app.Run(args);
