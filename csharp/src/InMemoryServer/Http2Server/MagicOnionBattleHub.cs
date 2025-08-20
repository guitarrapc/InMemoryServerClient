using MagicOnion.Server.Hubs;
using Shared.Contracts.Http2Server;
using Shared.Models;
using Shared.Battle;
using Shared.Constants;
using BattleLogic.Battle;
using BattleLogic.Constants;
using BattleLogic.Infrastructures.BattleReplayWriter;
using InMemoryServer.Services;
using InMemoryServer.Models;
using InMemoryServer.GameLift;
using MessagePack;
using BattleLogic.Models;

namespace InMemoryServer.Http2Server;

/// <summary>
/// MagicOnion streaming hub implementation for real-time communication
/// </summary>
public class MagicOnionBattleHub(
    ILogger<MagicOnionBattleHub> logger,
    InMemoryState state,
    ConnectionManager connectionManager,
    InMemoryServer.Services.IGroupManager groupManager,
    CrossProtocolNotificationService notificationService,
    ILoggerFactory loggerFactory,
    BattleReplayWriterFactory replayWriterFactory,
    MagicOnionGroupService magicOnionGroupService,
    BattleCompletionService battleCompletionService,
    GameLift.GameSessionManager? gameSessionManager = null) : StreamingHubBase<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>, IMagicOnionBattleHub
{
    private static readonly object _eventSetupLock = new();
    private static bool _eventHandlersSetup = false;


    // Key-Value operations
    /// <summary>
    /// Get value by key
    /// </summary>
    public Task<string?> GetAsync(string key)
    {
        try
        {
            logger.LogDebug("MagicOnion GetAsync called for key: {Key} from context: {ContextId}", key, Context.ContextId);
            var result = state.GetValue(key);
            logger.LogDebug("MagicOnion GetAsync result for key {Key}: {Result}, returning Task", key, result ?? "null");
            var task = Task.FromResult(result);
            logger.LogDebug("MagicOnion GetAsync Task created for key {Key}, task status: {Status}", key, task.Status);
            return task;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting key {Key}", key);
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Set key-value pair
    /// </summary>
    public async Task<bool> SetAsync(string key, string value)
    {
        try
        {
            var connectionId = Context.ContextId.ToString();
            state.SetValue(key, value);

            // Notify watchers about the key change
            var watchers = state.GetWatchers(key);
            foreach (var watcherConnectionId in watchers)
            {
                // Send notification to specific watchers
                var group = await Group.AddAsync($"watcher_{watcherConnectionId}");
                group.All.OnKeyChanged(key, value);
            }

            logger.LogDebug("Key '{Key}' set by connection {ConnectionId}", key, connectionId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting key {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Delete a key
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            var connectionId = Context.ContextId.ToString();
            var deleted = state.DeleteValue(key);

            if (deleted)
            {
                // Notify watchers about the key deletion
                var watchers = state.GetWatchers(key);
                foreach (var watcherConnectionId in watchers)
                {
                    // Send notification to specific watchers
                    var group = await Group.AddAsync($"watcher_{watcherConnectionId}");
                    group.All.OnKeyDeleted(key);
                }
                logger.LogDebug("Key '{Key}' deleted by connection {ConnectionId}", key, connectionId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting key {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// List all keys matching a pattern
    /// </summary>
    public async Task<IEnumerable<string>> ListKeysAsync(string? pattern = null)
    {
        try
        {
            return state.ListKeys(pattern);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing keys with pattern {Pattern}", pattern);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Watch a key for changes
    /// </summary>
    public async Task<bool> WatchAsync(string key)
    {
        try
        {
            var connectionId = Context.ContextId.ToString();
            state.AddWatcher(connectionId, key);

            // Join a watcher group for this connection to receive notifications
            await Group.AddAsync($"watcher_{connectionId}");

            logger.LogDebug("Connection {ConnectionId} watching key '{Key}'", connectionId, key);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up watch for key {Key}", key);
            return false;
        }
    }

    // battle group operation
    /// <summary>
    /// Broadcast message to current group
    /// </summary>
    public async Task<bool> BroadcastAsync(string message)
    {
        var connectionId = Context.ContextId.ToString();

        var groupId = await groupManager.GetGroupIdForConnectionAsync(connectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("MagicOnion client {ConnectionId} tried to broadcast but is not in any group",
                connectionId);
            return false;
        }

        var group = await groupManager.GetGroupInfoAsync(groupId);
        if (group == null)
        {
            logger.LogWarning("Group {GroupId} not found for broadcast", groupId);
            return false;
        }

        logger.LogDebug("MagicOnion client {ConnectionId} broadcasting message to group {GroupId}", connectionId, groupId);

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
        var connectionId = Context.ContextId.ToString();

        // Check if this is a GameLift GameSession group
        if (GameSessionManager.IsGameLiftAnywhereGroup(groupName) && gameSessionManager != null)
        {
            return await JoinGameLiftGameSessionAsync(connectionId, groupName!);
        }

        // Regular group joining for Direct mode
        return await JoinRegularGroupAsync(connectionId, groupName);
    }

    /// <summary>
    /// Join a GameLift GameSession
    /// </summary>
    private async Task<string> JoinGameLiftGameSessionAsync(string connectionId, string groupName)
    {
        logger.LogInformation("MagicOnion client {ConnectionId} attempting to join GameLift GameSession group: {GroupName}", connectionId, groupName);

        // Use GameSessionManager to handle GameLift Anywhere connection
        var success = await gameSessionManager!.TryHandlePlayerConnectionAsync(groupName, connectionId);
        if (!success)
        {
            logger.LogWarning("Failed to connect MagicOnion client {ConnectionId} to GameLift group: {GroupName}", connectionId, groupName);
            throw new InvalidOperationException($"Cannot join GameLift group {groupName}");
        }

        // Add to MagicOnion group for communication
        magicOnionGroupService.AddClientToGroup(groupName, Context.ContextId, Client);

        logger.LogInformation("MagicOnion client {ConnectionId} successfully joined GameLift group: {GroupName}", connectionId, groupName);
        return groupName;
    }

    /// <summary>
    /// Join a regular group (Direct mode)
    /// </summary>
    private async Task<string> JoinRegularGroupAsync(string connectionId, string? groupName)
    {
        // Find or create group
        var group = await groupManager.JoinGroupAsync(connectionId, groupName);
        magicOnionGroupService.AddClientToGroup(group.GroupId, Context.ContextId, Client);

        logger.LogDebug("MagicOnion client {ConnectionId} joined group: {GroupName} (ID: {GroupId})", connectionId, group.Name, group.GroupId);

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
                logger.LogError(ex, "MagicOnion NotifyGroupAsync failed for connection {ConnectionId}, group: {GroupId}", connectionId, group.GroupId);
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
        var connectionId = Context.ContextId.ToString();
        logger.LogDebug("Client {ConnectionId} requesting group list", connectionId);
        return await groupManager.GetAllGroupsAsync();
    }

    /// <summary>
    /// Get current group info
    /// </summary>
    public async Task<GroupInfo?> GetCurrentGroupAsync()
    {
        var connectionId = Context.ContextId.ToString();

        var groupId = await groupManager.GetGroupIdForConnectionAsync(connectionId);
        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("MagicOnion client {ConnectionId} requested current group but is not in any group", connectionId);
            return null;
        }

        return await groupManager.GetGroupInfoAsync(groupId);
    }

    /// <summary>
    /// Get battle status
    /// </summary>
    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        var connectionId = Context.ContextId.ToString();
        var groupId = await groupManager.GetGroupIdForConnectionAsync(connectionId);
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
    public async Task<BattleReplayData?> GetBattleReplayAsync(Guid battleId)
    {
        var connectionId = Context.ContextId.ToString();
        logger.LogDebug("Client {ConnectionId} requested battle replay for battle: {BattleId}", connectionId, battleId);

        var replayPath = Path.Combine(BattleSystemDefines.BattleReplayDirectory, $"{battleId}.jsonl");
        if (File.Exists(replayPath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(replayPath);
                return MessagePackSerializer.Deserialize<BattleReplayData>(bytes);
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
    /// Confirm connection ready
    /// </summary>
    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        var connectionId = Context.ContextId.ToString();
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
    /// Reproduce battle
    /// </summary>
    public async Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        var connectionId = Context.ContextId.ToString();
        var seed = BattleSeed.CreateCombinedSeed(battleId, seedValue);

        logger.LogDebug("Client {ConnectionId} requesting battle reproduction - BattleId: {BattleId}, SeedValue: {SeedValue}, NumericSeed: {NumericSeed}",
            connectionId, battleId, seedValue, seed);

        // Get or create group for reproduction
        var group = await groupManager.JoinGroupAsync(connectionId, groupName);

        // Add to MagicOnion group
        magicOnionGroupService.AddClientToGroup(group.GroupId, Context.ContextId, Client);

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

                if (await Task.WhenAny(Task.Delay(50), timeoutTask) == timeoutTask)
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

        logger.LogDebug("Battle {BattleId} (Seed: {Seed}): Sending {TurnCount} turns in {ChunkCount} chunk(s)", battleId, seed, allTurnData.Count, chunks.Count);

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

        logger.LogDebug("Battle reproduction started - BattleId: {BattleId}, Seed: {Seed}", battleId, seed);
        logger.LogDebug("Group {GroupId} has {ConnectionCount} members and will start battle reproduction",
            group.GroupId, group.ConnectionCount);

        // Create and store battle state with specific battle ID and seed
        var battleLogger = loggerFactory.CreateLogger<BattleState>();
        var battle = new BattleState(battleId, seed, group, battleLogger, replayWriterFactory);
        state.BattleStates[battleId.ToString()] = battle;

        // Get the correct MagicOnion group for broadcasting
        logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Notifying all clients that connections are ready", battleId, seed);
        var connectionsReadyData = new ConnectionsReadyData { BattleId = battleId, Seed = seed };
        magicOnionGroupService.SendToAll(group.GroupId, receiver => receiver.OnConnectionsReady(connectionsReadyData));

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

                if (await Task.WhenAny(Task.Delay(50), timeoutTask) == timeoutTask)
                {
                    // 繧ｿ繧､繝繧｢繧ｦ繝育匱逕溘∫｢ｺ隱阪′謠・ｏ縺ｪ縺九▲縺・
                    var elapsed = DateTime.UtcNow - startTime;
                    var finalConfirmedCount = battle.GetConfirmedConnectionCount();
                    logger.LogWarning("Battle reproduction {BattleId} (Seed: {Seed}): Timed out after {Elapsed:F1}s waiting for client confirmations. Got {ConfirmedCount}/{TotalCount} confirmations. Proceeding anyway.", battleId, seed, elapsed.TotalSeconds, finalConfirmedCount, group.ConnectionCount);
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

            // 4. Run pre-computation (螳悟・縺ｫ繧ｵ繝ｼ繝舌・繧ｵ繧､繝峨〒險育ｮ怜ｮ御ｺ・
            logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Starting pre-computation of battle simulation", battleId, seed);
            await battle.RunBattleAsync();

            // 5. Send all battle data to clients for replay
            logger.LogDebug("Battle reproduction {BattleId} (Seed: {Seed}): Sending battle replay data to clients", battleId, seed);
            await SendBattleReplayData(group, battle, battleId, seed);

            // 6. Handle battle completion with unified service (uses configuration default)
            await battleCompletionService.HandleBattleCompletionAsync(group, battle, battleId, seed);
        });
    }

    /// <summary>
    /// Notify clients about group dissolution
    /// </summary>
    private async Task NotifyGroupDissolved(string groupId, string groupName, List<string> clientIds, string reason)
    {
        logger.LogDebug("Notifying {ClientCount} clients about group {GroupName} (ID: {GroupId}) dissolution. Reason: {Reason}",
            clientIds.Count, groupName, groupId, reason);

        var groupDissolvedData = new GroupDissolvedData
        {
            GroupId = groupId,
            GroupName = groupName,
            Reason = reason
        };

        magicOnionGroupService.SendToAll(groupId, receiver => receiver.OnGroupDissolved(groupDissolvedData));
    }

    /// <summary>
    /// Get server status information
    /// </summary>
    public async Task<ServerStatus> GetServerStatusAsync()
    {
        logger.LogDebug($"Client {Context.ContextId} requested server status");
        var groups = await groupManager.GetAllGroupsAsync();
        var status = new ServerStatus
        {
            Uptime = DateTime.UtcNow - state.StartTime,
            TotalConnections = state.ConnectionCount,
            GroupCount = groups.Count(),
            ActiveBattleCount = state.BattleStates.Count
        };

        // Get group summaries
        foreach (var group in groups)
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
    protected override ValueTask OnConnected()
    {
        var connectionId = Context.ContextId.ToString();
        connectionManager.RegisterConnection(connectionId, ConnectionProtocol.MagicOnion);

        state.ConnectionCount++;
        logger.LogDebug("MagicOnion client {ConnectionId} connected. Total connections: {Count}", connectionId, state.ConnectionCount);
        return default;
    }

    protected override ValueTask OnConnecting()
    {
        var connectionId = Context.ContextId.ToString();
        logger.LogDebug("Client {ConnectionId} connecting via MagicOnion hub", connectionId);
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
                logger.LogDebug("Group dissolution event handler set up");
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    protected override ValueTask OnDisconnected()
    {
        var connectionId = Context.ContextId.ToString();
        var connectionRemoved = connectionManager.UnregisterConnection(connectionId);

        if (connectionRemoved)
        {
            state.ConnectionCount = Math.Max(0, state.ConnectionCount - 1);

            // Handle group leaving in background task to avoid blocking OnDisconnected
            _ = Task.Run(async () =>
            {
                try
                {
                    var groupId = await groupManager.GetGroupIdForConnectionAsync(connectionId);
                    if (!string.IsNullOrEmpty(groupId))
                    {
                        // Handle GameLift Anywhere disconnection if applicable
                        if (gameSessionManager != null)
                        {
                            await gameSessionManager.TryHandlePlayerDisconnectionAsync(groupId, connectionId);
                        }

                        // Remove from MagicOnion group
                        magicOnionGroupService.RemoveClientFromGroup(groupId, Context.ContextId);

                        // Remove from GroupManager and notify other members
                        var (leftGroup, newCount) = await groupManager.LeaveGroupAsync(connectionId);
                        if (leftGroup != null)
                        {
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

                            // Send notification to remaining clients via cross-protocol service
                            await notificationService.NotifyGroupAsync(leftGroup.GroupId, remainingClients, "MemberLeft", memberLeftData);

                            logger.LogDebug("Notified {Count} remaining clients about member leaving group {GroupName}", remainingClients.Count(), leftGroup.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error handling group leaving for disconnected client {ConnectionId}", connectionId);
                }
            });

            logger.LogDebug("MagicOnion client {ConnectionId} disconnected. Total connections: {Count}", connectionId, state.ConnectionCount);
        }

        return default;
    }
}
