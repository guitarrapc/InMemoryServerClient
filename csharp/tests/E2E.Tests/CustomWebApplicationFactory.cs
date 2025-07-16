using InMemoryServer.Http1Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shared.Constants;

namespace E2E.Tests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseKestrel(options =>
        {
            // Configure to support both HTTP/1 and HTTP/2 on the same port
            options.ConfigureEndpointDefaults(endpoints =>
            {
                endpoints.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
            });
        });

        builder.ConfigureServices(services =>
        {
            // Add the same services as in Program.RunServerAsync
            services.AddSignalR();
            services.AddMagicOnion();

            // Core state and services
            services.AddSingleton<InMemoryServer.InMemoryState>();
            services.AddSingleton<InMemoryServer.Services.ConnectionManager>();
            services.AddSingleton<InMemoryServer.Services.GroupManager>();
            services.AddSingleton<InMemoryServer.Services.MagicOnionGroupService>();
            services.AddSingleton<InMemoryServer.Services.CrossProtocolNotificationService>();
            services.AddSingleton<BattleLogic.Infrastructures.BattleReplayWriter.BattleReplayWriterFactory>();

            // Hubs and services (order matters for dependency injection)
            services.AddSingleton<SignalRBattleHub>();
            services.AddSingleton<InMemoryServer.Http2Server.MagicOnionBattleHub>();

            // Configure test logging
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<SignalRBattleHub>(SystemDefines.HubRoute);
                endpoints.MapMagicOnionService();
                endpoints.MapGet("/health", () => "Healthy");
            });
        });

        builder.UseEnvironment("Testing");

        // Create directory for battle replays
        Directory.CreateDirectory(BattleLogic.Constans.BattleSystemDefines.BattleReplayDirectory);
    }
}
