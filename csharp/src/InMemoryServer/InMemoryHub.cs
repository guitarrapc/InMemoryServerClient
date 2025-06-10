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
    /// Notify that battle replay is complete for this client
    /// </summary>
    public async Task<bool> BattleReplayCompleteAsync()
    {
        var groupId = _groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            _logger.LogWarning($"Client {Context.ConnectionId} notified battle replay completion but is not in any group");
            return false;
        }

        var group = _groupManager.GetGroupInfo(groupId);
        if (group == null || string.IsNullOrEmpty(group.BattleId))
        {
            _logger.LogWarning($"Group {groupId} does not have an active battle for replay completion");
            return false;
        }

        // Get battle state
        if (!_state.BattleStates.TryGetValue(group.BattleId, out var battle))
        {
            _logger.LogWarning($"Battle state not found for battle {group.BattleId}");
            return false;
        }

        // Mark this client as having completed the replay
        var clientId = Context.ConnectionId;
        battle.MarkReplayCompleteForClient(clientId);
        _logger.LogInformation($"Client {clientId} completed battle replay for battle {group.BattleId}");

        // Check if all clients have completed the replay
        if (battle.AreAllReplaysCompleted())
        {
            _logger.LogInformation($"All clients completed battle replay for battle {group.BattleId}. Notifying group.");
            await Clients.Group(groupId).SendAsync("AllBattleReplaysCompleted", group.BattleId);
        }

        return true;
    }

    /// <summary>
    /// Start a battle for a full group
    /// </summary>
    private async Task StartBattleAsync(GroupInfo group)
    {
        var battleId = Guid.NewGuid().ToString();
        group.BattleId = battleId;

        _logger.LogInformation($"Starting battle {battleId} for group {group.Id}");
        _logger.LogInformation($"Group {group.Id} has {group.ConnectionCount} members and will start a battle");

        // Create and store battle state
        var battle = new BattleState(battleId, group);
        _state.BattleStates[battleId] = battle;

        // 1. Notify all clients that connections are ready
        _logger.LogInformation($"Battle {battleId}: Notifying all clients that connections are ready");
        await Clients.Group(group.Id).SendAsync("ConnectionsReady", battleId);

        // 2. Start battle processing in background after all clients confirm readiness
        _ = Task.Run(async () =>
        {
            // Wait for all clients to confirm they received the ConnectionsReady notification
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30)); // 30秒のタイムアウト

            while (!battle.AreAllConnectionsReadyConfirmed())
            {
                if (await Task.WhenAny(Task.Delay(100), timeoutTask) == timeoutTask)
                {
                    // タイムアウト発生、確認が揃わなかった
                    _logger.LogWarning($"Battle {battleId}: Timed out waiting for client confirmations. Proceeding anyway.");
                    break;
                }
            }

            // 3. Send BattleStarted notification once all clients have confirmed
            _logger.LogInformation($"Battle {battleId}: All clients confirmed. Starting battle.");
            await Clients.Group(group.Id).SendAsync("BattleStarted", battleId);

            // 4. Run pre-computation
            _logger.LogInformation($"Battle {battleId}: Starting pre-computation of battle simulation");
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
    /// Handle client connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client {Context.ConnectionId} connected");
        _state.ConnectionCount++;
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handle client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"Client {Context.ConnectionId} disconnected");
        _state.ConnectionCount--;

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

    /// <summary>
    /// Get server status
    /// </summary>
    public async Task<ServerStatus> GetServerStatusAsync()
    {
        _logger.LogInformation($"Client {Context.ConnectionId} requested server status");

        var status = new ServerStatus
        {
            Uptime = DateTime.UtcNow - _state.StartTime,
            TotalConnections = _state.ConnectionCount,
            GroupCount = _groupManager.GetAllGroups().Count(),
            ActiveBattleCount = _state.BattleStates.Count
        };

        // Get group summaries
        foreach (var group in _groupManager.GetAllGroups())
        {
            status.Groups.Add(new GroupSummary
            {
                Id = group.Id,
                Name = group.Name,
                ConnectionCount = group.ConnectionCount,
                BattleId = group.BattleId
            });
        }

        // Get battle summaries
        foreach (var battleEntry in _state.BattleStates)
        {
            var battleState = battleEntry.Value;
            var battleStatus = battleState.GetStatus();

            status.ActiveBattles.Add(new BattleSummary
            {
                Id = battleEntry.Key,
                GroupId = battleState.GroupId,
                CurrentTurn = battleStatus.CurrentTurn,
                PlayerCount = battleStatus.Players.Count,
                EnemyCount = battleStatus.Enemies.Count,
                StartedAt = battleState.StartTime
            });
        }

        return status;
    }

    /// <summary>
    /// Confirms that a client has received the ConnectionsReady notification
    /// </summary>
    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        var groupId = _groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            _logger.LogWarning($"Client {Context.ConnectionId} attempted to confirm connection ready but is not in any group");
            return false;
        }

        var group = _groupManager.GetGroupInfo(groupId);
        if (group == null || string.IsNullOrEmpty(group.BattleId))
        {
            _logger.LogWarning($"Group {groupId} does not have an active battle for connection ready confirmation");
            return false;
        }

        // Get battle state
        if (!_state.BattleStates.TryGetValue(group.BattleId, out var battle))
        {
            _logger.LogWarning($"Battle state not found for battle {group.BattleId}");
            return false;
        }

        // Mark this client as having confirmed connection readiness
        var clientId = Context.ConnectionId;
        battle.MarkConnectionReadyConfirmed(clientId);
        _logger.LogInformation($"Client {clientId} confirmed connection ready for battle {group.BattleId}");

        return true;
    }
}
