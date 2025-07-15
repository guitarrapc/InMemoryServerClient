using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Shared.Battle;
using Shared.Constants;
using Shared.Contracts;
using Shared.Models;

namespace CliClient.Clients;

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
            _logger.LogInformation("[GROUP] 👤 New member joined! Connection ID: {ConnectionId} in group {GroupName}",
                data.ConnectionId, data.GroupName);
            _logger.LogInformation("[GROUP] 🔢 Total group members: {MemberCount}/{MaxMembers}",
                data.CurrentMemberCount, data.MaxMembers);
            if (data.CurrentMemberCount == data.MaxMembers)
            {
                _logger.LogInformation("[GROUP] ✅ Group is now full! Battle will start soon...");
            }
            OnMemberJoined?.Invoke(data);
        });
        _connection.On<MemberLeftData>("MemberLeft", (data) =>
        {
            _logger.LogInformation("[GROUP] 👋 Member left! Connection ID: {ConnectionId} from group {GroupName}",
                data.ConnectionId, data.GroupName);
            _logger.LogInformation("[GROUP] 🔢 Total group members: {MemberCount}/{MaxMembers}",
                data.CurrentMemberCount, data.MaxMembers);
            OnMemberLeft?.Invoke(data);
        });
        _connection.On<ConnectionsReadyData>("ConnectionsReady", data =>
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

    /// <summary>
    /// Renders a visual representation of the battle field using box-drawing characters
    /// </summary>
    private void RenderBattleField(BattleStatus status)
    {
        // First build the field with entity positions
        var field = BuildBattleField(status);

        // Calculate correct border width (each cell is 2 chars wide + separators)
        // For a 20x20 field with 2 chars per cell and a space between: 20*2 + 19 = 59 chars total width
        int borderWidth = status.FieldWidth * 2 + (status.FieldWidth - 1);

        // Draw top border
        _logger.LogInformation("[BATTLE FIELD] ┌{Border}┐", new string('─', borderWidth));

        // Draw field rows
        for (int y = 0; y < status.FieldHeight; y++)
        {
            var line = new System.Text.StringBuilder("│");

            for (int x = 0; x < status.FieldWidth; x++)
            {
                var cellContent = field[y, x];

                if (cellContent == null)
                {
                    // Empty cell
                    line.Append("  ");
                }
                else
                {
                    // Determine if this is a player or enemy
                    bool isPlayer = status.Players.Any(p => p.EntityId == cellContent);

                    if (isPlayer)
                    {
                        // Player: P1, P2, etc.
                        int playerIdx = status.Players.FindIndex(p => p.EntityId == cellContent) + 1;
                        line.Append($"P{playerIdx}");
                    }
                    else
                    {
                        // Enemy: E1, E2, etc.
                        int enemyIdx = status.Enemies.FindIndex(e => e.EntityId == cellContent) + 1;
                        line.Append($"E{enemyIdx}");
                    }
                }

                // Add separator except for the last column
                if (x < status.FieldWidth - 1)
                {
                    line.Append(' ');
                }
            }

            line.Append('│');
            _logger.LogInformation("[BATTLE FIELD] {Line}", line.ToString());
        }

        // Draw bottom border with the same width as the top border
        _logger.LogInformation("[BATTLE FIELD] └{Border}┘", new string('─', borderWidth));

        // Add a legend for easier identification
        var playerLegend = new System.Text.StringBuilder("Players: ");
        for (int i = 0; i < status.Players.Count; i++)
        {
            var player = status.Players[i];
            if (player.CurrentHp > 0)
            {
                playerLegend.Append($"P{i + 1}={player.Name}({player.CurrentHp}/{player.MaxHp}) ");
            }
        }
        _logger.LogInformation("[BATTLE FIELD] {PlayerLegend}", playerLegend.ToString());

        var enemyLegend = new System.Text.StringBuilder("Enemies: ");
        for (int i = 0; i < status.Enemies.Count; i++)
        {
            var enemy = status.Enemies[i];
            if (enemy.CurrentHp > 0)
            {
                enemyLegend.Append($"E{i + 1}={enemy.Name}({enemy.CurrentHp}/{enemy.MaxHp}) ");
            }
        }
        _logger.LogInformation("[BATTLE FIELD] {EnemyLegend}", enemyLegend.ToString());
    }

    /// <summary>
    /// Generate a text-based health bar
    /// </summary>
    private string GenerateHealthBar(int current, int max, int length)
    {
        int filledLength = (int)Math.Round((double)current / max * length);

        // ASCII-compatible characters for better Windows cmd.exe compatibility
        string filled = new string('=', filledLength);
        string empty = new string('-', length - filledLength);

        return $"[{filled}{empty}]";
    }

    /// <summary>
    /// Builds a 2D field array from player and enemy positions
    /// </summary>
    private Guid?[,] BuildBattleField(BattleStatus status)
    {
        var field = new Guid?[status.FieldHeight, status.FieldWidth];

        // Place players on field
        foreach (var player in status.Players)
        {
            if (player.CurrentHp > 0 &&
                player.Position.X >= 0 && player.Position.X < status.FieldWidth &&
                player.Position.Y >= 0 && player.Position.Y < status.FieldHeight)
            {
                field[player.Position.Y, player.Position.X] = player.EntityId;
            }
        }

        // Place enemies on field
        foreach (var enemy in status.Enemies)
        {
            if (enemy.CurrentHp > 0 &&
                enemy.Position.X >= 0 && enemy.Position.X < status.FieldWidth &&
                enemy.Position.Y >= 0 && enemy.Position.Y < status.FieldHeight)
            {
                field[enemy.Position.Y, enemy.Position.X] = enemy.EntityId;
            }
        }

        return field;
    }

   public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
