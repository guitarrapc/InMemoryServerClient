namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// In-memory session management service
/// </summary>
public sealed class SessionManager(ILogger<SessionManager> logger, IOptions<ServiceDiscoveryOptions> options, IBattleServerRegistry serverRegistry) : ISessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly ConcurrentDictionary<string, List<string>> _groupSessions = new();
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
                        session.Status == SessionStatus.Active &&
                        session.CurrentPlayers < session.MaxPlayers)
                    {
                        // Found existing session with available slots
                        var connectionInfo = await GetConnectionInfoForSessionAsync(session);
                        if (connectionInfo.HasValue)
                        {
                            logger.LogInformation("Found existing session {SessionId} for group {GroupName}",
                                sessionId, request.GroupName);

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
                CurrentPlayers = 0,
                MaxPlayers = request.MaxPlayers,
                CreatedAt = DateTime.UtcNow
            };

            _sessions[newSessionId] = newSession;
            _groupSessions.AddOrUpdate(request.GroupName,
                [newSessionId],
                (key, existing) => [.. existing, newSessionId]);

            var newConnectionInfo = new BattleServerConnectionInfo
            {
                ServerId = availableServer.Value.ServerId,
                Address = availableServer.Value.Address,
                SignalRPort = availableServer.Value.SignalRPort,
                MagicOnionPort = availableServer.Value.MagicOnionPort,
                SupportedTypes = Models.Server.ConnectionType.Both
            };

            // Update session status to Active
            var updatedSession = newSession with { Status = SessionStatus.Active };
            _sessions[newSessionId] = updatedSession;

            logger.LogInformation("Created new session {SessionId} for group {GroupName} on server {ServerId}",
                newSessionId, request.GroupName, availableServer.Value.ServerId);

            return new SessionCreationResponse
            {
                IsSuccess = true,
                Session = updatedSession,
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

        var updatedSession = session with { CurrentPlayers = playerCount };
        _sessions[sessionId] = updatedSession;

        logger.LogDebug("Updated session {SessionId} player count to {PlayerCount}", sessionId, playerCount);

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
            SupportedTypes = Models.Server.ConnectionType.Both
        };
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
