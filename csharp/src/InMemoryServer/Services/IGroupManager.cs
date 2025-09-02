namespace InMemoryServer.Services;

/// <summary>
/// Interface for group management to allow easy swapping between implementations
/// </summary>
public interface IGroupManager
{
    Task<BattleGroupContext> JoinGroupAsync(string connectionId, string? groupName = null);
    Task<(BattleGroupContext? group, int newCount)> LeaveGroupAsync(string connectionId);
    Task<IEnumerable<BattleGroupContext>> GetAllGroupsAsync();
    Task<BattleGroupContext?> GetGroupInfoAsync(string groupId);
    Task<string?> GetGroupIdForConnectionAsync(string connectionId);
    Task<bool> ExtendGroupWaitingTimeAsync(string groupId);
    Task<List<string>> DissolveGroupAsync(string groupId, string reason = "Group disbanded");
    IEnumerable<BattleGroupContext> GetGroupsNeedingAttention();

    event Action<string, string, List<string>, string>? OnGroupDissolved;
}

/// <summary>
/// Adapter to make GroupManagerActor compatible with existing interfaces
/// </summary>
public class GroupManagerAdapter : IGroupManager, IDisposable
{
    private readonly GroupManagerActor _actor;

    public GroupManagerAdapter(GroupManagerActor actor)
    {
        _actor = actor;
        _actor.OnGroupDissolved += (groupId, groupName, clientIds, reason) =>
            OnGroupDissolved?.Invoke(groupId, groupName, clientIds, reason);
    }

    public async Task<BattleGroupContext> JoinGroupAsync(string connectionId, string? groupName = null)
        => await _actor.JoinGroupAsync(connectionId, groupName);

    public async Task<(BattleGroupContext? group, int newCount)> LeaveGroupAsync(string connectionId)
        => await _actor.LeaveGroupAsync(connectionId);

    public async Task<IEnumerable<BattleGroupContext>> GetAllGroupsAsync()
        => await _actor.GetAllGroupsAsync();

    public async Task<BattleGroupContext?> GetGroupInfoAsync(string groupId)
        => await _actor.GetGroupInfoAsync(groupId);

    public async Task<string?> GetGroupIdForConnectionAsync(string connectionId)
        => await _actor.GetGroupIdForConnectionAsync(connectionId);

    public async Task<bool> ExtendGroupWaitingTimeAsync(string groupId)
        => await _actor.ExtendGroupWaitingTimeAsync(groupId);

    public async Task<List<string>> DissolveGroupAsync(string groupId, string reason = "Group disbanded")
        => await _actor.DissolveGroupAsync(groupId, reason);

    public IEnumerable<BattleGroupContext> GetGroupsNeedingAttention()
    {
        // This is handled internally by the actor's cleanup process
        // Return empty collection as this method is deprecated in favor of actor's internal cleanup
        return [];
    }

    public event Action<string, string, List<string>, string>? OnGroupDissolved;

    public void Dispose()
    {
        _actor?.Dispose();
    }
}
