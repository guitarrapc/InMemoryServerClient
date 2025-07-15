using MagicOnion;
using Shared.Battle;
using Shared.Models;

namespace Shared.Contracts.MagicOnion;

/// <summary>
/// MagicOnion hub interface for real-time communication
/// </summary>
public interface IInMemoryHub : IStreamingHub<IInMemoryHub, IInMemoryHubReceiver>
{
    /// <summary>
    /// Broadcast message to current group
    /// </summary>
    /// <param name="message">Message to broadcast</param>
    /// <returns>True if successful</returns>
    Task<bool> BroadcastAsync(string message);

    /// <summary>
    /// Join a group
    /// </summary>
    /// <param name="groupName">Optional group name to join</param>
    /// <returns>The group ID that was joined</returns>
    Task<string> JoinGroupAsync(string? groupName = null);

    /// <summary>
    /// Get all available groups
    /// </summary>
    /// <returns>List of all groups</returns>
    Task<IEnumerable<GroupInfo>> GetGroupsAsync();

    /// <summary>
    /// Get current group info
    /// </summary>
    /// <returns>Current group info if in a group, null otherwise</returns>
    Task<GroupInfo?> GetCurrentGroupAsync();

    /// <summary>
    /// Get battle status
    /// </summary>
    /// <returns>Current battle status if in a battle, null otherwise</returns>
    Task<BattleStatus?> GetBattleStatusAsync();

    /// <summary>
    /// Get battle replay data
    /// </summary>
    /// <param name="battleId">Battle ID to get replay for</param>
    /// <returns>Battle replay data as JSON string, null if not found</returns>
    Task<string?> GetBattleReplayAsync(Guid battleId);

    /// <summary>
    /// Confirm that client has received ConnectionsReady notification
    /// </summary>
    /// <returns>True if confirmation was successful</returns>
    Task<bool> ConfirmConnectionReadyAsync();

    /// <summary>
    /// Reproduce a battle with specific battle ID and seed
    /// </summary>
    /// <param name="battleId">Battle ID for reproduction</param>
    /// <param name="seedValue">Seed value for reproduction</param>
    /// <param name="groupName">Group name for the reproduction session</param>
    /// <returns>True if reproduction was started successfully</returns>
    Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName);
}

/// <summary>
/// Client-side receiver interface for MagicOnion hub
/// </summary>
public interface IInMemoryHubReceiver
{
    /// <summary>
    /// Called when a key is changed
    /// </summary>
    /// <param name="key">The key that was changed</param>
    /// <param name="value">The new value</param>
    void OnKeyChanged(string key, string value);

    /// <summary>
    /// Called when a key is deleted
    /// </summary>
    /// <param name="key">The key that was deleted</param>
    void OnKeyDeleted(string key);

    /// <summary>
    /// Called when a group message is received
    /// </summary>
    /// <param name="senderId">ID of the sender</param>
    /// <param name="message">The message content</param>
    void OnGroupMessage(string senderId, string message);

    /// <summary>
    /// Called when a member joins the group
    /// </summary>
    /// <param name="data">Member joined data</param>
    void OnMemberJoined(MemberJoinedData data);

    /// <summary>
    /// Called when a member leaves the group
    /// </summary>
    /// <param name="data">Member left data</param>
    void OnMemberLeft(MemberLeftData data);

    /// <summary>
    /// Called when connections are ready for battle
    /// </summary>
    /// <param name="data">Connections ready data</param>
    void OnConnectionsReady(ConnectionsReadyData data);

    /// <summary>
    /// Called when battle is started
    /// </summary>
    /// <param name="data">Battle started data</param>
    void OnBattleStarted(BattleStartedData data);

    /// <summary>
    /// Called when battle replay data is available
    /// </summary>
    /// <param name="data">Battle replay data</param>
    void OnBattleReplayData(BattleReplayData data);

    /// <summary>
    /// Called when battle is completed
    /// </summary>
    /// <param name="battleStatus">Final battle status</param>
    void OnBattleCompleted(BattleStatus battleStatus);

    /// <summary>
    /// Called when a group is dissolved
    /// </summary>
    /// <param name="data">Group dissolved data</param>
    void OnGroupDissolved(GroupDissolvedData data);

    /// <summary>
    /// Called when a group is extended
    /// </summary>
    /// <param name="data">Group extended data</param>
    void OnGroupExtended(GroupExtendedData data);
}
