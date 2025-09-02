using ServiceDiscoveryServer.Http1Server.Hubs;
using Shared.Constants;

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
    /// <param name="options">ServiceDiscovery options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddServiceDiscoverySignalR(this IServiceCollection services, ServiceDiscoveryOptions options)
    {
        services.AddSignalR(hubOptions =>
        {
            hubOptions.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
            hubOptions.StreamBufferCapacity = 10;
            hubOptions.EnableDetailedErrors = true;
        })
        .AddJsonProtocol(jsonOptions =>
        {
            jsonOptions.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            jsonOptions.PayloadSerializerOptions.WriteIndented = false;
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
