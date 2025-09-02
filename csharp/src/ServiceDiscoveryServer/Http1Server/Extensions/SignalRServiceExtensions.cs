using ServiceDiscoveryServer.Http1Server.Hubs;
using Shared.BattleServer.Constants;

namespace ServiceDiscoveryServer.Http1Server.Extensions;

/// <summary>
/// SignalR service configuration extensions
/// </summary>
public static class SignalRServiceExtensions
{
    /// <summary>
    /// Add SignalR services for ServiceDiscovery
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddServiceDiscoverySignalR(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
            options.StreamBufferCapacity = 10;
            options.EnableDetailedErrors = true;
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PayloadSerializerOptions.WriteIndented = false;
        });

        return services;
    }

    /// <summary>
    /// Configure SignalR endpoints
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Web application</returns>
    public static WebApplication MapServiceDiscoverySignalR(this WebApplication app)
    {
        app.MapHub<ServiceDiscoveryHub>(SystemDefines.ServiceDiscoveryHubRoute);
        return app;
    }
}
