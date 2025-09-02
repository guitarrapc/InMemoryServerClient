using Amazon.GameLift;
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
using BattleLogic.Battle;
using Microsoft.Extensions.Options;
using Shared.GameLift;
using Shared.Models;
using System.Collections.Concurrent;

namespace InMemoryServer.GameLift;

/// <summary>
/// Manages the lifecycle of GameSessions and their associated battles
/// </summary>
public sealed class GameSessionManager(
    ILogger<GameSessionManager> logger,
    IServiceProvider serviceProvider,
    IOptions<GameLiftOptions> options,
    IAmazonGameLift gameLiftClient)
{
    private readonly ConcurrentDictionary<string, GameSessionInfo> _activeSessions = new();
    private readonly ConcurrentDictionary<string, string> _groupNameToGameSessionId = new();

    /// <summary>
    /// Information about an active GameSession and its associated battle
    /// </summary>
    public sealed class GameSessionInfo
    {
        public required string GameSessionId { get; init; }
        public required string GroupId { get; init; }
        public string? GroupName { get; set; }
        public Guid? BattleId { get; set; }
        public BattleState? BattleState { get; set; }
        public GroupInfo GroupInfo { get; set; } = null!;
        public DateTime StartTime { get; init; } = DateTime.UtcNow;
        public DateTime? BattleStartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public bool IsBattleStarted { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
        public bool IsTerminating { get; set; } = false;
        public bool IsActivated { get; set; } = false;
        public List<string> ConnectedClients { get; init; } = []; // TODO: Thread safety?
        public int RequiredPlayers { get; init; } = 5;

        private readonly Lock _lock = new();

        /// <summary>
        /// Check if enough players have joined to start the battle
        /// </summary>
        public bool CanStartBattle => ConnectedClients.Count >= RequiredPlayers && !IsBattleStarted && !IsCompleted;

        public void RemoveConnectedClient(string clientId)
        {
            lock (_lock)
            {
                ConnectedClients.Remove(clientId);
            }
        }
    }

    /// <summary>
    /// Check if server can accept new GameSessions
    /// </summary>
    public bool CanAcceptNewGameSession()
    {
        var o = options.Value;
        var activeCount = _activeSessions.Values.Count(s => !s.IsCompleted);
        var canAccept = activeCount < o.Anywhere.MaxConcurrentGameSessions;

        logger.LogDebug("GameSession capacity check: {ActiveSessions}/{MaxSessions}, CanAccept: {CanAccept}", activeCount, o.Anywhere.MaxConcurrentGameSessions, canAccept);

        return canAccept;
    }

    /// <summary>
    /// Prepare a GameSession and wait for players to connect
    /// </summary>
    public async Task<bool> StartGameSessionAsync(GameSession gameSession)
    {
        var o = options.Value;

        try
        {
            // Check capacity before starting
            if (!CanAcceptNewGameSession())
            {
                logger.LogWarning("Cannot accept GameSession {GameSessionId}: Server at capacity ({Active}/{Max})", gameSession.GameSessionId, _activeSessions.Values.Count(s => !s.IsCompleted), o.Anywhere.MaxConcurrentGameSessions);
                return false;
            }

            // Extract group name from GameSession properties
            var groupName = ExtractGroupNameFromGameSession(gameSession);

            logger.LogInformation("Preparing GameSession for players: {GameSessionId} with GroupName: {GroupName}", gameSession.GameSessionId, groupName ?? "N/A");

            // Create a group for this GameSession (initially empty, waiting for real clients)
            var groupId = $"gamelift-{gameSession.GameSessionId}";

            // Store session info in waiting state
            var sessionInfo = new GameSessionInfo
            {
                GameSessionId = gameSession.GameSessionId,
                GroupId = groupId,
                GroupName = groupName,
                RequiredPlayers = 5,
            };

            _activeSessions[gameSession.GameSessionId] = sessionInfo;

            // Register group name mapping if provided
            if (!string.IsNullOrEmpty(groupName))
            {
                _groupNameToGameSessionId[groupName] = gameSession.GameSessionId;
                logger.LogInformation("Registered GroupName mapping: {GroupName} -> {GameSessionId}", groupName, gameSession.GameSessionId);
            }

            logger.LogInformation("GameSession {GameSessionId} prepared and waiting for {RequiredPlayers} players to connect. Active sessions: {ActiveCount}/{MaxCount}", gameSession.GameSessionId, sessionInfo.RequiredPlayers, _activeSessions.Values.Count(s => !s.IsCompleted), o.Anywhere.MaxConcurrentGameSessions);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare GameSession: {GameSessionId}", gameSession.GameSessionId);
            return false;
        }
    }

    /// <summary>
    /// Extract group name from GameSession properties
    /// </summary>
    private string? ExtractGroupNameFromGameSession(GameSession gameSession)
    {
        // Use GameSession.Name as the group name (user-friendly name)
        if (!string.IsNullOrEmpty(gameSession.Name))
        {
            logger.LogDebug("Using GameSession.Name as GroupName: {GroupName}", gameSession.Name);
            return gameSession.Name;
        }

        // Fallback to GameSessionData if Name is not available
        if (!string.IsNullOrEmpty(gameSession.GameSessionData))
        {
            logger.LogDebug("Using GameSession.GameSessionData as GroupName: {GroupName}", gameSession.GameSessionData);
            return gameSession.GameSessionData;
        }

        // Generate a default group name based on GameSessionId
        var defaultGroupName = $"battle-{gameSession.GameSessionId.Substring(0, 8)}";
        logger.LogDebug("Generated default GroupName: {GroupName}", defaultGroupName);
        return defaultGroupName;
    }

    /// <summary>
    /// Handle player connection to a GameLift Anywhere group (if applicable)
    /// </summary>
    /// <param name="groupId">The group ID to check</param>
    /// <param name="connectionId">The connection ID of the player</param>
    /// <returns>True if this was a GameLift Anywhere connection, false otherwise</returns>
    public async Task<bool> TryHandlePlayerConnectionAsync(string groupId, string connectionId)
    {
        // Check if this is a GameLift Anywhere group
        if (!IsGameLiftAnywhereGroup(groupId))
        {
            return false;
        }

        var gameSessionId = ExtractGameSessionIdFromGroup(groupId);
        var connected = await OnPlayerConnectedAsync(gameSessionId, connectionId);

        if (connected)
        {
            logger.LogDebug("GameLift Anywhere: Notified GameSessionManager about client {ConnectionId} connection to GameSession {GameSessionId}", connectionId, gameSessionId);
        }

        return true;
    }

    /// <summary>
    /// Handle player disconnection from a GameLift Anywhere group (if applicable)
    /// </summary>
    /// <param name="groupId">The group ID to check</param>
    /// <param name="connectionId">The connection ID of the player</param>
    /// <returns>True if this was a GameLift Anywhere disconnection, false otherwise</returns>
    public async Task<bool> TryHandlePlayerDisconnectionAsync(string groupId, string connectionId)
    {
        // Check if this is a GameLift Anywhere group
        if (!IsGameLiftAnywhereGroup(groupId))
        {
            return false;
        }

        var gameSessionId = ExtractGameSessionIdFromGroup(groupId);
        await OnPlayerDisconnectedAsync(gameSessionId, connectionId);

        logger.LogDebug("GameLift Anywhere: Notified GameSessionManager about client {ConnectionId} disconnection from GameSession {GameSessionId}", connectionId, gameSessionId);

        return true;
    }

    /// <summary>
    /// Check if a group ID represents a GameLift Anywhere group
    /// </summary>
    /// <param name="groupId">The group ID to check</param>
    /// <returns>True if this is a GameLift Anywhere group, false otherwise</returns>
    public static bool IsGameLiftAnywhereGroup(string? groupId)
    {
        return !string.IsNullOrEmpty(groupId) && groupId.StartsWith("gamelift-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolve a group name to a GameLift GroupId if applicable
    /// </summary>
    /// <param name="groupName">The group name provided by the client</param>
    /// <returns>The resolved GroupId if this is a GameLift Anywhere group, otherwise null</returns>
    public string? TryResolveGameLiftGroupId(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            logger.LogDebug("TryResolveGameLiftGroupId: GroupName is null or empty");
            return null;
        }

        logger.LogDebug("TryResolveGameLiftGroupId: Looking for group name '{GroupName}' in mapping. Total mappings: {MappingCount}",
            groupName, _groupNameToGameSessionId.Count);

        // Log all current mappings for debugging
        foreach (var mapping in _groupNameToGameSessionId)
        {
            logger.LogDebug("Available mapping: {GroupName} -> {GameSessionId}", mapping.Key, mapping.Value);
        }

        // Try to find a GameSession with this group name
        if (_groupNameToGameSessionId.TryGetValue(groupName, out var gameSessionId))
        {
            logger.LogDebug("Successfully resolved GroupName {GroupName} to GameSessionId: {GameSessionId}", groupName, gameSessionId);
            return gameSessionId;
        }

        logger.LogDebug("Could not resolve GroupName {GroupName} - no matching GameSession found", groupName);
        return null;
    }

    /// <summary>
    /// Find or create a GameSession for the given group name
    /// </summary>
    /// <param name="groupName">The group name requested by the client</param>
    /// <returns>The GroupId if a suitable GameSession exists or was created, null otherwise</returns>
    public async Task<string?> FindOrCreateGameSessionForGroupAsync(string groupName)
    {
        // First, try to resolve existing GameSession
        var existingGroupId = TryResolveGameLiftGroupId(groupName);
        if (!string.IsNullOrEmpty(existingGroupId))
        {
            var gameSessionId = ExtractGameSessionIdFromGroup(existingGroupId);
            if (_activeSessions.TryGetValue(gameSessionId, out var sessionInfo) &&
                !sessionInfo.IsCompleted &&
                sessionInfo.ConnectedClients.Count < sessionInfo.RequiredPlayers)
            {
                logger.LogInformation("Found existing GameSession {GameSessionId} for GroupName: {GroupName}", gameSessionId, groupName);
                return existingGroupId;
            }
        }

        // Create new GameSession on-demand
        logger.LogInformation("Creating new GameSession on-demand for GroupName: {GroupName}", groupName);
        return await CreateGameSessionOnDemandAsync(groupName);
    }

    /// <summary>
    /// Create a new GameSession on-demand for client requests
    /// </summary>
    /// <param name="groupName">The group name to create GameSession for</param>
    /// <returns>The GroupId for the created GameSession, null if creation failed</returns>
    public async Task<string?> CreateGameSessionOnDemandAsync(string groupName)
    {
        try
        {
            // Check capacity
            if (!CanAcceptNewGameSession())
            {
                logger.LogWarning("Cannot create GameSession for GroupName {GroupName}: Server at capacity", groupName);
                return null;
            }

            var o = options.Value;

            // Create GameSession request
            var request = CreateGameSessionRequest.ForAutoBattle(o.Anywhere.FleetId, groupName);

            logger.LogInformation("Creating GameSession for GroupName: {GroupName} on Fleet: {FleetId}", groupName, request.FleetId);

            // Call GameLift API to create GameSession
            var response = await gameLiftClient.CreateGameSessionAsync(new Amazon.GameLift.Model.CreateGameSessionRequest
            {
                FleetId = request.FleetId,
                MaximumPlayerSessionCount = request.MaxPlayers,
                Name = request.Name,
                GameSessionData = request.GameSessionData,
                Location = o.Anywhere.CustomLocation // Required for GameLift Anywhere
            });

            if (response.GameSession != null)
            {
                logger.LogInformation("Successfully created GameSession {GameSessionId} for GroupName: {GroupName}",
                    response.GameSession.GameSessionId, groupName);

                // Return the group ID that will be created when OnStartGameSession is called
                return $"gamelift-{response.GameSession.GameSessionId}";
            }
            else
            {
                logger.LogError("Failed to create GameSession for GroupName: {GroupName} - No GameSession in response", groupName);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating GameSession for GroupName: {GroupName}", groupName);
            return null;
        }
    }

    /// <summary>
    /// Extract GameSession ID from a GameLift group ID
    /// </summary>
    /// <param name="groupId">The GameLift group ID (e.g., "gamelift-12345")</param>
    /// <returns>The GameSession ID (e.g., "12345")</returns>
    private static string ExtractGameSessionIdFromGroup(string groupId)
    {
        return groupId.Replace("gamelift-", "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Handle player connection to a GameSession
    /// </summary>
    public async Task<bool> OnPlayerConnectedAsync(string gameSessionId, string playerId)
    {
        if (!_activeSessions.TryGetValue(gameSessionId, out var sessionInfo))
        {
            logger.LogWarning("Player {PlayerId} tried to connect to unknown GameSession: {GameSessionId}", playerId, gameSessionId);
            return false;
        }

        if (sessionInfo.IsCompleted || sessionInfo.IsTerminating)
        {
            logger.LogWarning("Player {PlayerId} tried to connect to completed GameSession: {GameSessionId}", playerId, gameSessionId);
            return false;
        }

        if (sessionInfo.ConnectedClients.Contains(playerId))
        {
            logger.LogWarning("Player {PlayerId} is already connected to GameSession: {GameSessionId}", playerId, gameSessionId);
            return true;
        }

        if (sessionInfo.ConnectedClients.Count >= sessionInfo.RequiredPlayers)
        {
            logger.LogWarning("GameSession {GameSessionId} is already full ({Count}/{Required})",
                gameSessionId, sessionInfo.ConnectedClients.Count, sessionInfo.RequiredPlayers);
            return false;
        }

        // Add player to the session
        sessionInfo.ConnectedClients.Add(playerId);

        // Activate GameSession on first player connection
        if (!sessionInfo.IsActivated)
        {
            try
            {
                logger.LogInformation("First player connecting to GameSession {GameSessionId}, activating session", gameSessionId);
                GameLiftServerAPI.ActivateGameSession();
                sessionInfo.IsActivated = true;
                logger.LogInformation("GameSession {GameSessionId} activated successfully", gameSessionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to activate GameSession {GameSessionId}", gameSessionId);
                sessionInfo.ConnectedClients.Remove(playerId);
                return false;
            }
        }

        logger.LogInformation("Player {PlayerId} connected to GameSession {GameSessionId} ({Count}/{Required})", playerId, gameSessionId, sessionInfo.ConnectedClients.Count, sessionInfo.RequiredPlayers);

        // Check if we have enough players to start the battle
        if (sessionInfo.CanStartBattle)
        {
            logger.LogInformation("GameSession {GameSessionId} has enough players ({Count}/{Required}). Starting battle...", gameSessionId, sessionInfo.ConnectedClients.Count, sessionInfo.RequiredPlayers);

            await StartBattleForGameSessionAsync(sessionInfo);
        }

        return true;
    }

    /// <summary>
    /// Handle player disconnection from a GameSession
    /// </summary>
    public async Task OnPlayerDisconnectedAsync(string gameSessionId, string playerId)
    {
        if (!_activeSessions.TryGetValue(gameSessionId, out var sessionInfo))
        {
            logger.LogDebug("Player {PlayerId} disconnected from unknown GameSession: {GameSessionId}", playerId, gameSessionId);
            return;
        }

        sessionInfo.RemoveConnectedClient(playerId);

        logger.LogInformation("Player {PlayerId} disconnected from GameSession {GameSessionId} ({Count}/{Required})",
            playerId, gameSessionId, sessionInfo.ConnectedClients.Count, sessionInfo.RequiredPlayers);

        // Check if all players have disconnected
        if (sessionInfo.ConnectedClients.Count == 0)
        {
            logger.LogInformation("All players have disconnected from GameSession {GameSessionId}. Terminating GameSession...", gameSessionId);

            // Mark as completed regardless of battle state since no players remain
            sessionInfo.IsCompleted = true;
            sessionInfo.CompletionTime = DateTime.UtcNow;

            // If battle hasn't started yet, terminate immediately
            if (!sessionInfo.IsBattleStarted)
            {
                logger.LogInformation("GameSession {GameSessionId} terminated before battle start due to no players", gameSessionId);
                await TerminateGameSessionAsync(gameSessionId);
                return;
            }

            // If battle has started, clean up battle memory immediately since no clients are connected
            CleanupBattleMemory(sessionInfo);

            // Schedule immediate GameSession termination
            _ = Task.Run(async () => await TerminateGameSessionAsync(gameSessionId));
        }

        // If battle hasn't started yet and we don't have enough players, continue waiting
        // If battle has started, let it continue (players are simulated)
    }

    /// <summary>
    /// Handle player disconnection from any GameSession they might be connected to
    /// </summary>
    public async Task<bool> OnPlayerDisconnectedFromAnySessionAsync(string playerId)
    {
        var disconnectedFromAnySession = false;

        // Check all active sessions to see if this player is connected
        var sessionsToCheck = _activeSessions.Values.ToList();

        foreach (var sessionInfo in sessionsToCheck)
        {
            if (sessionInfo.ConnectedClients.Contains(playerId))
            {
                logger.LogDebug("Found player {PlayerId} in GameSession {GameSessionId}, disconnecting...",
                    playerId, sessionInfo.GameSessionId);

                await OnPlayerDisconnectedAsync(sessionInfo.GameSessionId, playerId);
                disconnectedFromAnySession = true;
            }
        }

        return disconnectedFromAnySession;
    }

    /// <summary>
    /// Start battle for a GameSession once enough players have connected
    /// </summary>
    private async Task StartBattleForGameSessionAsync(GameSessionInfo sessionInfo)
    {
        try
        {
            sessionInfo.IsBattleStarted = true;
            sessionInfo.BattleStartTime = DateTime.UtcNow;
            sessionInfo.BattleId = Guid.NewGuid();

            // Create services
            using var scope = serviceProvider.CreateScope();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var replayWriterFactory = scope.ServiceProvider.GetRequiredService<BattleLogic.Infrastructures.BattleReplayWriter.BattleReplayWriterFactory>();

            // Create actual group with connected players
            var groupInfo = new GroupInfo
            {
                GroupId = sessionInfo.GroupId,
                Name = $"GameLift-{sessionInfo.GameSessionId}",
                MaxConnections = sessionInfo.RequiredPlayers,
                ConnectionCount = sessionInfo.ConnectedClients.Count,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                ClientIds = new List<string>(sessionInfo.ConnectedClients),
            };

            sessionInfo.GroupInfo = groupInfo;

            // Create battle context
            var battleContext = new GameLiftBattleGroupContext(
                sessionInfo.GroupId,
                sessionInfo.ConnectedClients.Count,
                sessionInfo.ConnectedClients);

            // Generate random seed for the battle
            var random = new Random();
            var seed = random.Next(1, int.MaxValue);

            // Create battle state
            var battleLogger = loggerFactory.CreateLogger<BattleState>();
            var battleState = new BattleState(sessionInfo.BattleId.Value, seed, battleContext, battleLogger, replayWriterFactory);
            sessionInfo.BattleState = battleState;

            logger.LogInformation(
                "Starting battle for GameSession {GameSessionId} with Battle {BattleId} (Seed: {Seed}) and players: [{Players}]",
                sessionInfo.GameSessionId,
                sessionInfo.BattleId,
                seed,
                string.Join(", ", sessionInfo.ConnectedClients));

            // Start battle asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Running battle for GameSession: {GameSessionId}", sessionInfo.GameSessionId);
                    await battleState.RunBattleAsync();

                    logger.LogInformation("Battle completed for GameSession: {GameSessionId}", sessionInfo.GameSessionId);

                    // Battle is completed, mark session as ready for termination
                    sessionInfo.IsCompleted = true;
                    sessionInfo.CompletionTime = DateTime.UtcNow;

                    // Immediate memory cleanup for battle data
                    CleanupBattleMemory(sessionInfo);

                    // Schedule GameSession termination after cleanup delay
                    var o = options.Value;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(o.Anywhere.GameSessionCleanupDelay);
                        await TerminateGameSessionAsync(sessionInfo.GameSessionId);
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during battle execution for GameSession: {GameSessionId}", sessionInfo.GameSessionId);
                    sessionInfo.IsCompleted = true;
                    sessionInfo.CompletionTime = DateTime.UtcNow;
                    CleanupBattleMemory(sessionInfo);
                    await TerminateGameSessionAsync(sessionInfo.GameSessionId);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start battle for GameSession: {GameSessionId}", sessionInfo.GameSessionId);
            sessionInfo.IsCompleted = true;
            sessionInfo.CompletionTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Clean up battle memory immediately after completion
    /// </summary>
    private void CleanupBattleMemory(GameSessionInfo sessionInfo)
    {
        try
        {
            // Clear battle data to free memory immediately
            sessionInfo.BattleState?.ClearBattleData();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during battle memory cleanup for GameSession: {GameSessionId}", sessionInfo.GameSessionId);
        }
    }

    /// <summary>
    /// Terminate a GameSession and cleanup associated resources
    /// </summary>
    public async Task TerminateGameSessionAsync(string gameSessionId)
    {
        try
        {
            if (_activeSessions.TryRemove(gameSessionId, out var sessionInfo))
            {
                if (sessionInfo.IsTerminating)
                {
                    logger.LogDebug("GameSession {GameSessionId} is already terminating", gameSessionId);
                    return;
                }

                sessionInfo.IsTerminating = true;
                logger.LogInformation("Terminating GameSession: {GameSessionId}", gameSessionId);

                // Remove group name mapping
                if (!string.IsNullOrEmpty(sessionInfo.GroupName) && _groupNameToGameSessionId.TryRemove(sessionInfo.GroupName, out _))
                {
                    logger.LogDebug("Removed GroupName mapping for: {GroupName}", sessionInfo.GroupName);
                }

                // Final cleanup if not already done
                if (!sessionInfo.IsCompleted)
                {
                    CleanupBattleMemory(sessionInfo);
                }

                // For GameLift Anywhere, clean up local player tracking
                if (sessionInfo.ConnectedClients.Count > 0)
                {
                    logger.LogInformation("Clearing {PlayerCount} remaining player connections from GameSession {GameSessionId}",
                        sessionInfo.ConnectedClients.Count, gameSessionId);
                    sessionInfo.ConnectedClients.Clear();
                }

                logger.LogInformation("GameSession {GameSessionId} resources cleaned up locally.", gameSessionId);

                // Continue with local cleanup
                var activeSessions = _activeSessions.Values.Count(s => !s.IsCompleted);
                logger.LogInformation("GameSession {GameSessionId} terminated. Battle {BattleId} cleanup completed. Active sessions: {ActiveCount}", gameSessionId, sessionInfo.BattleId ?? Guid.Empty, activeSessions);
            }
            else
            {
                logger.LogWarning("Attempted to terminate unknown GameSession: {GameSessionId}", gameSessionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error terminating GameSession: {GameSessionId}", gameSessionId);
        }

        await Task.CompletedTask;
    }    /// <summary>
         /// Get information about an active GameSession
         /// </summary>
    public GameSessionInfo? GetGameSessionInfo(string gameSessionId)
    {
        return _activeSessions.TryGetValue(gameSessionId, out var info) ? info : null;
    }

    /// <summary>
    /// Get all active GameSessions
    /// </summary>
    public IReadOnlyCollection<GameSessionInfo> GetActiveGameSessions()
    {
        return _activeSessions.Values.ToList();
    }

    /// <summary>
    /// Get GameSession statistics
    /// </summary>
    public (int Active, int Completed, int Total) GetGameSessionStats()
    {
        var sessions = GetActiveGameSessions();
        var active = sessions.Count(s => !s.IsCompleted);
        var completed = sessions.Count(s => s.IsCompleted);
        var total = sessions.Count;

        return (active, completed, total);
    }

    /// <summary>
    /// Get all registered group name mappings
    /// </summary>
    public IReadOnlyDictionary<string, string> GetGroupNameMappings()
    {
        return _groupNameToGameSessionId.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Find GameSession by group name
    /// </summary>
    public GameSessionInfo? FindGameSessionByGroupName(string groupName)
    {
        if (_groupNameToGameSessionId.TryGetValue(groupName, out var gameSessionId))
        {
            return GetGameSessionInfo(gameSessionId);
        }
        return null;
    }

    /// <summary>
    /// Cleanup completed GameSessions that have been idle too long
    /// </summary>
    public async Task CleanupIdleGameSessionsAsync()
    {
        var o = options.Value;
        var idleThreshold = DateTime.UtcNow - o.Anywhere.GameSessionIdleTimeout;
        var idleSessions = _activeSessions.Values
            .Where(s => s.IsCompleted &&
                       s.CompletionTime.HasValue &&
                       s.CompletionTime.Value < idleThreshold &&
                       !s.IsTerminating);

        foreach (var session in idleSessions)
        {
            logger.LogInformation("Cleaning up idle GameSession: {GameSessionId}", session.GameSessionId);
            await TerminateGameSessionAsync(session.GameSessionId);
        }
    }

    /// <summary>
    /// Cleanup all GameSessions (called during shutdown)
    /// </summary>
    public async Task CleanupAllGameSessionsAsync()
    {
        logger.LogInformation("Cleaning up {SessionCount} GameSessions during shutdown", _activeSessions.Keys.Count);

        foreach (var sessionId in _activeSessions.Keys)
        {
            await TerminateGameSessionAsync(sessionId);
        }

        // Clear all mappings
        _groupNameToGameSessionId.Clear();
        logger.LogDebug("Cleared all GroupName mappings");
    }

    /// <summary>
    /// Convert a connection ID to GameLift-compatible Player Session ID format
    /// GameLift requires PlayerSessionId to match pattern: ^[a-zA-Z0-9.-]+$
    /// </summary>
    /// <param name="connectionId">Original connection ID (may contain underscores or other invalid characters)</param>
    /// <returns>GameLift-compatible Player Session ID</returns>
    private static string ConvertToGameLiftPlayerId(string connectionId)
    {
        // Replace underscores and other invalid characters with hyphens
        // Keep only alphanumeric characters, dots, and hyphens
        var converted = System.Text.RegularExpressions.Regex.Replace(connectionId, "[^a-zA-Z0-9.-]", "-");

        // Ensure the result is not empty and doesn't start/end with invalid characters
        if (string.IsNullOrEmpty(converted))
        {
            converted = "player-session";
        }

        // Remove leading/trailing hyphens or dots if any
        converted = converted.Trim('-', '.');

        // Ensure minimum length and add prefix if needed
        if (converted.Length < 3)
        {
            converted = $"player-{converted}";
        }

        return converted;
    }

    /// <summary>
    /// Create or find a GameSession for the specified group name (server-side API)
    /// </summary>
    /// <param name="groupName">Group name to create or find GameSession for</param>
    /// <returns>GameSession information if successful, null otherwise</returns>
    public async Task<Shared.GameLift.GameSessionInfo?> CreateOrFindGameSessionAsync(string groupName)
    {
        logger.LogInformation("Creating or finding GameSession for group: {GroupName}", groupName);

        try
        {
            // First, check if we already have a GameSession for this group name
            if (_groupNameToGameSessionId.TryGetValue(groupName, out var existingGameSessionId) &&
                _activeSessions.TryGetValue(existingGameSessionId, out var existingSession) &&
                !existingSession.IsCompleted &&
                !existingSession.IsTerminating)
            {
                logger.LogInformation("Found existing GameSession for group {GroupName}: {GameSessionId}", groupName, existingGameSessionId);

                return new Shared.GameLift.GameSessionInfo
                {
                    GameSessionId = existingSession.GameSessionId,
                    FleetId = options.Value.Anywhere.FleetId,
                    Name = groupName,
                    Status = existingSession.IsActivated ? Shared.GameLift.GameSessionStatus.Active : Shared.GameLift.GameSessionStatus.Activating,
                    CurrentPlayerCount = existingSession.ConnectedClients.Count,
                    MaxPlayers = existingSession.RequiredPlayers,
                    Address = "localhost", // This server
                    Port = GetServerPort(),
                    GameSessionData = groupName,
                    CreationTime = existingSession.StartTime
                };
            }

            // Create new GameSession via AWS GameLift API
            logger.LogInformation("Creating new GameSession for group: {GroupName}", groupName);

            var createRequest = new Amazon.GameLift.Model.CreateGameSessionRequest
            {
                FleetId = options.Value.Anywhere.FleetId,
                MaximumPlayerSessionCount = 5,
                Name = groupName,
                Location = options.Value.Anywhere.CustomLocation,
                GameSessionData = groupName
            };

            var response = await gameLiftClient.CreateGameSessionAsync(createRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK && response.GameSession != null)
            {
                var gameSession = response.GameSession;
                logger.LogInformation("Successfully created GameSession: {GameSessionId} for group: {GroupName}", gameSession.GameSessionId, groupName);

                // Register the new session in our tracking
                var sessionInfo = new GameSessionInfo
                {
                    GameSessionId = gameSession.GameSessionId,
                    GroupId = $"gamelift-{gameSession.GameSessionId}",
                    GroupName = groupName,
                    RequiredPlayers = gameSession.MaximumPlayerSessionCount ?? 5,
                };

                _activeSessions[gameSession.GameSessionId] = sessionInfo;
                _groupNameToGameSessionId[groupName] = gameSession.GameSessionId;

                return new Shared.GameLift.GameSessionInfo
                {
                    GameSessionId = gameSession.GameSessionId,
                    FleetId = gameSession.FleetId,
                    Name = gameSession.Name ?? groupName,
                    Status = gameSession.Status?.Value ?? Shared.GameLift.GameSessionStatus.Activating,
                    CurrentPlayerCount = gameSession.CurrentPlayerSessionCount ?? 0,
                    MaxPlayers = gameSession.MaximumPlayerSessionCount ?? 5,
                    Address = gameSession.DnsName ?? gameSession.IpAddress ?? "localhost",
                    Port = gameSession.Port ?? GetServerPort(),
                    GameSessionData = gameSession.GameSessionData,
                    CreationTime = gameSession.CreationTime ?? DateTime.UtcNow
                };
            }
            else
            {
                logger.LogWarning("Failed to create GameSession for group {GroupName}: HTTP {StatusCode}", groupName, response.HttpStatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating or finding GameSession for group: {GroupName}", groupName);
            return null;
        }
    }

    /// <summary>
    /// Create a PlayerSession via AWS GameLift API
    /// </summary>
    /// <param name="gameSessionId">GameSession ID to join</param>
    /// <param name="playerId">Player ID</param>
    /// <returns>PlayerSession information if successful, null otherwise</returns>
    public async Task<Shared.GameLift.PlayerSessionInfo?> CreatePlayerSessionAsync(string gameSessionId, string playerId)
    {
        logger.LogInformation("Creating PlayerSession for GameSession: {GameSessionId}, Player: {PlayerId}", gameSessionId, playerId);

        try
        {
            var createRequest = new Amazon.GameLift.Model.CreatePlayerSessionRequest
            {
                GameSessionId = gameSessionId,
                PlayerId = playerId
            };

            var response = await gameLiftClient.CreatePlayerSessionAsync(createRequest);

            if (response.PlayerSession != null)
            {
                var playerSession = response.PlayerSession;
                logger.LogInformation("Successfully created PlayerSession: {PlayerSessionId} for Player: {PlayerId}", playerSession.PlayerSessionId, playerId);

                return new Shared.GameLift.PlayerSessionInfo
                {
                    PlayerSessionId = playerSession.PlayerSessionId,
                    PlayerId = playerSession.PlayerId,
                    GameSessionId = playerSession.GameSessionId,
                    Status = playerSession.Status?.Value ?? Shared.GameLift.PlayerSessionStatus.Reserved,
                    CreationTime = playerSession.CreationTime ?? DateTime.UtcNow,
                    IpAddress = playerSession.IpAddress,
                    Port = playerSession.Port ?? 0
                };
            }
            else
            {
                logger.LogWarning("Failed to create PlayerSession for GameSession {GameSessionId}, Player: {PlayerId}", gameSessionId, playerId);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating PlayerSession for GameSession: {GameSessionId}, Player: {PlayerId}", gameSessionId, playerId);
            return null;
        }
    }

    /// <summary>
    /// Get the connection endpoint for this server
    /// </summary>
    /// <returns>Server connection endpoint</returns>
    public string GetServerConnectionEndpoint()
    {
        var port = GetServerPort();
        var scheme = port == 443 ? "https" : "http";
        return $"{scheme}://localhost:{port}";
    }

    /// <summary>
    /// Get the server port from configuration or default
    /// </summary>
    /// <returns>Server port number</returns>
    private int GetServerPort()
    {
        // Try to get port from environment or configuration
        if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var envPort))
            return envPort;

        if (int.TryParse(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(':').LastOrDefault()?.TrimEnd('/'), out var aspnetPort))
            return aspnetPort;

        // Default port for development
        return 5000;
    }
}

/// <summary>
/// Battle group context for GameLift-initiated battles
/// </summary>
internal sealed class GameLiftBattleGroupContext : Shared.Contracts.IBattleGroupContext
{
    public string GroupId { get; }
    public string Name { get; }
    public int MaxClients { get; }
    public int ConnectedCount { get; }
    public IReadOnlyList<string> ClientIds { get; }

    public GameLiftBattleGroupContext(string groupId, int connectedCount, IReadOnlyList<string> clientIds)
    {
        GroupId = groupId;
        Name = $"GameLift-{groupId}";
        MaxClients = 5;
        ConnectedCount = connectedCount;
        ClientIds = clientIds;
    }

    public GameLiftBattleGroupContext(string groupId, int connectedCount)
        : this(groupId, connectedCount, Enumerable.Range(1, connectedCount)
            .Select(i => $"gamelift-player-{i}")
            .ToList())
    {
    }
}
