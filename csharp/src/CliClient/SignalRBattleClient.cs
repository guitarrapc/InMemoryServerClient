using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Shared.Battle;
using Shared.Constants;
using Shared.Contracts;
using Shared.Models;

namespace CliClient;

/// <summary>
/// SignalR implementation of IInMemoryServerClient
/// </summary>
internal class SignalRBattleClient : IBattleClient
{
    private readonly ILogger<SignalRBattleClient> _logger;
    private HubConnection? _connection;
    private string _serverUrl = string.Empty;
    private string _currentGroupId = string.Empty;

    // Battle replay settings
    private const int BattleReplayFps = 5; // 5fps for battle replay
    private const int BattleReplayFrameTimeMs = 1000 / BattleReplayFps; // Time in ms between frames

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
    public event Action<string, int>? OnMemberJoined;
    public event Action<string, string>? OnGroupMessage;
    public event Action<string>? OnConnectionsReady;
    public event Action<string>? OnBattleStarted;
    public event Action<BattleReplayData>? OnBattleReplayData;

    public SignalRBattleClient(ILogger<SignalRBattleClient> logger)
    {
        _logger = logger;
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

            if (!string.IsNullOrEmpty(groupName))
            {
                return await JoinGroupAsync(groupName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try
            {
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

    public async Task<BattleReplayData?> GetBattleReplayAsync(string battleId)
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

    private void SetupEventHandlers()
    {
        if (_connection == null) return;

        _connection.On<string, string>("KeyChanged", (key, value) => OnKeyChanged?.Invoke(key, value));
        _connection.On<string>("KeyDeleted", key => OnKeyDeleted?.Invoke(key));
        _connection.On<string, int>("MemberJoined", (connectionId, count) => OnMemberJoined?.Invoke(connectionId, count));
        _connection.On<string, string>("GroupMessage", (connectionId, message) => OnGroupMessage?.Invoke(connectionId, message));

        _connection.On<string>("ConnectionsReady", async (battleId) =>
        {
            _logger.LogInformation("[BATTLE] ========== Connections Ready! ==========");
            _logger.LogInformation("[BATTLE] 🔄 Battle ID: {BattleId}", battleId);
            _logger.LogInformation("[BATTLE] Group is full! All clients connected.");
            _logger.LogInformation("[BATTLE] ========================================");

            OnConnectionsReady?.Invoke(battleId);

            // Automatically confirm connection ready
            try
            {
                var result = await ConfirmConnectionReadyAsync();
                _logger.LogInformation("[BATTLE] Connection ready confirmation sent. Result: {Result}", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm connection ready status");
            }
        });

        _connection.On<string>("BattleStarted", (battleId) =>
        {
            _logger.LogInformation("[BATTLE] ========== Battle Started! ==========");
            _logger.LogInformation("[BATTLE] 🏆 Battle ID: {BattleId}", battleId);
            _logger.LogInformation("[BATTLE] ====================================");
            OnBattleStarted?.Invoke(battleId);
        });

        _connection.On<BattleReplayData>("BattleReplayData", async (replayData) =>
        {
            try
            {
                _logger.LogInformation("[BATTLE] Received replay chunk {ChunkIndex}/{TotalChunks} with {TurnCount} turns",
                    replayData.ChunkIndex + 1, replayData.TotalChunks, replayData.TurnData.Count);

                // Store the chunk
                _replayChunks[replayData.ChunkIndex] = replayData.TurnData;
                _expectedTotalChunks = replayData.TotalChunks;

                OnBattleReplayData?.Invoke(replayData);

                // Check if we have all chunks
                if (_replayChunks.Count == _expectedTotalChunks)
                {
                    await PlayBattleReplayAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing battle replay data");
            }
        });

        _connection.Closed += error =>
        {
            OnDisconnected?.Invoke(error?.Message ?? "Connection closed");
            return Task.CompletedTask;
        };
    }

    private async Task PlayBattleReplayAsync()
    {
        _logger.LogInformation("[BATTLE] All chunks received. Starting replay playback...");

        // Reconstruct complete replay data
        List<BattleStatus> battleStatuses = [];
        for (int i = 0; i < _expectedTotalChunks; i++)
        {
            if (_replayChunks.TryGetValue(i, out var chunk))
            {
                battleStatuses.AddRange(chunk);
            }
        }

        _logger.LogInformation("[BATTLE] Playing {TurnCount} turns at {Fps} FPS", battleStatuses.Count, BattleReplayFps);

        // Play battle replay
        for (int i = 0; i < battleStatuses.Count; i++)
        {
            var status = battleStatuses[i];
            DisplayBattleStatus(status, i + 1, battleStatuses.Count);
            await Task.Delay(BattleReplayFrameTimeMs);
        }

        // Notify completion
        try
        {
            var result = await NotifyBattleReplayCompleteAsync();
            _logger.LogInformation("[BATTLE] Replay completion notification sent. Result: {Result}", result);
            _battleCompletionSource.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify battle replay completion");
            _battleCompletionSource.TrySetException(ex);
        }

        // Clean up
        _replayChunks.Clear();
    }

    private void DisplayBattleStatus(BattleStatus status, int currentTurn, int totalTurns)
    {
        // Display battle status (simplified)
        var alivePlayerCount = status.Players.Count(e => e.CurrentHp > 0);
        var aliveEnemyCount = status.Enemies.Count(e => e.CurrentHp > 0);
        _logger.LogInformation("[BATTLE] Turn {CurrentTurn}/{TotalTurns} - Players: {AlivePlayerCount}, Enemies: {AliveEnemyCount}",
            currentTurn, totalTurns, alivePlayerCount, aliveEnemyCount);
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to server");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        var groupId = await _connection!.InvokeAsync<string>("JoinGroupAsync", groupName);
        if (!string.IsNullOrEmpty(groupId))
        {
            _currentGroupId = groupId;
            return true;
        }
        return false;
    }

    public async Task<bool> BroadcastMessageAsync(string message)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("BroadcastMessageAsync", message);
    }

    public async Task<IReadOnlyList<ClientGroupInfo>> GetGroupsAsync()
    {
        EnsureConnected();
        var groups = await _connection!.InvokeAsync<GroupInfo[]>("GetGroupsAsync");
        return groups.Select(g => new ClientGroupInfo(
            g.Id,
            g.Name,
            g.ConnectionCount,
            g.MaxConnections,
            g.ExpiresAt - DateTime.UtcNow // Remaining time
        )).ToArray();
    }

    public async Task<ClientGroupInfo?> GetCurrentGroupAsync()
    {
        if (string.IsNullOrEmpty(_currentGroupId))
            return null;

        EnsureConnected();
        var group = await _connection!.InvokeAsync<GroupInfo?>("GetGroupAsync", _currentGroupId);
        if (group == null)
            return null;

        return new ClientGroupInfo(
            group.Id,
            group.Name,
            group.ConnectionCount,
            group.MaxConnections,
            group.ExpiresAt - DateTime.UtcNow // Remaining time
        );
    }

    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
    }

    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<BattleStatus?>("GetBattleStatusAsync");
    }

    public async Task<bool> NotifyBattleReplayCompleteAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("NotifyBattleReplayCompleteAsync");
    }

    public async Task<ServerStatusInfo> GetServerStatusAsync()
    {
        EnsureConnected();
        var status = await _connection!.InvokeAsync<ServerStatus>("GetServerStatusAsync");

        // Convert ServerStatus to ServerStatusInfo
        var groups = status.Groups.Select(g => new ClientGroupInfo(
            g.Id,
            g.Name,
            g.ConnectionCount,
            5, // Max members (SystemDefines.MaxConnectionsPerGroup)
            TimeSpan.Zero // RemainingTime - not available in current GroupSummary
        )).ToArray();

        return new ServerStatusInfo(
            status.Uptime,
            status.TotalConnections,
            status.GroupCount,
            status.ActiveBattleCount,
            groups
        );
    }
}
