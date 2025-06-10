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

    public InMemoryClient(ILogger<InMemoryClient> logger)
    {
        _logger = logger;
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

            _connection.On<string>("BattleStarted", (battleId) =>
            {
                Console.WriteLine($"[BATTLE] Battle started! Battle ID: {battleId}");
            });

            _connection.On<BattleStatus>("BattleStatusUpdated", (status) =>
            {
                Console.WriteLine($"[BATTLE] Turn {status.CurrentTurn}/{status.TotalTurns}");
                Console.WriteLine($"[BATTLE] Players alive: {status.Players.Count}, Enemies alive: {status.Enemies.Count}");
                foreach (var log in status.RecentLogs)
                {
                    Console.WriteLine($"[BATTLE] {log}");
                }
            });

            _connection.On<BattleStatus>("BattleCompleted", (status) =>
            {
                Console.WriteLine($"[BATTLE] Battle completed!");
                var playerCount = status.Players.FindAll(p => p.CurrentHp > 0).Count;
                var enemyCount = status.Enemies.FindAll(e => e.CurrentHp > 0).Count;

                if (enemyCount == 0)
                {
                    Console.WriteLine($"[BATTLE] Victory! All enemies defeated!");
                }
                else
                {
                    Console.WriteLine($"[BATTLE] Defeat! All players defeated!");
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
    public async Task<string?> GetBattleStatusAsync()
    {
        EnsureConnected();
        var status = await _connection!.InvokeAsync<BattleStatus?>("GetBattleStatusAsync");
        return status?.ToString();
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
