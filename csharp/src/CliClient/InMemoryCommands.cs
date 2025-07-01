using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Shared;

namespace CliClient;

/// <summary>
/// CLI commands for InMemory server
/// Public Method will be automatically registered as commands.
/// ListFooAsync will be registered as list-foo command.
/// </summary>
public class InMemoryCommands(InMemoryClient client, MultiClientManager multiClientManager, ILogger<InMemoryCommands> logger)
{
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
                        var group = args.Length > 2 ? args[2] : null;
                        await ConnectAsync(url, group);
                        break;

                    case "connect-battle":
                        var battleUrl = args.Length > 1 ? args[1] : "http://localhost:5000";
                        var battleGroup = args.Length > 2 ? args[2] : "battle-group";
                        var count = args.Length > 3 && int.TryParse(args[3], out var c) ? c : 5;
                        await ConnectMultipleAsync(battleUrl, battleGroup, count);
                        break;

                    case "disconnect":
                        await DisconnectAsync();
                        break;

                    case "status":
                        Status();
                        break;

                    case "server-status":
                        await ServerStatusAsync();
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

                    case "groups":
                        await GroupsAsync();
                        break;

                    case "mygroup":
                        await MyGroupAsync();
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

        // Ensure disconnection on exit
        if (client.IsConnected)
        {
            await client.DisconnectAsync();
        }
    }

    /// <summary>Connect to InMemory server</summary>
    /// <param name="url">-u, Server URL</param>
    /// <param name="group">-g, Group name (optional)</param>
    private async Task ConnectAsync(
        string url = "http://localhost:5000",
        string? group = null)
    {
        try
        {
            if (await client.ConnectAsync(url, group))
            {
                logger.LogInformation($"Connected to server: {url}");
                if (!string.IsNullOrEmpty(group))
                {
                    logger.LogInformation($"Joined group: {group}");
                }
            }
            else
            {
                logger.LogInformation($"Failed to connect to server: {url}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error connecting to server: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Check connection status</summary>
    private void Status()
    {
        logger.LogInformation($"Connection status: {(client.IsConnected ? "Connected" : "Disconnected")}");
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

        try
        {
            var value = await client.GetAsync(key);
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

        try
        {
            if (await client.SetAsync(key, value))
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

        try
        {
            if (await client.DeleteAsync(key))
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
        try
        {
            var keys = await client.ListAsync(pattern);
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

        try
        {
            if (await client.WatchAsync(key))
            {
                logger.LogInformation($"Watching key: {key}");
            }
            else
            {
                logger.LogInformation($"Failed to watch key: {key}");
                Environment.ExitCode = 1;
            }
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

        try
        {
            if (await client.JoinGroupAsync(groupName))
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

        try
        {
            if (await client.BroadcastAsync(message))
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

    /// <summary>Get list of available groups</summary>
    private async Task GroupsAsync()
    {
        try
        {
            var groups = await client.GetGroupsAsync();
            logger.LogInformation("Available groups:");
            foreach (var group in groups)
            {
                logger.LogInformation($"  {group}");
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get current group information</summary>
    private async Task MyGroupAsync()
    {
        try
        {
            var currentGroup = await client.GetMyGroupAsync();
            if (currentGroup != null)
            {
                logger.LogInformation($"Current group: {currentGroup}");
            }
            else
            {
                logger.LogInformation("Not in any group");
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
        try
        {
            var battleStatus = await client.GetBattleStatusAsync();
            if (battleStatus != null)
            {
                if (battleStatus.IsInProgress)
                {
                    logger.LogInformation($"[BATTLE] ========== Battle Status ==========");
                    logger.LogInformation($"[BATTLE] Battle ID: {battleStatus.BattleId}");
                    logger.LogInformation($"[BATTLE] Turn: {battleStatus.CurrentTurn}/{battleStatus.TotalTurns}");

                    // Display players
                    var alivePlayers = battleStatus.Players.Count(p => p.CurrentHp > 0);
                    logger.LogInformation($"[BATTLE] Players alive: {alivePlayers}/{battleStatus.Players.Count}");
                    foreach (var player in battleStatus.Players)
                    {
                        var status = player.CurrentHp > 0 ? "Alive" : "Defeated";
                        var jobInfo = player.Job.HasValue ? $" (Job: {player.Job})" : "";
                        logger.LogInformation($"[BATTLE] - {player.Name}{jobInfo}: {status}, HP: {player.CurrentHp}/{player.MaxHp}, ATK: {player.Attack}, DEF: {player.Defense}, SPD: {player.Speed}, Position: ({player.Position})");
                    }

                    // Display enemies
                    var aliveEnemies = battleStatus.Enemies.Count(e => e.CurrentHp > 0);
                    logger.LogInformation($"[BATTLE] Enemies alive: {aliveEnemies}/{battleStatus.Enemies.Count}");
                    foreach (var enemy in battleStatus.Enemies.Take(3)) // Show first 3 enemies to avoid spam
                    {
                        var status = enemy.CurrentHp > 0 ? "Alive" : "Defeated";
                        var jobInfo = enemy.Job.HasValue ? $" (Job: {enemy.Job})" : "";
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
                    logger.LogInformation("No active battle in progress.");
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
        if (string.IsNullOrEmpty(battleId))
        {
            logger.LogInformation("Error: Battle ID is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            logger.LogInformation($"Requesting battle replay for battle {battleId}...");
            var replayData = await client.GetBattleReplayAsync(battleId);
            if (replayData is null)
            {
                logger.LogInformation($"Replay data not found for battle: {battleId}");
                Environment.ExitCode = 1;
                return;
            }

            logger.LogInformation($"Replay data received for battle {battleId}");
            logger.LogInformation("Processing battle replay data...");

            // Parse the JSONL file into BattleStatus objects
            List<BattleStatus> battleStatuses = [];
            var lines = replayData.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    try
                    {
                        var status = System.Text.Json.JsonSerializer.Deserialize<BattleStatus>(line);
                        if (status != null)
                        {
                            battleStatuses.Add(status);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation($"Error parsing battle status: {ex.Message}");
                    }
                }
            }

            logger.LogInformation($"Found {battleStatuses.Count} turns in replay data");

            if (battleStatuses.Count <= 0)
            {
                logger.LogInformation("No valid battle data found in the replay");
                Environment.ExitCode = 1;
            }
            else
            {
                // Play the battle replay using InMemoryClient's replay functionality
                await client.PlayBattleReplayAsync(battleStatuses);
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
        try
        {
            await client.DisconnectAsync();
            logger.LogInformation("Disconnected from server");
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error disconnecting from server: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get server status</summary>
    private async Task ServerStatusAsync()
    {
        try
        {
            if (!client.IsConnected)
            {
                logger.LogInformation("Not connected to server. Connect first.");
                Environment.ExitCode = 1;
                return;
            }
            var serverStatus = await client.GetServerStatusAsync();
            if (serverStatus != null)
            {
                logger.LogInformation("============ SERVER STATUS ============");
                logger.LogInformation($"Uptime: {serverStatus.Uptime:d\\d\\ h\\h\\ m\\m\\ s\\s}");
                logger.LogInformation($"Total Connections: {serverStatus.TotalConnections}");
                logger.LogInformation($"Group Count: {serverStatus.GroupCount}");
                logger.LogInformation($"Active Battle Count: {serverStatus.ActiveBattleCount}");
                if (serverStatus.Groups.Count > 0)
                {
                    logger.LogInformation("\n---------- GROUPS ----------");
                    foreach (var groupSummary in serverStatus.Groups)
                    {
                        var battleStatusText = !string.IsNullOrEmpty(groupSummary.BattleId) ? "[Battle in progress]" : "";
                        logger.LogInformation($"{groupSummary.Name} (ID: {groupSummary.Id}): {groupSummary.ConnectionCount}/{SystemDefines.MaxConnectionsPerGroup} connections {battleStatusText}");
                    }
                }

                if (serverStatus.ActiveBattles.Count > 0)
                {
                    logger.LogInformation("\n---------- ACTIVE BATTLES ----------");
                    foreach (var battle in serverStatus.ActiveBattles)
                    {
                        var duration = DateTime.UtcNow - battle.StartedAt;
                        logger.LogInformation($"Battle {battle.Id} (Group: {battle.GroupId})");
                        logger.LogInformation($"  Turn: {battle.CurrentTurn}, Players: {battle.PlayerCount}, Enemies: {battle.EnemyCount}");
                        logger.LogInformation($"  Duration: {duration:h\\h\\ m\\m\\ s\\s}");
                    }
                }

                logger.LogInformation("=======================================");
            }
            else
            {
                logger.LogInformation("Failed to get server status.");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Connect multiple sessions to the server with the same group</summary>
    /// <param name="url">-u, Server URL</param>
    /// <param name="group">-g, Group name</param>
    /// <param name="count">-c, Number of sessions to connect (default: 5)</param>
    [Command("connect-battle")]
    public async Task ConnectMultipleAsync(string url = "http://localhost:5000", string group = "battle-group", int count = 5)
    {
        if (count <= 0)
        {
            logger.LogInformation("Error: Count must be greater than 0");
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
            if (await multiClientManager.ConnectMultipleClientsAsync(url, group, count))
            {
                logger.LogInformation($"Successfully connected {count} clients to group: {group}");
                logger.LogInformation($"If this completes the group (5 sessions), a battle should start automatically!");

                // バトルの完了を待機
                var timeout = TimeSpan.FromMinutes(5);
                logger.LogInformation($"Waiting for battle to complete (timeout: {timeout})...");

                if (await multiClientManager.WaitForBattleCompletionAsync(timeout))
                {
                    logger.LogInformation("Battle completed successfully!");
                }
                else
                {
                    logger.LogInformation("Timed out or error occurred while waiting for battle completion");
                }
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

    private static void ShowInteractiveHelp()
    {
        Console.WriteLine("""
        Available commands:
          connect [url] [group]  - Connect to server (default: http://localhost:5000)
          connect-battle [url] [group] [count] - Connect multiple sessions (default: 5) to start a battle
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
          mygroup                - Show current group information          battle-status          - Show battle status
          battle-replay <id>     - Play battle replay at 5fps speed
          battle-complete        - Notify server that battle replay is complete
          exit, quit             - Exit the program
          help                   - Show this help
        """);
    }
}
