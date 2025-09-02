using Shared.GameLift;

namespace Shared.Contracts;

/// <summary>
/// Interface for GameLift client functionality
/// </summary>
public interface IGameLiftClientProvider
{
    /// <summary>
    /// Resolve server endpoint for connection by requesting GameSession creation from server
    /// </summary>
    /// <param name="fleetId">Fleet ID</param>
    /// <param name="location">Location to search</param>
    /// <param name="groupName">Group name for the session</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Server endpoint URL</returns>
    Task<string> ResolveServerEndpointAsync(string fleetId, string location, string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Request GameSession creation from server via SignalR
    /// </summary>
    /// <param name="request">GameSession creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>GameSession creation response</returns>
    Task<GameSessionCreationResponse> RequestGameSessionCreationAsync(GameSessionCreationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Request PlayerSession creation from server via SignalR
    /// </summary>
    /// <param name="request">PlayerSession creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PlayerSession creation response</returns>
    Task<PlayerSessionCreationResponse> RequestPlayerSessionCreationAsync(PlayerSessionCreationRequest request, CancellationToken cancellationToken = default);
}
