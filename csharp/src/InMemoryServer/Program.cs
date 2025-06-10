using InMemoryServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

var builder = new WebHostBuilder()
    .UseKestrel()
    .UseContentRoot(Directory.GetCurrentDirectory())
    .UseIISIntegration()
    .UseStartup<Startup>()
    .Build();

Console.WriteLine($"InMemory Server starting on port {Constants.DefaultServerPort}...");
Console.WriteLine($"Hub available at {Constants.HubRoute}");

await builder.RunAsync();

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Configure logging
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        // Add services to the container
        services.AddSignalR();
        services.AddSingleton<InMemoryState>();
        services.AddSingleton<GroupManager>();
    }
    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        // Configure the HTTP request pipeline
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == Constants.HubRoute)
            {
                var hubHandler = app.ApplicationServices.GetRequiredService<InMemoryHub>();
                // Here would be SignalR hub handling code
                // This is a simplified placeholder
            }
            else
            {
                await next();
            }
        });

        // Create directory for battle replays
        Directory.CreateDirectory(Constants.BattleReplayDirectory);
    }
}
