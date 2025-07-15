using MagicOnion.Client;
using Microsoft.Extensions.Logging;
using Shared.Battle;
using Shared.Constants;
using Shared.Contracts;
using Shared.Contracts.MagicOnion;
using Shared.Models;
using Grpc.Net.Client;

namespace CliClient.Clients;

/// <summary>
/// MagicOnion implementation of IInMemoryServerClient
/// </summary>
internal class MagicOnionBattleClient : IBattleClient, IInMemoryHubReceiver, IAsyncDisposable
{
    private readonly ILogger<MagicOnionBattleClient> _logger;
    private IInMemoryHub? _hub;
    private GrpcChannel? _channel;
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
#pragma warning disable CS0067 // The event is never used
    public event Action<string>? OnDisconnected;
#pragma warning restore CS0067
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

    public MagicOnionBattleClient(ILogger<MagicOnionBattleClient> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _hub != null;

    public async Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        if (_hub != null && IsConnected)
        {
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation("Connecting to MagicOnion server: {ServerUrl}", serverUrl);

            // Create gRPC channel
            _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
            {
                UnsafeUseInsecureChannelCallCredentials = serverUrl.StartsWith("http://"),
            });

            // Connect to the streaming hub
            _hub = await StreamingHubClient.ConnectAsync<IInMemoryHub, IInMemoryHubReceiver>(_channel, this);

            _logger.LogInformation("Connected to MagicOnion server");

            if (!string.IsNullOrEmpty(groupName))
            {
                await JoinGroupAsync(groupName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MagicOnion server");
            await DisconnectAsync();
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_hub != null)
            {
                _logger.LogInformation("Disconnecting from MagicOnion server");
                await _hub.DisposeAsync();
                _hub = null;
            }

            if (_channel != null)
            {
                _channel.Dispose();
                _channel = null;
            }

            _currentGroupId = string.Empty;
            _logger.LogInformation("Disconnected from MagicOnion server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MagicOnion disconnection");
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        EnsureConnected();
        return await _hub!.GetAsync(key);
    }

    public async Task<bool> SetAsync(string key, string value)
    {
        EnsureConnected();
        return await _hub!.SetAsync(key, value);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        EnsureConnected();
        return await _hub!.DeleteAsync(key);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null)
    {
        return await ListAsync(pattern);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string? pattern = null)
    {
        EnsureConnected();
        var result = await _hub!.ListKeysAsync(pattern);
        return result.ToList();
    }

    public async Task WatchAsync(string key)
    {
        EnsureConnected();
        _logger.LogInformation("Watching key: {Key}", key);
        await _hub!.WatchAsync(key);
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
        var replayString = await _hub!.GetBattleReplayAsync(battleId);

        // TODO: Parse the JSON string into BattleReplayData if needed
        // For now, return null as the server returns raw JSON
        return null;
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
        return await _hub!.BroadcastAsync(message);
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        _currentGroupId = await _hub!.JoinGroupAsync(groupName);
        _logger.LogInformation("Joined group: {GroupId}", _currentGroupId);
        return !string.IsNullOrEmpty(_currentGroupId);
    }

    public async Task<IReadOnlyList<ClientGroupInfo>> GetGroupsAsync()
    {
        EnsureConnected();
        var groups = await _hub!.GetGroupsAsync();

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
        var group = await _hub!.GetCurrentGroupAsync();

        if (group == null) return null;

        // Convert GroupInfo to ClientGroupInfo
        return new ClientGroupInfo(
            group.GroupId,
            group.Name,
            group.ConnectionCount,
            group.MaxConnections,
            group.CreatedAt.Add(TimeSpan.FromMinutes(10)) - DateTime.UtcNow // Approximate remaining time
        );
    }

    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        EnsureConnected();
        return await _hub!.GetBattleStatusAsync();
    }

    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        return await _hub!.ConfirmConnectionReadyAsync();
    }

    public async Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        EnsureConnected();
        return await _hub!.ReproduceBattleAsync(battleId, seedValue, groupName);
    }

    public async Task<ServerStatusInfo> GetServerStatusAsync()
    {
        EnsureConnected();
        var serverStatus = await _hub!.GetServerStatusAsync();

        // Convert ServerStatus to ServerStatusInfo
        return new ServerStatusInfo(
            serverStatus.Uptime,
            serverStatus.TotalConnections,
            serverStatus.GroupCount,
            serverStatus.ActiveBattleCount,
            serverStatus.Groups.Select(g => new ClientGroupInfo(
                g.GroupId,
                g.Name,
                g.ConnectionCount,
                SystemDefines.MaxConnectionsPerGroup,
                TimeSpan.Zero // TODO: Calculate remaining time
            )).ToList()
        );
    }

    private void EnsureConnected()
    {
        if (_hub == null)
        {
            throw new InvalidOperationException("Not connected to server. Call ConnectAsync first.");
        }
    }

    // IInMemoryHubReceiver implementation
    void IInMemoryHubReceiver.OnKeyChanged(string key, string value)
    {
        OnKeyChanged?.Invoke(key, value);
    }

    void IInMemoryHubReceiver.OnKeyDeleted(string key)
    {
        OnKeyDeleted?.Invoke(key);
    }

    void IInMemoryHubReceiver.OnGroupMessage(string senderId, string message)
    {
        OnGroupMessage?.Invoke(senderId, message);
    }

    void IInMemoryHubReceiver.OnMemberJoined(MemberJoinedData data)
    {
        _logger.LogInformation("[GROUP] 👤 New member joined! Connection ID: {ConnectionId} in group {GroupName}",
            data.ConnectionId, data.GroupName);
        _logger.LogInformation("[GROUP] 🔢 Total group members: {MemberCount}/{MaxMembers}",
            data.CurrentMemberCount, data.MaxMembers);
        if (data.CurrentMemberCount == data.MaxMembers)
        {
            _logger.LogInformation("[GROUP] ✅ Group is now full! Battle will start soon...");
        }
        OnMemberJoined?.Invoke(data);
    }

    void IInMemoryHubReceiver.OnMemberLeft(MemberLeftData data)
    {
        _logger.LogInformation("[GROUP] 👋 Member left! Connection ID: {ConnectionId} from group {GroupName}",
            data.ConnectionId, data.GroupName);
        _logger.LogInformation("[GROUP] 🔢 Total group members: {MemberCount}/{MaxMembers}",
            data.CurrentMemberCount, data.MaxMembers);
        OnMemberLeft?.Invoke(data);
    }

    void IInMemoryHubReceiver.OnConnectionsReady(ConnectionsReadyData data)
    {
        _logger.LogInformation("[BATTLE] ========== Connections Ready! ==========");
        _logger.LogInformation("[BATTLE] 🔄 Battle ID: {BattleId}", data.BattleId);
        _logger.LogInformation("[BATTLE] 🎲 Seed: {Seed}", data.Seed);
        _logger.LogInformation("[BATTLE] Group is full! All clients connected.");
        _logger.LogInformation("[BATTLE] Sending confirmation to server...");
        _logger.LogInformation("[BATTLE] ========================================");

        OnConnectionsReady?.Invoke(data);

        // Automatically confirm connection ready
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("[BATTLE] Confirming connection ready status...");
                var result = await ConfirmConnectionReadyAsync();
                _logger.LogInformation("[BATTLE] ✅ Connection ready confirmation sent successfully. Result: {Result}", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BATTLE] ❌ Failed to confirm connection ready status");
            }
        });
    }

    void IInMemoryHubReceiver.OnBattleStarted(BattleStartedData data)
    {
        _logger.LogInformation("[BATTLE] ========== Battle Started! ==========");
        _logger.LogInformation("[BATTLE] 🏆 Battle ID: {BattleId}", data.BattleId);
        _logger.LogInformation("[BATTLE] 🎲 Seed: {Seed}", data.Seed);
        _logger.LogInformation("[BATTLE] ====================================");
        OnBattleStarted?.Invoke(data);
    }

    void IInMemoryHubReceiver.OnBattleReplayData(BattleReplayData replayData)
    {
        _ = Task.Run(async () =>
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
    }

    void IInMemoryHubReceiver.OnBattleCompleted(BattleStatus battleStatus)
    {
        _logger.LogInformation("[BATTLE] Battle completed!");
        // Battle completion is handled in the replay playback
    }

    void IInMemoryHubReceiver.OnGroupDissolved(GroupDissolvedData data)
    {
        _logger.LogWarning("[GROUP] ❌ Group dissolved! Group: {GroupName} (ID: {GroupId})", data.GroupName, data.GroupId);
        _logger.LogWarning("[GROUP] 📄 Reason: {Reason}", data.Reason);
        _logger.LogInformation("[GROUP] Connection will be closed automatically.");
        OnGroupDissolved?.Invoke(data);
    }

    void IInMemoryHubReceiver.OnGroupExtended(GroupExtendedData data)
    {
        _logger.LogInformation("[GROUP] ⏰ Group extended! Group: {GroupName} (ID: {GroupId})", data.GroupName, data.GroupId);
        _logger.LogInformation("[GROUP] 🔄 Extension count: {ExtensionCount}/{MaxExtensions}", data.ExtensionCount, data.MaxExtensions);
        _logger.LogInformation("[GROUP] 📅 New expiry time: {NewExpiryTime:yyyy-MM-dd HH:mm:ss}", data.NewExpiryTime);
        OnGroupExtended?.Invoke(data);
    }

    private async Task PlayBattleReplayAsync(Guid battleId, int? seed)
    {
        _logger.LogInformation("[BATTLE] All chunks received. Starting replay playback - BattleId: {BattleId}, Seed: {Seed}",
            battleId, seed);

        // Reconstruct complete replay data
        List<BattleStatus> battleStatuses = [];
        for (int i = 0; i < _expectedTotalChunks; i++)
        {
            if (_replayChunks.TryGetValue(i, out var chunk))
            {
                battleStatuses.AddRange(chunk);
            }
        }

        _logger.LogInformation("[BATTLE] Playing {TurnCount} turns at {Fps} FPS - BattleId: {BattleId}, Seed: {Seed}",
            battleStatuses.Count, BattleReplayFps, battleId, seed);
        _logger.LogInformation("[BATTLE REPLAY] ========== Starting Battle Replay ==========");

        // Play battle replay
        for (int i = 0; i < battleStatuses.Count; i++)
        {
            var status = battleStatuses[i];
            DisplayBattleStatus(status, i + 1, battleStatuses.Count);
            await Task.Delay(BattleReplayFrameTimeMs);
        }

        // Display final results
        var finalStatus = battleStatuses.Last();
        var finalAlivePlayers = finalStatus.Players.Count(p => p.CurrentHp > 0);
        var finalAliveEnemies = finalStatus.Enemies.Count(e => e.CurrentHp > 0);

        _logger.LogInformation("[BATTLE REPLAY] ========== Battle Replay Completed! ==========");

        if (finalAliveEnemies == 0)
        {
            _logger.LogInformation("[BATTLE REPLAY] 🎉 Victory! All enemies defeated! 🎉");
            _logger.LogInformation("[BATTLE REPLAY] Surviving players: {AlivePlayers}/{TotalPlayers}", finalAlivePlayers, finalStatus.Players.Count);

            // Show surviving players stats
            foreach (var player in finalStatus.Players.Where(p => p.CurrentHp > 0))
            {
                var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                _logger.LogInformation("[BATTLE REPLAY] {PlayerName}: HP {CurrentHp}/{MaxHp} {HealthBar}", player.Name, player.CurrentHp, player.MaxHp, healthBar);
            }
        }
        else
        {
            _logger.LogInformation("[BATTLE REPLAY] ❌ Defeat! All players defeated! ❌");
            _logger.LogInformation("[BATTLE REPLAY] Remaining enemies: {AliveEnemies}/{TotalEnemies}", finalAliveEnemies, finalStatus.Enemies.Count);

            // Show surviving enemy stats
            foreach (var enemy in finalStatus.Enemies.Where(p => p.CurrentHp > 0))
            {
                var healthBar = GenerateHealthBar(enemy.CurrentHp, enemy.MaxHp, 20);
                _logger.LogInformation("[BATTLE REPLAY] {EnemyName}: HP {CurrentHp}/{MaxHp} {HealthBar}", enemy.Name, enemy.CurrentHp, enemy.MaxHp, healthBar);
            }
        }
        _logger.LogInformation("[BATTLE REPLAY] Total turns: {TotalTurns}", finalStatus.CurrentTurn);
        _logger.LogInformation("[BATTLE REPLAY] Battle completed - BattleId: {BattleId}, Seed: {Seed} (replay completed)",
            battleId, seed);
        _logger.LogInformation("[BATTLE REPLAY] ===============================================");

        // Clean up
        _replayChunks.Clear();

        // Signal that the battle is complete
        _battleCompletionSource.TrySetResult(true);

        // Auto-disconnect after replay completion
        _logger.LogInformation("[BATTLE] Auto-disconnecting after battle replay completion");
        await DisconnectAsync();
    }

    private void DisplayBattleStatus(BattleStatus status, int currentTurn, int totalTurns)
    {
        // Display only every 5th turn, plus the first and last turns
        bool shouldDisplay = currentTurn == 1 || currentTurn == totalTurns || status.CurrentTurn % 5 == 0;

        if (shouldDisplay)
        {
            // Display turn information
            _logger.LogInformation("[BATTLE] ===== Turn {CurrentTurn}/{TotalTurns} =====", status.CurrentTurn, status.TotalTurns);

            // Display visual battle field first for better overview
            RenderBattleField(status);

            // Display players info
            var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
            _logger.LogInformation("[BATTLE] Players alive: {AlivePlayers}/{TotalPlayers}", alivePlayers, status.Players.Count);
            foreach (var player in status.Players)
            {
                var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                var jobInfo = player.PlayerJob.HasValue ? $" ({player.PlayerJob})" : "";
                _logger.LogInformation("[BATTLE] {PlayerName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}",
                    player.Name, jobInfo, player.CurrentHp, player.MaxHp, healthBar, player.Attack, player.Defense, player.Speed, player.Position);
            }

            // Display enemies info
            var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);
            _logger.LogInformation("[BATTLE] Enemies alive: {AliveEnemies}/{TotalEnemies}", aliveEnemies, status.Enemies.Count);
            foreach (var enemy in status.Enemies.Where(x => x.CurrentHp > 0).Take(2)) // Show first 2 enemies to avoid spam
            {
                var healthBar = GenerateHealthBar(enemy.CurrentHp, enemy.MaxHp, 10);
                var jobInfo = enemy.EnemyJob.HasValue ? $" ({enemy.EnemyJob})" : "";
                _logger.LogInformation("[BATTLE] {EnemyName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}",
                    enemy.Name, jobInfo, enemy.CurrentHp, enemy.MaxHp, healthBar, enemy.Attack, enemy.Defense, enemy.Speed, enemy.Position);
            }

            // Display recent logs
            if (status.RecentLogs.Count > 0)
            {
                _logger.LogInformation("[BATTLE] Recent actions:");
                foreach (var log in status.RecentLogs)
                {
                    _logger.LogInformation("[BATTLE] > {Log}", log);
                }
            }

            _logger.LogInformation("[BATTLE] ========================================");
        }
    }

    private void RenderBattleField(BattleStatus status)
    {
        var field = new char[status.FieldHeight, status.FieldWidth];

        // Initialize field with empty spaces
        for (int y = 0; y < status.FieldHeight; y++)
        {
            for (int x = 0; x < status.FieldWidth; x++)
            {
                field[y, x] = '.';
            }
        }

        // Place players on the field
        foreach (var player in status.Players.Where(p => p.CurrentHp > 0))
        {
            if (player.Position.X >= 0 && player.Position.X < status.FieldWidth &&
                player.Position.Y >= 0 && player.Position.Y < status.FieldHeight)
            {
                field[player.Position.Y, player.Position.X] = 'P';
            }
        }

        // Place enemies on the field
        foreach (var enemy in status.Enemies.Where(e => e.CurrentHp > 0))
        {
            if (enemy.Position.X >= 0 && enemy.Position.X < status.FieldWidth &&
                enemy.Position.Y >= 0 && enemy.Position.Y < status.FieldHeight)
            {
                field[enemy.Position.Y, enemy.Position.X] = 'E';
            }
        }

        // Render the field
        _logger.LogInformation("[BATTLE] Battle Field:");
        for (int y = 0; y < status.FieldHeight; y++)
        {
            var row = "";
            for (int x = 0; x < status.FieldWidth; x++)
            {
                row += field[y, x] + " ";
            }
            _logger.LogInformation("[BATTLE] {Row}", row);
        }
    }

    private static string GenerateHealthBar(int currentHp, int maxHp, int barLength)
    {
        var percentage = (double)currentHp / maxHp;
        var filledLength = (int)(percentage * barLength);
        var emptyLength = barLength - filledLength;

        var bar = new string('█', filledLength) + new string('░', emptyLength);
        return $"[{bar}]";
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}
