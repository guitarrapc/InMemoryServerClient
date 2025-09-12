using WasmClient.Models;

namespace WasmClient.Services;

/// <summary>
/// Factory for creating battle connections
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Create a SignalR connection
    /// </summary>
    /// <param name="connectionInfo">Connection information</param>
    /// <returns>Battle connection instance</returns>
    Task<IBattleConnection> CreateSignalRConnectionAsync(ConnectionInfo connectionInfo);

    /// <summary>
    /// Create a MagicOnion connection
    /// </summary>
    /// <param name="connectionInfo">Connection information</param>
    /// <returns>Battle connection instance</returns>
    Task<IBattleConnection> CreateMagicOnionConnectionAsync(ConnectionInfo connectionInfo);
}
