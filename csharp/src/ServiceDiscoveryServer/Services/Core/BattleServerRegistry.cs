namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// In-memory BattleServer registry service
/// </summary>
public sealed class BattleServerRegistry : IBattleServerRegistry, IDisposable
{
    private readonly ILogger<BattleServerRegistry> _logger;
    private readonly IOptions<ServiceDiscoveryOptions> _options;
    private readonly ConcurrentDictionary<string, BattleServerInfo> _servers = new();
    private readonly ConcurrentDictionary<string, int> _healthFailureCount = new();
    private readonly ConcurrentDictionary<string, string> _sessionAssignments = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public BattleServerRegistry(
        ILogger<BattleServerRegistry> logger,
        IOptions<ServiceDiscoveryOptions> options)
    {
        _logger = logger;
        _options = options;
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

    /// <summary>
    /// Check server health (called by ServerHealthCheckService)
    /// </summary>
    public void CheckServerHealth()
    {
        var serversToCheck = _servers.Values.ToList();
        foreach (var server in serversToCheck)
        {
            // Simulate health check - in real scenario, ping the server
            if (DateTime.UtcNow - server.LastHeartbeat > TimeSpan.FromMinutes(5))
            {
                _logger.LogWarning("Server {ServerId} has not sent heartbeat for {ElapsedTime}", server.ServerId, DateTime.UtcNow - server.LastHeartbeat);
            }
        }
    }

    private double CalculateLoadScore(double cpuUsage, double memoryUsage, int activeSessions, int maxSessions)
    {
        var sessionLoad = maxSessions > 0 ? (double)activeSessions / maxSessions : 0.0;
        var resourceLoad = (cpuUsage + memoryUsage) / 200.0; // Normalize to 0-1 range

        // Weighted average: 60% session load, 40% resource load
        return (sessionLoad * 0.6) + (resourceLoad * 0.4);
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
