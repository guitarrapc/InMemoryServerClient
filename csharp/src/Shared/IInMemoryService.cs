namespace Shared;

/// <summary>
/// InMemoryServer service interface for SignalR hub
/// </summary>
public interface IInMemoryService
{
    // Basic key-value operations
    Task<string> GetAsync(string key);
    Task<bool> SetAsync(string key, string value);
    Task<bool> DeleteAsync(string key);        Task<IEnumerable<string>> ListAsync(string pattern = "*");
    Task<bool> WatchAsync(string key);

    // Group management operations
    Task<string> JoinGroupAsync(string? groupName = null);
    Task<bool> BroadcastAsync(string message);
    Task<IEnumerable<GroupInfo>> GetGroupsAsync();
    Task<GroupInfo> GetCurrentGroupAsync();

    // Battle operations
    Task<BattleStatus> GetBattleStatusAsync();
    Task<bool> BattleActionAsync(string actionType, string? parameters = null);
    Task<string> GetBattleReplayAsync(string battleId);
}
