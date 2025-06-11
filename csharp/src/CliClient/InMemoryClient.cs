using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Shared;

namespace CliClient;

/// <summary>
/// Client for InMemory server
/// </summary>
public class InMemoryClient
{
    private readonly ILogger<InMemoryClient> _logger;
    private HubConnection? _connection;
    private string _serverUrl = string.Empty;
    private string _currentGroupId = string.Empty;
    private int _clientId;

    // Battle replay settings
    private const int BattleReplayFps = 5; // 5fps for battle replay
    private const int BattleReplayFrameTimeMs = 1000 / BattleReplayFps; // Time in ms between frames

    // バトル完了を追跡するためのフィールド
    private TaskCompletionSource<bool>? _battleCompletionSource = null;
    private Action? _battleCompletedCallback = null;

    public InMemoryClient(ILogger<InMemoryClient> logger) : this(0, logger)
    {
    }

    public InMemoryClient(int clientId, ILogger<InMemoryClient> logger)
    {
        _clientId = clientId;
        _logger = logger;
    }

    /// <summary>
    /// Generate a text-based health bar
    /// </summary>
    private string GenerateHealthBar(int current, int max, int length)
    {
        int filledLength = (int)Math.Round((double)current / max * length);
        string filled = new string('█', filledLength);
        string empty = new string('░', length - filledLength);

        // Determine color based on health percentage (not used in console output but kept for future UI implementations)
        double percentage = (double)current / max;
        // Color would be used in a graphical UI

        return $"[{filled}{empty}]";
    }

    /// <summary>
    /// Connect to server
    /// </summary>
    public async Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        if (_connection != null && _connection.State == HubConnectionState.Connected)
        {
            _logger.LogInformation($"Client {_clientId}: Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation($"Client {_clientId}: Connecting to server: {serverUrl}");

            _connection = new HubConnectionBuilder()
                .WithUrl(_serverUrl + Constants.HubRoute)
                .WithAutomaticReconnect()
                .Build();

            // Set up event handlers
            _connection.On<string, string>("KeyChanged", (key, value) =>
            {
                _logger.LogInformation($"Client {_clientId}: [NOTIFICATION] Key changed: {key} = {value}");
            });

            _connection.On<string>("KeyDeleted", (key) =>
            {
                _logger.LogInformation($"Client {_clientId}: [NOTIFICATION] Key deleted: {key}");
            });

            _connection.On<string, int>("MemberJoined", (connectionId, count) =>
            {
                _logger.LogInformation($"Client {_clientId}: [GROUP] New member joined: {connectionId} (Total: {count})");
            });

            _connection.On<string, string>("GroupMessage", (connectionId, message) =>
            {
                _logger.LogInformation($"Client {_clientId}: [GROUP] Message from {connectionId}: {message}");
            });

            _connection.On<string>("ConnectionsReady", (battleId) =>
            {
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ========== Connections Ready! ==========");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] 🔄 Battle ID: {battleId}");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Group is full! All clients connected.");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Confirming connection ready status...");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ========================================");

                // Automatically confirm connection ready status
                Task.Run(async () =>
                {
                    try
                    {
                        await ConfirmConnectionReadyAsync();
                        _logger.LogInformation($"Client {_clientId}: [BATTLE] Connection ready confirmation sent to server");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Client {_clientId}: Failed to confirm connection ready status: {ex.Message}");
                    }
                });
            });

            _connection.On<string>("BattleStarted", (battleId) =>
            {
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ========== Battle Started! ==========");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] 🏆 Battle ID: {battleId}");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] All clients confirmed! Automatic battle starting...");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Preparing battlefield and players...");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ======================================");
            });

            _connection.On<BattleStatus>("BattleStatusUpdated", (status) =>
            {
                // Add delay for frame rate control (10fps)
                Task.Delay(BattleReplayFrameTimeMs).Wait();

                _logger.LogInformation($"Client {_clientId}: [BATTLE] ========== Turn {status.CurrentTurn}/{status.TotalTurns} ==========");

                // Display players info
                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Players alive: {alivePlayers}/{status.Players.Count}");
                foreach (var player in status.Players)
                {
                    var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                    _logger.LogInformation($"Client {_clientId}: [BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar} ATK:{player.Attack} DEF:{player.Defense} SPD:{player.Speed}");
                }

                // Display enemies info
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Enemies alive: {aliveEnemies}/{status.Enemies.Count}");

                // Display logs
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Recent actions:");
                foreach (var log in status.RecentLogs)
                {
                    _logger.LogInformation($"Client {_clientId}: [BATTLE] > {log}");
                }
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ====================================");
            });

            _connection.On<BattleStatus>("BattleCompleted", (status) =>
            {
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ========== Battle Completed! ==========");

                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);

                // Display outcome
                if (aliveEnemies == 0)
                {
                    _logger.LogInformation($"Client {_clientId}: [BATTLE] 🎉 Victory! All enemies defeated! 🎉");
                    _logger.LogInformation($"Client {_clientId}: [BATTLE] Surviving players: {alivePlayers}/{status.Players.Count}");

                    // Show surviving players stats
                    foreach (var player in status.Players.Where(p => p.CurrentHp > 0))
                    {
                        var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                        _logger.LogInformation($"Client {_clientId}: [BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Client {_clientId}: [BATTLE] ❌ Defeat! All players defeated! ❌");
                    _logger.LogInformation($"Client {_clientId}: [BATTLE] Remaining enemies: {aliveEnemies}/{status.Enemies.Count}");
                }

                _logger.LogInformation($"Client {_clientId}: [BATTLE] Total turns: {status.CurrentTurn}");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Battle ID: {status.BattleId} (replay available)");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Simulation complete! Notifying server...");

                // Automatically notify server that replay is complete
                Task.Run(async () =>
                {
                    try
                    {
                        await BattleReplayCompleteAsync();
                        _logger.LogInformation($"Client {_clientId}: [BATTLE] Successfully notified server about replay completion");

                        // バトルの完了を通知
                        _battleCompletionSource?.TrySetResult(true);

                        // コールバックがあれば実行
                        _battleCompletedCallback?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Client {_clientId}: [BATTLE] Failed to notify server about replay completion: {ex.Message}");
                        _battleCompletionSource?.TrySetException(ex);
                    }
                });

                _logger.LogInformation($"Client {_clientId}: [BATTLE] ========================================");
            });

            _connection.On<string>("AllBattleReplaysCompleted", async (battleId) =>
            {
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ========== All Replays Completed! ==========");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] All clients have completed watching the battle replay");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] Battle ID: {battleId}");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] You can now disconnect or start a new battle");
                _logger.LogInformation($"Client {_clientId}: [BATTLE] ===========================================");

                // バトル完了後に自動的に切断する
                try
                {
                    await Task.Delay(1000); // 1秒待機してから切断
                    await DisconnectAsync();
                    _logger.LogInformation($"Client {_clientId}: [BATTLE] Automatically disconnected from server after battle completion");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Client {_clientId}: [BATTLE] Error automatically disconnecting after battle: {ex.Message}");
                }
            });

            await _connection.StartAsync();

            // Join group if specified
            if (!string.IsNullOrEmpty(groupName))
            {
                _currentGroupId = await _connection.InvokeAsync<string>("JoinGroupAsync", groupName);
                _logger.LogInformation($"Client {_clientId}: Joined group: {groupName} (ID: {_currentGroupId})");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Client {_clientId}: Failed to connect to server: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnect from server
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync(); _connection = null;
                _currentGroupId = string.Empty;
                _logger.LogInformation($"Client {_clientId}: Disconnected from server");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Client {_clientId}: Error disconnecting from server: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Check if connected to server
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Get value by key
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<string?>("GetAsync", key);
    }

    /// <summary>
    /// Set key-value pair
    /// </summary>
    public async Task<bool> SetAsync(string key, string value)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("SetAsync", key, value);
    }

    /// <summary>
    /// Delete key
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("DeleteAsync", key);
    }

    /// <summary>
    /// List keys matching pattern
    /// </summary>
    public async Task<IEnumerable<string>> ListAsync(string pattern = "*")
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<IEnumerable<string>>("ListAsync", pattern);
    }

    /// <summary>
    /// Watch key for changes
    /// </summary>
    public async Task<bool> WatchAsync(string key)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("WatchAsync", key);
    }

    /// <summary>
    /// Join a group
    /// </summary>
    public async Task<bool> JoinGroupAsync(string? groupName = null)
    {
        EnsureConnected();
        var result = await _connection!.InvokeAsync<string>("JoinGroupAsync", groupName);
        if (!string.IsNullOrEmpty(result))
        {
            _currentGroupId = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Broadcast message to current group
    /// </summary>
    public async Task<bool> BroadcastAsync(string message)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("BroadcastAsync", message);
    }

    /// <summary>
    /// Get all available groups
    /// </summary>
    public async Task<IEnumerable<string>> GetGroupsAsync()
    {
        EnsureConnected();
        var groups = await _connection!.InvokeAsync<IEnumerable<GroupInfo>>("GetGroupsAsync");
        return groups.Select(g => g.Id);
    }    /// <summary>
         /// Get current group info
         /// </summary>
    public async Task<string?> GetMyGroupAsync()
    {
        EnsureConnected();
        var groupInfo = await _connection!.InvokeAsync<GroupInfo?>("GetCurrentGroupAsync");
        return groupInfo?.Id;
    }

    /// <summary>
    /// Get current group info (detailed)
    /// </summary>
    public async Task<GroupInfo?> GetCurrentGroupAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<GroupInfo?>("GetCurrentGroupAsync");
    }

    /// <summary>
    /// Get battle status
    /// </summary>
    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<BattleStatus?>("GetBattleStatusAsync");
    }

    /// <summary>
    /// Execute battle action
    /// </summary>
    public async Task<bool> BattleActionAsync(string actionType, string? parameters = null)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("BattleActionAsync", actionType, parameters);
    }

    /// <summary>
    /// Get battle replay data
    /// </summary>
    public async Task<string?> GetBattleReplayAsync(string battleId)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<string?>("GetBattleReplayAsync", battleId);
    }

    /// <summary>
    /// Set a callback to be triggered when a battle is completed
    /// </summary>
    public void SetBattleCompletedCallback(Action callback)
    {
        _battleCompletedCallback = callback;
    }

    /// <summary>
    /// Notify server that battle replay is complete
    /// </summary>
    public async Task<bool> BattleReplayCompleteAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("BattleReplayCompleteAsync");
    }

    /// <summary>
    /// Get server status
    /// </summary>
    public async Task<ServerStatus?> GetServerStatusAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<ServerStatus?>("GetServerStatusAsync");
    }

    /// <summary>
    /// Confirm that client has received the ConnectionsReady notification
    /// </summary>
    private async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
    }

    /// <summary>
    /// Ensure client is connected to server
    /// </summary>
    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to server. Call ConnectAsync first.");
        }
    }
}
