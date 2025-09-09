using ServiceDiscoveryServer.Http1Server.Extensions;
using ServiceDiscoveryServer.Http2Server.Extensions;
using ServiceDiscoveryServer.Services.GameLift;

namespace ServiceDiscoveryServer.Extensions;

/// <summary>
/// Service collection extensions for ServiceDiscoveryServer
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add ServiceDiscoveryServer services
    /// </summary>
    /// <param name="builder">WebApplicationBuilder</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddServiceDiscoveryServer(this WebApplicationBuilder builder, IConfiguration configuration)
    {
        var services = builder.Services;

        // Add structured logging
        builder.AddServiceDiscoveryLogging();

        // Configuration
        services.Configure<ServiceDiscoveryOptions>(configuration.GetSection(ServiceDiscoveryOptions.SectionName));

        // Core services (business logic)
        services.AddSingleton<IBattleServerRegistry, BattleServerRegistry>();
        services.AddSingleton<ISessionManager, InmemorySessionManager>();
        services.AddSingleton<IGameLiftIntegration, GameLiftSessionManager>();
        services.AddSingleton<IBattleServerNotifier, BattleServerNotifier>();

        // Background services (lifecycle management)
        services.AddHostedService<SessionCleanupService>();
        services.AddHostedService<ServerHealthCheckService>();

        // HTTP/1 services (SignalR)
        services.AddServiceDiscoverySignalR();

        // HTTP/2 services (MagicOnion)
        builder.AddServiceDiscoveryMagicOnion();

        // Health checks
        services.AddHealthChecks();

        return services;
    }

    /// <summary>
    /// Configure ServiceDiscoveryServer application
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Web application</returns>
    public static WebApplication ConfigureServiceDiscoveryServer(this WebApplication app)
    {
        // Development environment configuration
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Routing
        app.UseRouting();

        // Health checks
        app.MapGet("/health", () => "Healthy");

        // SignalR hubs
        app.MapServiceDiscoverySignalR();

        // MagicOnion services
        app.MapServiceDiscoveryMagicOnion();

        return app;
    }

    /// <summary>
    /// Add structured logging configuration
    /// </summary>
    /// <param name="builder">Web application builder</param>
    /// <returns>Web application builder</returns>
    public static WebApplicationBuilder AddServiceDiscoveryLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = "custom-timestamp";
        });

        builder.Logging.AddConsoleFormatter<CustomTimestampConsoleFormatter, CustomTimestampConsoleFormatterOptions>();

        // Configure logging levels
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Extensions", LogLevel.Warning);
        builder.Logging.AddFilter("ServiceDiscoveryServer", LogLevel.Debug);

        return builder;
    }
}
