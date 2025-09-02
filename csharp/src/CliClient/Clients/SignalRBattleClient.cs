using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using CliClient.Extensions;
using CliClient.Models;
using CliClient.Services;
using Shared.BattleServer.Models;
using Shared.BattleServer.Constants;
using Shared.BattleLogic.Models;

namespace CliClient.Clients;

/// <summary>
/// SignalR implementation of IInMemoryServerClient
/// </summary>
internal class SignalRBattleClient : IBattleClient
{
    private readonly ILogger<SignalRBattleClient> _logger;
    private readonly BattleReplayRenderer _replayRenderer;
    private HubConnection? _connection;
    private string _serverUrl = string.Empty;
    private string _currentGroupId = string.Empty;

    // Battle replay data storage
    private readonly Dictionary<int, List<BattleStatus>> _replayChunks = [];
    private int _expectedTotalChunks = 0;
    private BattleReplaySummary? _battleSummary = null;

    // This is used to track if the battle has completed and to notify the client when it is done
    private readonly TaskCompletionSource<bool> _battleCompletionSource = new();

    public TaskCompletionSource<bool> BattleCompletionSource => _battleCompletionSource;

    // Events
    public event Action<string>? OnDisconnected;
    public event Action<string, string>? OnKeyChanged;
    public event Action<string>? OnKeyDeleted;
    public event Action<MemberJoinedData>? OnMemberJoined;
    public event Action<MemberLeftData>? OnMemberLeft;
    public event Action<string, string>? OnGroupMessage;
    public event Action<ConnectionsReadyData>? OnConnectionsReady;
    public event Action<BattleStartedData>? OnBattleStarted;
    public event Action<BattleReplayData>? OnBattleReplayData;
    public event Action<GroupDissolvedData>? OnGroupDissolved;
    public event Action<GroupExtendedData>? OnGroupExtended;

    public SignalRBattleClient(ILogger<SignalRBattleClient> logger)
    {
        _logger = logger;
        _replayRenderer = new BattleReplayRenderer(_logger);
    }

    public bool IsConnected => _connection != null &&(_connection?.State == HubConnectionState.Connected);

    public async Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        if (IsConnected)
        {
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation("Connecting to server: {ServerUrl}", serverUrl);

            _connection = new HubConnectionBuilder()
                .WithUrl(_serverUrl + SystemDefines.BattleHubRoute)
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();
            await _connection.StartAsync();
            _logger.LogInformation("Connected to SignalR server");

            if (!string.IsNullOrEmpty(groupName))
            {
                return await JoinGroupAsync(groupName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server");
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
                _currentGroupId = string.Empty;
                _logger.LogInformation("Disconnected from SignalR server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SignalR disconnection");
            }
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<string?>("GetAsync", key);
    }

    public async Task<bool> SetAsync(string key, string value)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("SetAsync", key, value);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("DeleteAsync", key);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null)
    {
        return await ListAsync(pattern);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string? pattern = null)
    {
        EnsureConnected();
        var result = await _connection!.InvokeAsync<string[]>("ListKeysAsync", pattern ?? string.Empty);
        return result;
    }

    public async Task WatchAsync(string key)
    {
        EnsureConnected();
        _logger.LogInformation("Watching key: {Key}", key);
        await _connection!.InvokeAsync("WatchAsync", key);
    }

    public async Task<bool> BroadcastAsync(string message)
    {
        return await BroadcastMessageAsync(message);
    }

    public async Task<BattleReplayData?> GetBattleReplayAsync(Guid battleId)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<BattleReplayData?>("GetBattleReplayAsync", battleId);
    }

    public async Task PlayBattleReplayAsync(BattleReplayData replayData)
    {
        // This is handled automatically when OnBattleReplayData event is triggered
        _logger.LogInformation("Battle replay play requested for {TurnCount} turns", replayData.TurnData.Count);
        await Task.CompletedTask;
    }

    // IBattleClient specific methods
    public async Task<bool> BroadcastMessageAsync(string message)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("BroadcastAsync", message);
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        _currentGroupId = await _connection!.InvokeAsync<string>("JoinGroupAsync", groupName);
        _logger.LogInformation("Joined group: {GroupName} (ID: {GroupId})", groupName, _currentGroupId);
        return !string.IsNullOrEmpty(_currentGroupId);
    }

    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<BattleStatus?>("GetBattleStatusAsync");
    }

    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
    }

    public async Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        EnsureConnected();
        _logger.LogInformation("Requesting battle reproduction - BattleId: {BattleId}, Seed: {Seed}, GroupName: {GroupName}", battleId, seedValue, groupName);

        var result = await _connection!.InvokeAsync<bool>("ReproduceBattleAsync", battleId, seedValue, groupName);
        return result;
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

        _connection.On<string, string>("KeyChanged", (key, value) => OnKeyChanged?.Invoke(key, value));
        _connection.On<string>("KeyDeleted", key => OnKeyDeleted?.Invoke(key));
        _connection.On<string, string>("GroupMessage", (connectionId, message) => OnGroupMessage?.Invoke(connectionId, message));
        _connection.On<MemberJoinedData>("MemberJoined", (data) =>
        {
            _logger.LogBattleInfo(new BattleLogMessages.MemberJoined(data.ConnectionId, data.GroupName));
            _logger.LogBattleInfo(new BattleLogMessages.GroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
            if (data.CurrentMemberCount == data.MaxMembers)
            {
                _logger.LogBattleInfo(new BattleLogMessages.GroupFull());
            }
            OnMemberJoined?.Invoke(data);
        });
        _connection.On<MemberLeftData>("MemberLeft", (data) =>
        {
            _logger.LogBattleInfo(new BattleLogMessages.MemberLeft(data.ConnectionId, data.GroupName));
            _logger.LogBattleInfo(new BattleLogMessages.GroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
            OnMemberLeft?.Invoke(data);
        });
        _connection.On<ConnectionsReadyData>("ConnectionsReady", data =>
        {
            _logger.LogBattleInfo(new BattleLogMessages.ConnectionReadyHeader());
            _logger.LogBattleInfo(new BattleLogMessages.ConnectionsReady());
            _logger.LogBattleInfo(new BattleLogMessages.ConnectionsReadyDetails(data.BattleId.ToString(), data.Seed));

            OnConnectionsReady?.Invoke(data);

            // Automatically confirm connection ready
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogBattleInfo(new BattleLogMessages.ConfirmingConnection());
                    var result = await ConfirmConnectionReadyAsync();
                    _logger.LogBattleInfo(new BattleLogMessages.ConnectionConfirmed(result));
                }
                catch (Exception ex)
                {
                    _logger.LogBattleError(new BattleLogMessages.ConnectionConfirmationFailed());
                    _logger.LogError(ex, "Exception details");
                }
            });
        });

        _connection.On<BattleStartedData>("BattleStarted", (data) =>
        {
            _logger.LogBattleInfo(new BattleLogMessages.BattleStarted());
            _logger.LogBattleInfo(new BattleLogMessages.BattleStartedDetails(data.BattleId.ToString(), data.Seed));
            OnBattleStarted?.Invoke(data);
        });

        _connection.On<BattleReplayData>("BattleReplayData", async (replayData) =>
        {
            try
            {
                _logger.LogBattleInfo(new BattleLogMessages.ReplayChunkReceived(replayData.ChunkIndex, replayData.TotalChunks, replayData.TurnData.Count, (long)replayData.Seed));

                // Store the chunk
                _replayChunks[replayData.ChunkIndex] = replayData.TurnData;
                _expectedTotalChunks = replayData.TotalChunks;

                // Store battle summary if this is the last chunk
                if (replayData.IsLastChunk && replayData.Summary.HasValue)
                {
                    _battleSummary = replayData.Summary.Value;
                }

                OnBattleReplayData?.Invoke(replayData);

                // Check if we have all chunks
                if (_replayChunks.Count == _expectedTotalChunks)
                {
                    await PlayBattleReplayAsync(replayData.BattleId, replayData.Seed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing battle replay data");
            }
        });

        _connection.On<BattleStatus>("BattleCompleted", (battleStatus) =>
        {
            _logger.LogInformation("[BATTLE] Battle completed! Final status received.");
            // Battle completion is handled in the replay playback
        });

        _connection.On<GroupDissolvedData>("GroupDissolved", (data) =>
        {
        _logger.LogBattleWarning(new BattleLogMessages.GroupDissolved(data.GroupName, data.GroupId, data.Reason));
            OnGroupDissolved?.Invoke(data);
        });

        _connection.On<GroupExtendedData>("GroupExtended", (data) =>
        {
            _logger.LogBattleInfo(new BattleLogMessages.GroupExtended(data.GroupName, data.GroupId, data.ExtensionCount, data.MaxExtensions, data.NewExpiryTime));
            OnGroupExtended?.Invoke(data);
        });

        _connection.Closed += error =>
        {
            OnDisconnected?.Invoke(error?.Message ?? "Connection closed");
            return Task.CompletedTask;
        };
    }

    private async Task PlayBattleReplayAsync(Guid battleId, int? seed)
    {
        var seedValue = seed ?? 0;
        _logger.LogBattleInfo(new BattleLogMessages.AllChunksReceived(battleId.ToString(), seedValue));

        // Reconstruct complete replay data using the service
        var battleStatuses = _replayRenderer.ReconstructReplayData(_replayChunks, _expectedTotalChunks);

        // Play the replay using the service (disable showing total turns to avoid spoilers)
        await _replayRenderer.PlayReplayAsync(battleStatuses, battleId, seed, _battleSummary);

        // Clean up
        _replayChunks.Clear();

        // Signal that the battle is complete
        _battleCompletionSource.TrySetResult(true);

        // Auto-disconnect after replay completion
        _logger.LogBattleInfo(new BattleLogMessages.AutoDisconnecting());
        await DisconnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
