using InMemoryServer.Models;
using System.Collections.Concurrent;

namespace InMemoryServer.Services;

/// <summary>
/// Manages connections across different protocols (SignalR and MagicOnion)
/// Uses original connection IDs directly - both SignalR (128-bit cryptographic random)
/// and MagicOnion (GUID) provide sufficient uniqueness guarantees
/// </summary>
public class ConnectionManager
{
    private readonly ILogger<ConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, Models.ConnectionInfo> _connections = new();

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a new connection using the original connection ID directly
    /// </summary>
    /// <param name="connectionId">Original connection ID from the protocol</param>
    /// <param name="protocol">Connection protocol</param>
    /// <returns>The connection ID (same as input for simplified management)</returns>
    public string RegisterConnection(string connectionId, ConnectionProtocol protocol)
    {

        var connectionInfo = new Models.ConnectionInfo
        {
            ConnectionId = connectionId,
            OriginalConnectionId = connectionId, // Same as ConnectionId in simplified approach
            Protocol = protocol
        };

        _connections[connectionId] = connectionInfo;

        _logger.LogInformation("Registered {Protocol} connection: {ConnectionId}",
            protocol, connectionId);

        return connectionId;
    }

    /// <summary>
    /// Unregister a connection
    /// </summary>
    /// <param name="connectionId">Connection ID to unregister</param>
    /// <returns>True if the connection was removed, false if not found</returns>
    public bool UnregisterConnection(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connectionInfo))
        {
            _logger.LogInformation("Unregistered {Protocol} connection: {ConnectionId}",
                connectionInfo.Protocol, connectionId);
            return true;
        }

        _logger.LogWarning("Attempted to unregister unknown connection: {ConnectionId}", connectionId);
        return false;
    }

    /// <summary>
    /// Get connection info by connection ID
    /// </summary>
    /// <param name="connectionId">Connection ID</param>
    /// <returns>Connection info or null if not found</returns>
    public Models.ConnectionInfo? GetConnectionInfo(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connectionInfo) ? connectionInfo : null;
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

    /// <summary>
    /// Check if a connection is still active
    /// </summary>
    /// <param name="connectionId">Connection ID to check</param>
    /// <returns>True if the connection is active, false otherwise</returns>
    public virtual async Task<bool> IsConnectionActiveAsync(string connectionId)
    {
        // For now, just check if the connection exists in our tracking
        // In a real implementation, you might want to ping the actual connection
        return await Task.FromResult(_connections.ContainsKey(connectionId));
    }
}
