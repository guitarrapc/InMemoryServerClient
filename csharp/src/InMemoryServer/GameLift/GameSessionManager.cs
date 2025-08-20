using Aws.GameLift.Server.Model;
using BattleLogic.Battle;
using InMemoryServer.Services;
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
    IOptions<GameLiftOptions> options)
{
    private readonly ConcurrentDictionary<string, GameSessionInfo> _activeSessions = new();

    /// <summary>
    /// Information about an active GameSession and its associated battle
    /// </summary>
    public sealed class GameSessionInfo
    {
        public required string GameSessionId { get; init; }
        public required string GroupId { get; init; }
        public Guid? BattleId { get; set; }
        public BattleState? BattleState { get; set; }
        public GroupInfo GroupInfo { get; set; } = null!;
        public DateTime StartTime { get; init; } = DateTime.UtcNow;
        public DateTime? BattleStartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public bool IsBattleStarted { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
        public bool IsTerminating { get; set; } = false;
        public List<string> ConnectedClients { get; init; } = new();
        public int RequiredPlayers { get; init; } = 5;

        /// <summary>
        /// Check if enough players have joined to start the battle
        /// </summary>
        public bool CanStartBattle => ConnectedClients.Count >= RequiredPlayers && !IsBattleStarted && !IsCompleted;
    }

    /// <summary>
    /// Check if server can accept new GameSessions
    /// </summary>
    public bool CanAcceptNewGameSession()
    {
        var o = options.Value;
        var activeCount = _activeSessions.Values.Count(s => !s.IsCompleted);
        var canAccept = activeCount < o.Anywhere.MaxConcurrentGameSessions;

        logger.LogDebug(
            "GameSession capacity check: {ActiveSessions}/{MaxSessions}, CanAccept: {CanAccept}",
            activeCount,
            o.Anywhere.MaxConcurrentGameSessions,
            canAccept);

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
                logger.LogWarning(
                    "Cannot accept GameSession {GameSessionId}: Server at capacity ({Active}/{Max})",
                    gameSession.GameSessionId,
                    _activeSessions.Values.Count(s => !s.IsCompleted),
                    o.Anywhere.MaxConcurrentGameSessions);
                return false;
            }

            logger.LogInformation("Preparing GameSession for players: {GameSessionId}", gameSession.GameSessionId);

            // Create a group for this GameSession (initially empty, waiting for real clients)
            var groupId = $"gamelift-{gameSession.GameSessionId}";

            // Store session info in waiting state
            var sessionInfo = new GameSessionInfo
            {
                GameSessionId = gameSession.GameSessionId,
                GroupId = groupId,
                RequiredPlayers = 5
            };

            _activeSessions[gameSession.GameSessionId] = sessionInfo;

            logger.LogInformation(
                "GameSession {GameSessionId} prepared and waiting for {RequiredPlayers} players to connect. Active sessions: {ActiveCount}/{MaxCount}",
                gameSession.GameSessionId,
                sessionInfo.RequiredPlayers,
                _activeSessions.Values.Count(s => !s.IsCompleted),
                o.Anywhere.MaxConcurrentGameSessions);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare GameSession: {GameSessionId}", gameSession.GameSessionId);
            return false;
        }
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
            logger.LogDebug("GameLift Anywhere: Notified GameSessionManager about client {ConnectionId} connection to GameSession {GameSessionId}",
                connectionId, gameSessionId);
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

        logger.LogDebug("GameLift Anywhere: Notified GameSessionManager about client {ConnectionId} disconnection from GameSession {GameSessionId}",
            connectionId, gameSessionId);

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

        logger.LogInformation("Player {PlayerId} connected to GameSession {GameSessionId} ({Count}/{Required})",
            playerId, gameSessionId, sessionInfo.ConnectedClients.Count, sessionInfo.RequiredPlayers);

        // Check if we have enough players to start the battle
        if (sessionInfo.CanStartBattle)
        {
            logger.LogInformation("GameSession {GameSessionId} has enough players ({Count}/{Required}). Starting battle...",
                gameSessionId, sessionInfo.ConnectedClients.Count, sessionInfo.RequiredPlayers);

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

        sessionInfo.ConnectedClients.Remove(playerId);

        logger.LogInformation("Player {PlayerId} disconnected from GameSession {GameSessionId} ({Count}/{Required})",
            playerId, gameSessionId, sessionInfo.ConnectedClients.Count, sessionInfo.RequiredPlayers);

        // If battle hasn't started yet and we don't have enough players, continue waiting
        // If battle has started, let it continue (players are simulated)

        await Task.CompletedTask;
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
                ClientIds = new List<string>(sessionInfo.ConnectedClients)
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

                // Final cleanup if not already done
                if (!sessionInfo.IsCompleted)
                {
                    CleanupBattleMemory(sessionInfo);
                }

                // Notify GameLift of session termination
                Aws.GameLift.Server.GameLiftServerAPI.ProcessEnding();

                var activeSessions = _activeSessions.Values.Count(s => !s.IsCompleted);
                logger.LogInformation(
                    "GameSession {GameSessionId} terminated. Battle {BattleId} cleanup completed. Active sessions: {ActiveCount}",
                    gameSessionId,
                    sessionInfo.BattleId ?? Guid.Empty,
                    activeSessions);
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
    }

    /// <summary>
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
        var sessions = _activeSessions.Values.ToList();
        var active = sessions.Count(s => !s.IsCompleted);
        var completed = sessions.Count(s => s.IsCompleted);
        var total = sessions.Count;

        return (active, completed, total);
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
