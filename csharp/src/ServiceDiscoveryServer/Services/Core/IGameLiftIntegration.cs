namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// GameLift integration service interface
/// </summary>
public interface IGameLiftIntegration
{
    /// <summary>
    /// Create GameLift session
    /// </summary>
    /// <param name="request">Session creation request</param>
    /// <returns>Session creation response</returns>
    Task<SessionCreationResponse> CreateGameLiftSessionAsync(SessionCreationRequest request);

    /// <summary>
    /// Get GameLift session information
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Session information or null if not found</returns>
    Task<SessionInfo?> GetGameLiftSessionAsync(string sessionId);

    /// <summary>
    /// Terminate GameLift session
    /// </summary>
    /// <param name="sessionId">Session ID to terminate</param>
    /// <returns>True if successfully terminated</returns>
    Task<bool> TerminateGameLiftSessionAsync(string sessionId);

    /// <summary>
    /// List GameLift sessions
    /// </summary>
    /// <returns>List of GameLift sessions</returns>
    Task<IReadOnlyList<SessionInfo>> ListGameLiftSessionsAsync();

    /// <summary>
    /// Check if GameLift integration is enabled
    /// </summary>
    /// <returns>True if GameLift is enabled</returns>
    bool IsGameLiftEnabled();
}
