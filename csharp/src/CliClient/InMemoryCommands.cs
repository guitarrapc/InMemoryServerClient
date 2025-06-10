using ConsoleAppFramework;
using Microsoft.Extensions.Logging;

namespace CliClient;

/// <summary>
/// CLI commands for InMemory server
/// Public Method will be automatically registered as commands.
/// ListFooAsync will be registered as list-foo command.
/// </summary>
public class InMemoryCommands(InMemoryClient client, ILogger<InMemoryCommands> logger)
{
    private readonly InMemoryClient _client = client;
    private readonly ILogger<InMemoryCommands> _logger = logger;

    /// <summary>Start interactive mode</summary>
    [Command("")]
    public async Task InteractiveAsync()
    {
        Console.WriteLine("InMemory CLI Client - Interactive Mode");
        Console.WriteLine("=====================================");
        Console.WriteLine("Type 'help' for a list of commands, 'exit' to quit.");
        Console.WriteLine();

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
                        if (await _client.ConnectAsync(url, group))
                        {
                            Console.WriteLine($"Connected to server: {url}");
                            if (!string.IsNullOrEmpty(group))
                            {
                                Console.WriteLine($"Joined group: {group}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Failed to connect to server: {url}");
                        }
                        break;

                    case "connect-battle":
                        var battleUrl = args.Length > 1 ? args[1] : "http://localhost:5000";
                        var battleGroup = args.Length > 2 ? args[2] : "battle-group";
                        var count = args.Length > 3 && int.TryParse(args[3], out var c) ? c : 5;

                        Console.WriteLine($"Connecting {count} sessions to server: {battleUrl}");
                        Console.WriteLine($"Group name: {battleGroup}");

                        if (await _client.ConnectMultipleAsync(battleUrl, battleGroup, count))
                        {
                            Console.WriteLine($"Successfully connected {count} sessions to group: {battleGroup}");
                            Console.WriteLine($"If this completes the group (5 sessions), a battle should start automatically!");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to connect {count} sessions to server");
                        }
                        break;

                    case "disconnect":
                        await _client.DisconnectAsync();
                        Console.WriteLine("Disconnected from server");
                        break;

                    case "status":
                        Console.WriteLine($"Connection status: {(_client.IsConnected ? "Connected" : "Disconnected")}");
                        break;

                    case "server-status":
                        if (!_client.IsConnected)
                        {
                            Console.WriteLine("Not connected to server. Connect first.");
                            break;
                        }
                        var serverStatus = await _client.GetServerStatusAsync();
                        if (serverStatus != null)
                        {
                            Console.WriteLine("============ SERVER STATUS ============");
                            Console.WriteLine($"Uptime: {serverStatus.Uptime:d\\d\\ h\\h\\ m\\m\\ s\\s}");
                            Console.WriteLine($"Total Connections: {serverStatus.TotalConnections}");
                            Console.WriteLine($"Group Count: {serverStatus.GroupCount}");
                            Console.WriteLine($"Active Battle Count: {serverStatus.ActiveBattleCount}");

                            if (serverStatus.Groups.Count > 0)
                            {
                                Console.WriteLine("\n---------- GROUPS ----------");
                                foreach (var group in serverStatus.Groups)
                                {
                                    var battleStatus = !string.IsNullOrEmpty(group.BattleId) ? "[Battle in progress]" : "";
                                    Console.WriteLine($"{group.Name} (ID: {group.Id}): {group.ConnectionCount}/{Constants.MaxConnectionsPerGroup} connections {battleStatus}");
                                }
                            }

                            if (serverStatus.ActiveBattles.Count > 0)
                            {
                                Console.WriteLine("\n---------- ACTIVE BATTLES ----------");
                                foreach (var battle in serverStatus.ActiveBattles)
                                {
                                    var duration = DateTime.UtcNow - battle.StartedAt;
                                    Console.WriteLine($"Battle {battle.Id} (Group: {battle.GroupId})");
                                    Console.WriteLine($"  Turn: {battle.CurrentTurn}, Players: {battle.PlayerCount}, Enemies: {battle.EnemyCount}");
                                    Console.WriteLine($"  Duration: {duration:h\\h\\ m\\m\\ s\\s}");
                                }
                            }

                            Console.WriteLine("=======================================");
                        }
                        else
                        {
                            Console.WriteLine("Failed to get server status.");
                        }
                        break;

                    case "get":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: get <key>");
                            break;
                        }
                        var value = await _client.GetAsync(args[1]);
                        if (value != null)
                        {
                            Console.WriteLine($"{args[1]} = {value}");
                        }
                        else
                        {
                            Console.WriteLine($"Key not found: {args[1]}");
                        }
                        break;

                    case "set":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Usage: set <key> <value>");
                            break;
                        }
                        var setValue = string.Join(' ', args.Skip(2));
                        if (await _client.SetAsync(args[1], setValue))
                        {
                            Console.WriteLine($"Key {args[1]} set to: {setValue}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to set key: {args[1]}");
                        }
                        break;

                    case "delete":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: delete <key>");
                            break;
                        }
                        if (await _client.DeleteAsync(args[1]))
                        {
                            Console.WriteLine($"Key deleted: {args[1]}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to delete key: {args[1]}");
                        }
                        break;

                    case "list":
                        var pattern = args.Length > 1 ? args[1] : "*";
                        var keys = await _client.ListAsync(pattern);
                        Console.WriteLine($"Keys matching pattern '{pattern}':");
                        foreach (var key in keys)
                        {
                            Console.WriteLine($"  {key}");
                        }
                        break;

                    case "watch":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: watch <key>");
                            break;
                        }
                        if (await _client.WatchAsync(args[1]))
                        {
                            Console.WriteLine($"Watching key: {args[1]}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to watch key: {args[1]}");
                        }
                        break;

                    case "join":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: join <group_name>");
                            break;
                        }
                        if (await _client.JoinGroupAsync(args[1]))
                        {
                            Console.WriteLine($"Joined group: {args[1]}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to join group: {args[1]}");
                        }
                        break;

                    case "broadcast":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: broadcast <message>");
                            break;
                        }
                        var message = string.Join(' ', args.Skip(1));
                        if (await _client.BroadcastAsync(message))
                        {
                            Console.WriteLine($"Message broadcasted: {message}");
                        }
                        else
                        {
                            Console.WriteLine("Failed to broadcast message");
                        }
                        break;

                    case "groups":
                        var groups = await _client.GetGroupsAsync();
                        Console.WriteLine("Available groups:");
                        foreach (var g in groups)
                        {
                            Console.WriteLine($"  {g}");
                        }
                        break;

                    case "mygroup":
                        var groupInfo = await _client.GetMyGroupAsync();
                        if (groupInfo != null)
                        {
                            Console.WriteLine($"Current group: {groupInfo}");
                        }
                        else
                        {
                            Console.WriteLine("Not in any group");
                        }
                        break;
                    case "battle-status":
                        var battleStatus = await _client.GetBattleStatusAsync();
                        if (battleStatus != null)
                        {
                            if (battleStatus.IsInProgress)
                            {
                                Console.WriteLine($"[BATTLE] ========== Battle Status ==========");
                                Console.WriteLine($"[BATTLE] Battle ID: {battleStatus.BattleId}");
                                Console.WriteLine($"[BATTLE] Turn: {battleStatus.CurrentTurn}/{battleStatus.TotalTurns}");

                                // Display players
                                var alivePlayers = battleStatus.Players.Count(p => p.CurrentHp > 0);
                                Console.WriteLine($"[BATTLE] Players alive: {alivePlayers}/{battleStatus.Players.Count}");
                                foreach (var player in battleStatus.Players)
                                {
                                    var status = player.CurrentHp > 0 ? "Alive" : "Defeated";
                                    Console.WriteLine($"[BATTLE] - {player.Name}: {status}, HP: {player.CurrentHp}/{player.MaxHp}, Position: ({player.PositionX},{player.PositionY})");
                                }

                                // Display enemies
                                var aliveEnemies = battleStatus.Enemies.Count(e => e.CurrentHp > 0);
                                Console.WriteLine($"[BATTLE] Enemies alive: {aliveEnemies}/{battleStatus.Enemies.Count}");

                                // Show recent logs
                                if (battleStatus.RecentLogs.Count > 0)
                                {
                                    Console.WriteLine("[BATTLE] Recent actions:");
                                    foreach (var log in battleStatus.RecentLogs.TakeLast(5))
                                    {
                                        Console.WriteLine($"[BATTLE] > {log}");
                                    }
                                }

                                Console.WriteLine("[BATTLE] ===================================");
                            }
                            else
                            {
                                Console.WriteLine("No active battle in progress.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No active battle or not in a group.");
                        }
                        break;

                    case "battle-replay":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: battle-replay <battle_id>");
                            break;
                        }
                        var replayData = await _client.GetBattleReplayAsync(args[1]);
                        if (replayData != null)
                        {
                            Console.WriteLine($"Battle replay for battle {args[1]}:");
                            Console.WriteLine("Showing first 10 turns of replay data:");
                            var lines = replayData.Split('\n');
                            foreach (var line in lines.Take(10))
                            {
                                if (!string.IsNullOrEmpty(line))
                                {
                                    Console.WriteLine($"  {line[..Math.Min(100, line.Length)]}...");
                                }
                            }
                            Console.WriteLine($"Total turns in replay: {lines.Length}");
                        }
                        else
                        {
                            Console.WriteLine($"Replay data not found for battle: {args[1]}");
                        }
                        break;

                    case "battle-complete":
                        try
                        {
                            if (await _client.BattleReplayCompleteAsync())
                            {
                                Console.WriteLine("Successfully notified server about battle replay completion");
                            }
                            else
                            {
                                Console.WriteLine("Failed to notify server about battle replay completion");
                                Environment.ExitCode = 1;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                            Environment.ExitCode = 1;
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // Ensure disconnection on exit
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
    }

    /// <summary>Connect to InMemory server</summary>
    /// <param name="url">-u, Server URL</param>
    /// <param name="group">-g, Group name (optional)</param>
    public async Task ConnectAsync(
        string url = "http://localhost:5000",
        string? group = null)
    {
        try
        {
            if (await _client.ConnectAsync(url, group))
            {
                Console.WriteLine($"Connected to server: {url}");
                if (!string.IsNullOrEmpty(group))
                {
                    Console.WriteLine($"Joined group: {group}");
                }
            }
            else
            {
                Console.WriteLine($"Failed to connect to server: {url}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to server: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Disconnect from server</summary>
    public async Task DisconnectAsync()
    {
        try
        {
            await _client.DisconnectAsync();
            Console.WriteLine("Disconnected from server");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting from server: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Check connection status</summary>
    public void Status()
    {
        Console.WriteLine($"Connection status: {(_client.IsConnected ? "Connected" : "Disconnected")}");
    }

    /// <summary>Get value by key</summary>
    /// <param name="key">The key to get</param>
    public async Task GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            var value = await _client.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine($"{key} = {value}");
            }
            else
            {
                Console.WriteLine($"Key not found: {key}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Set key-value pair</summary>
    /// <param name="key">The key to set</param>
    /// <param name="value">The value to set</param>
    public async Task SetAsync(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine("Error: Value is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            if (await _client.SetAsync(key, value))
            {
                Console.WriteLine($"Key {key} set to: {value}");
            }
            else
            {
                Console.WriteLine($"Failed to set key: {key}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Delete key</summary>
    /// <param name="key">The key to delete</param>
    public async Task DeleteAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            if (await _client.DeleteAsync(key))
            {
                Console.WriteLine($"Key deleted: {key}");
            }
            else
            {
                Console.WriteLine($"Failed to delete key: {key}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>List keys matching pattern</summary>
    /// <param name="pattern">-p, The pattern to match</param>
    public async Task ListAsync(string pattern = "*")
    {
        try
        {
            var keys = await _client.ListAsync(pattern);
            Console.WriteLine($"Keys matching pattern '{pattern}':");
            foreach (var key in keys)
            {
                Console.WriteLine($"  {key}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Watch key for changes</summary>
    /// <param name="key">The key to watch</param>
    public async Task WatchAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Error: Key is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            if (await _client.WatchAsync(key))
            {
                Console.WriteLine($"Watching key: {key}");
            }
            else
            {
                Console.WriteLine($"Failed to watch key: {key}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Join a group</summary>
    /// <param name="groupName">The group name to join</param>
    public async Task JoinAsync(string groupName)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            Console.WriteLine("Error: Group name is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            if (await _client.JoinGroupAsync(groupName))
            {
                Console.WriteLine($"Joined group: {groupName}");
            }
            else
            {
                Console.WriteLine($"Failed to join group: {groupName}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Broadcast message to current group</summary>
    /// <param name="message">The message to broadcast</param>
    public async Task BroadcastAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Console.WriteLine("Error: Message is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            if (await _client.BroadcastAsync(message))
            {
                Console.WriteLine($"Message broadcasted: {message}");
            }
            else
            {
                Console.WriteLine("Failed to broadcast message");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get list of available groups</summary>
    public async Task GroupsAsync()
    {
        try
        {
            var groups = await _client.GetGroupsAsync();
            Console.WriteLine("Available groups:");
            foreach (var group in groups)
            {
                Console.WriteLine($"  {group}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get current group information</summary>
    [Command("my-group")]
    public async Task MyGroupAsync()
    {
        try
        {
            var groupInfo = await _client.GetMyGroupAsync();
            if (groupInfo != null)
            {
                Console.WriteLine($"Current group: {groupInfo}");
            }
            else
            {
                Console.WriteLine("Not in any group");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Get battle status</summary>
    [Command("battle-status")]
    public async Task BattleStatusAsync()
    {
        try
        {
            var battleStatus = await _client.GetBattleStatusAsync();
            if (battleStatus != null)
            {
                if (battleStatus.IsInProgress)
                {
                    Console.WriteLine($"[BATTLE] ========== Battle Status ==========");
                    Console.WriteLine($"[BATTLE] Battle ID: {battleStatus.BattleId}");
                    Console.WriteLine($"[BATTLE] Turn: {battleStatus.CurrentTurn}/{battleStatus.TotalTurns}");

                    // Display players
                    var alivePlayers = battleStatus.Players.Count(p => p.CurrentHp > 0);
                    Console.WriteLine($"[BATTLE] Players alive: {alivePlayers}/{battleStatus.Players.Count}");
                    foreach (var player in battleStatus.Players)
                    {
                        var status = player.CurrentHp > 0 ? "Alive" : "Defeated";
                        Console.WriteLine($"[BATTLE] - {player.Name}: {status}, HP: {player.CurrentHp}/{player.MaxHp}, Position: ({player.PositionX},{player.PositionY})");
                    }

                    // Display enemies
                    var aliveEnemies = battleStatus.Enemies.Count(e => e.CurrentHp > 0);
                    Console.WriteLine($"[BATTLE] Enemies alive: {aliveEnemies}/{battleStatus.Enemies.Count}");

                    // Show recent logs
                    if (battleStatus.RecentLogs.Count > 0)
                    {
                        Console.WriteLine("[BATTLE] Recent actions:");
                        foreach (var log in battleStatus.RecentLogs.TakeLast(5))
                        {
                            Console.WriteLine($"[BATTLE] > {log}");
                        }
                    }

                    Console.WriteLine("[BATTLE] ===================================");
                }
                else
                {
                    Console.WriteLine("No active battle in progress.");
                }
            }
            else
            {
                Console.WriteLine("No active battle or not in a group.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
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
            Console.WriteLine("Error: Battle ID is required");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            var replayData = await _client.GetBattleReplayAsync(battleId);
            if (replayData != null)
            {
                Console.WriteLine($"Battle replay for battle {battleId}:");
                Console.WriteLine("Showing first 10 turns of replay data:");
                var lines = replayData.Split('\n');
                foreach (var line in lines.Take(10))
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        Console.WriteLine($"  {line[..Math.Min(100, line.Length)]}...");
                    }
                }
                Console.WriteLine($"Total turns in replay: {lines.Length}");
            }
            else
            {
                Console.WriteLine($"Replay data not found for battle: {battleId}");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Connect multiple sessions to the server with the same group</summary>
    /// <param name="url">-u, Server URL</param>
    /// <param name="group">-g, Group name</param>
    /// <param name="count">-c, Number of sessions to connect (default: 5)</param>
    [Command("connect-battle")]
    public async Task ConnectMultipleAsync(
        string url = "http://localhost:5000",
        string group = "battle-group",
        int count = 5)
    {
        if (count <= 0)
        {
            Console.WriteLine("Error: Count must be greater than 0");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrEmpty(group))
        {
            Console.WriteLine("Error: Group name is required for multiple connections");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            Console.WriteLine($"Connecting {count} sessions to server: {url}");
            Console.WriteLine($"Group name: {group}");

            if (await _client.ConnectMultipleAsync(url, group, count))
            {
                Console.WriteLine($"Successfully connected {count} sessions to group: {group}");
                Console.WriteLine($"If this completes the group (5 sessions), a battle should start automatically!");
            }
            else
            {
                Console.WriteLine($"Failed to connect {count} sessions to server");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting multiple sessions: {ex.Message}");
            Environment.ExitCode = 1;
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
