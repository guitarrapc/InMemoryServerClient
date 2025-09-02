using Shared.GameLift;

namespace Shared.Contracts;

/// <summary>
/// Interface for GameLift client functionality
/// </summary>
public interface IGameLiftClientProvider
{
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
