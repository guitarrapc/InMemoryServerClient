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
    [Command("connect")]
    public async Task ConnectAsync(
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
    [Command("status")]
    public void Status()
    {
        logger.LogInformation($"Connection status: {(client.IsConnected ? "Connected" : "Disconnected")}");
    }

    /// <summary>Get value by key</summary>
    /// <param name="key">The key to get</param>
    [Command("get")]
    public async Task GetAsync(string key)
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
    [Command("set")]
    public async Task SetAsync(string key, string value)
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
    [Command("delete")]
    public async Task DeleteAsync(string key)
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
    [Command("list")]
    public async Task ListAsync(string pattern = "*")
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
    [Command("watch")]
    public async Task WatchAsync(string key)
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
    [Command("join")]
    public async Task JoinAsync(string groupName)
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
    [Command("broadcast")]
    public async Task BroadcastAsync(string message)
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
    [Command("group")]
    public async Task GroupsAsync()
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
    [Command("my-group")]
    public async Task MyGroupAsync()
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
    [Command("battle-status")]
    public async Task BattleStatusAsync()
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
                    logger.LogInformation($"[BATTLE] Players alive: {alivePlayers}/{battleStatus.Players.Length}");
                    foreach (var player in battleStatus.Players)
                    {
                        var status = player.CurrentHp > 0 ? "Alive" : "Defeated";
                        logger.LogInformation($"[BATTLE] - {player.Name}: {status}, HP: {player.CurrentHp}/{player.MaxHp}, Position: ({player.PositionX},{player.PositionY})");
                    }

                    // Display enemies
                    var aliveEnemies = battleStatus.Enemies.Count(e => e.CurrentHp > 0);
                    logger.LogInformation($"[BATTLE] Enemies alive: {aliveEnemies}/{battleStatus.Enemies.Length}");

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
    [Command("battle-replay")]
    public async Task BattleReplayAsync(string battleId)
    {
        if (string.IsNullOrEmpty(battleId))
        {
            logger.LogInformation("Error: Battle ID is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            var replayData = await client.GetBattleReplayAsync(battleId);
            if (replayData != null)
            {
                logger.LogInformation($"Battle replay for battle {battleId}:");
                logger.LogInformation("Showing first 10 turns of replay data:");
                var lines = replayData.Split('\n');
                foreach (var line in lines.Take(10))
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        logger.LogInformation($"  {line[..Math.Min(100, line.Length)]}...");
                    }
                }
                logger.LogInformation($"Total turns in replay: {lines.Length}");
            }
            else
            {
                logger.LogInformation($"Replay data not found for battle: {battleId}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Disconnect from server</summary>
    [Command("disconnect")]
    public async Task DisconnectAsync()
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
    [Command("server-status")]
    public async Task ServerStatusAsync()
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
                        logger.LogInformation($"{groupSummary.Name} (ID: {groupSummary.Id}): {groupSummary.ConnectionCount}/{Constants.MaxConnectionsPerGroup} connections {battleStatusText}");
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
        Console.WriteLine("Available commands:");
        Console.WriteLine("  connect [url] [group]  - Connect to server (default: http://localhost:5000)");
        Console.WriteLine("  connect-battle [url] [group] [count] - Connect multiple sessions (default: 5) to start a battle");
        Console.WriteLine("  disconnect             - Disconnect from server");
        Console.WriteLine("  status                 - Show connection status");
        Console.WriteLine("  server-status          - Show detailed server status");
        Console.WriteLine("  get <key>              - Get value by key");
        Console.WriteLine("  set <key> <value>      - Set key-value pair");
        Console.WriteLine("  delete <key>           - Delete key");
        Console.WriteLine("  list [pattern]         - List keys matching pattern (default: *)");
        Console.WriteLine("  watch <key>            - Watch key for changes");
        Console.WriteLine("  join <group_name>      - Join a group");
        Console.WriteLine("  broadcast <message>    - Broadcast message to current group");
        Console.WriteLine("  groups                 - List available groups");
        Console.WriteLine("  mygroup                - Show current group information");
        Console.WriteLine("  battle-status          - Show battle status");
        Console.WriteLine("  battle-replay <id>     - Show replay data for a battle");
        Console.WriteLine("  battle-complete        - Notify server that battle replay is complete");
        Console.WriteLine("  exit, quit             - Exit the program");
        Console.WriteLine("  help                   - Show this help");
    }
}
