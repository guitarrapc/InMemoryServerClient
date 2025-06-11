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
    private readonly int _clientIndex;

    // Battle replay settings
    private const int BattleReplayFps = 5; // 5fps for battle replay
    private const int BattleReplayFrameTimeMs = 1000 / BattleReplayFps; // Time in ms between frames

    // This is used to track if the battle has completed and to notify the client when it is done
    private readonly TaskCompletionSource<bool> _battleCompletionSource;

    public TaskCompletionSource<bool> BattleCompletionSource => _battleCompletionSource;

    public InMemoryClient(ILogger<InMemoryClient> logger) : this(0, logger)
    {
    }

    public InMemoryClient(int clientIndex, ILogger<InMemoryClient> logger)
    {
        _clientIndex = clientIndex;
        _logger = logger;
        _battleCompletionSource = new TaskCompletionSource<bool>();
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
            _logger.LogInformation($"Client {_clientIndex}: Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation($"Client {_clientIndex}: Connecting to server: {serverUrl}");

            _connection = new HubConnectionBuilder()
                .WithUrl(_serverUrl + Constants.HubRoute)
                .WithAutomaticReconnect()
                .Build();

            // Set up event handlers
            _connection.On<string, string>("KeyChanged", (key, value) =>
            {
                _logger.LogInformation($"Client {_clientIndex}: [NOTIFICATION] Key changed: {key} = {value}");
            });

            _connection.On<string>("KeyDeleted", (key) =>
            {
                _logger.LogInformation($"Client {_clientIndex}: [NOTIFICATION] Key deleted: {key}");
            });

            _connection.On<string, int>("MemberJoined", (connectionId, count) =>
            {
                _logger.LogInformation($"Client {_clientIndex}: [GROUP] New member joined: {connectionId} (Total: {count})");
            });

            _connection.On<string, string>("GroupMessage", (connectionId, message) =>
            {
                _logger.LogInformation($"Client {_clientIndex}: [GROUP] Message from {connectionId}: {message}");
            });

            _connection.On<string>("ConnectionsReady", async (battleId) =>
            {
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ========== Connections Ready! ==========");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] 🔄 Battle ID: {battleId}");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Group is full! All clients connected.");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Confirming connection ready status...");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ========================================");

                // Automatically notify server that connection is ready
                try
                {
                    await ConfirmConnectionReadyAsync();
                    _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Connection ready confirmation sent to server");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Client {_clientIndex}: Failed to confirm connection ready status: {ex.Message}");
                }
            });

            _connection.On<string>("BattleStarted", (battleId) =>
            {
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ========== Battle Started! ==========");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] 🏆 Battle ID: {battleId}");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] All clients confirmed! Automatic battle starting...");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Preparing battlefield and players...");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ======================================");
            });

            _connection.On<BattleStatus>("BattleStatusUpdated", async (status) =>
            {
                // Add delay for frame rate control (10fps)
                await Task.Delay(BattleReplayFrameTimeMs);

                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ========== Turn {status.CurrentTurn}/{status.TotalTurns} ==========");

                // Display players info
                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Players alive: {alivePlayers}/{status.Players.Count}");
                foreach (var player in status.Players)
                {
                    var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                    _logger.LogInformation($"Client {_clientIndex}: [BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar} ATK:{player.Attack} DEF:{player.Defense} SPD:{player.Speed}");
                }

                // Display enemies info
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Enemies alive: {aliveEnemies}/{status.Enemies.Count}");

                // Display logs
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Recent actions:");
                foreach (var log in status.RecentLogs)
                {
                    _logger.LogInformation($"Client {_clientIndex}: [BATTLE] > {log}");
                }
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ====================================");
            });

            _connection.On<BattleStatus>("BattleCompleted", async (status) =>
            {
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ========== Battle Completed! ==========");

                var alivePlayers = status.Players.Count(p => p.CurrentHp > 0);
                var aliveEnemies = status.Enemies.Count(e => e.CurrentHp > 0);

                // Display outcome
                if (aliveEnemies == 0)
                {
                    _logger.LogInformation($"Client {_clientIndex}: [BATTLE] 🎉 Victory! All enemies defeated! 🎉");
                    _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Surviving players: {alivePlayers}/{status.Players.Count}");

                    // Show surviving players stats
                    foreach (var player in status.Players.Where(p => p.CurrentHp > 0))
                    {
                        var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                        _logger.LogInformation($"Client {_clientIndex}: [BATTLE] {player.Name}: HP {player.CurrentHp}/{player.MaxHp} {healthBar}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ❌ Defeat! All players defeated! ❌");
                    _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Remaining enemies: {aliveEnemies}/{status.Enemies.Count}");
                }

                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Total turns: {status.CurrentTurn}");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Battle ID: {status.BattleId} (replay available)");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] Simulation complete! Notifying server...");
                _logger.LogInformation($"Client {_clientIndex}: [BATTLE] ========================================");

                _battleCompletionSource.TrySetResult(true);
            });

            await _connection.StartAsync();

            // Join group if specified
            if (!string.IsNullOrEmpty(groupName))
            {
                _currentGroupId = await _connection.InvokeAsync<string>("JoinGroupAsync", groupName);
                _logger.LogInformation($"Client {_clientIndex}: Joined group: {groupName} (ID: {_currentGroupId})");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Client {_clientIndex}: Failed to connect to server: {ex.Message}");
            _battleCompletionSource.SetException(ex);
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
                _logger.LogInformation($"Client {_clientIndex}: Disconnected from server");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Client {_clientIndex}: Error disconnecting from server: {ex.Message}");
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
