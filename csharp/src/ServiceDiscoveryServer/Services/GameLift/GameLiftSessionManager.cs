namespace ServiceDiscoveryServer.Services.GameLift;

/// <summary>
/// GameLift session management service (Phase 2 implementation)
/// </summary>
public sealed class GameLiftSessionManager : IGameLiftIntegration
{
    private readonly ILogger<GameLiftSessionManager> _logger;
    private readonly IOptions<ServiceDiscoveryOptions> _options;

    public GameLiftSessionManager(
        ILogger<GameLiftSessionManager> logger,
        IOptions<ServiceDiscoveryOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    public Task<SessionCreationResponse> CreateGameLiftSessionAsync(SessionCreationRequest request)
    {
        // TODO: Phase 2 implementation
        _logger.LogWarning("GameLift integration not implemented yet (Phase 2)");

        return Task.FromResult(new SessionCreationResponse
        {
            IsSuccess = false,
            ErrorMessage = "GameLift integration not implemented yet"
        });
    }

    public Task<SessionInfo?> GetGameLiftSessionAsync(string sessionId)
    {
        // TODO: Phase 2 implementation
        _logger.LogWarning("GameLift integration not implemented yet (Phase 2)");
        return Task.FromResult<SessionInfo?>(null);
    }

    public Task<bool> TerminateGameLiftSessionAsync(string sessionId)
    {
        // TODO: Phase 2 implementation
        _logger.LogWarning("GameLift integration not implemented yet (Phase 2)");
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<SessionInfo>> ListGameLiftSessionsAsync()
    {
        // TODO: Phase 2 implementation
        return Task.FromResult<IReadOnlyList<SessionInfo>>(Array.Empty<SessionInfo>());
    }

    public bool IsGameLiftEnabled()
    {
        var mode = _options.Value.GameLift.Mode;
        return !string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase);
    }
}
