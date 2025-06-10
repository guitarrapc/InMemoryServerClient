using Microsoft.AspNetCore.SignalR;
using Shared;

namespace InMemoryServer;

/// <summary>
/// InMemory SignalR Hub
/// </summary>
public class InMemoryHub(ILogger<InMemoryHub> logger, InMemoryState state, GroupManager groupManager) : Hub
{
    private readonly ILogger<InMemoryHub> _logger = logger;
    private readonly InMemoryState _state = state;
    private readonly GroupManager _groupManager = groupManager;

    /// <summary>
    /// Get value by key
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} requested value for key: {key}");
        return _state.KeyValueStore.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Set key-value pair
    /// </summary>
    public async Task<bool> SetAsync(string key, string value)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} setting key: {key} to value: {value}");
        _state.KeyValueStore[key] = value;

        // Notify any watchers of this key
        if (_state.KeyWatchers.TryGetValue(key, out var watchers))
        {
            foreach (var watcherId in watchers)
            {
                await Clients.Client(watcherId).SendAsync("KeyChanged", key, value);
            }
        }

        return true;
    }

    /// <summary>
    /// Delete key
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} deleting key: {key}");
        var result = _state.KeyValueStore.TryRemove(key, out _);

        // Notify any watchers of this key
        if (result && _state.KeyWatchers.TryGetValue(key, out var watchers))
        {
            foreach (var watcherId in watchers)
            {
                await Clients.Client(watcherId).SendAsync("KeyDeleted", key);
            }
        }

        return result;
    }

    /// <summary>
    /// List keys matching pattern
    /// </summary>
    public async Task<IEnumerable<string>> ListAsync(string pattern = "*")
    {
        _logger.LogInformation($"Client {Context.ConnectionId} listing keys with pattern: {pattern}");

        // Simple pattern matching, replace * with .* for regex
        if (pattern == "*")
        {
            return _state.KeyValueStore.Keys;
        }
        else
        {
            var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
            return _state.KeyValueStore.Keys.Where(k => System.Text.RegularExpressions.Regex.IsMatch(k, regexPattern));
        }
    }

    /// <summary>
    /// Watch key for changes
    /// </summary>
    public async Task<bool> WatchAsync(string key)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} watching key: {key}");

        if (!_state.KeyWatchers.TryGetValue(key, out var watchers))
        {
            watchers = [];
            _state.KeyWatchers[key] = watchers;
        }

        watchers.Add(Context.ConnectionId);
        return true;
    }

    /// <summary>
    /// Join a group
    /// </summary>
    public async Task<string> JoinGroupAsync(string? groupName = null)
    {
        // Find or create group
        var group = await _groupManager.JoinGroupAsync(Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, group.Id);

        _logger.LogInformation($"Client {Context.ConnectionId} joined group: {group.Name} (ID: {group.Id})");

        // Notify other members
        await Clients.OthersInGroup(group.Id).SendAsync("MemberJoined", Context.ConnectionId, group.ConnectionCount);

        // Check if group is full and battle should start
        if (group.ConnectionCount == Constants.MaxConnectionsPerGroup && string.IsNullOrEmpty(group.BattleId))
        {
            await StartBattleAsync(group);
        }

        return group.Id;
    }

    /// <summary>
    /// Broadcast message to current group
    /// </summary>
    public async Task<bool> BroadcastAsync(string message)
    {
        var groupId = _groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            _logger.LogWarning($"Client {Context.ConnectionId} tried to broadcast but is not in any group");
            return false;
        }

        _logger.LogInformation($"Client {Context.ConnectionId} broadcasting message to group {groupId}");
        await Clients.Group(groupId).SendAsync("GroupMessage", Context.ConnectionId, message);
        return true;
    }

    /// <summary>
    /// Get all available groups
    /// </summary>
    public async Task<IEnumerable<GroupInfo>> GetGroupsAsync()
    {
        _logger.LogInformation($"Client {Context.ConnectionId} requesting group list");
        return _groupManager.GetAllGroups();
    }

    /// <summary>
    /// Get current group info
    /// </summary>
    public async Task<GroupInfo?> GetCurrentGroupAsync()
    {
        var groupId = _groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            _logger.LogWarning($"Client {Context.ConnectionId} requested current group but is not in any group");
            return null;
        }

        return _groupManager.GetGroupInfo(groupId);
    }

    /// <summary>
    /// Get battle status
    /// </summary>
    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        var groupId = _groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            _logger.LogWarning($"Client {Context.ConnectionId} requested battle status but is not in any group");
            return null;
        }

        var group = _groupManager.GetGroupInfo(groupId);
        if (group == null || string.IsNullOrEmpty(group.BattleId))
        {
            _logger.LogWarning($"Group {groupId} does not have an active battle");
            return new BattleStatus
            {
                IsInProgress = false,
                Field = new BattleFieldInfo
                {
                    Width = Constants.BattleFieldWidth,
                    Height = Constants.BattleFieldHeight,
                    Cells = []
                }
            };
        }

        return _state.BattleStates.TryGetValue(group.BattleId, out var battle)
            ? battle.GetStatus()
            : new BattleStatus
            {
                IsInProgress = false,
                Field = new BattleFieldInfo
                {
                    Width = Constants.BattleFieldWidth,
                    Height = Constants.BattleFieldHeight,
                    Cells = []
                }
            };
    }

    /// <summary>
    /// Execute battle action
    /// </summary>
    public async Task<bool> BattleActionAsync(string actionType)
    {
        // For the initial implementation, battle is fully automated
        // This method is included for future expansion
        _logger.LogInformation($"Client {Context.ConnectionId} requested battle action {actionType}, but battles are currently automated");
        return false;
    }    /// <summary>
    /// Get battle replay data
    /// </summary>
    public async Task<string?> GetBattleReplayAsync(string battleId)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} requested battle replay for battle: {battleId}");

        var replayPath = Path.Combine(Constants.BattleReplayDirectory, $"{battleId}.jsonl");
        if (File.Exists(replayPath))
        {
            return await File.ReadAllTextAsync(replayPath);
        }

        return null;
    }

    /// <summary>
    /// Start a battle for a full group
    /// </summary>
    private async Task StartBattleAsync(GroupInfo group)
    {
        var battleId = Guid.NewGuid().ToString();
        group.BattleId = battleId;

        _logger.LogInformation($"Starting battle {battleId} for group {group.Id}");

        // Create and store battle state
        var battle = new BattleState(battleId, group);
        _state.BattleStates[battleId] = battle;

        // Notify group members
        await Clients.Group(group.Id).SendAsync("BattleStarted", battleId);

        // Start battle processing in background
        _ = Task.Run(async () =>
        {
            await battle.RunBattleAsync(async (status) =>
            {
                // Send status updates to clients
                await Clients.Group(group.Id).SendAsync("BattleStatusUpdated", status);
            });

            // Battle completed
            await Clients.Group(group.Id).SendAsync("BattleCompleted", battle.GetStatus());
            _logger.LogInformation($"Battle {battleId} completed");

            // Reset battle ID in group to allow starting a new battle
            group.BattleId = null;

            // Schedule cleanup for battle state (to save memory)
            _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
            {
                _state.BattleStates.TryRemove(battleId, out BattleState? _);
                _logger.LogInformation($"Cleaned up battle state for {battleId}");
            });
        });
    }

    /// <summary>
    /// Handle client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} disconnected");

        // Remove from group
        await _groupManager.LeaveGroupAsync(Context.ConnectionId);

        // Remove from watchers
        foreach (var key in _state.KeyWatchers.Keys.ToList())
        {
            if (_state.KeyWatchers.TryGetValue(key, out var watchers))
            {
                watchers.Remove(Context.ConnectionId);
                if (watchers.Count == 0)
                {
                    _state.KeyWatchers.TryRemove(key, out _);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
