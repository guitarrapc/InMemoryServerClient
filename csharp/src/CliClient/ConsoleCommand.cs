using CliClient.Clients;
using CliClient.Services;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.ServiceDiscovery.Models;

namespace CliClient;

/// <summary>
/// Connection configuration for battle reproduction
/// </summary>
internal readonly record struct ConnectionOptions
{
    public string ServerUrl { get; init; }
    public string? GroupName { get; init; }
}

/// <summary>
/// CLI commands for InMemory server
/// Public Method will be automatically registered as commands.
/// ListFooAsync will be registered as list-foo command.
/// </summary>
public class ConsoleCommand(MultiBattleClientManager multiClientManager, ServiceDiscoveryClientProvider serviceDiscoveryProvider, ILoggerFactory loggerFactory, ILogger<ConsoleCommand> logger)
{
    private IBattleClient? _client;

    /// <summary>Start interactive mode</summary>
    [Command("")]
    public async Task InteractiveAsync()
    {
        logger.LogInformation("InMemory CLI Client - Interactive Mode");
        logger.LogInformation("=====================================");
        logger.LogInformation("Type 'help' for a list of commands, 'exit' to quit.");

        bool exit = false;
        while (!exit)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = args[0].ToLower();

            try
            {
                switch (command)
                {
                    case "exit":
                    case "quit":
                        exit = true;
                        break;

                    case "help":
                        ShowInteractiveHelp();
                        break;

                    case "connect":
                        var url = args.Length > 1 ? args[1] : "http://localhost:5000";
                        var group = args.Length > 2 ? args[2] : "battle-group";
                        var connectionType = Enum.Parse<ConnectionType>(args.Length > 3 ? args[3] : "SignalR");
                        await ConnectAsync(url, group, connectionType);
                        break;

                    case "connect-battle":
                        var battleUrl = args.Length > 1 ? args[1] : "http://localhost:5000";
                        var battleGroup = args.Length > 2 ? args[2] : "battle-group";
                        var count = args.Length > 3 && int.TryParse(args[3], out var c) ? c : 5;
                        var battleConnectionType = Enum.Parse<ConnectionType>(args.Length > 4 ? args[4] : "SignalR");
                        await ConnectMultipleAsync(battleUrl, battleGroup, count, battleConnectionType);
                        break;

                    case "disconnect":
                        await DisconnectAsync();
                        break;

                    case "get":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: get <key>");
                            break;
                        }
                        await GetAsync(args[1]);
                        break;

                    case "set":
                        if (args.Length < 3)
                        {
                            logger.LogInformation("Usage: set <key> <value>");
                            break;
                        }
                        var setValue = string.Join(' ', args.Skip(2));
                        await SetAsync(args[1], setValue);
                        break;

                    case "delete":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: delete <key>");
                            break;
                        }
                        await DeleteAsync(args[1]);
                        break;

                    case "list":
                        var pattern = args.Length > 1 ? args[1] : "*";
                        await ListAsync(pattern);
                        break;

                    case "watch":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: watch <key>");
                            break;
                        }
                        await WatchAsync(args[1]);
                        break;

                    case "join":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: join <group_name>");
                            break;
                        }
                        await JoinAsync(args[1]);
                        break;

                    case "broadcast":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: broadcast <message>");
                            break;
                        }
                        var message = string.Join(' ', args.Skip(1));
                        await BroadcastAsync(message);
                        break;

                    case "sd-connect":
                        var sdUrl = args.Length > 1 ? args[1] : "http://localhost:5010";
                        await ServiceDiscoveryConnectAsync(sdUrl);
                        break;

                    case "sd-create-session":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: sd-create-session <group_name> [max_players] [connection_type]");
                            break;
                        }
                        var sdGroupName = args[1];
                        var sdMaxPlayers = args.Length > 2 && int.TryParse(args[2], out var mp) ? mp : 5;
                        var sdConnectionType = args.Length > 3 && Enum.TryParse<ConnectionType>(args[3], out var ct) ? ct : ConnectionType.SignalR;
                        await ServiceDiscoveryCreateSessionAsync(sdGroupName, sdMaxPlayers, sdConnectionType);
                        break;

                    case "sd-list-sessions":
                        await ServiceDiscoveryListSessionsAsync();
                        break;

                    case "sd-get-session":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: sd-get-session <session_id>");
                            break;
                        }
                        await ServiceDiscoveryGetSessionAsync(args[1]);
                        break;

                    case "sd-battle":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: sd-battle <group_name> [count] [connection_type]");
                            break;
                        }
                        var sdBattleGroupName = args[1];
                        var sdBattleCount = args.Length > 2 && int.TryParse(args[2], out var bc) ? bc : 5;
                        var sdBattleConnectionType = args.Length > 3 && Enum.TryParse<ConnectionType>(args[3], out var bct) ? bct : ConnectionType.SignalR;
                        await ServiceDiscoveryBattleAsync(sdBattleGroupName, sdBattleCount, sdBattleConnectionType);
                        break;

                    case "battle-status":
                        await BattleStatusAsync();
                        break;

                    case "battle-replay":
                        if (args.Length < 2)
                        {
                            logger.LogInformation("Usage: battle-replay <battle_id>");
                            break;
                        }
                        await BattleReplayAsync(args[1]);
                        break;

                    default:
                        logger.LogInformation($"Unknown command: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Error: {ex.Message}");
            }
        }

        // Ensure proper cleanup on exit
        if (_client is not null)
        {
            try
            {
                if (_client.IsConnected)
                {
                    await _client.DisconnectAsync();
                }
                await _client.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Warning: Error during cleanup: {ex.Message}");
            }
            finally
            {
                _client = null;
            }
        }
    }

    /// <summary>Connect to InMemory server</summary>
    /// <param name="url">-u, Server URL</param>
    /// <param name="group">-g, Group name (optional)</param>
    /// <param name="connectionType">-t, Connection type (default: SignalR)</param>
    private async Task ConnectAsync(
        string url = "http://localhost:5000",
        string group = "battle-group",
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        try
        {
            // 既存の接続があれば先にクリーンアップ
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            _client = BattleClientFactory.Create(connectionType, loggerFactory);
            if (await _client.ConnectAsync(url, group))
            {
                logger.LogInformation($"Connected to server: {url}");
                if (!string.IsNullOrEmpty(group))
                {
                    logger.LogInformation($"Joined group: {group}");
                }
            }
            else
            {
                await _client.DisposeAsync();
                _client = null;
                logger.LogInformation($"Failed to connect to server: {url}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }
            logger.LogInformation($"Error connecting to server: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get value by key</summary>
    /// <param name="key">The key to get</param>
    private async Task GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            logger.LogInformation("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            var value = await _client.GetAsync(key);
            if (value != null)
            {
                logger.LogInformation($"{key} = {value}");
            }
            else
            {
                logger.LogInformation($"Key not found: {key}");
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Set key-value pair</summary>
    /// <param name="key">The key to set</param>
    /// <param name="value">The value to set</param>
    private async Task SetAsync(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            logger.LogInformation("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrEmpty(value))
        {
            logger.LogInformation("Error: Value is required");
            Environment.ExitCode = 1;
            return;
        }

        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            if (await _client.SetAsync(key, value))
            {
                logger.LogInformation($"Key {key} set to: {value}");
            }
            else
            {
                logger.LogInformation($"Failed to set key: {key}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Delete key</summary>
    /// <param name="key">The key to delete</param>
    private async Task DeleteAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            logger.LogInformation("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            if (await _client.DeleteAsync(key))
            {
                logger.LogInformation($"Key deleted: {key}");
            }
            else
            {
                logger.LogInformation($"Failed to delete key: {key}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>List keys matching pattern</summary>
    /// <param name="pattern">-p, The pattern to match</param>
    private async Task ListAsync(string pattern = "*")
    {
        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            var keys = await _client.ListAsync(pattern);
            logger.LogInformation($"Keys matching pattern '{pattern}':");
            foreach (var key in keys)
            {
                logger.LogInformation($"  {key}");
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Watch key for changes</summary>
    /// <param name="key">The key to watch</param>
    private async Task WatchAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            logger.LogInformation("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            await _client.WatchAsync(key);
            logger.LogInformation($"Watching key: {key}");
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Join a group</summary>
    /// <param name="groupName">The group name to join</param>
    private async Task JoinAsync(string groupName)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            logger.LogInformation("Error: Group name is required");
            Environment.ExitCode = 1;
            return;
        }

        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            if (await _client.JoinGroupAsync(groupName))
            {
                logger.LogInformation($"Joined group: {groupName}");
            }
            else
            {
                logger.LogInformation($"Failed to join group: {groupName}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Broadcast message to current group</summary>
    /// <param name="message">The message to broadcast</param>
    private async Task BroadcastAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            logger.LogInformation("Error: Message is required");
            Environment.ExitCode = 1;
            return;
        }

        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            if (await _client.BroadcastAsync(message))
            {
                logger.LogInformation($"Message broadcasted: {message}");
            }
            else
            {
                logger.LogInformation("Failed to broadcast message");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get battle status</summary>
    private async Task BattleStatusAsync()
    {
        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            var battleStatus = await _client.GetBattleStatusAsync();
            if (battleStatus != null)
            {
                if (battleStatus.IsInProgress)
                {
                    logger.LogInformation($"[BATTLE] ========== Battle Status ==========");
                    logger.LogInformation($"[BATTLE] Battle ID: {battleStatus.BattleId}");
                    logger.LogInformation($"[BATTLE] Turn: {battleStatus.CurrentTurn}/{battleStatus.TotalTurns}");

                    // Display players
                    var alivePlayers = battleStatus.Players.Count(p => p.IsAlive);
                    logger.LogInformation($"[BATTLE] Players alive: {alivePlayers}/{battleStatus.Players.Count}");
                    foreach (var player in battleStatus.Players)
                    {
                        var status = player.IsAlive ? "Alive" : "Defeated";
                        var jobInfo = player.PlayerJob.HasValue ? $" (Job: {player.PlayerJob})" : "";
                        logger.LogInformation($"[BATTLE] - {player.Name}{jobInfo}: {status}, HP: {player.CurrentHp}/{player.MaxHp}, ATK: {player.Attack}, DEF: {player.Defense}, SPD: {player.Speed}, Position: ({player.Position})");
                    }

                    // Display enemies
                    var aliveEnemies = battleStatus.Enemies.Count(e => e.IsAlive);
                    logger.LogInformation($"[BATTLE] Enemies alive: {aliveEnemies}/{battleStatus.Enemies.Count}");
                    foreach (var enemy in battleStatus.Enemies.Take(3)) // Show first 3 enemies to avoid spam
                    {
                        var status = enemy.IsAlive ? "Alive" : "Defeated";
                        var jobInfo = enemy.EnemyJob.HasValue ? $" (Job: {enemy.EnemyJob})" : "";
                        logger.LogInformation($"[BATTLE] - {enemy.Name}{jobInfo}: {status}, HP: {enemy.CurrentHp}/{enemy.MaxHp}, ATK: {enemy.Attack}, DEF: {enemy.Defense}, SPD: {enemy.Speed}, Position: ({enemy.Position})");
                    }

                    // Show recent logs
                    if (battleStatus.RecentLogs.Count > 0)
                    {
                        logger.LogInformation("[BATTLE] Recent actions:");
                        foreach (var log in battleStatus.RecentLogs.TakeLast(5))
                        {
                            logger.LogInformation($"[BATTLE] > {log}");
                        }
                    }

                    logger.LogInformation("[BATTLE] ===================================");
                }
                else
                {
                    // Battle is completed - show result
                    logger.LogInformation($"[BATTLE] ========== Battle Completed ==========");
                    logger.LogInformation($"[BATTLE] Battle ID: {battleStatus.BattleId}");
                    logger.LogInformation($"[BATTLE] Final Turn: {battleStatus.CurrentTurn}/{battleStatus.TotalTurns}");

                    // Show how the battle ended
                    if (battleStatus.IsEndedByTurnLimit == true)
                    {
                        logger.LogInformation("[BATTLE] ⏰ Battle ended due to turn limit reached!");
                    }
                    else if (battleStatus.IsEndedByTurnLimit == false)
                    {
                        logger.LogInformation("[BATTLE] ⚔️ Battle ended due to complete elimination!");
                    }

                    // Show result
                    if (battleStatus.IsPlayerVictory == true)
                    {
                        logger.LogInformation("[BATTLE] 🎉 Result: Victory! 🎉");
                    }
                    else
                    {
                        logger.LogInformation("[BATTLE] ❌ Result: Defeat! ❌");
                    }

                    logger.LogInformation("[BATTLE] ===================================");
                }
            }
            else
            {
                logger.LogInformation("No active battle or not in a group.");
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get battle replay data</summary>
    /// <param name="battleId">The battle ID to get replay for</param>
    private async Task BattleReplayAsync(string battleId)
    {
        if (!Guid.TryParse(battleId, out var parsedBattleId))
        {
            logger.LogInformation("Error: Battle ID is required");
            Environment.ExitCode = 1;
            return;
        }

        if (_client is null || !_client.IsConnected)
        {
            logger.LogInformation("Connection status: Not connected");
            return;
        }

        try
        {
            logger.LogInformation($"Requesting battle replay for battle {parsedBattleId}...");
            var replayData = await _client.GetBattleReplayAsync(parsedBattleId);
            if (replayData is null)
            {
                logger.LogInformation($"Replay data not found for battle: {parsedBattleId}");
                Environment.ExitCode = 1;
                return;
            }

            logger.LogInformation($"Replay data received for battle {parsedBattleId}");
            logger.LogInformation("Processing battle replay data...");

            // Use the turn data from the replay data
            var battleStatuses = replayData.Value.TurnData;

            if (battleStatuses.Count == 0)
            {
                logger.LogInformation("No battle data found in replay");
                Environment.ExitCode = 1;
                return;
            }
            else
            {
                // Play the battle replay using InMemoryClient's replay functionality
                await _client.PlayBattleReplayAsync(replayData.Value);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Disconnect from server</summary>
    private async Task DisconnectAsync()
    {
        if (_client is null)
        {
            logger.LogInformation("Not connected to any server");
            return;
        }

        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
                logger.LogInformation("Disconnected from server");
            }
            else
            {
                logger.LogInformation("Already disconnected from server");
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error disconnecting from server: {ex.Message}");
        }
        finally
        {
            // 常にリソースをクリーンアップ
            try
            {
                await _client.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Warning: Error during cleanup: {ex.Message}");
            }
            finally
            {
                _client = null;
            }
        }
    }

    /// <summary>Connect multiple sessions to the server with the same group</summary>
    /// <param name="url">-u, Server URL</param>
    /// <param name="group">-g, Group name</param>
    /// <param name="count">-c, Number of sessions to connect (default: 5)</param>
    /// <param name="connectionType">-t, Connection type (default: SignalR)</param>
    [Command("connect-battle")]
    public async Task ConnectMultipleAsync(string url = "http://localhost:5000", string group = "battle-group", int count = 5, ConnectionType connectionType = ConnectionType.SignalR)
    {
        if (count <= 0 || count > 10)
        {
            logger.LogError("接続数は1から10の間で指定してください");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrEmpty(group))
        {
            logger.LogInformation("Error: Group name is required for multiple connections");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            logger.LogInformation($"Connecting {count} sessions to server: {url}");
            logger.LogInformation($"Group name: {group}");

            // 新しいMultiClientManagerを使用
            if (await multiClientManager.ConnectMultipleAsync(count, url, group, connectionType))
            {
                logger.LogInformation($"Successfully connected {count} clients to group: {group}");
                logger.LogInformation($"If this completes the group (5 sessions), a battle should start automatically!");

                // バトルの完了を待機
                logger.LogInformation("Waiting for battle complete...");
                await multiClientManager.WaitForBattleCompletionAsync();

                logger.LogInformation("Battle completed successfully!");
            }
            else
            {
                logger.LogInformation($"Failed to connect {count} clients to server");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error connecting multiple clients: {ex.Message}");
            Environment.ExitCode = 1;
        }
        finally
        {
            // 最後に必ずクリーンアップを実行
            try
            {
                await multiClientManager.CleanupClientsAsync();
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Error during cleanup: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// <summary>Reproduce a battle with specific battle ID and seed value</summary>
    /// </summary>
    /// <param name="battleId">-b, BattleId to get reproduce result</param>
    /// <param name="seed">-s, Battle Seed to specify reproduce result</param>
    /// <param name="count">-c, Number of sessions to connect (default: 5)</param>
    /// <param name="groupName">-g, Group name (optional)</param>
    /// <param name="serverUrl">-u, Server URL</param>
    /// <param name="connectionType">-t, Connection type (default: SignalR)</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [Command("battle-reproduce")]
    public async Task ReproduceBattleAsync(
        string battleId,
        string seed,
        int count = 5,
        string groupName = "test-group",
        string serverUrl = "http://localhost:5000",
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        // Validate parameters
        if (!Guid.TryParse(battleId, out var parsedBattleId))
        {
            logger.LogError("無効なバトルIDです: {BattleId}", battleId);
            Environment.ExitCode = 1;
            return;
        }

        if (!int.TryParse(seed, out var parsedSeed))
        {
            logger.LogError("無効なシードです: {Seed}", seed);
            Environment.ExitCode = 1;
            return;
        }

        if (count <= 0 || count > 10)
        {
            logger.LogError("接続数は1から10の間で指定してください");
            Environment.ExitCode = 1;
            return;
        }

        logger.LogInformation("指定されたバトルID '{BattleId}' とシード値 '{SeedValue}' でバトルを再現します...", battleId, seed);

        logger.LogInformation("{Count}つの接続を作成中...", count);

        var connections = new List<IBattleClient>();
        var connectionFailures = 0;

        try
        {
            // Generate unique group name for this reproduction
            logger.LogInformation("グループ名: {GroupName}", groupName);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var connection = BattleClientFactory.Create(connectionType, loggerFactory);

                    var success = await connection.ConnectAsync(serverUrl, groupName);
                    if (!success)
                    {
                        throw new InvalidOperationException("Failed to connect to server");
                    }

                    // Call the server's ReproduceBattleAsync method to start reproduction
                    var reproduced = await connection.ReproduceBattleAsync(parsedBattleId, parsedSeed, groupName);
                    if (!reproduced)
                    {
                        logger.LogWarning("サーバーでのバトル再現リクエストが失敗しました");
                    }

                    connections.Add(connection);

                    logger.LogInformation("接続 {Current}/{Total} 完了", i + 1, count);
                    await Task.Delay(100); // Avoid overwhelming the server
                }
                catch (Exception ex)
                {
                    connectionFailures++;
                    logger.LogWarning("接続 {Current} が失敗: {Message}", i + 1, ex.Message);

                    if (connectionFailures > count / 2)
                    {
                        logger.LogError("接続失敗が多すぎるため、処理を中断します");
                        Environment.ExitCode = 1;
                        return;
                    }
                }
            }

            if (connections.Count == 0)
            {
                logger.LogError("接続できたクライアントがありません");
                Environment.ExitCode = 1;
                return;
            }

            logger.LogInformation("有効な接続数: {ConnectionCount}/{RequestedCount}", connections.Count, count);
            logger.LogInformation("バトルID '{BattleId}' とシード値 '{SeedValue}' でバトルが再現されます。", battleId, seed);
            logger.LogInformation("バトル完了まで待機中...");

            // Wait for battle completion with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(10));
            var completedTask = await Task.WhenAny(
                Task.Delay(TimeSpan.FromMinutes(5)), // バトル待機
                timeoutTask
            );

            if (completedTask == timeoutTask)
            {
                logger.LogWarning("バトル完了のタイムアウトが発生しました");
            }
            else
            {
                logger.LogInformation("バトルID '{BattleId}' とシード値 '{SeedValue}' のバトル再現が正常に完了しました", battleId, seed);
            }
        }
        finally
        {
            logger.LogInformation("接続をクリーンアップ中...");
            foreach (var connection in connections)
            {
                try
                {
                    await connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning("接続のクリーンアップ中にエラーが発生: {Message}", ex.Message);
                }
            }
        }
    }

    private async Task<IBattleClient> ConnectWithOptionsAsync(ConnectionOptions options, ConnectionType connectionType)
    {
        IBattleClient? client = null;
        try
        {
            client = BattleClientFactory.Create(connectionType, loggerFactory);

            var success = await client.ConnectAsync(options.ServerUrl, options.GroupName);
            if (!success)
            {
                throw new InvalidOperationException("Failed to connect to server");
            }

            return client;
        }
        catch
        {
            if (client is not null)
            {
                await client.DisposeAsync();
            }
            throw;
        }
    }

    private static void ShowInteractiveHelp()
    {
        Console.WriteLine("""
        Available commands:
          connect [url] [group]  - Connect to server (default: http://localhost:5000)
          connect-battle [url] [group] [count] [connectionType] - Connect multiple sessions (default: 5) to start a battle (use: SignalR or MagicOnion)
          disconnect             - Disconnect from server
          status                 - Show connection status
          server-status          - Show detailed server status
          get <key>              - Get value by key
          set <key> <value>      - Set key-value pair
          delete <key>           - Delete key
          list [pattern]         - List keys matching pattern (default: *)
          watch <key>            - Watch key for changes
          join <group_name>      - Join a group
          broadcast <message>    - Broadcast message to current group
          groups                 - List available groups
          mygroup                - Show current group information
          sd-connect [url]       - Connect to ServiceDiscovery server (default: http://localhost:5010)
          sd-create-session <group_name> [max_players] [connection_type] - Create session via ServiceDiscovery
          sd-list-sessions       - List active sessions
          sd-get-session <id>    - Get session information
          sd-battle <group_name> [count] [connection_type] - Start battle via ServiceDiscovery (default: 5 connections)
          battle-status          - Show battle status
          battle-replay <id>     - Play battle replay at 5fps speed
          battle-reproduce <battleId> <seedValue> [count] [groupName] [serverUrl] - Reproduce battle with specific battle ID and seed value
          battle-complete        - Notify server that battle replay is complete
          exit, quit             - Exit the program
          help                   - Show this help
        """);
    }

    // ServiceDiscovery related methods

    /// <summary>Connect to ServiceDiscovery server</summary>
    /// <param name="serviceDiscoveryUrl">ServiceDiscovery server URL</param>
    private async Task ServiceDiscoveryConnectAsync(string serviceDiscoveryUrl)
    {
        try
        {
            logger.LogInformation("Connecting to ServiceDiscovery server at {Url}", serviceDiscoveryUrl);
            await serviceDiscoveryProvider.InitializeAsync(serviceDiscoveryUrl);
            logger.LogInformation("Successfully connected to ServiceDiscovery server");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to ServiceDiscovery server");
        }
    }

    /// <summary>Create or find session via ServiceDiscovery</summary>
    /// <param name="groupName">Group name</param>
    /// <param name="maxPlayers">Maximum players per session</param>
    /// <param name="connectionType">Connection type</param>
    private async Task ServiceDiscoveryCreateSessionAsync(string groupName, int maxPlayers, ConnectionType connectionType)
    {
        try
        {
            logger.LogInformation("Creating session for group {GroupName} with {MaxPlayers} players using {ConnectionType}",
                groupName, maxPlayers, connectionType);

            var (client, session) = await serviceDiscoveryProvider.CreateSessionAndConnectAsync(groupName, connectionType, maxPlayers);

            logger.LogInformation("Successfully created session and connected:");
            logger.LogInformation("  Session ID: {SessionId}", session.SessionId);
            logger.LogInformation("  Group Name: {GroupName}", session.GroupName);
            logger.LogInformation("  Server ID: {ServerId}", session.AssignedServerId);
            logger.LogInformation("  Current/Max Players: {Current}/{Max}", session.CurrentPlayers, session.MaxPlayers);
            logger.LogInformation("  Status: {Status}", session.Status);

            // Clean up connection (for demo purposes)
            await client.DisposeAsync();
            logger.LogInformation("Connection cleaned up");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create session and connect");
        }
    }

    /// <summary>List all active sessions via ServiceDiscovery</summary>
    private async Task ServiceDiscoveryListSessionsAsync()
    {
        try
        {
            var sessions = await serviceDiscoveryProvider.ListActiveSessionsAsync();

            if (sessions.Count == 0)
            {
                logger.LogInformation("No active sessions found");
                return;
            }

            logger.LogInformation("Active Sessions ({Count} total):", sessions.Count);
            logger.LogInformation("┌────────────────────────────────────────┬─────────────────┬─────────────┬──────────┬─────────┐");
            logger.LogInformation("│ Session ID                             │ Group Name      │ Server ID   │ Players  │ Status  │");
            logger.LogInformation("├────────────────────────────────────────┼─────────────────┼─────────────┼──────────┼─────────┤");

            foreach (var session in sessions)
            {
                logger.LogInformation("│ {SessionId,-38} │ {GroupName,-15} │ {ServerId,-11} │ {Players,-8} │ {Status,-7} │",
                    session.SessionId, session.GroupName, session.AssignedServerId,
                    $"{session.CurrentPlayers}/{session.MaxPlayers}", session.Status);
            }
            logger.LogInformation("└────────────────────────────────────────┴─────────────────┴─────────────┴──────────┴─────────┘");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list active sessions");
        }
    }

    /// <summary>Get session information via ServiceDiscovery</summary>
    /// <param name="sessionId">Session ID</param>
    private async Task ServiceDiscoveryGetSessionAsync(string sessionId)
    {
        try
        {
            var session = await serviceDiscoveryProvider.GetSessionInfoAsync(sessionId);

            if (session is null)
            {
                logger.LogInformation("Session {SessionId} not found", sessionId);
                return;
            }

            logger.LogInformation("Session Information:");
            logger.LogInformation("  Session ID: {SessionId}", session.Value.SessionId);
            logger.LogInformation("  Group Name: {GroupName}", session.Value.GroupName);
            logger.LogInformation("  Server ID: {ServerId}", session.Value.AssignedServerId);
            logger.LogInformation("  Players: {Current}/{Max}", session.Value.CurrentPlayers, session.Value.MaxPlayers);
            logger.LogInformation("  Status: {Status}", session.Value.Status);
            logger.LogInformation("  Mode: {Mode}", session.Value.Mode);
            logger.LogInformation("  Created At: {CreatedAt}", session.Value.CreatedAt);
            if (session.Value.CompletedAt.HasValue)
            {
                logger.LogInformation("  Completed At: {CompletedAt}", session.Value.CompletedAt.Value);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get session information for {SessionId}", sessionId);
        }
    }

    /// <summary>Start batch battle via ServiceDiscovery</summary>
    /// <param name="groupName">Group name</param>
    /// <param name="count">Number of connections</param>
    /// <param name="connectionType">Connection type</param>
    private async Task ServiceDiscoveryBattleAsync(string groupName, int count, ConnectionType connectionType)
    {
        var connections = new List<IBattleClient>();
        try
        {
            logger.LogInformation("Starting batch battle via ServiceDiscovery for group {GroupName} with {Count} connections using {ConnectionType}",
                groupName, count, connectionType);

            var results = await serviceDiscoveryProvider.CreateMultipleSessionsAndConnectAsync(groupName, count, connectionType);

            logger.LogInformation("Successfully created {Successful}/{Total} connections", results.Count, count);

            foreach (var (client, session) in results)
            {
                connections.Add(client);
                logger.LogInformation("Connected to session {SessionId} on server {ServerId}",
                    session.SessionId, session.AssignedServerId);
            }

            if (connections.Count == 0)
            {
                logger.LogError("No connections established");
                return;
            }

            logger.LogInformation("Battle should start automatically when server has 5 connections");
            logger.LogInformation("Waiting for battle completion... (Press any key to disconnect all connections)");

            // Wait for user input to disconnect
            Console.ReadKey();

            logger.LogInformation("Disconnecting all connections...");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start batch battle");
        }
        finally
        {
            // Clean up all connections
            foreach (var connection in connections)
            {
                try
                {
                    await connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Error cleaning up connection: {Message}", ex.Message);
                }
            }
        }
    }

    /// <summary>Create session via ServiceDiscovery and connect to battle server</summary>
    /// <param name="groupName">Group name for the session</param>
    /// <param name="serviceDiscoveryUrl">ServiceDiscovery server URL</param>
    /// <param name="maxPlayers">Maximum players per session</param>
    /// <param name="connectionType">Connection type (SignalR or MagicOnion)</param>
    [Command("create-session")]
    public async Task CreateSessionAsync(
        [Argument] string groupName,
        string serviceDiscoveryUrl = "http://localhost:5010",
        int maxPlayers = 5,
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        try
        {
            logger.LogInformation("ServiceDiscovery Session Creation");
            logger.LogInformation("================================");
            logger.LogInformation("Group Name: {GroupName}", groupName);
            logger.LogInformation("ServiceDiscovery URL: {Url}", serviceDiscoveryUrl);
            logger.LogInformation("Max Players: {MaxPlayers}", maxPlayers);
            logger.LogInformation("Connection Type: {ConnectionType}", connectionType);

            // Initialize ServiceDiscovery connection
            await serviceDiscoveryProvider.InitializeAsync(serviceDiscoveryUrl);

            // Create session and connect to battle server
            var (client, session) = await serviceDiscoveryProvider.CreateSessionAndConnectAsync(groupName, connectionType, maxPlayers);

            logger.LogInformation("Session created successfully:");
            logger.LogInformation("  Session ID: {SessionId}", session.SessionId);
            logger.LogInformation("  Server ID: {ServerId}", session.AssignedServerId);
            logger.LogInformation("  Status: {Status}", session.Status);

            // Clean up
            await client.DisposeAsync();
            logger.LogInformation("Connection cleaned up");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create session via ServiceDiscovery");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Start multiple session battle via ServiceDiscovery</summary>
    /// <param name="url">-u, ServiceDiscovery server URL</param>
    /// <param name="group">-g, Group name for the battle</param>
    /// <param name="count">-c, Number of connections to create</param>
    /// <param name="connectionType">-t, Connection type (SignalR or MagicOnion)</param>
    [Command("sd-battle")]
    public async Task ServiceDiscoveryBattleCommandAsync(
        string url = "http://localhost:5010",
        string group = "battle-group",
        int count = 5,
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        var connections = new List<IBattleClient>();
        try
        {
            logger.LogInformation("ServiceDiscovery Batch Battle");
            logger.LogInformation("============================");
            logger.LogInformation("Group Name: {group}", group);
            logger.LogInformation("Connection Count: {Count}", count);
            logger.LogInformation("ServiceDiscovery URL: {Url}", url);
            logger.LogInformation("Connection Type: {ConnectionType}", connectionType);

            // Initialize ServiceDiscovery connection
            await serviceDiscoveryProvider.InitializeAsync(url);

            // Create multiple sessions and connect to battle servers
            var results = await serviceDiscoveryProvider.CreateMultipleSessionsAndConnectAsync(group, count, connectionType);

            logger.LogInformation("Successfully created {Successful}/{Total} connections", results.Count, count);

            if (results.Count == 0)
            {
                logger.LogError("No connections established");
                Environment.ExitCode = 1;
                return;
            }

            foreach (var (client, session) in results)
            {
                connections.Add(client);
                logger.LogInformation("Connected to session {SessionId} on server {ServerId}",
                    session.SessionId, session.AssignedServerId);
            }

            logger.LogInformation("Battle should start automatically when server has 5 connections");
            logger.LogInformation("Monitoring battle progress... (Press Ctrl+C to exit)");

            // Keep connections alive until interrupted
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Shutting down...");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start ServiceDiscovery batch battle");
            Environment.ExitCode = 1;
        }
        finally
        {
            // Clean up all connections
            logger.LogInformation("Disconnecting all connections...");
            foreach (var connection in connections)
            {
                try
                {
                    await connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Error cleaning up connection: {Message}", ex.Message);
                }
            }
        }
    }
}
