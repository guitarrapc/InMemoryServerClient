namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// Session management service interface
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Create or find existing session
    /// </summary>
    /// <param name="request">Session creation request</param>
    /// <returns>Session creation response</returns>
    Task<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request);

    /// <summary>
    /// Get session information by session ID
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Session information or null if not found</returns>
    Task<SessionInfo?> GetSessionInfoAsync(string sessionId);

    /// <summary>
    /// List all active sessions
    /// </summary>
    /// <returns>List of active sessions</returns>
    Task<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync();

    /// <summary>
    /// Terminate session
    /// </summary>
    /// <param name="sessionId">Session ID to terminate</param>
    /// <returns>True if successfully terminated</returns>
    Task<bool> TerminateSessionAsync(string sessionId);

    /// <summary>
    /// Update session status
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="status">New status</param>
    /// <returns>True if successfully updated</returns>
    Task<bool> UpdateSessionStatusAsync(string sessionId, SessionStatus status);

    /// <summary>
    /// Update session player count
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="playerCount">Current player count</param>
    /// <returns>True if successfully updated</returns>
    Task<bool> UpdateSessionPlayerCountAsync(string sessionId, int playerCount);

    /// <summary>
    /// Cleanup expired sessions
    /// </summary>
    void CleanupExpiredSessions();
}
