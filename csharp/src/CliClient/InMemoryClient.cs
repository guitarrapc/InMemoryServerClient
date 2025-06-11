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

    // バトル完了を追跡するためのフィールド
    private TaskCompletionSource<bool>? _battleCompletionSource = null;
    private Action? _battleCompletedCallback = null;

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
                _logger.LogInformation($"[NOTIFICATION] Key changed: {key} = {value}");
            });

            _connection.On<string>("KeyDeleted", (key) =>
            {
                _logger.LogInformation($"[NOTIFICATION] Key deleted: {key}");
            });

            _connection.On<string, int>("MemberJoined", (connectionId, count) =>
            {
                _logger.LogInformation($"[GROUP] New member joined: {connectionId} (Total: {count})");
            });

            _connection.On<string, string>("GroupMessage", (connectionId, message) =>
            {
                _logger.LogInformation($"[GROUP] Message from {connectionId}: {message}");
            });

            _connection.On<string>("ConnectionsReady", (battleId) =>
            {
                _logger.LogInformation($"[BATTLE] ========== Connections Ready! ==========");
                _logger.LogInformation($"[BATTLE] 🔄 Battle ID: {battleId}");
                _logger.LogInformation($"[BATTLE] Group is full! All clients connected.");
                _logger.LogInformation($"[BATTLE] Confirming connection ready status...");
                _logger.LogInformation("[BATTLE] ========================================");

                // Automatically confirm connection ready status
                Task.Run(async () =>
                {
                    try
                    {
                        await ConfirmConnectionReadyAsync();
                        _logger.LogInformation($"[BATTLE] Connection ready confirmation sent to server");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to confirm connection ready status: {ex.Message}");
                    }
                });
            });

            _connection.On<string>("BattleStarted", (battleId) =>
            {
                _logger.LogInformation($"[BATTLE] ========== Battle Started! ==========");
                _logger.LogInformation($"[BATTLE] 🏆 Battle ID: {battleId}");
                _logger.LogInformation($"[BATTLE] All clients confirmed! Automatic battle starting...");
                _logger.LogInformation($"[BATTLE] Preparing battlefield and players...");
                _logger.LogInformation("[BATTLE] ======================================");
            });

            _connection.On<BattleStatus>("BattleStatusUpdated", (status) =>
            {
                // Add delay for frame rate control (10fps)
                Task.Delay(BattleReplayFrameTimeMs).Wait();

                _logger.LogInformation($"[BATTLE] ========== Turn {status.CurrentTurn}/{status.TotalTurns} ==========");

                // Display players info
                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                _logger.LogInformation($"[BATTLE] Players alive: {alivePlayers}/{status.Players.Count}");
                foreach (var player in status.Players)
                {
                    var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                    _logger.LogInformation($"[BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar} ATK:{player.Attack} DEF:{player.Defense} SPD:{player.Speed}");
                }

                // Display enemies info
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);
                _logger.LogInformation($"[BATTLE] Enemies alive: {aliveEnemies}/{status.Enemies.Count}");

                // Display logs
                _logger.LogInformation("[BATTLE] Recent actions:");
                foreach (var log in status.RecentLogs)
                {
                    _logger.LogInformation($"[BATTLE] > {log}");
                }
                _logger.LogInformation("[BATTLE] ====================================");
            });

            _connection.On<BattleStatus>("BattleCompleted", (status) =>
            {
                _logger.LogInformation($"[BATTLE] ========== Battle Completed! ==========");

                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);

                // Display outcome
                if (aliveEnemies == 0)
                {
                    _logger.LogInformation($"[BATTLE] 🎉 Victory! All enemies defeated! 🎉");
                    _logger.LogInformation($"[BATTLE] Surviving players: {alivePlayers}/{status.Players.Count}");

                    // Show surviving players stats
                    foreach (var player in status.Players.Where(p => p.CurrentHp > 0))
                    {
                        var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                        _logger.LogInformation($"[BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar}");
                    }
                }
                else
                {
                    _logger.LogInformation($"[BATTLE] ❌ Defeat! All players defeated! ❌");
                    _logger.LogInformation($"[BATTLE] Remaining enemies: {aliveEnemies}/{status.Enemies.Count}");
                }

                _logger.LogInformation($"[BATTLE] Total turns: {status.CurrentTurn}");
                _logger.LogInformation($"[BATTLE] Battle ID: {status.BattleId} (replay available)");
                _logger.LogInformation($"[BATTLE] Simulation complete! Notifying server...");

                // Automatically notify server that replay is complete
                Task.Run(async () =>
                {
                    try
                    {
                        await BattleReplayCompleteAsync();
                        _logger.LogInformation($"[BATTLE] Successfully notified server about replay completion");

                        // バトルの完了を通知
                        _battleCompletionSource?.TrySetResult(true);

                        // コールバックがあれば実行
                        _battleCompletedCallback?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to notify server about replay completion: {ex.Message}");
                        _battleCompletionSource?.TrySetException(ex);
                    }
                });

                _logger.LogInformation("[BATTLE] ========================================");
            });

            _connection.On<string>("AllBattleReplaysCompleted", async (battleId) =>
            {
                _logger.LogInformation($"[BATTLE] ========== All Replays Completed! ==========");
                _logger.LogInformation($"[BATTLE] All clients have completed watching the battle replay");
                _logger.LogInformation($"[BATTLE] Battle ID: {battleId}");
                _logger.LogInformation($"[BATTLE] You can now disconnect or start a new battle");
                _logger.LogInformation("[BATTLE] ===========================================");

                // バトル完了後に自動的に切断する
                try
                {
                    await Task.Delay(1000); // 1秒待機してから切断
                    await DisconnectAsync();
                    _logger.LogInformation("[BATTLE] Automatically disconnected from server after battle completion");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error automatically disconnecting after battle: {ex.Message}");
                }
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
                await _connection.DisposeAsync(); _connection = null;
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
                    try
                    {
                        await connection.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
                        _logger.LogInformation($"Additional connection confirmed ready for battle");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to confirm connection ready status: {ex.Message}");
                    }
                });

                // バトル完了時に自動切断するようにイベントハンドラを設定
                connection.On<string>("AllBattleReplaysCompleted", async (battleId) =>
                {
                    try
                    {
                        // 少し待機してから切断
                        await Task.Delay(1000);
                        await connection.StopAsync();
                        await connection.DisposeAsync();
                        _logger.LogInformation($"Additional connection automatically disconnected after battle completion");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error automatically disconnecting additional connection: {ex.Message}");
                    }
                });

                connection.On<BattleStatus>("BattleCompleted", async (status) =>
                {
                    // サブ接続でもリプレイ完了を通知する
                    try
                    {
                        // タイムアウト付きで通知を送信
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await connection.InvokeAsync<bool>("BattleReplayCompleteAsync", cts.Token);
                        _logger.LogInformation($"Additional connection notified server about replay completion");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Battle replay complete notification timed out");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to notify server about replay completion from additional connection: {ex.Message}");
                    }
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
            // バトルが完了するまで待機（タイムアウト3分）
            _logger.LogInformation("Waiting for battle to complete. This may take a minute...");
            await Task.WhenAny(_battleCompletionSource.Task, Task.Delay(TimeSpan.FromMinutes(3)));

            if (_battleCompletionSource.Task.IsCompleted)
            {
                _logger.LogInformation("Battle has completed successfully!");
            }
            else
            {
                _logger.LogWarning("Timed out waiting for battle to complete.");
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
