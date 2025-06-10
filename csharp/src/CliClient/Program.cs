using CliClient;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Set up dependency injection
var host = Host.CreateDefaultBuilder()
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureServices((hostContext, services) =>
    {
        // Register client
        services.AddSingleton<InMemoryClient>();

        // Register commands
        services.AddTransient<CliClient.InMemoryCommands>();
    })
    .Build();

// Welcome message
Console.WriteLine("InMemory CLI Client");
Console.WriteLine("==================");
Console.WriteLine();

// Get command processor
var commands = host.Services.GetRequiredService<CliClient.InMemoryCommands>();

// Check for command line arguments
if (args.Length > 0)
{
    // Command mode
    try
    {
        var app = new CommandLineApplication(false);
        commands.Configure(app);
        app.Execute(args);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error running command: {ex.Message}");
        Environment.ExitCode = 1;
    }
}
else
{
    // Interactive mode
    try
    {
        await commands.InteractiveAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error in interactive mode: {ex.Message}");
        Environment.ExitCode = 1;
    }
}
