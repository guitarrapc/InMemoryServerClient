using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Shared.Battle;
using Shared.Constants;
using Shared.Contracts;
using Shared.Models;
using CliClient.Extensions;
using CliClient.Models;
using CliClient.Services;

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

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        if (_connection != null && IsConnected)
        {
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation("Connecting to server: {ServerUrl}", serverUrl);

            _connection = new HubConnectionBuilder()
                .WithUrl(_serverUrl + SystemDefines.HubRoute)
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
                _logger.LogInformation("Disconnecting from server");
                await _connection.DisposeAsync();
                _connection = null;
                _currentGroupId = string.Empty;
                _logger.LogInformation("Disconnected from server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnection");
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
        // Simplified implementation - just log that watching is requested
        _logger.LogInformation("Watching key: {Key}", key);
        await _connection!.InvokeAsync("WatchAsync", key);
    }

    public async Task<bool> BroadcastAsync(string message)
    {
        return await BroadcastMessageAsync(message);
    }

    public async Task<ClientGroupInfo?> GetMyGroupAsync()
    {
        return await GetCurrentGroupAsync();
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

    public async Task<IReadOnlyList<ClientGroupInfo>> GetGroupsAsync()
    {
        EnsureConnected();
        var groups = await _connection!.InvokeAsync<IEnumerable<GroupInfo>>("GetGroupsAsync");

        // Convert GroupInfo to ClientGroupInfo
        return groups.Select(g => new ClientGroupInfo(
            g.GroupId,
            g.Name,
            g.ConnectionCount,
            g.MaxConnections,
            g.CreatedAt.Add(TimeSpan.FromMinutes(10)) - DateTime.UtcNow // Approximate remaining time
        )).ToList();
    }

    public async Task<ClientGroupInfo?> GetCurrentGroupAsync()
    {
        EnsureConnected();
        var groupInfo = await _connection!.InvokeAsync<GroupInfo?>("GetCurrentGroupAsync");
        if (groupInfo == null) return null;

        return new ClientGroupInfo(
            groupInfo.GroupId,
            groupInfo.Name,
            groupInfo.ConnectionCount,
            groupInfo.MaxConnections,
            groupInfo.CreatedAt.Add(TimeSpan.FromMinutes(10)) - DateTime.UtcNow // Approximate remaining time
        );
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
        _logger.LogInformation("Requesting battle reproduction - BattleId: {BattleId}, Seed: {Seed}, GroupName: {GroupName}",
            battleId, seedValue, groupName);

        var result = await _connection!.InvokeAsync<bool>("ReproduceBattleAsync", battleId, seedValue, groupName);
        return result;
    }

    public async Task<ServerStatusInfo> GetServerStatusAsync()
    {
        EnsureConnected();
        var serverStatus = await _connection!.InvokeAsync<ServerStatus>("GetServerStatusAsync");

        // Convert ServerStatus to ServerStatusInfo
        var groups = serverStatus.Groups.Select(g => new ClientGroupInfo(
            g.GroupId,
            g.Name,
            g.ConnectionCount,
            SystemDefines.MaxConnectionsPerGroup,
            TimeSpan.Zero // TODO: Calculate remaining time
        )).ToList() ?? [];

        return new ServerStatusInfo(
            serverStatus.Uptime,
            serverStatus.TotalConnections,
            serverStatus.GroupCount,
            serverStatus.ActiveBattleCount,
            groups
        );
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
            _logger.LogInformation("[BATTLE] ========== Battle Started! ==========");
            _logger.LogInformation("[BATTLE] 🏆 Battle ID: {BattleId}", data.BattleId);
            _logger.LogInformation("[BATTLE] 🎲 Seed: {Seed}", data.Seed);
            _logger.LogInformation("[BATTLE] ====================================");
            OnBattleStarted?.Invoke(data);
        });

        _connection.On<BattleReplayData>("BattleReplayData", async (replayData) =>
        {
            try
            {
                _logger.LogInformation("[BATTLE] Received replay chunk {ChunkIndex}/{TotalChunks} with {TurnCount} turns - BattleId: {BattleId}, Seed: {Seed}",
                    replayData.ChunkIndex + 1, replayData.TotalChunks, replayData.TurnData.Count, replayData.BattleId, replayData.Seed);

                // Store the chunk
                _replayChunks[replayData.ChunkIndex] = replayData.TurnData;
                _expectedTotalChunks = replayData.TotalChunks;

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

        _connection.On<GroupDissolvedData>("GroupDissolved", (data) =>
        {
            _logger.LogWarning("[GROUP] ❌ Group dissolved! Group: {GroupName} (ID: {GroupId})", data.GroupName, data.GroupId);
            _logger.LogWarning("[GROUP] 📄 Reason: {Reason}", data.Reason);
            _logger.LogInformation("[GROUP] Connection will be closed automatically.");
            OnGroupDissolved?.Invoke(data);
        });

        _connection.On<GroupExtendedData>("GroupExtended", (data) =>
        {
            _logger.LogInformation("[GROUP] ⏰ Group extended! Group: {GroupName} (ID: {GroupId})", data.GroupName, data.GroupId);
            _logger.LogInformation("[GROUP] 🔄 Extension count: {ExtensionCount}/{MaxExtensions}", data.ExtensionCount, data.MaxExtensions);
            _logger.LogInformation("[GROUP] 📅 New expiry time: {NewExpiryTime:yyyy-MM-dd HH:mm:ss}", data.NewExpiryTime);
            OnGroupExtended?.Invoke(data);
        });

        _connection.On<BattleStatus>("BattleCompleted", (battleStatus) =>
        {
            _logger.LogInformation("[BATTLE] Battle completed! Final status received.");
            // Battle completion is handled in the replay playback
        });

        _connection.Closed += error =>
        {
            OnDisconnected?.Invoke(error?.Message ?? "Connection closed");
            return Task.CompletedTask;
        };
    }

    private async Task PlayBattleReplayAsync(Guid battleId, int? seed)
    {
        _logger.LogInformation("[BATTLE] All chunks received. Starting replay playback - BattleId: {BattleId}, Seed: {Seed}",
            battleId, seed);

        // Reconstruct complete replay data using the service
        var battleStatuses = _replayRenderer.ReconstructReplayData(_replayChunks, _expectedTotalChunks);

        // Play the replay using the service
        await _replayRenderer.PlayReplayAsync(battleStatuses, battleId, seed);

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
