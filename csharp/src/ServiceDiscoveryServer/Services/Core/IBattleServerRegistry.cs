namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// BattleServer registry service interface
/// </summary>
public interface IBattleServerRegistry
{
    /// <summary>
    /// Register a new BattleServer
    /// </summary>
    /// <param name="registration">Server registration information</param>
    /// <returns>True if successfully registered</returns>
    Task<bool> RegisterServerAsync(BattleServerRegistration registration);

    /// <summary>
    /// Update server status
    /// </summary>
    /// <param name="serverId">Server ID</param>
    /// <param name="status">Updated status information</param>
    /// <returns>True if successfully updated</returns>
    Task<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status);

    /// <summary>
    /// Unregister server
    /// </summary>
    /// <param name="serverId">Server ID to unregister</param>
    /// <returns>True if successfully unregistered</returns>
    Task<bool> UnregisterServerAsync(string serverId);

    /// <summary>
    /// Get available server for assignment
    /// </summary>
    /// <returns>Available server information or null</returns>
    Task<BattleServerInfo?> GetAvailableServerAsync();

    /// <summary>
    /// Get specific server information
    /// </summary>
    /// <param name="serverId">Server ID</param>
    /// <returns>Server information or null if not found</returns>
    Task<BattleServerInfo?> GetServerInfoAsync(string serverId);

    /// <summary>
    /// List all available servers
    /// </summary>
    /// <returns>List of available servers</returns>
    Task<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync();

    /// <summary>
    /// Get server assigned to specific session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Assigned server information or null</returns>
    Task<BattleServerInfo?> GetAssignedServerAsync(string sessionId);

    /// <summary>
    /// Check server health
    /// </summary>
    void CheckServerHealth();
}
