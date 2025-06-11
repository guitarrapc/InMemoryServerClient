using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Shared;

namespace CliClient;

/// <summary>
/// Client for InMemory server
/// </summary>
public class InMemoryClient(ILogger<InMemoryClient> logger)
{
    private readonly ILogger<InMemoryClient> _logger = logger;
    private HubConnection? _connection;
    private string _serverUrl = string.Empty;
    private string _currentGroupId = string.Empty;

    // Battle replay settings
    private const int BattleReplayFps = 5; // 5fps for battle replay
    private const int BattleReplayFrameTimeMs = 1000 / BattleReplayFps; // Time in ms between frames

    // バトル完了を追跡するためのTaskCompletionSourceフィールドを追加
    private TaskCompletionSource<bool>? _battleCompletionSource = null;

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
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation($"Connecting to server: {serverUrl}");

            _connection = new HubConnectionBuilder()
                .WithUrl(_serverUrl + Constants.HubRoute)
                .WithAutomaticReconnect()
                .Build();

            // Set up event handlers
            _connection.On<string, string>("KeyChanged", (key, value) =>
            {
                Console.WriteLine($"[NOTIFICATION] Key changed: {key} = {value}");
            });

            _connection.On<string>("KeyDeleted", (key) =>
            {
                Console.WriteLine($"[NOTIFICATION] Key deleted: {key}");
            });

            _connection.On<string, int>("MemberJoined", (connectionId, count) =>
            {
                Console.WriteLine($"[GROUP] New member joined: {connectionId} (Total: {count})");
            });

            _connection.On<string, string>("GroupMessage", (connectionId, message) =>
            {
                Console.WriteLine($"[GROUP] Message from {connectionId}: {message}");
            });

            _connection.On<string>("ConnectionsReady", (battleId) =>
            {
                Console.WriteLine($"[BATTLE] ========== Connections Ready! ==========");
                Console.WriteLine($"[BATTLE] 🔄 Battle ID: {battleId}");
                Console.WriteLine($"[BATTLE] Group is full! All clients connected.");
                Console.WriteLine($"[BATTLE] Confirming connection ready status...");
                Console.WriteLine("[BATTLE] ========================================");

                // Automatically confirm connection ready status
                Task.Run(async () => {
                    try {
                        await ConfirmConnectionReadyAsync();
                        Console.WriteLine($"[BATTLE] Connection ready confirmation sent to server");
                    } catch (Exception ex) {
                        _logger.LogError($"Failed to confirm connection ready status: {ex.Message}");
                    }
                });
            });

            _connection.On<string>("BattleStarted", (battleId) =>
            {
                Console.WriteLine($"[BATTLE] ========== Battle Started! ==========");
                Console.WriteLine($"[BATTLE] 🏆 Battle ID: {battleId}");
                Console.WriteLine($"[BATTLE] All clients confirmed! Automatic battle starting...");
                Console.WriteLine($"[BATTLE] Preparing battlefield and players...");
                Console.WriteLine("[BATTLE] ======================================");
            });

            _connection.On<BattleStatus>("BattleStatusUpdated", (status) =>
            {
                // Add delay for frame rate control (10fps)
                Task.Delay(BattleReplayFrameTimeMs).Wait();

                Console.WriteLine($"[BATTLE] ========== Turn {status.CurrentTurn}/{status.TotalTurns} ==========");

                // Display players info
                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                Console.WriteLine($"[BATTLE] Players alive: {alivePlayers}/{status.Players.Count}");
                foreach (var player in status.Players)
                {
                    var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                    Console.WriteLine($"[BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar} ATK:{player.Attack} DEF:{player.Defense} SPD:{player.Speed}");
                }

                // Display enemies info
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);
                Console.WriteLine($"[BATTLE] Enemies alive: {aliveEnemies}/{status.Enemies.Count}");

                // Display logs
                Console.WriteLine("[BATTLE] Recent actions:");
                foreach (var log in status.RecentLogs)
                {
                    Console.WriteLine($"[BATTLE] > {log}");
                }
                Console.WriteLine("[BATTLE] ====================================");
            });

            _connection.On<BattleStatus>("BattleCompleted", (status) =>
            {
                Console.WriteLine($"[BATTLE] ========== Battle Completed! ==========");

                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);

                // Display outcome
                if (aliveEnemies == 0)
                {
                    Console.WriteLine($"[BATTLE] 🎉 Victory! All enemies defeated! 🎉");
                    Console.WriteLine($"[BATTLE] Surviving players: {alivePlayers}/{status.Players.Count}");

                    // Show surviving players stats
                    foreach (var player in status.Players.Where(p => p.CurrentHp > 0))
                    {
                        var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                        Console.WriteLine($"[BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar}");
                    }
                }
                else
                {
                    Console.WriteLine($"[BATTLE] ❌ Defeat! All players defeated! ❌");
                    Console.WriteLine($"[BATTLE] Remaining enemies: {aliveEnemies}/{status.Enemies.Count}");
                }

                Console.WriteLine($"[BATTLE] Total turns: {status.CurrentTurn}");
                Console.WriteLine($"[BATTLE] Battle ID: {status.BattleId} (replay available)");
                Console.WriteLine($"[BATTLE] Simulation complete! Notifying server...");

                // Automatically notify server that replay is complete
                Task.Run(async () => {
                    try {
                        await BattleReplayCompleteAsync();
                        Console.WriteLine($"[BATTLE] Successfully notified server about replay completion");

                        // バトルの完了を通知
                        _battleCompletionSource?.TrySetResult(true);
                    } catch (Exception ex) {
                        _logger.LogError($"Failed to notify server about replay completion: {ex.Message}");
                        _battleCompletionSource?.TrySetException(ex);
                    }
                });

                Console.WriteLine("[BATTLE] ========================================");
            });

            _connection.On<string>("AllBattleReplaysCompleted", (battleId) =>
            {
                Console.WriteLine($"[BATTLE] ========== All Replays Completed! ==========");
                Console.WriteLine($"[BATTLE] All clients have completed watching the battle replay");
                Console.WriteLine($"[BATTLE] Battle ID: {battleId}");
                Console.WriteLine($"[BATTLE] You can now disconnect or start a new battle");
                Console.WriteLine("[BATTLE] ===========================================");
            });

            await _connection.StartAsync();

            // Join group if specified
            if (!string.IsNullOrEmpty(groupName))
            {
                _currentGroupId = await _connection.InvokeAsync<string>("JoinGroupAsync", groupName);
                _logger.LogInformation($"Joined group: {groupName} (ID: {_currentGroupId})");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to connect to server: {ex.Message}");
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
                await _connection.DisposeAsync();                    _connection = null;
                _currentGroupId = string.Empty;
                _logger.LogInformation("Disconnected from server");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disconnecting from server: {ex.Message}");
            }
        }
    }        /// <summary>
    /// Check if connected to server
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;        /// <summary>
    /// Get value by key
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<string?>("GetAsync", key);
    }        /// <summary>
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
    /// Connect multiple sessions to server with the same group
    /// </summary>
    public async Task<bool> ConnectMultipleAsync(string serverUrl, string groupName, int count)
    {
        _logger.LogInformation($"Connecting {count} sessions to group '{groupName}' on server '{serverUrl}'");

        if (count <= 0)
        {
            _logger.LogWarning($"Invalid session count: {count}, must be greater than 0");
            return false;
        }

        // If already connected, disconnect first
        if (_connection != null && _connection.State == HubConnectionState.Connected)
        {
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        // Create main connection
        bool success = await ConnectAsync(serverUrl, groupName);
        if (!success)
        {
            return false;
        }

        // If count is 1, we're done (already connected with the main connection)
        if (count <= 1)
        {
            return true;
        }

        // 完了通知を受け取るための TaskCompletionSource を作成
        _battleCompletionSource = new TaskCompletionSource<bool>();

        // メインコネクションに BattleCompleted イベントを追加
        _connection!.On<BattleStatus>("BattleCompleted", (status) =>
        {
            // すでに BattleCompleted イベントハンドラがあるので、
            // そちらで表示は行い、ここでは完了通知のみ行う
            _battleCompletionSource?.TrySetResult(true);
        });

        // Create additional connections (count-1 because we already have one connection)
        var additionalConnections = new List<HubConnection>();
        for (int i = 1; i < count; i++)
        {
            try
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl(serverUrl + Constants.HubRoute)
                    .WithAutomaticReconnect()
                    .Build();

                // 各接続に必要なイベントハンドラを設定
                connection.On<string>("ConnectionsReady", async (battleId) =>
                {
                    try {
                        await connection.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
                        _logger.LogInformation($"Additional connection confirmed ready for battle");
                    } catch (Exception ex) {
                        _logger.LogError($"Failed to confirm connection ready status: {ex.Message}");
                    }
                });

                connection.On<BattleStatus>("BattleCompleted", (status) =>
                {
                    // サブ接続では特に何もしない（メイン接続で処理する）
                });

                // Start connection
                await connection.StartAsync();

                // Join the same group
                await connection.InvokeAsync<string>("JoinGroupAsync", groupName);

                // Add to list of connections to manage
                additionalConnections.Add(connection);

                _logger.LogInformation($"Created additional connection {i} and joined group: {groupName}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create additional connection {i}: {ex.Message}");

                // Clean up already created connections
                foreach (var conn in additionalConnections)
                {
                    try
                    {
                        await conn.StopAsync();
                        await conn.DisposeAsync();
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }

                return false;
            }
        }

        _logger.LogInformation($"Successfully connected {count} sessions to group: {groupName}");

        try
        {
            // バトルが完了するまで待機（タイムアウト20分）
            Console.WriteLine("Waiting for battle to complete. This may take several minutes...");
            await Task.WhenAny(_battleCompletionSource.Task, Task.Delay(TimeSpan.FromMinutes(20)));

            if (_battleCompletionSource.Task.IsCompleted)
            {
                Console.WriteLine("Battle has completed successfully!");
            }
            else
            {
                Console.WriteLine("Timed out waiting for battle to complete.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while waiting for battle completion: {ex.Message}");
        }
        finally
        {
            // クリーンアップ - 追加接続を閉じる
            foreach (var conn in additionalConnections)
            {
                try
                {
                    await conn.StopAsync();
                    await conn.DisposeAsync();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
        }

        return true;
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
    public async Task<bool> ConfirmConnectionReadyAsync()
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
