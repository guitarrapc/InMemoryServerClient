using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Models.GameLift;

namespace InMemoryServer.Services;

/// <summary>
/// Direct connection provider that bypasses GameLift
/// </summary>
internal class DirectConnectionProvider(ILogger<DirectConnectionProvider> logger, IOptions<GameLiftOptions> options) : IGameServerProvider
{
    private readonly GameLiftOptions _options = options.Value;

    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Initializing direct connection provider");
        return Task.FromResult(true);
    }

    public Task<ComputeInfo> RegisterComputeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Direct connection mode - compute registration not applicable");
        return Task.FromResult(ComputeInfo.Empty);
    }

    public Task<AuthTokenInfo> GetAuthTokenAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Direct connection mode - auth token not applicable");
        return Task.FromResult(AuthTokenInfo.Empty);
    }

    public Task<bool> InitServerSdkAsync(AuthTokenInfo authToken, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Direct connection mode - server SDK initialization not applicable");
        return Task.FromResult(true);
    }

    public Task<bool> ProcessReadyAsync(ProcessParameters parameters, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Direct connection mode - process ready not applicable");
        return Task.FromResult(true);
    }

    public Task ActivateGameSessionAsync(string gameSessionId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Direct connection mode - game session activation not applicable");
        return Task.CompletedTask;
    }

    public Task ProcessEndingAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Direct connection mode - process ending notification not applicable");
        return Task.CompletedTask;
    }

    public Task<string> GetConnectionEndpointAsync(CancellationToken cancellationToken = default)
    {
        // For direct connection, return the configured WebSocket URL or default
        var endpoint = !string.IsNullOrEmpty(_options.Anywhere.WebSocketUrl)
            ? _options.Anywhere.WebSocketUrl
            : "wss://localhost:5001/battlehub";

        logger.LogInformation("Using direct connection endpoint: {Endpoint}", endpoint);
        return Task.FromResult(endpoint);
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Shutting down direct connection provider");
        return Task.CompletedTask;
    }
}
