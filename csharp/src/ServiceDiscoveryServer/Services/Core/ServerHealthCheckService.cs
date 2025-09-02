namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// Server health check background service
/// </summary>
public sealed class ServerHealthCheckService(ILogger<ServerHealthCheckService> logger, IBattleServerRegistry serverRegistry, IOptions<ServiceDiscoveryOptions> options) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ServerHealthCheckService started with health check interval of {HealthCheckInterval} seconds", options.Value.BattleServer.HeartbeatIntervalSeconds);
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ServerHealthCheckService stopping");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var healthCheckInterval = TimeSpan.FromSeconds(options.Value.BattleServer.HeartbeatIntervalSeconds);

        using var timer = new PeriodicTimer(healthCheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    serverRegistry.CheckServerHealth();
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in server health check background service");
                }
            }
        }
        catch (OperationCanceledException)
        {

        }
    }
}
