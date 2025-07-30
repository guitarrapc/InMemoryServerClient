using MagicOnion.Client;
using Microsoft.Extensions.Logging;
using Shared.Battle;
using Shared.Constants;
using Shared.Contracts;
using Shared.Contracts.Http2Server;
using Shared.Models;
using Grpc.Net.Client;
using System.Diagnostics.CodeAnalysis;
using CliClient.Extensions;
using CliClient.Models;

namespace CliClient.Clients;

/// <summary>
/// MagicOnion implementation of IInMemoryServerClient
/// </summary>
public class MagicOnionBattleClient : IBattleClient, IMagicOnionBattleHubReceiver, IAsyncDisposable
{
    private readonly ILogger<MagicOnionBattleClient> _logger;
    private IMagicOnionBattleHub? _hub;
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
            _hub = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(_channel, this);

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

    public async Task<bool> ConnectAsync(string serverUrl, HttpMessageHandler httpHandler, string? groupName = null)
    {
        if (_hub != null && IsConnected)
        {
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation("Connecting to MagicOnion test server: {ServerUrl}", serverUrl);

            // Create gRPC channel with test server handler
            _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
            {
                HttpHandler = httpHandler,
                UnsafeUseInsecureChannelCallCredentials = serverUrl.StartsWith("http://"),
            });

            // Connect to the streaming hub
            _hub = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(_channel, this);

            _logger.LogInformation("Connected to MagicOnion test server");

            if (!string.IsNullOrEmpty(groupName))
            {
                await JoinGroupAsync(groupName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MagicOnion test server");
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
        return await _hub.GetAsync(key);
    }

    public async Task<bool> SetAsync(string key, string value)
    {
        EnsureConnected();
        return await _hub.SetAsync(key, value);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        EnsureConnected();
        return await _hub.DeleteAsync(key);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null)
    {
        return await ListAsync(pattern);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string? pattern = null)
    {
        EnsureConnected();
        var result = await _hub.ListKeysAsync(pattern);
        return result.ToList();
    }

    public async Task WatchAsync(string key)
    {
        EnsureConnected();
        _logger.LogInformation("Watching key: {Key}", key);
        await _hub.WatchAsync(key);
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
        var battleReplayData = await _hub.GetBattleReplayAsync(battleId);
        return battleReplayData;
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
        return await _hub.BroadcastAsync(message);
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        _currentGroupId = await _hub.JoinGroupAsync(groupName);
        _logger.LogInformation("Joined group: {GroupId}", _currentGroupId);
        return !string.IsNullOrEmpty(_currentGroupId);
    }

    public async Task<IReadOnlyList<ClientGroupInfo>> GetGroupsAsync()
    {
        EnsureConnected();
        var groups = await _hub.GetGroupsAsync();

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
        var groupInfo = await _hub.GetCurrentGroupAsync();

        if (groupInfo == null) return null;

        // Convert GroupInfo to ClientGroupInfo
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
        return await _hub.GetBattleStatusAsync();
    }

    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        return await _hub.ConfirmConnectionReadyAsync();
    }

    public async Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        EnsureConnected();
        _logger.LogInformation("Requesting battle reproduction - BattleId: {BattleId}, Seed: {Seed}, GroupName: {GroupName}", battleId, seedValue, groupName);

        var result = await _hub.ReproduceBattleAsync(battleId, seedValue, groupName);
        return result;
    }

    public async Task<ServerStatusInfo> GetServerStatusAsync()
    {
        EnsureConnected();
        var serverStatus = await _hub.GetServerStatusAsync();

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

    [MemberNotNull(nameof(_hub))]
    private void EnsureConnected()
    {
        if (_hub == null)
        {
            throw new InvalidOperationException("Not connected to server. Call ConnectAsync first.");
        }
    }

    // IInMemoryHubReceiver implementation
    void IMagicOnionBattleHubReceiver.OnKeyChanged(string key, string value)
    {
        OnKeyChanged?.Invoke(key, value);
    }

    void IMagicOnionBattleHubReceiver.OnKeyDeleted(string key)
    {
        OnKeyDeleted?.Invoke(key);
    }

    void IMagicOnionBattleHubReceiver.OnGroupMessage(string senderId, string message)
    {
        OnGroupMessage?.Invoke(senderId, message);
    }

    void IMagicOnionBattleHubReceiver.OnMemberJoined(MemberJoinedData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.MemberJoined(data.ConnectionId, data.GroupName));
        _logger.LogBattleInfo(new BattleLogMessages.GroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
        if (data.CurrentMemberCount == data.MaxMembers)
        {
            _logger.LogBattleInfo(new BattleLogMessages.GroupFull());
        }
        OnMemberJoined?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnMemberLeft(MemberLeftData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.MemberLeft(data.ConnectionId, data.GroupName));
        _logger.LogBattleInfo(new BattleLogMessages.GroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
        OnMemberLeft?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnConnectionsReady(ConnectionsReadyData data)
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
    }

    void IMagicOnionBattleHubReceiver.OnBattleStarted(BattleStartedData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.BattleStarted());
        _logger.LogBattleInfo(new BattleLogMessages.BattleStartedDetails(data.BattleId.ToString(), data.Seed));
        OnBattleStarted?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnBattleReplayData(BattleReplayData replayData)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogBattleInfo(new BattleLogMessages.ReplayChunkReceived(replayData.ChunkIndex, replayData.TotalChunks, replayData.TurnData.Count, (long)replayData.Seed));

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

    void IMagicOnionBattleHubReceiver.OnBattleCompleted(BattleStatus battleStatus)
    {
        _logger.LogInformation("[BATTLE] Battle completed!");
        // Battle completion is handled in the replay playback
    }

    void IMagicOnionBattleHubReceiver.OnGroupDissolved(GroupDissolvedData data)
    {
        _logger.LogBattleWarning(new BattleLogMessages.GroupDissolved(data.GroupName, data.GroupId, data.Reason));
        OnGroupDissolved?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnGroupExtended(GroupExtendedData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.GroupExtended(data.GroupName, data.GroupId, data.ExtensionCount, data.MaxExtensions, data.NewExpiryTime));
        OnGroupExtended?.Invoke(data);
    }

    private async Task PlayBattleReplayAsync(Guid battleId, int? seed)
    {
        var seedValue = seed ?? 0;
        _logger.LogBattleInfo(new BattleLogMessages.AllChunksReceived(battleId.ToString(), seedValue));

        // Reconstruct complete replay data
        List<BattleStatus> battleStatuses = [];
        for (int i = 0; i < _expectedTotalChunks; i++)
        {
            if (_replayChunks.TryGetValue(i, out var chunk))
            {
                battleStatuses.AddRange(chunk);
            }
        }

        _logger.LogBattleInfo(new BattleLogMessages.ReplayStarting(battleStatuses.Count, BattleReplayFps, battleId.ToString(), seedValue));
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

        // Display battle completion details
        _logger.LogInformation("[BATTLE REPLAY] Total turns: {CurrentTurn}/{TotalTurns}", finalStatus.CurrentTurn, finalStatus.TotalTurns);

        // Display how the battle ended using the new property
        if (finalStatus.IsEndedByTurnLimit == true)
        {
            _logger.LogInformation("[BATTLE REPLAY] ⏰ Battle ended due to turn limit reached!");
        }
        else if (finalStatus.IsEndedByTurnLimit == false)
        {
            _logger.LogInformation("[BATTLE REPLAY] ⚔️ Battle ended due to complete elimination!");
        }

        _logger.LogInformation("[BATTLE REPLAY] Battle completed - BattleId: {BattleId}, Seed: {Seed} (replay completed)", battleId, seed);
        _logger.LogInformation("[BATTLE REPLAY] ===============================================");

        // Clean up
        _replayChunks.Clear();

        // Signal that the battle is complete
        _battleCompletionSource.TrySetResult(true);

        // Auto-disconnect after replay completion
        _logger.LogBattleInfo(new BattleLogMessages.AutoDisconnecting());
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
                _logger.LogInformation("[BATTLE] {PlayerName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}", player.Name, jobInfo, player.CurrentHp, player.MaxHp, healthBar, player.Attack, player.Defense, player.Speed, player.Position);
            }

            // Display enemies info
            var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);
            _logger.LogInformation("[BATTLE] Enemies alive: {AliveEnemies}/{TotalEnemies}", aliveEnemies, status.Enemies.Count);
            foreach (var enemy in status.Enemies.Where(x => x.CurrentHp > 0).Take(2)) // Show first 2 enemies to avoid spam
            {
                var healthBar = GenerateHealthBar(enemy.CurrentHp, enemy.MaxHp, 10);
                var jobInfo = enemy.EnemyJob.HasValue ? $" ({enemy.EnemyJob})" : "";
                _logger.LogInformation("[BATTLE] {EnemyName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}", enemy.Name, jobInfo, enemy.CurrentHp, enemy.MaxHp, healthBar, enemy.Attack, enemy.Defense, enemy.Speed, enemy.Position);
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
        int borderWidth = status.FieldSize.X * 2 + (status.FieldSize.X - 1);

        // Draw top border
        _logger.LogInformation("[BATTLE FIELD] ┌{Border}┐", new string('─', borderWidth));

        // Draw field rows
        for (int y = 0; y < status.FieldSize.Y; y++)
        {
            var line = new System.Text.StringBuilder("│");

            for (int x = 0; x < status.FieldSize.X; x++)
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
                if (x < status.FieldSize.X - 1)
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
        var field = new Guid?[status.FieldSize.Y, status.FieldSize.X];

        // Place players on field
        foreach (var player in status.Players)
        {
            if (player.CurrentHp > 0 &&
                player.Position.X >= 0 && player.Position.X < status.FieldSize.X &&
                player.Position.Y >= 0 && player.Position.Y < status.FieldSize.Y)
            {
                field[player.Position.Y, player.Position.X] = player.EntityId;
            }
        }

        // Place enemies on field
        foreach (var enemy in status.Enemies)
        {
            if (enemy.CurrentHp > 0 &&
                enemy.Position.X >= 0 && enemy.Position.X < status.FieldSize.X &&
                enemy.Position.Y >= 0 && enemy.Position.Y < status.FieldSize.Y)
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

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}
