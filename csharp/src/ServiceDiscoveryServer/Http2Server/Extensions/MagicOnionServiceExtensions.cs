using MagicOnion.Server;

namespace ServiceDiscoveryServer.Http2Server.Extensions;

/// <summary>
/// MagicOnion service configuration extensions
/// </summary>
public static class MagicOnionServiceExtensions
{
    /// <summary>
    /// Add MagicOnion services for ServiceDiscovery
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="options">ServiceDiscovery options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddServiceDiscoveryMagicOnion(this IServiceCollection services, ServiceDiscoveryOptions options)
    {
        services.AddMagicOnion(magicOnionOptions =>
        {
            magicOnionOptions.IsReturnExceptionStackTraceInErrorDetail = true;
        });

        return services;
    }

    /// <summary>
    /// Configure MagicOnion endpoints
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Web application</returns>
    public static WebApplication MapServiceDiscoveryMagicOnion(this WebApplication app)
    {
        app.MapMagicOnionService();
        return app;
    }
}

/// <summary>
/// MagicOnion logging filter for ServiceDiscovery
/// </summary>
public sealed class ServiceDiscoveryLoggingFilter : MagicOnionFilterAttribute
{
    private readonly ILogger<ServiceDiscoveryLoggingFilter> _logger;

    public ServiceDiscoveryLoggingFilter(ILogger<ServiceDiscoveryLoggingFilter> logger)
    {
        _logger = logger;
    }

    public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var serviceName = context.ServiceType.Name;
        var methodName = context.MethodInfo.Name;

        try
        {
            _logger.LogDebug("MagicOnion method start: {ServiceName}.{MethodName}", serviceName, methodName);

            await next(context);

            _logger.LogDebug("MagicOnion method completed: {ServiceName}.{MethodName} in {ElapsedMs}ms",
                serviceName, methodName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MagicOnion method error: {ServiceName}.{MethodName} in {ElapsedMs}ms",
                serviceName, methodName, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
