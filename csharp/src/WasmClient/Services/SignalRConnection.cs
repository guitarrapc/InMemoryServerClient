using Microsoft.AspNetCore.SignalR.Client;
using WasmClient.Models;
using Shared.Battle;
using Shared.Constants;
using Shared.Models;

namespace WasmClient.Services;

/// <summary>
/// SignalR implementation of IBattleConnection for WasmClient
/// </summary>
public class SignalRConnection : IBattleConnection
{
    private readonly ILogger<SignalRConnection> _logger;
    private HubConnection? _connection;

    public string ConnectionId => _connection?.ConnectionId ?? string.Empty;
    public Models.ConnectionType Type => Models.ConnectionType.SignalR;
    public ConnectionInfo Info { get; }
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    // Events
    public event Action<BattleReplayData>? OnBattleReplayReceived;
    public event Action<string>? OnBattleComplete;
    public event Action<Exception>? OnConnectionError;
    public event Action? OnDisconnected;
    public event Action<ConnectionsReadyData>? OnConnectionsReady;
    public event Action<BattleStartedData>? OnBattleStarted;

    public SignalRConnection(ConnectionInfo connectionInfo, ILogger<SignalRConnection> logger)
    {
        Info = connectionInfo;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            _logger.LogInformation("Connecting to SignalR server: {ServerUrl}", Info.ServerUrl);

            _connection = new HubConnectionBuilder()
                .WithUrl(Info.ServerUrl + SystemDefines.HubRoute)
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();
            await _connection.StartAsync();

            _logger.LogInformation("Connected to SignalR server. ConnectionId: {ConnectionId}", ConnectionId);

            // Join group if specified
            if (!string.IsNullOrEmpty(Info.GroupName))
            {
                var joinResult = await JoinGroupAsync(Info.GroupName);
                if (!joinResult)
                {
                    _logger.LogWarning("Failed to join group: {GroupName}", Info.GroupName);
                    await DisconnectAsync();
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR server");
            OnConnectionError?.Invoke(ex);
            await DisconnectAsync();
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try
            {
                _logger.LogInformation("Disconnecting from SignalR server");
                await _connection.DisposeAsync();
                _connection = null;
                _logger.LogInformation("Disconnected from SignalR server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SignalR disconnection");
            }
        }
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        try
        {
            var groupId = await _connection!.InvokeAsync<string>("JoinGroupAsync", groupName);
            _logger.LogInformation("Joined group: {GroupName} (ID: {GroupId})", groupName, groupId);
            return !string.IsNullOrEmpty(groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join group: {GroupName}", groupName);
            OnConnectionError?.Invoke(ex);
            return false;
        }
    }

    public async Task<Shared.Battle.BattleStatus?> GetBattleStatusAsync()
    {
        EnsureConnected();
        try
        {
            return await _connection!.InvokeAsync<Shared.Battle.BattleStatus?>("GetBattleStatusAsync");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get battle status");
            OnConnectionError?.Invoke(ex);
            return null;
        }
    }

    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        try
        {
            return await _connection!.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm connection ready");
            OnConnectionError?.Invoke(ex);
            return false;
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to server. Call ConnectAsync first.");
        }
    }

    private void SetupEventHandlers()
    {
        if (_connection == null) return;

        // Battle events
        _connection.On<ConnectionsReadyData>("ConnectionsReady", data =>
        {
            _logger.LogInformation("Connections ready received - BattleId: {BattleId}, Seed: {Seed}",
                data.BattleId, data.Seed);
            OnConnectionsReady?.Invoke(data);

            // Automatically confirm connection ready
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Confirming connection ready...");
                    var result = await ConfirmConnectionReadyAsync();
                    _logger.LogInformation("Connection confirmed: {Result}", result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to confirm connection ready");
                }
            });
        });

        _connection.On<BattleStartedData>("BattleStarted", data =>
        {
            _logger.LogInformation("Battle started - BattleId: {BattleId}, Seed: {Seed}",
                data.BattleId, data.Seed);
            OnBattleStarted?.Invoke(data);
        });

        _connection.On<BattleReplayData>("BattleReplayData", replayData =>
        {
            _logger.LogInformation("Battle replay data received - Chunk: {ChunkIndex}/{TotalChunks}, Turns: {TurnCount}",
                replayData.ChunkIndex, replayData.TotalChunks, replayData.TurnData.Count);
            OnBattleReplayReceived?.Invoke(replayData);
        });

        _connection.On<Shared.Battle.BattleStatus>("BattleCompleted", battleStatus =>
        {
            _logger.LogInformation("Battle completed - Turn: {Turn}", battleStatus.CurrentTurn);
            OnBattleComplete?.Invoke("Battle completed");
        });

        // Connection events
        _connection.Closed += error =>
        {
            _logger.LogWarning("Connection closed: {Error}", error?.Message ?? "Unknown reason");
            OnDisconnected?.Invoke();
            return Task.CompletedTask;
        };
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
