namespace ServiceDiscoveryServer.Controllers;

/// <summary>
/// Health check controller
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly ISessionManager _sessionManager;
    private readonly IBattleServerRegistry _serverRegistry;

    public HealthController(
        ILogger<HealthController> logger,
        ISessionManager sessionManager,
        IBattleServerRegistry serverRegistry)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _serverRegistry = serverRegistry;
    }

    /// <summary>
    /// Basic health check endpoint
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet]
    public IActionResult GetHealth()
    {
        try
        {
            return Ok(new HealthStatusResponse
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new HealthStatusResponse
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Detailed health check with service status
    /// </summary>
    /// <returns>Detailed health status</returns>
    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailedHealth()
    {
        try
        {
            var activeSessions = await _sessionManager.ListActiveSessionsAsync();
            var availableServers = await _serverRegistry.ListAvailableServersAsync();

            var response = new DetailedHealthResponse
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown",
                ServiceStatus = new ServiceStatusInfo
                {
                    ActiveSessions = activeSessions.Count,
                    AvailableServers = availableServers.Count,
                    HealthyServers = availableServers.Count(s => s.Health == ServerHealth.Healthy),
                    DegradedServers = availableServers.Count(s => s.Health == ServerHealth.Degraded),
                    UnhealthyServers = availableServers.Count(s => s.Health == ServerHealth.Unhealthy)
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed health check failed");
            return StatusCode(503, new HealthStatusResponse
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Readiness probe endpoint
    /// </summary>
    /// <returns>Readiness status</returns>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            // Check if core services are ready
            var availableServers = await _serverRegistry.ListAvailableServersAsync();
            var hasHealthyServers = availableServers.Any(s => s.Health == ServerHealth.Healthy);

            if (hasHealthyServers)
            {
                return Ok(new ReadinessResponse
                {
                    Ready = true,
                    Timestamp = DateTime.UtcNow,
                    Message = "Service is ready to accept traffic"
                });
            }

            return StatusCode(503, new ReadinessResponse
            {
                Ready = false,
                Timestamp = DateTime.UtcNow,
                Message = "No healthy battle servers available"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new ReadinessResponse
            {
                Ready = false,
                Timestamp = DateTime.UtcNow,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Liveness probe endpoint
    /// </summary>
    /// <returns>Liveness status</returns>
    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        // Simple liveness check - if we can respond, we're alive
        return Ok(new LivenessResponse
        {
            Alive = true,
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Basic health status response
/// </summary>
public readonly record struct HealthStatusResponse
{
    public required string Status { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Version { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Detailed health status response
/// </summary>
public readonly record struct DetailedHealthResponse
{
    public required string Status { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Version { get; init; }
    public string? Error { get; init; }
    public ServiceStatusInfo ServiceStatus { get; init; }
}

/// <summary>
/// Service status information
/// </summary>
public readonly record struct ServiceStatusInfo
{
    public int ActiveSessions { get; init; }
    public int AvailableServers { get; init; }
    public int HealthyServers { get; init; }
    public int DegradedServers { get; init; }
    public int UnhealthyServers { get; init; }
}

/// <summary>
/// Readiness response
/// </summary>
public readonly record struct ReadinessResponse
{
    public bool Ready { get; init; }
    public DateTime Timestamp { get; init; }
    public string Message { get; init; }
}

/// <summary>
/// Liveness response
/// </summary>
public readonly record struct LivenessResponse
{
    public bool Alive { get; init; }
    public DateTime Timestamp { get; init; }
}
