using Amazon.GameLift;
using Amazon.GameLift.Model;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.GameLift;

namespace CliClient.GameLift;

/// <summary>
/// GameLift client provider for connecting to GameLift Anywhere servers
/// </summary>
internal class GameLiftClientProvider : IGameLiftClientProvider
{
    private readonly ILogger<GameLiftClientProvider> _logger;
    private readonly GameLiftMode _mode;
    private readonly IAmazonGameLift _gameLiftClient;

    public GameLiftClientProvider(GameLiftMode mode, ILoggerFactory loggerFactory)
    {
        if (mode == GameLiftMode.None)
            throw new NotSupportedException($"Not supported exception. GameLift Mode {mode} means, it won't use GameLift Client.");
        _logger = loggerFactory.CreateLogger<GameLiftClientProvider>();
        _mode = mode;
        _gameLiftClient = CreateGameLiftClient();
    }

    /// <summary>
    /// Create GameLift client based on configuration
    /// </summary>
    private IAmazonGameLift CreateGameLiftClient()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION");
        var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var sessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");

        var config = new AmazonGameLiftConfig();

        if (!string.IsNullOrEmpty(region))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        }

        // Use explicit credentials if provided (not recommended for production)
        if (!string.IsNullOrEmpty(accessKeyId) && !string.IsNullOrEmpty(secretAccessKey))
        {
            Amazon.Runtime.AWSCredentials credentials;
            if (!string.IsNullOrEmpty(sessionToken))
            {
                credentials = new Amazon.Runtime.SessionAWSCredentials(accessKeyId, secretAccessKey, sessionToken);
            }
            else
            {
                credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyId, secretAccessKey);
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

    public async Task<string> ResolveServerEndpointAsync(string fleetId, string location, string groupName, CancellationToken cancellationToken = default)
    {
        return _mode switch
        {
            GameLiftMode.Anywhere => await ResolveAnywhereEndpointAsync(fleetId, location, groupName, cancellationToken),
            GameLiftMode.FleetIQ => throw new NotImplementedException("GameLift FleetIQ client support will be implemented in Phase 2"),
            _ => throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Unknown GameLift mode"),
        };
    }

    public async Task<List<GameServerInfo>> SearchGameServersAsync(string fleetId, string location, CancellationToken cancellationToken = default)
    {
        return _mode switch
        {
            GameLiftMode.Anywhere => await SearchAnywhereGameServersAsync(fleetId, location, cancellationToken),
            GameLiftMode.FleetIQ => throw new NotImplementedException("GameLift FleetIQ game server search will be implemented in Phase 2"),
            _ => throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Unknown GameLift mode"),
        };
    }

    public async Task<Shared.GameLift.CreateGameSessionResponse> CreateGameSessionAsync(Shared.GameLift.CreateGameSessionRequest request, string location, CancellationToken cancellationToken = default)
    {
        return _mode switch
        {
            GameLiftMode.Anywhere => await CreateAnywhereGameSessionAsync(request, location, cancellationToken),
            GameLiftMode.FleetIQ => throw new NotImplementedException("GameLift FleetIQ GameSession creation will be implemented in Phase 2"),
            _ => throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Unknown GameLift mode"),
        };
    }

    public async Task<List<GameSessionInfo>> SearchGameSessionsAsync(string fleetId, string? location = null, CancellationToken cancellationToken = default)
    {
        return _mode switch
        {
            GameLiftMode.Anywhere => await SearchAnywhereGameSessionsAsync(fleetId, location, cancellationToken),
            GameLiftMode.FleetIQ => throw new NotImplementedException("GameLift FleetIQ GameSession search will be implemented in Phase 2"),
            _ => throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Unknown GameLift mode"),
        };
    }

    public async Task<PlayerSessionInfo> CreatePlayerSessionAsync(string gameSessionId, string playerId, CancellationToken cancellationToken = default)
    {
        return _mode switch
        {
            GameLiftMode.Anywhere => await CreateAnywherePlayerSessionAsync(gameSessionId, playerId, cancellationToken),
            GameLiftMode.FleetIQ => throw new NotImplementedException("GameLift FleetIQ PlayerSession creation will be implemented in Phase 2"),
            _ => throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Unknown GameLift mode"),
        };
    }

    private async Task<string> ResolveAnywhereEndpointAsync(string fleetId, string location, CancellationToken cancellationToken)
    {
        return await ResolveAnywhereEndpointAsync(fleetId, location, "auto-client", cancellationToken);
    }

    private async Task<string> ResolveAnywhereEndpointAsync(string fleetId, string location, string groupName, CancellationToken cancellationToken)
    {
        // Try to find an active game session first
        var gameSessions = await SearchAnywhereGameSessionsAsync(fleetId, location, cancellationToken);

        // Select any active session
        if (gameSessions.Count > 0)
        {
            var activeSession = gameSessions.First();
            _logger.LogInformation("Found active GameSession: {GameSessionId}, using existing session", activeSession.GameSessionId);

            // For GameLift Anywhere, we connect to the server's WebSocket endpoint
            var endpoint = CreateEndpoint(activeSession.Address, activeSession.Port);
            _logger.LogInformation("Using GameLift Anywhere endpoint for existing session: {Endpoint}", endpoint);
            return endpoint;
        }
        else
        {
            // No active sessions found, create a new one
            _logger.LogInformation("No active GameSessions found, creating new session with group name: {GroupName}", groupName);
            var createRequest = Shared.GameLift.CreateGameSessionRequest.ForAutoBattle(fleetId, groupName);
            var createResponse = await CreateAnywhereGameSessionAsync(createRequest, location, cancellationToken);

            if (!createResponse.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to create GameSession: {createResponse.ErrorMessage}");
            }

            var session = createResponse.GameSession;
            _logger.LogInformation("Created new GameSession: {GameSessionId}", session.GameSessionId);

            var endpoint = CreateEndpoint(session.Address, session.Port);
            _logger.LogInformation("Using GameLift Anywhere endpoint for new session: {Endpoint}", endpoint);
            return endpoint;
        }
    }

    private async Task<List<GameServerInfo>> SearchAnywhereGameServersAsync(string fleetId, string location, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching for game servers in fleet: {FleetId}, location: {Location}", fleetId, location);

        var request = new ListComputeRequest
        {
            FleetId = fleetId,
            Location = location
        };

        var response = await _gameLiftClient.ListComputeAsync(request, cancellationToken);

        var gameServers = response.ComputeList
            .Where(c => c.ComputeStatus == Amazon.GameLift.ComputeStatus.ACTIVE)
            .Select(c => new GameServerInfo
            {
                ServerId = c.ComputeArn ?? string.Empty,
                FleetId = fleetId,
                Location = location,
                Status = c.ComputeStatus.Value,
                DnsName = c.DnsName,
                ConnectionEndpoint = CreateEndpoint(c.DnsName, Random.Shared.Next(5000, 5001))
            })
            .ToList();

        _logger.LogInformation("Found {Count} active game servers", gameServers.Count);
        return gameServers;
    }

    /// <summary>
    /// Return endpoint without schema
    /// </summary>
    /// <param name="address"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static string CreateEndpoint(string? address, int port)
    {
        if (string.IsNullOrEmpty(address) || port == 0)
        {
            throw new InvalidOperationException("Invalid IP address or port for GameLift Anywhere endpoint");
        }

        var scheme = port == 443 ? "https" : "http";

        var endpoint = $"{scheme}://{address}:{port}";
        return endpoint;
    }

    private async Task<Shared.GameLift.CreateGameSessionResponse> CreateAnywhereGameSessionAsync(Shared.GameLift.CreateGameSessionRequest request, string location, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating GameSession for fleet: {FleetId}, name: {Name}", request.FleetId, request.Name);

            var createRequest = new Amazon.GameLift.Model.CreateGameSessionRequest
            {
                FleetId = request.FleetId,
                MaximumPlayerSessionCount = request.MaxPlayers,
                Name = request.Name,
                Location = location
            };
            if (!string.IsNullOrEmpty(request.GameSessionData))
            {
                createRequest.GameSessionData = request.GameSessionData;
            }

            var response = await _gameLiftClient.CreateGameSessionAsync(createRequest, cancellationToken);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK && response.GameSession != null)
            {
                var gameSession = new GameSessionInfo
                {
                    GameSessionId = response.GameSession.GameSessionId,
                    FleetId = response.GameSession.FleetId,
                    Name = response.GameSession.Name ?? string.Empty,
                    Status = response.GameSession.Status.Value,
                    CurrentPlayerCount = response.GameSession.CurrentPlayerSessionCount ?? 0,
                    MaxPlayers = response.GameSession.MaximumPlayerSessionCount ?? 5,
                    Address = response.GameSession.DnsName ?? response.GameSession.IpAddress,
                    Port = response.GameSession.Port ?? 0,
                    GameSessionData = response.GameSession.GameSessionData,
                    CreationTime = response.GameSession.CreationTime ?? DateTime.UtcNow
                };

                _logger.LogInformation("Created GameSession successfully: {GameSessionId}", gameSession.GameSessionId);
                return Shared.GameLift.CreateGameSessionResponse.Success(gameSession);
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

        var response = await _gameLiftClient.SearchGameSessionsAsync(request, cancellationToken);

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
                Address = session.IpAddress,
                Port = session.Port ?? 0,
                GameSessionData = session.GameSessionData,
                CreationTime = session.CreationTime ?? DateTime.UtcNow
            }).ToList();

            return gameSessions;
        }

        return [];
    }

    private async Task<PlayerSessionInfo> CreateAnywherePlayerSessionAsync(string gameSessionId, string playerId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating PlayerSession for game: {GameSessionId}, player: {PlayerId}", gameSessionId, playerId);

            var request = new CreatePlayerSessionRequest
            {
                GameSessionId = gameSessionId,
                PlayerId = playerId
            };

            var response = await _gameLiftClient.CreatePlayerSessionAsync(request, cancellationToken);

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
