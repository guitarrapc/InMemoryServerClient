using Amazon.GameLift;
using Amazon.GameLift.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.GameLift;

namespace CliClient.GameLift;

/// <summary>
/// GameLift client provider for connecting to GameLift Anywhere servers
/// </summary>
internal class GameLiftClientProvider : IGameLiftClientProvider
{
    private readonly ILogger<GameLiftClientProvider> _logger;
    private readonly GameLiftOptions _options;
    private IAmazonGameLift? _gameLiftClient;

    public GameLiftClientProvider(
        ILogger<GameLiftClientProvider> logger,
        IOptions<GameLiftOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Get or create GameLift client if needed
    /// </summary>
    private IAmazonGameLift? GetGameLiftClient()
    {
        // Return null for Direct mode
        if (_options.Mode == GameLiftMode.Direct)
        {
            return null;
        }

        // Create client if not already created
        if (_gameLiftClient == null)
        {
            _gameLiftClient = CreateGameLiftClient();
        }

        return _gameLiftClient;
    }

    /// <summary>
    /// Create GameLift client based on configuration
    /// </summary>
    private IAmazonGameLift? CreateGameLiftClient()
    {
        try
        {
            var config = new AmazonGameLiftConfig();

            if (!string.IsNullOrEmpty(_options.AWS.Region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_options.AWS.Region);
            }

            // Use explicit credentials if provided (not recommended for production)
            if (!string.IsNullOrEmpty(_options.AWS.AccessKeyId) && !string.IsNullOrEmpty(_options.AWS.SecretAccessKey))
            {
                Amazon.Runtime.AWSCredentials credentials;
                if (!string.IsNullOrEmpty(_options.AWS.SessionToken))
                {
                    credentials = new Amazon.Runtime.SessionAWSCredentials(_options.AWS.AccessKeyId, _options.AWS.SecretAccessKey, _options.AWS.SessionToken);
                }
                else
                {
                    credentials = new Amazon.Runtime.BasicAWSCredentials(_options.AWS.AccessKeyId, _options.AWS.SecretAccessKey);
                }

                _logger.LogDebug("Creating GameLift client with explicit credentials");
                return new AmazonGameLiftClient(credentials, config);
            }
            else
            {
                // Use default credential chain (IAM role, environment variables, AWS Profile, etc.)
                _logger.LogDebug("Creating GameLift client with default credential chain");
                return new AmazonGameLiftClient(config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create GameLift client. GameLift functionality will be unavailable.");
            return null;
        }
    }

    public async Task<string> ResolveServerEndpointAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Mode == GameLiftMode.Direct)
        {
            var directEndpoint = _options.Client?.DefaultServerUrl ?? "wss://localhost:5001/battlehub";
            _logger.LogInformation("Using direct connection endpoint: {Endpoint}", directEndpoint);
            return directEndpoint;
        }

        if (_options.Mode == GameLiftMode.Anywhere)
        {
            return await ResolveAnywhereEndpointAsync(cancellationToken);
        }

        if (_options.Mode == GameLiftMode.FleetIQ)
        {
            throw new NotImplementedException("GameLift FleetIQ client support will be implemented in Phase 2");
        }

        throw new ArgumentOutOfRangeException(nameof(_options.Mode), _options.Mode, "Unknown GameLift mode");
    }

    public async Task<List<GameServerInfo>> SearchGameServersAsync(string fleetId, string location, CancellationToken cancellationToken = default)
    {
        if (_options.Mode == GameLiftMode.Direct)
        {
            _logger.LogDebug("Direct mode - game server search not applicable");
            return [];
        }

        if (_options.Mode == GameLiftMode.Anywhere)
        {
            return await SearchAnywhereGameServersAsync(fleetId, location, cancellationToken);
        }

        if (_options.Mode == GameLiftMode.FleetIQ)
        {
            throw new NotImplementedException("GameLift FleetIQ game server search will be implemented in Phase 2");
        }

        throw new ArgumentOutOfRangeException(nameof(_options.Mode), _options.Mode, "Unknown GameLift mode");
    }

    public async Task<Shared.GameLift.CreateGameSessionResponse> CreateGameSessionAsync(Shared.GameLift.CreateGameSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (_options.Mode == GameLiftMode.Direct)
        {
            _logger.LogDebug("Direct mode - GameSession creation not applicable");
            return Shared.GameLift.CreateGameSessionResponse.Failed("Direct mode does not support GameSession creation");
        }

        if (_options.Mode == GameLiftMode.Anywhere)
        {
            return await CreateAnywhereGameSessionAsync(request, cancellationToken);
        }

        if (_options.Mode == GameLiftMode.FleetIQ)
        {
            throw new NotImplementedException("GameLift FleetIQ GameSession creation will be implemented in Phase 2");
        }

        throw new ArgumentOutOfRangeException(nameof(_options.Mode), _options.Mode, "Unknown GameLift mode");
    }

    public async Task<List<GameSessionInfo>> SearchGameSessionsAsync(string fleetId, string? location = null, CancellationToken cancellationToken = default)
    {
        if (_options.Mode == GameLiftMode.Direct)
        {
            _logger.LogDebug("Direct mode - GameSession search not applicable");
            return [];
        }

        if (_options.Mode == GameLiftMode.Anywhere)
        {
            return await SearchAnywhereGameSessionsAsync(fleetId, location, cancellationToken);
        }

        if (_options.Mode == GameLiftMode.FleetIQ)
        {
            throw new NotImplementedException("GameLift FleetIQ GameSession search will be implemented in Phase 2");
        }

        throw new ArgumentOutOfRangeException(nameof(_options.Mode), _options.Mode, "Unknown GameLift mode");
    }

    public async Task<PlayerSessionInfo> CreatePlayerSessionAsync(string gameSessionId, string playerId, CancellationToken cancellationToken = default)
    {
        if (_options.Mode == GameLiftMode.Direct)
        {
            _logger.LogDebug("Direct mode - PlayerSession creation not applicable");
            return PlayerSessionInfo.Empty;
        }

        if (_options.Mode == GameLiftMode.Anywhere)
        {
            return await CreateAnywherePlayerSessionAsync(gameSessionId, playerId, cancellationToken);
        }

        if (_options.Mode == GameLiftMode.FleetIQ)
        {
            throw new NotImplementedException("GameLift FleetIQ PlayerSession creation will be implemented in Phase 2");
        }

        throw new ArgumentOutOfRangeException(nameof(_options.Mode), _options.Mode, "Unknown GameLift mode");
    }

    private async Task<string> ResolveAnywhereEndpointAsync(CancellationToken cancellationToken)
    {
        try
        {
            var gameLiftClient = GetGameLiftClient();
            if (gameLiftClient == null)
            {
                _logger.LogWarning("GameLift client is not available for Anywhere mode, falling back to direct endpoint");
                return _options.Client?.DefaultServerUrl ?? "wss://localhost:5001/battlehub";
            }

            _logger.LogInformation("Resolving GameLift Anywhere server endpoint for fleet: {FleetId}", _options.Anywhere.FleetId);

            // Try to find an active game session first
            var gameSessions = await SearchAnywhereGameSessionsAsync(_options.Anywhere.FleetId, _options.Anywhere.CustomLocation, cancellationToken);

            if (gameSessions.Count > 0)
            {
                var activeSession = gameSessions.First();
                _logger.LogInformation("Found active GameSession: {GameSessionId}, using existing session", activeSession.GameSessionId);

                // For GameLift Anywhere, we connect to the server's WebSocket endpoint
                var endpoint = _options.Client?.DefaultServerUrl ?? "wss://localhost:5001/battlehub";
                _logger.LogInformation("Using GameLift Anywhere endpoint for existing session: {Endpoint}", endpoint);
                return endpoint;
            }

            // No active sessions found, create a new one
            _logger.LogInformation("No active GameSessions found, creating new session");
            var createRequest = Shared.GameLift.CreateGameSessionRequest.ForAutoBattle(_options.Anywhere.FleetId, "auto-client");
            var createResponse = await CreateAnywhereGameSessionAsync(createRequest, cancellationToken);

            if (!createResponse.Success)
            {
                _logger.LogWarning("Failed to create GameSession: {Error}", createResponse.ErrorMessage);
                throw new InvalidOperationException($"Failed to create GameSession: {createResponse.ErrorMessage}");
            }

            _logger.LogInformation("Created new GameSession: {GameSessionId}", createResponse.GameSession.GameSessionId);

            // Return the server endpoint
            var newEndpoint = _options.Client?.DefaultServerUrl ?? "wss://localhost:5001/battlehub";
            _logger.LogInformation("Using GameLift Anywhere endpoint for new session: {Endpoint}", newEndpoint);
            return newEndpoint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve GameLift Anywhere endpoint");

            // Fallback to default endpoint
            var fallbackEndpoint = _options.Client?.DefaultServerUrl ?? "wss://localhost:5001/battlehub";
            _logger.LogWarning("Using fallback endpoint: {Endpoint}", fallbackEndpoint);
            return fallbackEndpoint;
        }
    }

    private async Task<List<GameServerInfo>> SearchAnywhereGameServersAsync(string fleetId, string location, CancellationToken cancellationToken)
    {
        var gameLiftClient = GetGameLiftClient();
        if (gameLiftClient == null)
        {
            _logger.LogWarning("GameLift client is not available for Anywhere mode");
            return [];
        }

        try
        {
            _logger.LogDebug("Searching for game servers in fleet: {FleetId}, location: {Location}", fleetId, location);

            var request = new ListComputeRequest
            {
                FleetId = fleetId,
                Location = location
            };

            var response = await gameLiftClient.ListComputeAsync(request, cancellationToken);

            var gameServers = response.ComputeList
                .Where(c => c.ComputeStatus == Amazon.GameLift.ComputeStatus.ACTIVE)
                .Select(c => new GameServerInfo
                {
                    ServerId = c.ComputeArn ?? string.Empty,
                    FleetId = fleetId,
                    Location = location,
                    Status = c.ComputeStatus.Value,
                    IpAddress = c.IpAddress,
                    ConnectionEndpoint = _options.Client?.DefaultServerUrl ?? "wss://localhost:5001/battlehub"
                })
                .ToList();

            _logger.LogInformation("Found {Count} active game servers", gameServers.Count);
            return gameServers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search GameLift Anywhere game servers");
            return [];
        }
    }

    private async Task<Shared.GameLift.CreateGameSessionResponse> CreateAnywhereGameSessionAsync(Shared.GameLift.CreateGameSessionRequest request, CancellationToken cancellationToken)
    {
        var gameLiftClient = GetGameLiftClient();
        if (gameLiftClient == null)
        {
            return Shared.GameLift.CreateGameSessionResponse.Failed("GameLift client is not available");
        }

        try
        {
            _logger.LogInformation("Creating GameSession for fleet: {FleetId}, name: {Name}", request.FleetId, request.Name);

            var createRequest = new Amazon.GameLift.Model.CreateGameSessionRequest
            {
                FleetId = request.FleetId,
                MaximumPlayerSessionCount = request.MaxPlayers,
                Name = request.Name,
                Location = _options.Anywhere.CustomLocation
            };

            if (!string.IsNullOrEmpty(request.GameSessionData))
            {
                createRequest.GameSessionData = request.GameSessionData;
            }

            var response = await gameLiftClient.CreateGameSessionAsync(createRequest, cancellationToken);

            if (response.GameSession != null)
            {
                var gameSession = new GameSessionInfo
                {
                    GameSessionId = response.GameSession.GameSessionId,
                    FleetId = response.GameSession.FleetId,
                    Name = response.GameSession.Name ?? string.Empty,
                    Status = response.GameSession.Status.Value,
                    CurrentPlayerCount = response.GameSession.CurrentPlayerSessionCount ?? 0,
                    MaxPlayers = response.GameSession.MaximumPlayerSessionCount ?? 5,
                    IpAddress = response.GameSession.IpAddress,
                    Port = response.GameSession.Port ?? 0,
                    GameSessionData = response.GameSession.GameSessionData,
                    CreationTime = response.GameSession.CreationTime ?? DateTime.UtcNow
                };

                _logger.LogInformation("Created GameSession successfully: {GameSessionId}", gameSession.GameSessionId);
                return Shared.GameLift.CreateGameSessionResponse.CreateSuccessful(gameSession);
            }

            _logger.LogWarning("GameSession creation returned null response");
            return Shared.GameLift.CreateGameSessionResponse.Failed("GameSession creation returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GameSession for fleet {FleetId}", request.FleetId);
            return Shared.GameLift.CreateGameSessionResponse.Failed($"GameSession creation failed: {ex.Message}");
        }
    }

    private async Task<List<GameSessionInfo>> SearchAnywhereGameSessionsAsync(string fleetId, string? location, CancellationToken cancellationToken)
    {
        var gameLiftClient = GetGameLiftClient();
        if (gameLiftClient == null)
        {
            _logger.LogWarning("GameLift client is not available");
            return [];
        }

        try
        {
            _logger.LogDebug("Searching for GameSessions in fleet: {FleetId}, location: {Location}", fleetId, location ?? "any");

            var request = new SearchGameSessionsRequest
            {
                FleetId = fleetId,
                FilterExpression = "hasAvailablePlayerSessions=true"
            };

            if (!string.IsNullOrEmpty(location))
            {
                request.Location = location;
            }

            var response = await gameLiftClient.SearchGameSessionsAsync(request, cancellationToken);

            if (response.GameSessions?.Count > 0)
            {
                var gameSessions = response.GameSessions.Select(session => new GameSessionInfo
                {
                    GameSessionId = session.GameSessionId,
                    FleetId = session.FleetId,
                    Name = session.Name ?? string.Empty,
                    Status = session.Status.Value,
                    CurrentPlayerCount = session.CurrentPlayerSessionCount ?? 0,
                    MaxPlayers = session.MaximumPlayerSessionCount ?? 5,
                    IpAddress = session.IpAddress,
                    Port = session.Port ?? 0,
                    GameSessionData = session.GameSessionData,
                    CreationTime = session.CreationTime ?? DateTime.UtcNow
                }).ToList();

                _logger.LogInformation("Found {Count} GameSessions in fleet {FleetId}", gameSessions.Count, fleetId);
                return gameSessions;
            }

            _logger.LogInformation("No active GameSessions found in fleet {FleetId}", fleetId);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search GameSessions in fleet {FleetId}", fleetId);
            return [];
        }
    }

    private async Task<PlayerSessionInfo> CreateAnywherePlayerSessionAsync(string gameSessionId, string playerId, CancellationToken cancellationToken)
    {
        var gameLiftClient = GetGameLiftClient();
        if (gameLiftClient == null)
        {
            _logger.LogWarning("GameLift client is not available");
            return PlayerSessionInfo.Empty;
        }

        try
        {
            _logger.LogInformation("Creating PlayerSession for game: {GameSessionId}, player: {PlayerId}", gameSessionId, playerId);

            var request = new CreatePlayerSessionRequest
            {
                GameSessionId = gameSessionId,
                PlayerId = playerId
            };

            var response = await gameLiftClient.CreatePlayerSessionAsync(request, cancellationToken);

            if (response.PlayerSession != null)
            {
                var playerSession = new PlayerSessionInfo
                {
                    PlayerSessionId = response.PlayerSession.PlayerSessionId,
                    PlayerId = response.PlayerSession.PlayerId,
                    GameSessionId = response.PlayerSession.GameSessionId,
                    Status = response.PlayerSession.Status.Value,
                    CreationTime = response.PlayerSession.CreationTime ?? DateTime.UtcNow,
                    IpAddress = response.PlayerSession.IpAddress,
                    Port = response.PlayerSession.Port ?? 0
                };

                _logger.LogInformation("Created PlayerSession successfully: {PlayerSessionId}", playerSession.PlayerSessionId);
                return playerSession;
            }

            _logger.LogWarning("PlayerSession creation returned null response");
            return PlayerSessionInfo.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PlayerSession for game {GameSessionId}", gameSessionId);
            return PlayerSessionInfo.Empty;
        }
    }
}
