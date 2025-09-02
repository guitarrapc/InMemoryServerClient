namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// In-memory BattleServer registry service
/// </summary>
public sealed class BattleServerRegistry : IBattleServerRegistry, IHostedService, IDisposable
{
    private readonly ILogger<BattleServerRegistry> _logger;
    private readonly IOptions<ServiceDiscoveryOptions> _options;
    private readonly ConcurrentDictionary<string, BattleServerInfo> _servers = new();
    private readonly ConcurrentDictionary<string, int> _healthFailureCount = new();
    private readonly ConcurrentDictionary<string, string> _sessionAssignments = new();
    private readonly Timer _healthCheckTimer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public BattleServerRegistry(
        ILogger<BattleServerRegistry> logger,
        IOptions<ServiceDiscoveryOptions> options)
    {
        _logger = logger;
        _options = options;

        var heartbeatInterval = TimeSpan.FromSeconds(_options.Value.BattleServer.HeartbeatIntervalSeconds);
        _healthCheckTimer = new Timer(CheckServerHealth, null, heartbeatInterval, heartbeatInterval);
    }

    public Task<bool> RegisterServerAsync(BattleServerRegistration registration)
    {
        try
        {
            var serverInfo = new BattleServerInfo
            {
                ServerId = registration.ServerId,
                Address = registration.Address,
                SignalRPort = registration.SignalRPort,
                MagicOnionPort = registration.MagicOnionPort,
                Health = ServerHealth.Healthy,
                ActiveSessions = 0,
                MaxSessions = registration.MaxConcurrentSessions,
                LastHeartbeat = DateTime.UtcNow,
                LoadScore = 0.0
            };

            _servers[registration.ServerId] = serverInfo;
            _healthFailureCount[registration.ServerId] = 0;

            _logger.LogInformation("Registered BattleServer {ServerId} at {Address}:{SignalRPort}/{MagicOnionPort}",
                registration.ServerId, registration.Address, registration.SignalRPort, registration.MagicOnionPort);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register BattleServer {ServerId}", registration.ServerId);
            return Task.FromResult(false);
        }
    }

    public Task<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status)
    {
        if (!_servers.TryGetValue(serverId, out var existingServer))
        {
            _logger.LogWarning("Attempted to update status for unknown server {ServerId}", serverId);
            return Task.FromResult(false);
        }

        var updatedServer = existingServer with
        {
            Health = status.Health,
            ActiveSessions = status.ActiveSessions,
            MaxSessions = status.MaxSessions,
            LastHeartbeat = DateTime.UtcNow,
            LoadScore = CalculateLoadScore(status.CpuUsage, status.MemoryUsage, status.ActiveSessions, status.MaxSessions)
        };

        _servers[serverId] = updatedServer;
        _healthFailureCount[serverId] = 0; // Reset failure count on successful update

        _logger.LogDebug("Updated status for server {ServerId}: Health={Health}, ActiveSessions={ActiveSessions}, LoadScore={LoadScore:F2}",
            serverId, status.Health, status.ActiveSessions, updatedServer.LoadScore);

        return Task.FromResult(true);
    }

    public Task<bool> UnregisterServerAsync(string serverId)
    {
        var removed = _servers.TryRemove(serverId, out var server);
        if (removed)
        {
            _healthFailureCount.TryRemove(serverId, out _);
            _logger.LogInformation("Unregistered BattleServer {ServerId}", serverId);
        }

        return Task.FromResult(removed);
    }

    public Task<BattleServerInfo?> GetAvailableServerAsync()
    {
        var availableServers = _servers.Values
            .Where(s => s.Health == ServerHealth.Healthy && s.ActiveSessions < s.MaxSessions)
            .OrderBy(s => s.LoadScore)
            .ThenBy(s => s.ActiveSessions)
            .ToList();

        var selectedServer = availableServers.FirstOrDefault();

        if (selectedServer.ServerId is not null)
        {
            _logger.LogDebug("Selected server {ServerId} with load score {LoadScore:F2} and {ActiveSessions} active sessions",
                selectedServer.ServerId, selectedServer.LoadScore, selectedServer.ActiveSessions);
        }

        return Task.FromResult<BattleServerInfo?>(selectedServer.ServerId is null ? null : selectedServer);
    }

    public Task<BattleServerInfo?> GetServerInfoAsync(string serverId)
    {
        _servers.TryGetValue(serverId, out var server);
        return Task.FromResult<BattleServerInfo?>(server.ServerId is null ? null : server);
    }

    public Task<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync()
    {
        var servers = _servers.Values
            .Where(s => s.Health == ServerHealth.Healthy)
            .OrderBy(s => s.LoadScore)
            .ToList();

        return Task.FromResult<IReadOnlyList<BattleServerInfo>>(servers);
    }

    public Task<BattleServerInfo?> GetAssignedServerAsync(string sessionId)
    {
        if (!_sessionAssignments.TryGetValue(sessionId, out var serverId))
        {
            return Task.FromResult<BattleServerInfo?>(null);
        }

        _servers.TryGetValue(serverId, out var server);
        return Task.FromResult<BattleServerInfo?>(server.ServerId is null ? null : server);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BattleServerRegistry started with health check interval of {HealthCheckInterval} seconds",
            _options.Value.BattleServer.HeartbeatIntervalSeconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BattleServerRegistry stopping");
        _healthCheckTimer?.Dispose();
        return Task.CompletedTask;
    }

    private double CalculateLoadScore(double cpuUsage, double memoryUsage, int activeSessions, int maxSessions)
    {
        var sessionLoad = maxSessions > 0 ? (double)activeSessions / maxSessions : 0.0;
        var resourceLoad = (cpuUsage + memoryUsage) / 200.0; // Normalize to 0-1 range

        // Weighted average: 60% session load, 40% resource load
        return (sessionLoad * 0.6) + (resourceLoad * 0.4);
    }

    private void CheckServerHealth(object? state)
    {
        try
        {
            var heartbeatTimeout = TimeSpan.FromSeconds(_options.Value.BattleServer.HealthCheckTimeoutSeconds * 2);
            var unhealthyThreshold = _options.Value.BattleServer.UnhealthyThresholdCount;
            var removeAfterMinutes = _options.Value.BattleServer.RemoveUnhealthyAfterMinutes;
            var currentTime = DateTime.UtcNow;
            var serversToRemove = new List<string>();

            foreach (var (serverId, server) in _servers)
            {
                var timeSinceLastHeartbeat = currentTime - server.LastHeartbeat;

                if (timeSinceLastHeartbeat > heartbeatTimeout)
                {
                    var failureCount = _healthFailureCount.AddOrUpdate(serverId, 1, (key, value) => value + 1);

                    if (failureCount >= unhealthyThreshold)
                    {
                        // Mark as unhealthy
                        var unhealthyServer = server with { Health = ServerHealth.Unhealthy };
                        _servers[serverId] = unhealthyServer;

                        _logger.LogWarning("Marked server {ServerId} as unhealthy after {FailureCount} consecutive failures",
                            serverId, failureCount);

                        // Schedule for removal if unhealthy for too long
                        if (timeSinceLastHeartbeat > TimeSpan.FromMinutes(removeAfterMinutes))
                        {
                            serversToRemove.Add(serverId);
                        }
                    }
                    else
                    {
                        // Mark as degraded
                        var degradedServer = server with { Health = ServerHealth.Degraded };
                        _servers[serverId] = degradedServer;

                        _logger.LogWarning("Marked server {ServerId} as degraded (failure {FailureCount}/{Threshold})",
                            serverId, failureCount, unhealthyThreshold);
                    }
                }
            }

            // Remove servers that have been unhealthy for too long
            foreach (var serverId in serversToRemove)
            {
                _ = UnregisterServerAsync(serverId);
                _logger.LogError("Removed unhealthy server {ServerId} after {RemoveAfterMinutes} minutes",
                    serverId, removeAfterMinutes);
            }            if (_servers.Count > 0)
            {
                var healthyCount = _servers.Values.Count(s => s.Health == ServerHealth.Healthy);
                var degradedCount = _servers.Values.Count(s => s.Health == ServerHealth.Degraded);
                var unhealthyCount = _servers.Values.Count(s => s.Health == ServerHealth.Unhealthy);

                _logger.LogDebug("Server health check completed: {HealthyCount} healthy, {DegradedCount} degraded, {UnhealthyCount} unhealthy",
                    healthyCount, degradedCount, unhealthyCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during server health check");
        }
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
        _semaphore?.Dispose();
    }
}
