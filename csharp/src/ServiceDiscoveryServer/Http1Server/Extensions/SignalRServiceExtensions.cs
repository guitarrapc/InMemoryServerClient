using ServiceDiscoveryServer.Http1Server.Hubs;

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
        app.MapHub<ServiceDiscoveryHub>("/discoveryHub");
        return app;
    }

    /// <summary>
    /// Configure CORS for SignalR
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="allowedOrigins">Allowed origins</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddServiceDiscoveryCors(this IServiceCollection services, string[] allowedOrigins)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.WithOrigins(allowedOrigins)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
            });
        });

        return services;
    }
}
