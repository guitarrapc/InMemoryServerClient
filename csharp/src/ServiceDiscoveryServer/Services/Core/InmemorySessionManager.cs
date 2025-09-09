namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// In-memory session management service
/// </summary>
public sealed class InmemorySessionManager(ILogger<InmemorySessionManager> logger, IOptions<ServiceDiscoveryOptions> options, IBattleServerRegistry serverRegistry, IBattleServerNotifier battleServerNotifier) : ISessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly ConcurrentDictionary<string, List<string>> _groupSessions = new();
    private readonly ConcurrentDictionary<string, int> _sessionPlayerCounts = new(); // スレッドセーフなプレイヤー数管理
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request)
    {
        try
        {
            await _semaphore.WaitAsync();

            // Check if session already exists for this group
            if (_groupSessions.TryGetValue(request.GroupName, out var existingSessions))
            {
                foreach (var sessionId in existingSessions.ToList())
                {
                    if (_sessions.TryGetValue(sessionId, out var session) &&
                        session.Status == SessionStatus.Active)
                    {
                        var currentPlayerCount = GetCurrentPlayerCount(sessionId);
                        if (currentPlayerCount < session.MaxPlayers)
                        {
                            // Found existing session with available slots
                            var connectionInfo = await GetConnectionInfoForSessionAsync(session);
                            if (connectionInfo.HasValue)
                            {
                                // Increment current players count using thread-safe operation
                                var newPlayerCount = IncrementPlayerCount(sessionId);

                                logger.LogInformation("Found existing session {SessionId} for group {GroupName} (CurrentPlayers: {CurrentPlayers}/{MaxPlayers})",
                                    sessionId, request.GroupName, newPlayerCount, session.MaxPlayers);

                                // Check if session is now full and notify BattleServer
                                if (newPlayerCount >= session.MaxPlayers)
                                {
                                    logger.LogInformation("Session {SessionId} is now full, notifying BattleServer {ServerId}",
                                        sessionId, session.AssignedServerId);
                                    _ = Task.Run(async () => await battleServerNotifier.NotifyBattleReadyAsync(session.AssignedServerId, session));
                                }

                                return new SessionCreationResponse
                                {
                                    IsSuccess = true,
                                    Session = session,
                                    ConnectionInfo = connectionInfo
                                };
                            }
                        }
                    }
                }
            }

            // Create new session
            var availableServer = await serverRegistry.GetAvailableServerAsync();
            if (availableServer is null)
            {
                logger.LogWarning("No available battle servers for group {GroupName}", request.GroupName);
                return new SessionCreationResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "No available battle servers"
                };
            }

            var newSessionId = Guid.CreateVersion7().ToString();
            var newSession = new SessionInfo
            {
                SessionId = newSessionId,
                GroupName = request.GroupName,
                Mode = request.Mode,
                Status = SessionStatus.Creating,
                AssignedServerId = availableServer.Value.ServerId,
                CurrentPlayers = 0, // プレイヤー数は別途管理
                MaxPlayers = request.MaxPlayers,
                CreatedAt = DateTime.UtcNow
            };

            _sessions[newSessionId] = newSession;
            _groupSessions.AddOrUpdate(request.GroupName,
                [newSessionId],
                (key, existing) => [.. existing, newSessionId]);

            // Initialize player count using thread-safe operation
            var initialPlayerCount = IncrementPlayerCount(newSessionId);

            var newConnectionInfo = new BattleServerConnectionInfo
            {
                ServerId = availableServer.Value.ServerId,
                Address = availableServer.Value.Address,
                SignalRPort = availableServer.Value.SignalRPort,
                MagicOnionPort = availableServer.Value.MagicOnionPort,
                SupportedTypes = BattleServerConnectionType.Both
            };

            // Update session status to Active
            var activeSession = newSession with { Status = SessionStatus.Active };
            _sessions[newSessionId] = activeSession;

            logger.LogInformation("Created new session {SessionId} for group {GroupName} on server {ServerId} (CurrentPlayers: {CurrentPlayers}/{MaxPlayers})",
                newSessionId, request.GroupName, availableServer.Value.ServerId, initialPlayerCount, activeSession.MaxPlayers);

            return new SessionCreationResponse
            {
                IsSuccess = true,
                Session = activeSession,
                ConnectionInfo = newConnectionInfo
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create or find session for group {GroupName}", request.GroupName);
            return new SessionCreationResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult<SessionInfo?>(session);
    }

    public Task<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync()
    {
        var activeSessions = _sessions.Values
            .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.InBattle)
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionInfo>>(activeSessions);
    }

    public Task<bool> TerminateSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(false);
        }

        var terminatedSession = session with
        {
            Status = SessionStatus.Terminated,
            CompletedAt = DateTime.UtcNow
        };

        _sessions[sessionId] = terminatedSession;

        // Remove player count information
        _sessionPlayerCounts.TryRemove(sessionId, out _);

        // Remove from group sessions
        if (_groupSessions.TryGetValue(session.GroupName, out var groupSessions))
        {
            var updatedList = groupSessions.Where(id => id != sessionId).ToList();
            if (updatedList.Count == 0)
            {
                _groupSessions.TryRemove(session.GroupName, out _);
            }
            else
            {
                _groupSessions[session.GroupName] = updatedList;
            }
        }

        logger.LogInformation("Terminated session {SessionId} for group {GroupName}",
            sessionId, session.GroupName);

        return Task.FromResult(true);
    }

    public Task<bool> UpdateSessionStatusAsync(string sessionId, SessionStatus status)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(false);
        }

        var updatedSession = session with { Status = status };
        if (status == SessionStatus.Completed || status == SessionStatus.Terminated)
        {
            updatedSession = updatedSession with { CompletedAt = DateTime.UtcNow };
        }

        _sessions[sessionId] = updatedSession;

        logger.LogDebug("Updated session {SessionId} status to {Status}", sessionId, status);

        return Task.FromResult(true);
    }

    public Task<bool> UpdateSessionPlayerCountAsync(string sessionId, int playerCount)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(false);
        }

        // Update player count using thread-safe operation
        SetPlayerCount(sessionId, playerCount);

        logger.LogDebug("Updated session {SessionId} player count to {PlayerCount}", sessionId, playerCount);

        return Task.FromResult(true);
    }

    public Task<bool> RemovePlayerFromSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(false);
        }

        // Use thread-safe decrement operation
        var newPlayerCount = DecrementPlayerCount(sessionId);

        logger.LogDebug("Removed player from session {SessionId}, CurrentPlayers: {CurrentPlayers}",
            sessionId, newPlayerCount);

        return Task.FromResult(true);
    }

    public Task<PlayerCountInfo?> GetPlayerCountAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult<PlayerCountInfo?>(null);
        }

        // Get current player count using thread-safe operation
        var currentPlayers = GetCurrentPlayerCount(sessionId);

        var playerCountInfo = new PlayerCountInfo(
            sessionId: session.SessionId,
            currentPlayers: currentPlayers,
            maxPlayers: session.MaxPlayers,
            lastUpdated: DateTime.UtcNow);

        return Task.FromResult<PlayerCountInfo?>(playerCountInfo);
    }

    public Task<bool> NotifyBattleStartedAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            logger.LogWarning("Session {SessionId} not found for battle started notification", sessionId);
            return Task.FromResult(false);
        }

        var updatedSession = session with { Status = SessionStatus.InBattle };
        _sessions[sessionId] = updatedSession;

        logger.LogInformation("Session {SessionId} battle started", sessionId);
        return Task.FromResult(true);
    }

    public Task<bool> NotifyBattleCompletedAsync(string sessionId, BattleResult result)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            logger.LogWarning("Session {SessionId} not found for battle completed notification", sessionId);
            return Task.FromResult(false);
        }

        var updatedSession = session with
        {
            Status = SessionStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        _sessions[sessionId] = updatedSession;

        logger.LogInformation("Session {SessionId} battle completed with outcome {Outcome}", sessionId, result.Outcome);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Cleanup expired sessions (called by SessionCleanupService)
    /// </summary>
    public void CleanupExpiredSessions()
    {
        try
        {
            var timeoutMinutes = options.Value.Session.SessionTimeoutMinutes;
            var cutoffTime = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
            var expiredSessions = new List<string>();

            foreach (var (sessionId, session) in _sessions)
            {
                if (session.CreatedAt < cutoffTime &&
                    (session.Status == SessionStatus.Completed || session.Status == SessionStatus.Terminated))
                {
                    expiredSessions.Add(sessionId);
                }
            }

            foreach (var sessionId in expiredSessions)
            {
                if (_sessions.TryRemove(sessionId, out var session))
                {
                    // Remove player count information
                    _sessionPlayerCounts.TryRemove(sessionId, out _);

                    // Remove from group sessions
                    if (_groupSessions.TryGetValue(session.GroupName, out var groupSessions))
                    {
                        var updatedList = groupSessions.Where(id => id != sessionId).ToList();
                        if (updatedList.Count == 0)
                        {
                            _groupSessions.TryRemove(session.GroupName, out _);
                        }
                        else
                        {
                            _groupSessions[session.GroupName] = updatedList;
                        }
                    }

                    logger.LogDebug("Cleaned up expired session {SessionId} for group {GroupName}",
                        sessionId, session.GroupName);
                }
            }

            if (expiredSessions.Count > 0)
            {
                logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during session cleanup");
        }
    }

    private async Task<BattleServerConnectionInfo?> GetConnectionInfoForSessionAsync(SessionInfo session)
    {
        var serverInfo = await serverRegistry.GetServerInfoAsync(session.AssignedServerId);
        if (serverInfo is null)
        {
            return null;
        }

        return new BattleServerConnectionInfo
        {
            ServerId = serverInfo.Value.ServerId,
            Address = serverInfo.Value.Address,
            SignalRPort = serverInfo.Value.SignalRPort,
            MagicOnionPort = serverInfo.Value.MagicOnionPort,
            SupportedTypes = BattleServerConnectionType.Both
        };
    }

    /// <summary>
    /// Get current player count for session using thread-safe operations
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Current player count</returns>
    private int GetCurrentPlayerCount(string sessionId)
    {
        return _sessionPlayerCounts.TryGetValue(sessionId, out var count) ? count : 0;
    }

    /// <summary>
    /// Increment player count for session using thread-safe operations
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>New player count</returns>
    private int IncrementPlayerCount(string sessionId)
    {
        return _sessionPlayerCounts.AddOrUpdate(sessionId, 1, (key, currentCount) => currentCount + 1);
    }

    /// <summary>
    /// Decrement player count for session using thread-safe operations
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>New player count (minimum 0)</returns>
    private int DecrementPlayerCount(string sessionId)
    {
        return _sessionPlayerCounts.AddOrUpdate(sessionId, 0, (key, currentCount) => Math.Max(0, currentCount - 1));
    }

    /// <summary>
    /// Set player count for session using thread-safe operations
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="count">Player count</param>
    private void SetPlayerCount(string sessionId, int count)
    {
        _sessionPlayerCounts.AddOrUpdate(sessionId, count, (key, oldValue) => count);
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
