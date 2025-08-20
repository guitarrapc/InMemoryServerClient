using Shared.GameLift;

namespace Shared.Contracts;

/// <summary>
/// Interface for GameLift client functionality
/// </summary>
public interface IGameLiftClientProvider
{
    /// <summary>
    /// Search for available game servers
    /// </summary>
    /// <param name="fleetId">Fleet ID to search</param>
    /// <param name="location">Location to search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available game servers</returns>
    Task<List<GameServerInfo>> SearchGameServersAsync(string fleetId, string location, CancellationToken cancellationToken = default);
}
