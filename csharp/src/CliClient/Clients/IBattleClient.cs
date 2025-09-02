using Shared.BattleLogic.Models;
using Shared.BattleServer.Models;

namespace CliClient.Clients;

/// <summary>
/// Abstract interface for InMemory server client operations
/// This interface provides protocol-independent business methods
/// </summary>
public interface IBattleClient : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the client is connected to the server
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Battle completion source for waiting until the battle is complete
    /// </summary>
    TaskCompletionSource<bool> BattleCompletionSource { get; }

    /// <summary>
    /// Connect to the InMemory server
    /// </summary>
    /// <param name="serverUrl">Server URL</param>
    /// <param name="groupName">Optional group name to join after connection</param>
    /// <returns>True if connection succeeded</returns>
    Task<bool> ConnectAsync(string serverUrl, string? groupName = null);

    /// <summary>
    /// Disconnect from the server
    /// </summary>
    Task DisconnectAsync();

    // Key-Value operations
    /// <summary>
    /// Get value by key
    /// </summary>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Set key-value pair
    /// </summary>
    Task<bool> SetAsync(string key, string value);

    /// <summary>
    /// Delete key
    /// </summary>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// List keys matching pattern
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null);

    // Group operations
    /// <summary>
    /// Join a group
    /// </summary>
    Task<bool> JoinGroupAsync(string groupName);

    /// <summary>
    /// Broadcast message to group members
    /// </summary>
    Task<bool> BroadcastMessageAsync(string message);

    // Additional methods
    /// <summary>
    /// List keys matching pattern
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(string? pattern = null);

    /// <summary>
    /// Watch key for changes (simplified implementation)
    /// </summary>
    Task WatchAsync(string key);

    /// <summary>
    /// Broadcast message (alternative name)
    /// </summary>
    Task<bool> BroadcastAsync(string message);

    /// <summary>
    /// Get battle replay data
    /// </summary>
    Task<BattleReplayData?> GetBattleReplayAsync(Guid battleId);

    /// <summary>
    /// Play battle replay
    /// </summary>
    Task PlayBattleReplayAsync(BattleReplayData replayData);

    // Battle operations
    /// <summary>
    /// Confirm connection ready for battle
    /// </summary>
    Task<bool> ConfirmConnectionReadyAsync();

    /// <summary>
    /// Get current battle status
    /// </summary>
    Task<BattleStatus?> GetBattleStatusAsync();

    // Battle reproduction
    /// <summary>
    /// Reproduce a battle with specific battle ID and seed
    /// </summary>
    /// <param name="battleId">Battle ID to reproduce</param>
    /// <param name="seedValue">String seed value (will be converted to numeric by server)</param>
    /// <param name="groupName">Optional group name for reproduction</param>
    /// <returns>True if reproduction request was successful</returns>
    Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName);

    // Events (業務レベルのイベント)
    /// <summary>
    /// Fired when disconnected from server
    /// </summary>
    event Action<string>? OnDisconnected;

    /// <summary>
    /// Fired when a key value is changed
    /// </summary>
    event Action<string, string>? OnKeyChanged;

    /// <summary>
    /// Fired when a key is deleted
    /// </summary>
    event Action<string>? OnKeyDeleted;

    /// <summary>
    /// Fired when a new member joins the group
    /// </summary>
    event Action<MemberJoinedData>? OnMemberJoined;

    /// <summary>
    /// Fired when a member leaves the group
    /// </summary>
    event Action<MemberLeftData>? OnMemberLeft;

    /// <summary>
    /// Fired when a group message is received
    /// </summary>
    event Action<string, string>? OnGroupMessage;

    /// <summary>
    /// Fired when all connections are ready for battle
    /// </summary>
    event Action<ConnectionsReadyData>? OnConnectionsReady;

    /// <summary>
    /// Fired when battle starts
    /// </summary>
    event Action<BattleStartedData>? OnBattleStarted;

    /// <summary>
    /// Fired when battle replay data is received
    /// </summary>
    event Action<BattleReplayData>? OnBattleReplayData;

    /// <summary>
    /// Fired when a group is dissolved
    /// </summary>
    event Action<GroupDissolvedData>? OnGroupDissolved;

    /// <summary>
    /// Fired when a group waiting time is extended
    /// </summary>
    event Action<GroupExtendedData>? OnGroupExtended;
}
