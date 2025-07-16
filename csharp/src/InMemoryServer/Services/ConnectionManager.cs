using InMemoryServer.Models;
using System.Collections.Concurrent;

namespace InMemoryServer.Services;

/// <summary>
/// Manages connections across different protocols (SignalR and MagicOnion)
/// </summary>
public class ConnectionManager
{
    private readonly ILogger<ConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, Models.ConnectionInfo> _connections = new();
    private readonly ConcurrentDictionary<string, string> _originalToNormalized = new();
    private long _connectionCounter = 0;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a new connection and return a normalized connection ID
    /// </summary>
    /// <param name="originalConnectionId">Original connection ID from the protocol</param>
    /// <param name="protocol">Connection protocol</param>
    /// <returns>Normalized connection ID</returns>
    public string RegisterConnection(string originalConnectionId, ConnectionProtocol protocol)
    {
        // Generate a normalized connection ID
        var normalizedId = $"conn_{Interlocked.Increment(ref _connectionCounter):D6}_{protocol}";

        var connectionInfo = new Models.ConnectionInfo
        {
            ConnectionId = normalizedId,
            OriginalConnectionId = originalConnectionId,
            Protocol = protocol
        };

        _connections[normalizedId] = connectionInfo;
        _originalToNormalized[originalConnectionId] = normalizedId;

        _logger.LogInformation("Registered {Protocol} connection: {OriginalId} -> {NormalizedId}",
            protocol, originalConnectionId, normalizedId);

        return normalizedId;
    }

    /// <summary>
    /// Unregister a connection
    /// </summary>
    /// <param name="originalConnectionId">Original connection ID from the protocol</param>
    /// <returns>The normalized connection ID that was removed, or null if not found</returns>
    public string? UnregisterConnection(string originalConnectionId)
    {
        if (_originalToNormalized.TryRemove(originalConnectionId, out var normalizedId))
        {
            if (_connections.TryRemove(normalizedId, out var connectionInfo))
            {
                _logger.LogInformation("Unregistered {Protocol} connection: {OriginalId} -> {NormalizedId}",
                    connectionInfo.Protocol, originalConnectionId, normalizedId);
                return normalizedId;
            }
        }

        _logger.LogWarning("Attempted to unregister unknown connection: {OriginalId}", originalConnectionId);
        return null;
    }

    /// <summary>
    /// Get normalized connection ID from original connection ID
    /// </summary>
    /// <param name="originalConnectionId">Original connection ID</param>
    /// <returns>Normalized connection ID or null if not found</returns>
    public string? GetNormalizedConnectionId(string originalConnectionId)
    {
        return _originalToNormalized.TryGetValue(originalConnectionId, out var normalizedId) ? normalizedId : null;
    }

    /// <summary>
    /// Get connection info by normalized connection ID
    /// </summary>
    /// <param name="normalizedConnectionId">Normalized connection ID</param>
    /// <returns>Connection info or null if not found</returns>
    public Models.ConnectionInfo? GetConnectionInfo(string normalizedConnectionId)
    {
        return _connections.TryGetValue(normalizedConnectionId, out var connectionInfo) ? connectionInfo : null;
    }

    /// <summary>
    /// Get all connections for a specific protocol
    /// </summary>
    /// <param name="protocol">Protocol to filter by</param>
    /// <returns>Enumerable of connection info for the specified protocol</returns>
    public IEnumerable<Models.ConnectionInfo> GetConnectionsByProtocol(ConnectionProtocol protocol)
    {
        return _connections.Values.Where(c => c.Protocol == protocol);
    }

    /// <summary>
    /// Get all active connections
    /// </summary>
    /// <returns>Enumerable of all connection info</returns>
    public IEnumerable<Models.ConnectionInfo> GetAllConnections()
    {
        return _connections.Values;
    }

    /// <summary>
    /// Get total connection count
    /// </summary>
    /// <returns>Total number of active connections</returns>
    public int GetConnectionCount()
    {
        return _connections.Count;
    }
}
