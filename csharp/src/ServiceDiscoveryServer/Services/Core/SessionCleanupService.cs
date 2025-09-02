namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// Session cleanup background service
/// </summary>
public sealed class SessionCleanupService(ILogger<SessionCleanupService> logger, ISessionManager sessionManager, IOptions<ServiceDiscoveryOptions> options) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SessionCleanupService started with cleanup interval of {CleanupInterval} minutes", options.Value.Session.CleanupIntervalMinutes);
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SessionCleanupService stopping");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanupInterval = TimeSpan.FromMinutes(options.Value.Session.CleanupIntervalMinutes);
        using var timer = new PeriodicTimer(cleanupInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    sessionManager.CleanupExpiredSessions();
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in session cleanup background service");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
