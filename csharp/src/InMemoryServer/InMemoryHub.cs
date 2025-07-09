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
    private static readonly object _eventSetupLock = new();
    private static bool _eventHandlersSetup = false;
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
        await Groups.AddToGroupAsync(Context.ConnectionId, group.GroupId);

        logger.LogInformation($"Client {Context.ConnectionId} joined group: {group.Name} (ID: {group.GroupId})");

        // Notify other members
        var memberJoinedData = new MemberJoinedData
        {
            ConnectionId = Context.ConnectionId,
            GroupId = group.GroupId,
            GroupName = group.Name,
            CurrentMemberCount = group.ConnectionCount,
            MaxMembers = SystemDefines.MaxConnectionsPerGroup
        };
        await Clients.OthersInGroup(group.GroupId).SendAsync("MemberJoined", memberJoinedData);

        // Check if group is full and battle should start
        if (group.ConnectionCount == SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(group.BattleId))
        {
            await StartBattleAsync(group);
        }

        return group.GroupId;
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
    public async Task<string?> GetBattleReplayAsync(Guid battleId)
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
    }    /// <summary>
    /// Start a battle for a full group
    /// </summary>
    private async Task StartBattleAsync(GroupInfo group)
    {
        // Generate completely independent battle ID and seed
        var battleId = BattleSeed.GenerateBattleId();
        var seed = BattleSeed.GenerateSecureSeed();
        group.BattleId = battleId.ToString();

        // Log both battle ID and seed for debugging/reproduce purposes
        logger.LogInformation("Battle started - BattleId: {BattleId}, Seed: {Seed}", battleId, seed);
        logger.LogInformation("Group {GroupId} has {ConnectionCount} members and will start a battle",
            group.GroupId, group.ConnectionCount);

        // Create and store battle state
        var battleLogger = loggerFactory.CreateLogger<BattleState>();
        var battle = new BattleState(battleId, seed, group, battleLogger, replayWriterFactory);
        state.BattleStates[battleId.ToString()] = battle;

        // 1. Notify all clients that connections are ready
        logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Notifying all clients that connections are ready",
            battleId, seed);
        var connectionsReadyData = new ConnectionsReadyData { BattleId = battleId, Seed = seed };
        await Clients.Group(group.GroupId).SendAsync("ConnectionsReady", connectionsReadyData);

        // 2. Start battle processing in background after all clients confirm readiness
        _ = Task.Run(async () =>
        {
            // Wait for all clients to confirm they received the ConnectionsReady notification
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10)); // Reduced to 10 seconds for faster response
            var startTime = DateTime.UtcNow;

            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Waiting for client confirmations ({ConnectionCount} clients)...",
                battleId, seed, group.ConnectionCount);

            // More responsive polling with progress feedback
            var lastConfirmedCount = 0;
            while (!battle.AreAllConnectionsReadyConfirmed())
            {
                var currentConfirmedCount = battle.GetConfirmedConnectionCount();
                if (currentConfirmedCount != lastConfirmedCount)
                {
                    logger.LogInformation("Battle {BattleId} (Seed: {Seed}): {ConfirmedCount}/{TotalCount} clients confirmed ready",
                        battleId, seed, currentConfirmedCount, group.ConnectionCount);
                    lastConfirmedCount = currentConfirmedCount;
                }

                if (await Task.WhenAny(Task.Delay(50), timeoutTask) == timeoutTask) // 50ms polling for better responsiveness
                {
                    // タイムアウト発生、確認が揃わなかった
                    var elapsed = DateTime.UtcNow - startTime;
                    var finalConfirmedCount = battle.GetConfirmedConnectionCount();
                    logger.LogWarning("Battle {BattleId} (Seed: {Seed}): Timed out after {Elapsed:F1}s waiting for client confirmations. Got {ConfirmedCount}/{TotalCount} confirmations. Proceeding anyway.",
                        battleId, seed, elapsed.TotalSeconds, finalConfirmedCount, group.ConnectionCount);
                    break;
                }
            }

            var finalElapsed = DateTime.UtcNow - startTime;
            if (battle.AreAllConnectionsReadyConfirmed())
            {
                logger.LogInformation("Battle {BattleId} (Seed: {Seed}): All clients confirmed ready in {Elapsed:F1}s. Starting battle.",
                    battleId, seed, finalElapsed.TotalSeconds);
            }

            // 3. Send BattleStarted notification once all clients have confirmed
            var battleStartedData = new BattleStartedData { BattleId = battleId, Seed = seed };
            await Clients.Group(group.GroupId).SendAsync("BattleStarted", battleStartedData);

            // 4. Run pre-computation (完全にサーバーサイドで計算完了)
            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Starting pre-computation of battle simulation",
                battleId, seed);
            await battle.RunBattleAsync();

            // 5. Send all battle data to clients for replay
            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Sending battle replay data to clients",
                battleId, seed);
            var allTurnData = battle.GetAllTurnData();

            // Check if data is too large, split if necessary
            const int maxTurnsPerChunk = 50; // Send in chunks of 50 turns
            var chunks = allTurnData.Chunk(maxTurnsPerChunk).ToList();

            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Sending {TurnCount} turns in {ChunkCount} chunk(s)",
                battleId, seed, allTurnData.Count, chunks.Count);

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var isLastChunk = i == chunks.Count - 1;
                var turnDataList = new List<BattleStatus>(chunk.Length);
                turnDataList.AddRange(chunk);

                var replayData = new BattleReplayData
                {
                    BattleId = battleId,
                    Seed = seed,
                    TurnData = turnDataList,
                    ChunkIndex = i,
                    TotalChunks = chunks.Count,
                    IsLastChunk = isLastChunk,
                };

                await Clients.Group(group.GroupId).SendAsync("BattleReplayData", replayData);

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
            await Clients.Group(group.GroupId).SendAsync("BattleCompleted", battle.GetStatus());
            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): All replay data sent, battle marked as completed",
                battleId, seed);

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

        // Set up event handlers once (thread-safe)
        lock (_eventSetupLock)
        {
            if (!_eventHandlersSetup)
            {
                groupManager.OnGroupDissolved += async (groupId, groupName, clientIds, reason) =>
                {
                    try
                    {
                        await NotifyGroupDissolved(groupId, groupName, clientIds, reason);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error notifying group dissolution for group {GroupId}", groupId);
                    }
                };
                _eventHandlersSetup = true;
                logger.LogInformation("Group dissolution event handler set up");
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handle client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation($"Client {Context.ConnectionId} disconnected");
        state.ConnectionCount--;

        // Remove from group and notify other members
        var (group, newCount) = await groupManager.LeaveGroupAsync(Context.ConnectionId);
        if (group != null)
        {
            // Notify other members about the disconnection
            var memberLeftData = new MemberLeftData
            {
                ConnectionId = Context.ConnectionId,
                GroupId = group.GroupId,
                GroupName = group.Name,
                CurrentMemberCount = newCount,
                MaxMembers = SystemDefines.MaxConnectionsPerGroup
            };
            await Clients.OthersInGroup(group.GroupId).SendAsync("MemberLeft", memberLeftData);
        }

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
                Id = group.GroupId,
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
                BattleId = battleEntry.Key,
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

    /// <summary>
    /// Reproduce a battle with specific battle ID and seed
    /// </summary>
    public async Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        // Convert seed value to numeric using server-side logic
        var seed = BattleSeed.CreateCombinedSeed(battleId, seedValue);

        var clientId = Context.ConnectionId;
        logger.LogInformation("Client {ClientId} requesting battle reproduction - BattleId: {BattleId}, SeedValue: {SeedValue}, NumericSeed: {NumericSeed}",
            clientId, battleId, seedValue, seed);

        // Get or create group for reproduction
        var group = await groupManager.JoinGroupAsync(clientId, groupName);
        await Groups.AddToGroupAsync(clientId, group.GroupId);

        logger.LogInformation("Client {ClientId} joined reproduction group: {GroupName} (ID: {GroupId})",
            clientId, group.Name, group.GroupId);

        // Check if group is full and battle should start with reproduction
        if (group.ConnectionCount == SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(group.BattleId))
        {
            await StartReproduceBattleAsync(group, battleId, seed);
        }

        return true;
    }

    /// <summary>
    /// Start a battle reproduction with specific battle ID and seed
    /// </summary>
    private async Task StartReproduceBattleAsync(GroupInfo group, Guid battleId, int seed)
    {
        group.BattleId = battleId.ToString();

        // Log battle reproduction start with both battle ID and seed
        logger.LogInformation("Battle reproduction started - BattleId: {BattleId}, Seed: {Seed}", battleId, seed);
        logger.LogInformation("Group {GroupId} has {ConnectionCount} members and will start battle reproduction",
            group.GroupId, group.ConnectionCount);

        // Create and store battle state with specific battle ID and seed
        var battleLogger = loggerFactory.CreateLogger<BattleState>();
        var battle = new BattleState(battleId, seed, group, battleLogger, replayWriterFactory);
        state.BattleStates[battleId.ToString()] = battle;

        // 1. Notify all clients that connections are ready
        logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): Notifying all clients that connections are ready",
            battleId, seed);
        var connectionsReadyData = new ConnectionsReadyData { BattleId = battleId, Seed = seed };
        await Clients.Group(group.GroupId).SendAsync("ConnectionsReady", connectionsReadyData);

        // 2. Start battle processing in background after all clients confirm readiness
        _ = Task.Run(async () =>
        {
            // Wait for all clients to confirm they received the ConnectionsReady notification
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10)); // Reduced to 10 seconds for faster response
            var startTime = DateTime.UtcNow;

            logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): Waiting for client confirmations ({ConnectionCount} clients)...",
                battleId, seed, group.ConnectionCount);

            // More responsive polling with progress feedback
            var lastConfirmedCount = 0;
            while (!battle.AreAllConnectionsReadyConfirmed())
            {
                var currentConfirmedCount = battle.GetConfirmedConnectionCount();
                if (currentConfirmedCount != lastConfirmedCount)
                {
                    logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): {ConfirmedCount}/{TotalCount} clients confirmed ready",
                        battleId, seed, currentConfirmedCount, group.ConnectionCount);
                    lastConfirmedCount = currentConfirmedCount;
                }

                if (await Task.WhenAny(Task.Delay(50), timeoutTask) == timeoutTask) // 50ms polling for better responsiveness
                {
                    // タイムアウト発生、確認が揃わなかった
                    var elapsed = DateTime.UtcNow - startTime;
                    var finalConfirmedCount = battle.GetConfirmedConnectionCount();
                    logger.LogWarning("Battle reproduction {BattleId} (Seed: {Seed}): Timed out after {Elapsed:F1}s waiting for client confirmations. Got {ConfirmedCount}/{TotalCount} confirmations. Proceeding anyway.",
                        battleId, seed, elapsed.TotalSeconds, finalConfirmedCount, group.ConnectionCount);
                    break;
                }
            }

            var finalElapsed = DateTime.UtcNow - startTime;
            if (battle.AreAllConnectionsReadyConfirmed())
            {
                logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): All clients confirmed ready in {Elapsed:F1}s. Starting battle.",
                    battleId, seed, finalElapsed.TotalSeconds);
            }

            // 3. Send BattleStarted notification once all clients have confirmed
            var battleStartedData = new BattleStartedData { BattleId = battleId, Seed = seed };
            await Clients.Group(group.GroupId).SendAsync("BattleStarted", battleStartedData);

            // 4. Run pre-computation (完全にサーバーサイドで計算完了)
            logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): Starting pre-computation of battle simulation",
                battleId, seed);
            await battle.RunBattleAsync();

            // 5. Send all battle data to clients for replay
            logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): Sending battle replay data to clients",
                battleId, seed);
            var allTurnData = battle.GetAllTurnData();

            // Check if data is too large, split if necessary
            const int maxTurnsPerChunk = 50; // Send in chunks of 50 turns
            var chunks = allTurnData.Chunk(maxTurnsPerChunk).ToList();

            logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): Sending {TurnCount} turns in {ChunkCount} chunk(s)",
                battleId, seed, allTurnData.Count, chunks.Count);

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var isLastChunk = i == chunks.Count - 1;
                var turnDataList = new List<BattleStatus>(chunk.Length);
                turnDataList.AddRange(chunk);

                var replayData = new BattleReplayData
                {
                    BattleId = battleId,
                    Seed = seed,
                    TurnData = turnDataList,
                    ChunkIndex = i,
                    TotalChunks = chunks.Count,
                    IsLastChunk = isLastChunk,
                };

                await Clients.Group(group.GroupId).SendAsync("BattleReplayData", replayData);

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
            await Clients.Group(group.GroupId).SendAsync("BattleCompleted", battle.GetStatus());
            logger.LogInformation("Battle reproduction {BattleId} (Seed: {Seed}): All replay data sent, battle marked as completed",
                battleId, seed);

            // Clear entire allTurnData after all chunks sent
            battle.ClearBattleData();
        });
    }

    /// <summary>
    /// Notify clients about group dissolution
    /// </summary>
    private async Task NotifyGroupDissolved(string groupId, string groupName, List<string> clientIds, string reason)
    {
        logger.LogInformation("Notifying {ClientCount} clients about group {GroupName} (ID: {GroupId}) dissolution. Reason: {Reason}",
            clientIds.Count, groupName, groupId, reason);

        var groupDissolvedData = new GroupDissolvedData
        {
            GroupId = groupId,
            GroupName = groupName,
            Reason = reason
        };

        foreach (var clientId in clientIds)
        {
            try
            {
                await Clients.Client(clientId).SendAsync("GroupDissolved", groupDissolvedData);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to notify client {ClientId} about group dissolution", clientId);
            }
        }
    }

    /// <summary>
    /// Manually extend a group's waiting time (for testing or admin purposes)
    /// </summary>
    public async Task<bool> ExtendGroupAsync(string? groupName = null)
    {
        var groupId = groupName != null ?
            groupManager.GetAllGroups().FirstOrDefault(g => g.Name == groupName)?.GroupId :
            groupManager.GetGroupIdForConnection(Context.ConnectionId);

        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning($"Client {Context.ConnectionId} tried to extend group but is not in any group or group not found");
            return false;
        }

        var success = groupManager.ExtendGroupWaitingTime(groupId);
        if (success)
        {
            var group = groupManager.GetGroupInfo(groupId);
            if (group != null)
            {
                // Notify all group members about the extension
                var groupExtendedData = new GroupExtendedData
                {
                    GroupId = groupId,
                    GroupName = group.Name,
                    ExtensionCount = group.ExtensionCount,
                    MaxExtensions = SystemDefines.MaxGroupExtensions,
                    NewExpiryTime = group.ExpiresAt
                };
                await Clients.Group(groupId).SendAsync("GroupExtended", groupExtendedData);

                logger.LogInformation($"Group {group.Name} (ID: {groupId}) extended by client {Context.ConnectionId}");
            }
        }

        return success;
    }
}
