using Microsoft.AspNetCore.SignalR;
using BattleLogic.Battle;
using BattleLogic.Models;
using Shared.Battle;
using Shared.Models;
using Shared.Constants;
using BattleLogic.Constans;
using BattleLogic.Infrastructures.BattleReplayWriter;

namespace InMemoryServer;

/// <summary>
/// InMemory SignalR Hub
/// </summary>
public class InMemoryHub(ILogger<InMemoryHub> logger, InMemoryState state, GroupManager groupManager, ILoggerFactory loggerFactory, BattleReplayWriterFactory replayWriterFactory) : Hub
{
    /// <summary>
    /// Get value by key
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        logger.LogInformation($"Client {Context.ConnectionId} requested value for key: {key}");
        return state.KeyValueStore.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Set key-value pair
    /// </summary>
    public async Task<bool> SetAsync(string key, string value)
    {
        logger.LogInformation($"Client {Context.ConnectionId} setting key: {key} to value: {value}");
        state.KeyValueStore[key] = value;

        // Notify any watchers of this key
        if (state.KeyWatchers.TryGetValue(key, out var watchers))
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
        logger.LogInformation($"Client {Context.ConnectionId} deleting key: {key}");
        var result = state.KeyValueStore.TryRemove(key, out _);

        // Notify any watchers of this key
        if (result && state.KeyWatchers.TryGetValue(key, out var watchers))
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
        logger.LogInformation($"Client {Context.ConnectionId} listing keys with pattern: {pattern}");

        // Simple pattern matching, replace * with .* for regex
        if (pattern == "*")
        {
            return state.KeyValueStore.Keys;
        }
        else
        {
            var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
            return state.KeyValueStore.Keys.Where(k => System.Text.RegularExpressions.Regex.IsMatch(k, regexPattern));
        }
    }

    /// <summary>
    /// Watch key for changes
    /// </summary>
    public async Task<bool> WatchAsync(string key)
    {
        logger.LogInformation($"Client {Context.ConnectionId} watching key: {key}");

        if (!state.KeyWatchers.TryGetValue(key, out var watchers))
        {
            watchers = [];
            state.KeyWatchers[key] = watchers;
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
        var group = await groupManager.JoinGroupAsync(Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, group.Id);

        logger.LogInformation($"Client {Context.ConnectionId} joined group: {group.Name} (ID: {group.Id})");

        // Notify other members
        await Clients.OthersInGroup(group.Id).SendAsync("MemberJoined", Context.ConnectionId, group.ConnectionCount);

        // Check if group is full and battle should start
        if (group.ConnectionCount == SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(group.BattleId))
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
        var groupId = groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning($"Client {Context.ConnectionId} tried to broadcast but is not in any group");
            return false;
        }

        logger.LogInformation($"Client {Context.ConnectionId} broadcasting message to group {groupId}");
        await Clients.Group(groupId).SendAsync("GroupMessage", Context.ConnectionId, message);
        return true;
    }

    /// <summary>
    /// Get all available groups
    /// </summary>
    public async Task<IEnumerable<GroupInfo>> GetGroupsAsync()
    {
        logger.LogInformation($"Client {Context.ConnectionId} requesting group list");
        return groupManager.GetAllGroups();
    }

    /// <summary>
    /// Get current group info
    /// </summary>
    public async Task<GroupInfo?> GetCurrentGroupAsync()
    {
        var groupId = groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning($"Client {Context.ConnectionId} requested current group but is not in any group");
            return null;
        }

        return groupManager.GetGroupInfo(groupId);
    }

    /// <summary>
    /// Get battle status
    /// </summary>
    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        var groupId = groupManager.GetGroupIdForConnection(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning($"Client {Context.ConnectionId} requested battle status but is not in any group");
            return null;
        }

        var group = groupManager.GetGroupInfo(groupId);
        if (group is null || string.IsNullOrEmpty(group.BattleId))
        {
            logger.LogWarning($"Group {groupId} does not have an active battle");

            return new BattleStatus
            {
                IsInProgress = false,
                FieldWidth = BattleSystemDefines.BattleFieldWidth,
                FieldHeight = BattleSystemDefines.BattleFieldHeight
            };
        }

        return state.BattleStates.TryGetValue(group.BattleId, out var battle)
            ? battle.GetStatus()
            : new BattleStatus
            {
                IsInProgress = false,
                FieldWidth = BattleSystemDefines.BattleFieldWidth,
                FieldHeight = BattleSystemDefines.BattleFieldHeight
            };
    }

    /// <summary>
    /// Execute battle action
    /// </summary>
    public async Task<bool> BattleActionAsync(string actionType)
    {
        // For the initial implementation, battle is fully automated
        // This method is included for future expansion
        logger.LogInformation($"Client {Context.ConnectionId} requested battle action {actionType}, but battles are currently automated");
        return false;
    }

    /// <summary>
    /// Get battle replay data
    /// </summary>
    public async Task<string?> GetBattleReplayAsync(string battleId)
    {
        logger.LogInformation($"Client {Context.ConnectionId} requested battle replay for battle: {battleId}");

        var replayPath = Path.Combine(BattleSystemDefines.BattleReplayDirectory, $"{battleId}.jsonl");
        if (File.Exists(replayPath))
        {
            // Ensure directory exists for battle replays
            Directory.CreateDirectory(BattleSystemDefines.BattleReplayDirectory);

            try
            {
                // Use memory-efficient file reading
                return await File.ReadAllTextAsync(replayPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error reading battle replay file: {replayPath}");
                return null;
            }
        }
        else
        {
            logger.LogWarning($"Battle replay file not found: {replayPath}");
            return null;
        }
    }

    /// <summary>
    /// Start a battle for a full group
    /// </summary>
    private async Task StartBattleAsync(GroupInfo group)
    {
        var battleId = BattleSeed.NewTimestampId().ToString(); // Use GUID v7 for timestamp ordering
        group.BattleId = battleId;

        logger.LogInformation($"Starting battle {battleId} for group {group.Id}");
        logger.LogInformation($"Group {group.Id} has {group.ConnectionCount} members and will start a battle");

        // Create and store battle state
        var battleLogger = loggerFactory.CreateLogger<BattleState>();
        var battle = new BattleState(battleId, group, battleLogger, replayWriterFactory);
        state.BattleStates[battleId] = battle;

        // 1. Notify all clients that connections are ready
        logger.LogInformation($"Battle {battleId}: Notifying all clients that connections are ready");
        await Clients.Group(group.Id).SendAsync("ConnectionsReady", battleId);

        // 2. Start battle processing in background after all clients confirm readiness
        _ = Task.Run(async () =>
        {
            // Wait for all clients to confirm they received the ConnectionsReady notification
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30)); // 30秒のタイムアウト
            var startTime = DateTime.UtcNow;

            logger.LogInformation($"Battle {battleId}: Waiting for client confirmations ({group.ConnectionCount} clients)...");

            while (!battle.AreAllConnectionsReadyConfirmed())
            {
                if (await Task.WhenAny(Task.Delay(100), timeoutTask) == timeoutTask)
                {
                    // タイムアウト発生、確認が揃わなかった
                    var elapsed = DateTime.UtcNow - startTime;
                    logger.LogWarning($"Battle {battleId}: Timed out after {elapsed.TotalSeconds:F1}s waiting for client confirmations. Proceeding anyway.");
                    break;
                }
            }

            // 3. Send BattleStarted notification once all clients have confirmed
            logger.LogInformation($"Battle {battleId}: All clients confirmed or timeout reached. Starting battle.");
            await Clients.Group(group.Id).SendAsync("BattleStarted", battleId);

            // 4. Run pre-computation (完全にサーバーサイドで計算完了)
            logger.LogInformation($"Battle {battleId}: Starting pre-computation of battle simulation");
            await battle.RunBattleAsync();

            // 5. Send all battle data to clients for replay
            logger.LogInformation($"Battle {battleId}: Sending battle replay data to clients");
            var allTurnData = battle.GetAllTurnData();

            // Check if data is too large, split if necessary
            const int maxTurnsPerChunk = 50; // Send in chunks of 50 turns
            var chunks = allTurnData.Chunk(maxTurnsPerChunk).ToList();

            logger.LogInformation($"Battle {battleId}: Sending {allTurnData.Count} turns in {chunks.Count} chunk(s)");

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var isLastChunk = i == chunks.Count - 1;
                var turnDataList = new List<BattleStatus>(chunk.Length);
                turnDataList.AddRange(chunk);

                var replayData = new BattleReplayData
                {
                    BattleId = battleId,
                    TurnData = turnDataList,
                    ChunkIndex = i,
                    TotalChunks = chunks.Count,
                    IsLastChunk = isLastChunk,
                };

                await Clients.Group(group.Id).SendAsync("BattleReplayData", replayData);

                // Clear chunk data immediately after sending to reduce memory pressure
                turnDataList.Clear();

                // Free memory for processed chunk
                if (i > 0 && chunks.Count > 2)
                {
                    // Clear previous chunk data from allTurnData to help GC
                    var startIndex = (i - 1) * maxTurnsPerChunk;
                    var endIndex = Math.Min(startIndex + maxTurnsPerChunk, allTurnData.Count);
                    for (int j = startIndex; j < endIndex; j++)
                    {
                        if (j < allTurnData.Count)
                        {
                            // Clear references within the status object
                            allTurnData[j].Players.Clear();
                            allTurnData[j].Enemies.Clear();
                            allTurnData[j].RecentLogs.Clear();
                        }
                    }
                }
            }

            // 6. Battle completed notification
            await Clients.Group(group.Id).SendAsync("BattleCompleted", battle.GetStatus());
            logger.LogInformation($"Battle {battleId}: All replay data sent, battle marked as completed");

            // Clear entire allTurnData after all chunks sent
            battle.ClearBattleData();
        });
    }

    /// <summary>
    /// Handle client connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation($"Client {Context.ConnectionId} connected");
        state.ConnectionCount++;
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handle client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation($"Client {Context.ConnectionId} disconnected");
        state.ConnectionCount--;

        // Remove from group
        await groupManager.LeaveGroupAsync(Context.ConnectionId);

        // Remove from watchers
        foreach (var key in state.KeyWatchers.Keys.ToList())
        {
            if (state.KeyWatchers.TryGetValue(key, out var watchers))
            {
                watchers.Remove(Context.ConnectionId);
                if (watchers.Count == 0)
                {
                    state.KeyWatchers.TryRemove(key, out _);
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
        logger.LogInformation($"Client {Context.ConnectionId} requested server status");

        var status = new ServerStatus
        {
            Uptime = DateTime.UtcNow - state.StartTime,
            TotalConnections = state.ConnectionCount,
            GroupCount = groupManager.GetAllGroups().Count(),
            ActiveBattleCount = state.BattleStates.Count
        };

        // Get group summaries
        foreach (var group in groupManager.GetAllGroups())
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
        foreach (var battleEntry in state.BattleStates)
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
        var clientId = Context.ConnectionId;
        logger.LogInformation($"Client {clientId} is attempting to confirm connection ready");

        var groupId = groupManager.GetGroupIdForConnection(clientId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning($"Client {clientId} attempted to confirm connection ready but is not in any group");
            return false;
        }

        var group = groupManager.GetGroupInfo(groupId);
        if (group is null || string.IsNullOrEmpty(group.BattleId))
        {
            logger.LogWarning($"Group {groupId} does not have an active battle for connection ready confirmation");
            return false;
        }

        // Get battle state
        if (!state.BattleStates.TryGetValue(group.BattleId, out var battle))
        {
            logger.LogWarning($"Battle state not found for battle {group.BattleId}");
            return false;
        }

        // Mark this client as having confirmed connection readiness
        battle.MarkConnectionReadyConfirmed(clientId);
        logger.LogInformation($"Client {clientId} confirmed connection ready for battle {group.BattleId}");

        return true;
    }
}
