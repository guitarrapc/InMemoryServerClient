using Shared.GameLift;

namespace Shared.Contracts;

/// <summary>
/// Interface for GameLift client functionality
/// </summary>
public interface IGameLiftClientProvider
{
    /// <summary>
    /// Resolve server endpoint for connection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Server endpoint URL</returns>
    Task<string> ResolveServerEndpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for available game servers
    /// </summary>
    /// <param name="fleetId">Fleet ID to search</param>
    /// <param name="location">Location to search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available game servers</returns>
    Task<List<GameServerInfo>> SearchGameServersAsync(string fleetId, string location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new game session
    /// </summary>
    /// <param name="request">Game session creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Game session creation response</returns>
    Task<CreateGameSessionResponse> CreateGameSessionAsync(CreateGameSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for existing game sessions
    /// </summary>
    /// <param name="fleetId">Fleet ID to search</param>
    /// <param name="location">Optional location filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available game sessions</returns>
    Task<List<GameSessionInfo>> SearchGameSessionsAsync(string fleetId, string? location = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a player session to join a game session
    /// </summary>
    /// <param name="gameSessionId">Game session ID to join</param>
    /// <param name="playerId">Player ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player session information</returns>
    Task<PlayerSessionInfo> CreatePlayerSessionAsync(string gameSessionId, string playerId, CancellationToken cancellationToken = default);
}
