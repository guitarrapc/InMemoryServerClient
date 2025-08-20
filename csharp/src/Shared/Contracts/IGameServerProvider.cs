using Shared.Models.GameLift;

namespace Shared.Contracts;

/// <summary>
/// Interface for game server providers (Direct, GameLift Anywhere, GameLift FleetIQ)
/// </summary>
public interface IGameServerProvider
{
    /// <summary>
    /// Initialize the provider
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if initialization was successful</returns>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Register compute for GameLift Anywhere (control plane operation)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compute information</returns>
    Task<ComputeInfo> RegisterComputeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get auth token for GameLift server SDK (control plane operation)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auth token information</returns>
    Task<AuthTokenInfo> GetAuthTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize GameLift server SDK (server SDK operation)
    /// </summary>
    /// <param name="authToken">Auth token information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if server SDK initialization was successful</returns>
    Task<bool> InitServerSdkAsync(AuthTokenInfo authToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register process as ready to accept game sessions (server SDK operation)
    /// </summary>
    /// <param name="parameters">Process parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if process ready was successful</returns>
    Task<bool> ProcessReadyAsync(GameServerProcessParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activate game session (server SDK operation)
    /// </summary>
    /// <param name="gameSessionId">Game session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task ActivateGameSessionAsync(string gameSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify GameLift that the process is ending (server SDK operation)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task ProcessEndingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get connection endpoint for clients
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection endpoint URL</returns>
    Task<string> GetConnectionEndpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shutdown the provider
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory interface for creating game server providers
/// </summary>
public interface IGameServerProviderFactory
{
    /// <summary>
    /// Create a game server provider based on configuration
    /// </summary>
    /// <returns>Game server provider instance</returns>
    IGameServerProvider CreateProvider();
}

/// <summary>
/// Interface for GameLift client operations
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
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available game servers</returns>
    Task<List<GameServerInfo>> SearchGameServersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Game server information for client use
/// </summary>
public readonly record struct GameServerInfo(
    string ServerId,
    string ServerName,
    string FleetId,
    string Location,
    GameServerStatus Status);

/// <summary>
/// Game server status enumeration
/// </summary>
public enum GameServerStatus
{
    Unknown,
    Pending,
    Active,
    Terminating,
    Terminated
}

/// <summary>
/// Process parameters for Game Server ProcessReady
/// </summary>
/// <param name="Port">Server port</param>
/// <param name="LogPaths">Log file paths</param>
public readonly record struct GameServerProcessParameters(
    int Port,
    string[] LogPaths);
