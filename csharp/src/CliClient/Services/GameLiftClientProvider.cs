using Amazon.GameLift;
using Amazon.GameLift.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Models.GameLift;

namespace CliClient.Services;

/// <summary>
/// GameLift client provider for connecting to GameLift Anywhere servers
/// </summary>
internal class GameLiftClientProvider : IGameLiftClientProvider
{
    private readonly ILogger<GameLiftClientProvider> _logger;
    private readonly GameLiftOptions _options;
    private readonly IAmazonGameLift? _gameLiftClient;

    public GameLiftClientProvider(
        ILogger<GameLiftClientProvider> logger,
        IOptions<GameLiftOptions> options,
        IAmazonGameLift? gameLiftClient)
    {
        _logger = logger;
        _options = options.Value;
        _gameLiftClient = gameLiftClient;
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

    public async Task<List<GameServerInfo>> SearchGameServersAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Mode == GameLiftMode.Direct)
        {
            _logger.LogDebug("Direct mode - game server search not applicable");
            return [];
        }

        if (_options.Mode == GameLiftMode.Anywhere)
        {
            return await SearchAnywhereGameServersAsync(cancellationToken);
        }

        if (_options.Mode == GameLiftMode.FleetIQ)
        {
            throw new NotImplementedException("GameLift FleetIQ game server search will be implemented in Phase 2");
        }

        throw new ArgumentOutOfRangeException(nameof(_options.Mode), _options.Mode, "Unknown GameLift mode");
    }

    private async Task<string> ResolveAnywhereEndpointAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_gameLiftClient == null)
            {
                _logger.LogWarning("GameLift client is not available for Anywhere mode, falling back to direct endpoint");
                return _options.Client?.DefaultServerUrl ?? "wss://localhost:5001/battlehub";
            }

            _logger.LogInformation("Resolving GameLift Anywhere server endpoint for fleet: {FleetId}", _options.Anywhere.FleetId);

            // For GameLift Anywhere, we typically connect directly to the server's public endpoint
            // In a real implementation, you might want to:
            // 1. Query GameLift for active game sessions
            // 2. Create a new game session if needed
            // 3. Get the connection info from the game session

            // For this implementation, we'll use the configured WebSocket URL
            var endpoint = !string.IsNullOrEmpty(_options.Anywhere.WebSocketUrl)
                ? _options.Anywhere.WebSocketUrl
                : "wss://localhost:5001/battlehub";

            _logger.LogInformation("Using GameLift Anywhere endpoint: {Endpoint}", endpoint);
            return endpoint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve GameLift Anywhere endpoint");

            // Fallback to default endpoint
            var fallbackEndpoint = "wss://localhost:5001/battlehub";
            _logger.LogWarning("Using fallback endpoint: {Endpoint}", fallbackEndpoint);
            return fallbackEndpoint;
        }
    }

    private async Task<List<GameServerInfo>> SearchAnywhereGameServersAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_gameLiftClient == null)
            {
                _logger.LogWarning("GameLift client is not available for Anywhere mode");
                return [];
            }

            _logger.LogInformation("Searching for GameLift Anywhere compute instances in fleet: {FleetId}", _options.Anywhere.FleetId);

            var request = new ListComputeRequest
            {
                FleetId = _options.Anywhere.FleetId,
                Location = _options.Anywhere.CustomLocation
            };

            var response = await _gameLiftClient.ListComputeAsync(request, cancellationToken);

            var gameServers = response.ComputeList
                .Where(c => c.ComputeStatus == Amazon.GameLift.ComputeStatus.ACTIVE)
                .Select(c => new GameServerInfo(
                    c.ComputeArn,
                    c.ComputeName,
                    c.FleetId,
                    c.Location,
                    MapComputeStatus(c.ComputeStatus)))
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

    private static GameServerStatus MapComputeStatus(Amazon.GameLift.ComputeStatus status)
    {
        return status.Value switch
        {
            "PENDING" => GameServerStatus.Pending,
            "ACTIVE" => GameServerStatus.Active,
            "TERMINATING" => GameServerStatus.Terminating,
            "TERMINATED" => GameServerStatus.Terminated,
            _ => GameServerStatus.Unknown
        };
    }
}
