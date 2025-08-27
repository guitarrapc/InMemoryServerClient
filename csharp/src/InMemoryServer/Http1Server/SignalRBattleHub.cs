using Microsoft.AspNetCore.SignalR;
using BattleLogic.Battle;
using Shared.Battle;
using Shared.Models;
using Shared.Constants;
using BattleLogic.Constants;
using BattleLogic.Infrastructures.BattleReplayWriter;
using BattleLogic.Models;
using InMemoryServer.GameLift;
using InMemoryServer.Services;
using InMemoryServer.Models;

namespace InMemoryServer.Http1Server;

/// <summary>
/// InMemory SignalR Hub
/// </summary>
public class SignalRBattleHub(
    ILogger<SignalRBattleHub> logger,
    InMemoryState state,
    ConnectionManager connectionManager,
    InMemoryServer.Services.IGroupManager groupManager,
    CrossProtocolNotificationService notificationService,
    ILoggerFactory loggerFactory,
    BattleReplayWriterFactory replayWriterFactory,
    BattleCompletionService battleCompletionService,
    GameLift.GameSessionManager? gameSessionManager = null) : Hub
{
    private static readonly Lock _eventSetupLock = new();

    // key-value operations
    /// <summary>
    /// Get value by key
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        logger.LogDebug($"Client {Context.ConnectionId} requested value for key: {key}");
        return state.KeyValueStore.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Set key-value pair
    /// </summary>
    public async Task<bool> SetAsync(string key, string value)
    {
        logger.LogDebug($"Client {Context.ConnectionId} setting key: {key} to value: {value}");
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
        logger.LogDebug($"Client {Context.ConnectionId} deleting key: {key}");
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
        logger.LogDebug($"Client {Context.ConnectionId} listing keys with pattern: {pattern}");

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
        logger.LogDebug($"Client {Context.ConnectionId} watching key: {key}");

        if (!state.KeyWatchers.TryGetValue(key, out var watchers))
        {
            watchers = [];
            state.KeyWatchers[key] = watchers;
        }

        watchers.Add(Context.ConnectionId);
        return true;
    }

    // battle group operation
    /// <summary>
    /// Broadcast message to current group
    /// </summary>
    public async Task<bool> BroadcastAsync(string message)
    {
        var connectionId = Context.ConnectionId;

        var groupId = await groupManager.GetGroupIdForConnectionAsync(connectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("SignalR client {ConnectionId} tried to broadcast but is not in any group",
                connectionId);
            return false;
        }

        var group = await groupManager.GetGroupInfoAsync(groupId);
        if (group == null)
        {
            logger.LogWarning("Group {GroupId} not found for broadcast", groupId);
            return false;
        }

        logger.LogDebug("SignalR client {ConnectionId} broadcasting message to group {GroupId}",
            connectionId, groupId);

        var messageData = new GroupMessageData
        {
            SenderId = connectionId,
            Message = message
        };
        await notificationService.NotifyGroupAsync(groupId, group.ClientIds, "GroupMessage", messageData);
        return true;
    }

    /// <summary>
    /// Join a group
    /// </summary>
    public async Task<string> JoinGroupAsync(string? groupName = null)
    {
        var connectionId = Context.ConnectionId;

        logger.LogInformation("SignalR Client {ConnectionId} attempting to join group with name: {GroupName}", connectionId, groupName ?? "null");

        // Check if this group name is associated with a GameLift GameSession
        if (!string.IsNullOrEmpty(groupName) && gameSessionManager != null)
        {
            logger.LogDebug("Checking if group name {GroupName} is associated with GameLift GameSession", groupName);

            // Try to find or create a GameSession for the group name
            var resolvedGroupId = await gameSessionManager.FindOrCreateGameSessionForGroupAsync(groupName);

            logger.LogDebug("GameLift group resolution result for {GroupName}: {ResolvedGroupId}", groupName, resolvedGroupId ?? "null");

            if (!string.IsNullOrEmpty(resolvedGroupId))
            {
                logger.LogInformation("Resolved group name {GroupName} to GameLift GroupId {ResolvedGroupId}. Proceeding with GameLift join.", groupName, resolvedGroupId);
                return await JoinGameLiftGameSessionAsync(connectionId, groupName, resolvedGroupId);
            }
            else
            {
                logger.LogInformation("Could not resolve or create GameSession for GroupName {GroupName}. Proceeding with regular group join.", groupName);
            }
        }
        else
        {
            logger.LogDebug("Skipping GameLift resolution - GroupName: {GroupName}, GameSessionManager: {HasGameSessionManager}",
                groupName ?? "null", gameSessionManager != null);
        }

        // Regular group joining for Direct mode
        logger.LogDebug("Proceeding with regular group join for connection {ConnectionId}", connectionId);
        return await JoinRegularGroupAsync(connectionId, groupName);
    }

    /// <summary>
    /// Join a GameLift GameSession
    /// </summary>
    private async Task<string> JoinGameLiftGameSessionAsync(string connectionId, string groupName, string gameSessionId)
    {
        logger.LogInformation("SignalR Client {ConnectionId} attempting to join GameLift GameSession group: {GroupName} with GameSessionId: {GameSessionId}",
            connectionId, groupName, gameSessionId);

        // Use GameSessionManager to handle GameLift Anywhere connection directly
        var success = await gameSessionManager!.OnPlayerConnectedAsync(gameSessionId, connectionId);
        if (!success)
        {
            logger.LogWarning("Failed to add connection {ConnectionId} to GameLift GameSession {GameSessionId}", connectionId, gameSessionId);
            throw new InvalidOperationException($"Could not join GameLift GameSession '{gameSessionId}'. The session might be full or unavailable.");
        }

        var groupId = $"gamelift-{gameSessionId}";

        // Add to SignalR group
        await Groups.AddToGroupAsync(connectionId, groupId);
        logger.LogInformation("SignalR Client {ConnectionId} successfully joined GameLift GameSession {GameSessionId} as group {GroupId}",
            connectionId, gameSessionId, groupId);

        return groupId;
    }

    /// <summary>
    /// Join a regular group (Direct mode)
    /// </summary>
    private async Task<string> JoinRegularGroupAsync(string connectionId, string? groupName)
    {
        // Find or create group
        var group = await groupManager.JoinGroupAsync(connectionId, groupName);
        await Groups.AddToGroupAsync(connectionId, group.GroupId);

        logger.LogDebug("SignalR client {ConnectionId} joined group: {GroupName} (ID: {GroupId})", connectionId, group.Name, group.GroupId);

        // Notify all members across protocols
        var memberJoinedData = new MemberJoinedData
        {
            ConnectionId = connectionId,
            GroupId = group.GroupId,
            GroupName = group.Name,
            CurrentMemberCount = group.ConnectionCount,
            MaxMembers = SystemDefines.MaxConnectionsPerGroup
        };
        _ = Task.Run(async () =>
        {
            try
            {
                await notificationService.NotifyGroupAsync(group.GroupId, group.ClientIds, "MemberJoined", memberJoinedData);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SignalR NotifyGroupAsync failed for connection {ConnectionId}, group: {GroupId}", connectionId, group.GroupId);
            }
        });


        // Check if group is full and battle should start
        if (group.IsFull() && string.IsNullOrEmpty(group.BattleId))
        {
            await StartBattleAsync(group);
        }

        return group.GroupId;
    }

    /// <summary>
    /// Get all available groups
    /// </summary>
    public async Task<IEnumerable<GroupInfo>> GetGroupsAsync()
    {
        var connectionId = Context.ConnectionId;
        logger.LogDebug("Client {ConnectionId} requesting group list", connectionId);
        return await groupManager.GetAllGroupsAsync();
    }

    /// <summary>
    /// Get current group info
    /// </summary>
    public async Task<GroupInfo?> GetCurrentGroupAsync()
    {
        var connectionId = Context.ConnectionId;
        var groupId = await groupManager.GetGroupIdForConnectionAsync(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("Client {ConnectionId} requested battle status but is not in any group", connectionId);
            return null;
        }

        return await groupManager.GetGroupInfoAsync(groupId);
    }

    /// <summary>
    /// Get battle status
    /// </summary>
    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        var connectionId = Context.ConnectionId;
        var groupId = await groupManager.GetGroupIdForConnectionAsync(Context.ConnectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("Client {ConnectionId} requested battle status but is not in any group", connectionId);
            return null;
        }

        var group = await groupManager.GetGroupInfoAsync(groupId);
        if (group is null || string.IsNullOrEmpty(group.BattleId))
        {
            logger.LogWarning("Group {GroupId} does not have an active battle", groupId);
            return new BattleStatus
            {
                IsInProgress = false,
                FieldSize = BattleSystemDefines.BattleFieldSize,
            };
        }

        return state.BattleStates.TryGetValue(group.BattleId, out var battle)
            ? battle.GetStatus()
            : new BattleStatus
            {
                IsInProgress = false,
                FieldSize = BattleSystemDefines.BattleFieldSize,
            };
    }

    /// <summary>
    /// Get battle replay data
    /// </summary>
    public async Task<string?> GetBattleReplayAsync(Guid battleId)
    {
        var connectionId = Context.ConnectionId;
        logger.LogDebug("Client {ConnectionId} requested battle replay for battle: {BattleId}", connectionId, battleId);

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
                logger.LogError(ex, "Error reading battle replay file: {ReplayPath}", replayPath);
                return null;
            }
        }
        else
        {
            logger.LogWarning("Battle replay file not found: {ReplayPath}", replayPath);
            return null;
        }
    }

    /// <summary>
    /// Confirms that a client has received the ConnectionsReady notification
    /// </summary>
    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        var connectionId = Context.ConnectionId;
        logger.LogDebug("Client {ConnectionId} is attempting to confirm connection ready", connectionId);

        var groupId = await groupManager.GetGroupIdForConnectionAsync(connectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("Client {ConnectionId} attempted to confirm connection ready but is not in any group", connectionId);
            return false;
        }

        var group = await groupManager.GetGroupInfoAsync(groupId);
        if (group is null || string.IsNullOrEmpty(group.BattleId))
        {
            logger.LogWarning("Group {GroupId} does not have an active battle for connection ready confirmation", groupId);
            return false;
        }

        // Get battle state
        if (!state.BattleStates.TryGetValue(group.BattleId, out var battle))
        {
            logger.LogWarning("Battle state not found for battle {BattleId}", group.BattleId);
            return false;
        }

        // Mark this client as having confirmed connection readiness
        battle.MarkConnectionReadyConfirmed(connectionId);
        logger.LogDebug("Client {ConnectionId} confirmed connection ready for battle {BattleId}", connectionId, group.BattleId);

        return true;
    }

    /// <summary>
    /// Reproduce a battle with specific battle ID and seed
    /// </summary>
    public async Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        var connectionId = Context.ConnectionId;
        var seed = BattleSeed.CreateCombinedSeed(battleId, seedValue);

        logger.LogDebug("Client {ConnectionId} requesting battle reproduction - BattleId: {BattleId}, SeedValue: {SeedValue}, NumericSeed: {NumericSeed}",
    connectionId, battleId, seedValue, seed);

        // Get or create group for reproduction
        var group = await groupManager.JoinGroupAsync(connectionId, groupName);

        // Add to MagicOnion group
        await Groups.AddToGroupAsync(connectionId, group.GroupId);

        logger.LogDebug("Client {ConnectionId} joined reproduction group: {GroupName} (ID: {GroupId})", connectionId, group.Name, group.GroupId);

        // Check if group is full and battle should start with reproduction
        if (group.IsFull() && string.IsNullOrEmpty(group.BattleId))
        {
            await StartReproduceBattleAsync(group, battleId, seed);
        }

        return true;
    }

    /// <summary>
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
        logger.LogInformation("Group {GroupId} has {ConnectionCount} members and will start a battle", group.GroupId, group.ConnectionCount);

        // Create and store battle state
        var battleLogger = loggerFactory.CreateLogger<BattleState>();
        var battle = new BattleState(battleId, seed, group, battleLogger, replayWriterFactory);
        state.BattleStates[battleId.ToString()] = battle;

        // 1. Notify all clients that connections are ready
        logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Notifying all clients that connections are ready", battleId, seed);
        var connectionsReadyData = new ConnectionsReadyData { BattleId = battleId, Seed = seed };
        await notificationService.NotifyGroupAsync(group.GroupId, group.ClientIds, "ConnectionsReady", connectionsReadyData);

        // 2. Start battle processing in background after all clients confirm readiness
        _ = Task.Run(async () =>
        {
            // Wait for all clients to confirm they received the ConnectionsReady notification
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var startTime = DateTime.UtcNow;

            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Waiting for client confirmations ({ConnectionCount} clients)...", battleId, seed, group.ConnectionCount);

            // More responsive polling with progress feedback
            var lastConfirmedCount = 0;
            while (!battle.AreAllConnectionsReadyConfirmed())
            {
                var currentConfirmedCount = battle.GetConfirmedConnectionCount();
                if (currentConfirmedCount != lastConfirmedCount)
                {
                    logger.LogInformation("Battle {BattleId} (Seed: {Seed}): {ConfirmedCount}/{TotalCount} clients confirmed ready", battleId, seed, currentConfirmedCount, group.ConnectionCount);
                    lastConfirmedCount = currentConfirmedCount;
                }

                if (await Task.WhenAny(Task.Delay(50), timeoutTask) == timeoutTask) // 50ms polling for better responsiveness
                {
                    // timeout occured, clients did not confirm within 30 seconds
                    var elapsed = DateTime.UtcNow - startTime;
                    var finalConfirmedCount = battle.GetConfirmedConnectionCount();
                    logger.LogWarning("Battle {BattleId} (Seed: {Seed}): Timed out after {Elapsed:F1}s waiting for client confirmations. Got {ConfirmedCount}/{TotalCount} confirmations. Proceeding anyway.", battleId, seed, elapsed.TotalSeconds, finalConfirmedCount, group.ConnectionCount);
                    break;
                }
            }

            var finalElapsed = DateTime.UtcNow - startTime;
            if (battle.AreAllConnectionsReadyConfirmed())
            {
                logger.LogInformation("Battle {BattleId} (Seed: {Seed}): All clients confirmed ready in {Elapsed:F1}s. Starting battle.", battleId, seed, finalElapsed.TotalSeconds);
            }

            // 3. Send BattleStarted notification once all clients have confirmed
            var battleStartedData = new BattleStartedData { BattleId = battleId, Seed = seed };
            await notificationService.NotifyGroupAsync(group.GroupId, group.ClientIds, "BattleStarted", battleStartedData);

            // 4. Run pre-computation (Complete pre-computation on server-side)
            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Starting pre-computation of battle simulation", battleId, seed);
            await battle.RunBattleAsync();

            // 5. Send all battle data to clients for replay
            logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Sending battle replay data to clients", battleId, seed);
            await SendBattleReplayData(group, battle, battleId, seed);

            // 6. Handle battle completion with unified service (uses configuration default)
            await battleCompletionService.HandleBattleCompletionAsync(group, battle, battleId, seed);
        });
    }

    /// <summary>
    /// Send battle replay data to clients in chunks
    /// </summary>
    private async Task SendBattleReplayData(GroupInfo group, BattleState battle, Guid battleId, int seed)
    {
        var allTurnData = battle.GetAllTurnData();

        // Check if data is too large, split if necessary
        const int maxTurnsPerChunk = 50; // Send in chunks of 50 turns
        var chunks = allTurnData.Chunk(maxTurnsPerChunk).ToList();

        logger.LogInformation("Battle {BattleId} (Seed: {Seed}): Sending {TurnCount} turns in {ChunkCount} chunk(s)", battleId, seed, allTurnData.Count, chunks.Count);

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
                Summary = isLastChunk ? battle.GetBattleReplaySummary() : null
            };

            await notificationService.NotifyGroupAsync(group.GroupId, group.ClientIds, "BattleReplayData", replayData);

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
    }

    /// <summary>
    /// Start a battle reproduction with specific battle ID and seed
    /// </summary>
    private async Task StartReproduceBattleAsync(GroupInfo group, Guid battleId, int seed)
    {
        group.BattleId = battleId.ToString();

        // Log battle reproduction start with both battle ID and seed
        logger.LogDebug("Battle reproduction started - BattleId: {BattleId}, Seed: {Seed}", battleId, seed);
        logger.LogDebug("Group {GroupId} has {ConnectionCount} members and will start battle reproduction", group.GroupId, group.ConnectionCount);

        // Create and store battle state with specific battle ID and seed
        var battleLogger = loggerFactory.CreateLogger<BattleState>();
        var battle = new BattleState(battleId, seed, group, battleLogger, replayWriterFactory);
        state.BattleStates[battleId.ToString()] = battle;

        // 1. Notify all clients that connections are ready
        logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Notifying all clients that connections are ready", battleId, seed);
        var connectionsReadyData = new ConnectionsReadyData { BattleId = battleId, Seed = seed };
        await Clients.Group(group.GroupId).SendAsync("ConnectionsReady", connectionsReadyData);

        // 2. Start battle processing in background after all clients confirm readiness
        _ = Task.Run(async () =>
        {
            // Wait for all clients to confirm they received the ConnectionsReady notification
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var startTime = DateTime.UtcNow;

            logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Waiting for client confirmations ({ConnectionCount} clients)...", battleId, seed, group.ConnectionCount);

            // More responsive polling with progress feedback
            var lastConfirmedCount = 0;
            while (!battle.AreAllConnectionsReadyConfirmed())
            {
                var currentConfirmedCount = battle.GetConfirmedConnectionCount();
                if (currentConfirmedCount != lastConfirmedCount)
                {
                    logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): {ConfirmedCount}/{TotalCount} clients confirmed ready", battleId, seed, currentConfirmedCount, group.ConnectionCount);
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
                logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): All clients confirmed ready in {Elapsed:F1}s. Starting battle.", battleId, seed, finalElapsed.TotalSeconds);
            }

            // 3. Send BattleStarted notification once all clients have confirmed
            var battleStartedData = new BattleStartedData { BattleId = battleId, Seed = seed };
            await notificationService.NotifyGroupAsync(group.GroupId, group.ClientIds, "BattleStarted", battleStartedData);

            // 4. Run pre-computation (完全にサーバーサイドで計算完了)
            logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Starting pre-computation of battle simulation", battleId, seed);
            await battle.RunBattleAsync();

            // 5. Send all battle data to clients for replay
            logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Sending battle replay data to clients", battleId, seed);
            var allTurnData = battle.GetAllTurnData();

            // Check if data is too large, split if necessary
            const int maxTurnsPerChunk = 50; // Send in chunks of 50 turns
            var chunks = allTurnData.Chunk(maxTurnsPerChunk).ToList();

            logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Sending {TurnCount} turns in {ChunkCount} chunk(s)", battleId, seed, allTurnData.Count, chunks.Count);

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
                    Summary = isLastChunk ? battle.GetBattleReplaySummary() : null
                };

                await notificationService.NotifyGroupAsync(group.GroupId, group.ClientIds, "BattleReplayData", replayData);

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

            // 6. Handle battle completion with unified service (uses configuration default)
            await battleCompletionService.HandleBattleCompletionAsync(group, battle, battleId, seed);
        });
    }

    /// <summary>
    /// Notify clients about group dissolution
    /// </summary>
    private async Task NotifyGroupDissolved(string groupId, string groupName, List<string> clientIds, string reason)
    {
        logger.LogDebug("Notifying {ClientCount} clients about group {GroupName} (ID: {GroupId}) dissolution. Reason: {Reason}", clientIds.Count, groupName, groupId, reason);

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
            (await groupManager.GetAllGroupsAsync()).FirstOrDefault(g => g.Name == groupName)?.GroupId :
            await groupManager.GetGroupIdForConnectionAsync(Context.ConnectionId);

        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning($"Client {Context.ConnectionId} tried to extend group but is not in any group or group not found");
            return false;
        }

        var success = await groupManager.ExtendGroupWaitingTimeAsync(groupId);
        if (success)
        {
            var group = await groupManager.GetGroupInfoAsync(groupId);
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

                logger.LogDebug($"Group {group.Name} (ID: {groupId}) extended by client {Context.ConnectionId}");
            }
        }

        return success;
    }

    /// <summary>
    /// Get server status
    /// </summary>
    public async Task<ServerStatus> GetServerStatusAsync()
    {
        logger.LogDebug($"Client {Context.ConnectionId} requested server status");

        var status = new ServerStatus
        {
            Uptime = DateTime.UtcNow - state.StartTime,
            TotalConnections = state.ConnectionCount,
            GroupCount = (await groupManager.GetAllGroupsAsync()).Count(),
            ActiveBattleCount = state.BattleStates.Count
        };

        // Get group summaries
        foreach (var group in await groupManager.GetAllGroupsAsync())
        {
            status.Groups.Add(new GroupSummary
            {
                GroupId = group.GroupId,
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
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        connectionManager.RegisterConnection(connectionId, ConnectionProtocol.SignalR);

        state.IncrementConnectionCount();
        logger.LogDebug("SignalR client {ConnectionId} connected. Total connections: {Count}", connectionId, state.ConnectionCount);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var connectionRemoved = connectionManager.UnregisterConnection(connectionId);

        // Log disconnection details for debugging
        if (exception != null)
        {
            logger.LogWarning("SignalR client {ConnectionId} disconnected with exception: {ExceptionType}: {Message}",
                connectionId, exception.GetType().Name, exception.Message);
        }
        else
        {
            logger.LogInformation("SignalR client {ConnectionId} disconnected gracefully", connectionId);
        }

        if (connectionRemoved)
        {
            state.DecrementConnectionCount();

            // Handle GameLift Anywhere disconnection first (before leaving group)
            if (gameSessionManager != null)
            {
                // Check all SignalR groups this connection belongs to
                await ProcessGameLiftDisconnectionAsync(connectionId);
            }

            // Get group ID before leaving group for regular group processing
            var groupId = await groupManager.GetGroupIdForConnectionAsync(connectionId);
            logger.LogDebug("OnDisconnectedAsync: Retrieved groupId '{GroupId}' for connection {ConnectionId}", groupId, connectionId);            // Leave group if in one and notify other members
            var (leftGroup, newCount) = await groupManager.LeaveGroupAsync(connectionId);
            if (leftGroup != null)
            {
                logger.LogInformation("Client {ConnectionId} left group {GroupName} (ID: {GroupId}). Remaining members: {Count}",
                    connectionId, leftGroup.Name, leftGroup.GroupId, newCount);

                // Notify remaining group members that this client left
                var memberLeftData = new MemberLeftData
                {
                    ConnectionId = connectionId,
                    GroupId = leftGroup.GroupId,
                    GroupName = leftGroup.Name,
                    CurrentMemberCount = newCount,
                    MaxMembers = SystemDefines.MaxConnectionsPerGroup
                };
                var remainingClients = leftGroup.ClientIds.Where(id => id != connectionId);
                await Clients.Clients(remainingClients).SendAsync("MemberLeft", memberLeftData);

                logger.LogDebug("Notified {Count} remaining clients about member leaving group {GroupName}", remainingClients.Count(), leftGroup.Name);
            }

            logger.LogInformation("SignalR client {ConnectionId} fully processed disconnection. Total connections: {Count}", connectionId, state.ConnectionCount);
        }
        else
        {
            logger.LogWarning("SignalR client {ConnectionId} disconnected but was not found in connection manager", connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Process GameLift disconnection for a client across all GameSessions
    /// </summary>
    private async Task ProcessGameLiftDisconnectionAsync(string connectionId)
    {
        try
        {
            logger.LogDebug("ProcessGameLiftDisconnectionAsync: Processing GameLift disconnection for client {ConnectionId}", connectionId);

            // Get all active GameSessions and check if this client is connected
            var disconnected = await gameSessionManager!.OnPlayerDisconnectedFromAnySessionAsync(connectionId);

            if (disconnected)
            {
                logger.LogInformation("Successfully processed GameLift disconnection for client {ConnectionId}", connectionId);
            }
            else
            {
                logger.LogDebug("Client {ConnectionId} was not connected to any GameLift GameSession", connectionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing GameLift disconnection for client {ConnectionId}", connectionId);
        }
    }
}
