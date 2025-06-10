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

                    case "disconnect":
                        await _client.DisconnectAsync();
                        Console.WriteLine("Disconnected from server");
                        break;

                    case "status":
                        Console.WriteLine($"Connection status: {(_client.IsConnected ? "Connected" : "Disconnected")}");
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
                        var status = await _client.GetBattleStatusAsync();
                        if (status != null)
                        {
                            Console.WriteLine($"Battle status: {status}");
                        }
                        else
                        {
                            Console.WriteLine("No active battle");
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
            var status = await _client.GetBattleStatusAsync();
            if (status != null)
            {
                Console.WriteLine($"Battle status: {status}");
            }
            else
            {
                Console.WriteLine("No active battle");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static void ShowInteractiveHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  connect [url] [group]  - Connect to server (default: http://localhost:5000)");
        Console.WriteLine("  disconnect             - Disconnect from server");
        Console.WriteLine("  status                 - Show connection status");
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
        Console.WriteLine("  exit, quit             - Exit the program");
        Console.WriteLine("  help                   - Show this help");
    }
}
