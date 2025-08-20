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
        public required Guid BattleId { get; init; }
        public required BattleState BattleState { get; init; }
        public required GroupInfo GroupInfo { get; init; }
        public DateTime StartTime { get; init; } = DateTime.UtcNow;
        public DateTime? CompletionTime { get; set; }
        public bool IsCompleted { get; set; } = false;
        public bool IsTerminating { get; set; } = false;
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
    /// Start a GameSession and its associated battle
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

            logger.LogInformation("Starting GameSession: {GameSessionId}", gameSession.GameSessionId);

            // Create a virtual group for this GameSession
            var groupId = $"gamelift-{gameSession.GameSessionId}";
            var battleId = Guid.NewGuid();

            // Create services
            using var scope = serviceProvider.CreateScope();
            var groupManager = scope.ServiceProvider.GetRequiredService<IGroupManager>();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var replayWriterFactory = scope.ServiceProvider.GetRequiredService<BattleLogic.Infrastructures.BattleReplayWriter.BattleReplayWriterFactory>();

            // Create a virtual group with max players (5)
            var virtualGroup = new GroupInfo
            {
                GroupId = groupId,
                Name = $"GameLift-{gameSession.GameSessionId}",
                MaxConnections = 5,
                ConnectionCount = 5,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(2), // GameLift sessions expire after some time
                ClientIds = new List<string> { "player1", "player2", "player3", "player4", "player5" }
            };

            // Create battle context
            var battleContext = new GameLiftBattleGroupContext(groupId, 5);

            // Generate random seed for the battle
            var random = new Random();
            var seed = random.Next(1, int.MaxValue);

            // Create battle state
            var battleLogger = loggerFactory.CreateLogger<BattleState>();
            var battleState = new BattleState(battleId, seed, battleContext, battleLogger, replayWriterFactory);

            // Store session info
            var sessionInfo = new GameSessionInfo
            {
                GameSessionId = gameSession.GameSessionId,
                GroupId = groupId,
                BattleId = battleId,
                BattleState = battleState,
                GroupInfo = virtualGroup
            };

            _activeSessions[gameSession.GameSessionId] = sessionInfo;

            logger.LogInformation(
                "GameSession {GameSessionId} associated with Battle {BattleId} (Seed: {Seed}). Active sessions: {ActiveCount}/{MaxCount}",
                gameSession.GameSessionId,
                battleId,
                seed,
                _activeSessions.Values.Count(s => !s.IsCompleted),
                o.Anywhere.MaxConcurrentGameSessions);

            // Start battle asynchronously (pre-computation)
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Starting battle pre-computation for GameSession: {GameSessionId}", gameSession.GameSessionId);
                    await battleState.RunBattleAsync();

                    logger.LogInformation("Battle pre-computation completed for GameSession: {GameSessionId}", gameSession.GameSessionId);

                    // Battle is completed, mark session as ready for termination
                    sessionInfo.IsCompleted = true;
                    sessionInfo.CompletionTime = DateTime.UtcNow;

                    // Immediate memory cleanup for battle data
                    CleanupBattleMemory(sessionInfo);

                    // Schedule GameSession termination after cleanup delay
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(o.Anywhere.GameSessionCleanupDelay);
                        await TerminateGameSessionAsync(gameSession.GameSessionId);
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during battle execution for GameSession: {GameSessionId}", gameSession.GameSessionId);
                    sessionInfo.IsCompleted = true;
                    sessionInfo.CompletionTime = DateTime.UtcNow;
                    CleanupBattleMemory(sessionInfo);
                    await TerminateGameSessionAsync(gameSession.GameSessionId);
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start GameSession: {GameSessionId}", gameSession.GameSessionId);
            return false;
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
            sessionInfo.BattleState.ClearBattleData();
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
                    sessionInfo.BattleId,
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
internal sealed class GameLiftBattleGroupContext(string groupId, int connectedCount) : Shared.Contracts.IBattleGroupContext
{
    public string GroupId { get; } = groupId;
    public string Name { get; } = $"GameLift-{groupId}";
    public int MaxClients { get; } = 5;
    public int ConnectedCount { get; } = connectedCount;
    public IReadOnlyList<string> ClientIds { get; } = Enumerable.Range(1, connectedCount)
        .Select(i => $"gamelift-player-{i}")
        .ToList();
}
