using WasmClient.Models;
using Shared.Battle;
using Shared.Models;

namespace WasmClient.Services;

/// <summary>
/// Unified battle connection interface for WasmClient
/// </summary>
public interface IBattleConnection : IAsyncDisposable
{
    /// <summary>
    /// Connection ID
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// Connection type (SignalR or MagicOnion)
    /// </summary>
    Models.ConnectionType Type { get; }

    /// <summary>
    /// Connection information
    /// </summary>
    ConnectionInfo Info { get; }

    /// <summary>
    /// Whether the connection is established
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to server and optionally join a group
    /// </summary>
    /// <returns>True if connection succeeded</returns>
    Task<bool> ConnectAsync();

    /// <summary>
    /// Disconnect from server
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Join a specific group
    /// </summary>
    /// <param name="groupName">Group name to join</param>
    /// <returns>True if join succeeded</returns>
    Task<bool> JoinGroupAsync(string groupName);

    /// <summary>
    /// Get current battle status
    /// </summary>
    /// <returns>Battle status or null if no active battle</returns>
    Task<Shared.Battle.BattleStatus?> GetBattleStatusAsync();

    /// <summary>
    /// Confirm connection ready for battle
    /// </summary>
    /// <returns>True if confirmation succeeded</returns>
    Task<bool> ConfirmConnectionReadyAsync();

    /// <summary>
    /// Fired when battle replay data is received
    /// </summary>
    event Action<BattleReplayData>? OnBattleReplayReceived;

    /// <summary>
    /// Fired when battle is completed
    /// </summary>
    event Action<string>? OnBattleComplete;

    /// <summary>
    /// Fired when a connection error occurs
    /// </summary>
    event Action<Exception>? OnConnectionError;

    /// <summary>
    /// Fired when disconnected from server
    /// </summary>
    event Action? OnDisconnected;

    /// <summary>
    /// Fired when all connections are ready for battle
    /// </summary>
    event Action<ConnectionsReadyData>? OnConnectionsReady;

    /// <summary>
    /// Fired when battle starts
    /// </summary>
    event Action<BattleStartedData>? OnBattleStarted;
}
