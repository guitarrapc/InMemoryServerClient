using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace E2ETests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Add the same services as in Program.RunServerAsync
            services.AddSignalR();
            services.AddSingleton<InMemoryServer.InMemoryState>();
            services.AddSingleton<InMemoryServer.GroupManager>();
            services.AddSingleton<InMemoryServer.InMemoryHub>();
            services.AddSingleton<BattleLogic.Infrastructures.BattleReplayWriter.BattleReplayWriterFactory>();
            
            // Configure test logging
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
        });

        builder.UseEnvironment("Testing");
        
        // Create directory for battle replays
        Directory.CreateDirectory(BattleLogic.Constans.BattleSystemDefines.BattleReplayDirectory);
    }
}
