using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.GameLift;

namespace CliClient.GameLift;

/// <summary>
/// GameLift client provider that communicates with server via SignalR for GameSession management
/// </summary>
internal class GameLiftClientProvider : IGameLiftClientProvider, IAsyncDisposable
{
    private readonly ILogger<GameLiftClientProvider> _logger;
    private readonly GameLiftMode _mode;
    private HubConnection? _hubConnection;
    private readonly string _serverEndpoint;

    public GameLiftClientProvider(GameLiftMode mode, string serverEndpoint, ILoggerFactory loggerFactory)
    {
        if (mode == GameLiftMode.None)
            throw new NotSupportedException($"Not supported exception. GameLift Mode {mode} means, it won't use GameLift Client.");

        _logger = loggerFactory.CreateLogger<GameLiftClientProvider>();
        _mode = mode;
        _serverEndpoint = serverEndpoint;
    }

    /// <summary>
    /// Ensure SignalR connection is established
    /// </summary>
    private async Task EnsureConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            return;

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        var serverUrl = _serverEndpoint.TrimEnd('/');
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/battlehub")
            .WithAutomaticReconnect()
            .Build();

        _logger.LogInformation("Connecting to SignalR hub at: {Url}", $"{serverUrl}/battlehub");
        await _hubConnection.StartAsync(cancellationToken);
        _logger.LogInformation("SignalR connection established");
    }

    public async Task<string> ResolveServerEndpointAsync(string fleetId, string location, string groupName, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken);

        var clientId = Guid.NewGuid().ToString("N")[..8]; // Short client ID for tracking
        var request = GameSessionCreationRequest.ForAutoBattle(fleetId, location, groupName, clientId);

        _logger.LogInformation("Requesting GameSession creation for group: {GroupName}, fleet: {FleetId}", groupName, fleetId);

        var response = await RequestGameSessionCreationAsync(request, cancellationToken);

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to resolve server endpoint: {response.ErrorMessage}");
        }

        _logger.LogInformation("Resolved server endpoint: {Endpoint} (GameSession: {GameSessionId})",
            response.ConnectionEndpoint, response.GameSession.GameSessionId);

        return response.ConnectionEndpoint;
    }

    public async Task<GameSessionCreationResponse> RequestGameSessionCreationAsync(GameSessionCreationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Invoking server-side GameSession creation for fleet: {FleetId}, location: {Location}",
                request.FleetId, request.Location);

            var response = await _hubConnection!.InvokeAsync<GameSessionCreationResponse>(
                "CreateGameSessionAsync", request, cancellationToken);

            if (response.IsSuccess)
            {
                _logger.LogInformation("Server-side GameSession creation successful: {GameSessionId} (New: {IsNew})",
                    response.GameSession.GameSessionId, response.IsNewSession);
            }
            else
            {
                _logger.LogWarning("Server-side GameSession creation failed: {ErrorMessage}", response.ErrorMessage);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke server-side GameSession creation");
            return GameSessionCreationResponse.Failed($"SignalR invocation failed: {ex.Message}");
        }
    }

    public async Task<PlayerSessionCreationResponse> RequestPlayerSessionCreationAsync(PlayerSessionCreationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureConnectionAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Invoking server-side PlayerSession creation for GameSession: {GameSessionId}, Player: {PlayerId}",
                request.GameSessionId, request.PlayerId);

            var response = await _hubConnection!.InvokeAsync<PlayerSessionCreationResponse>(
                "CreatePlayerSessionAsync", request, cancellationToken);

            if (response.IsSuccess)
            {
                _logger.LogInformation("Server-side PlayerSession creation successful: {PlayerSessionId}",
                    response.PlayerSession.PlayerSessionId);
            }
            else
            {
                _logger.LogWarning("Server-side PlayerSession creation failed: {ErrorMessage}", response.ErrorMessage);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke server-side PlayerSession creation");
            return PlayerSessionCreationResponse.Failed($"SignalR invocation failed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            _logger.LogInformation("GameLiftClientProvider disposed");
        }
    }
}
