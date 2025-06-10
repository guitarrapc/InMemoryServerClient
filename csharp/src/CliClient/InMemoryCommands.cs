using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace CliClient;

/// <summary>
/// CLI commands for InMemory server
/// </summary>
public class InMemoryCommands
{
    private readonly InMemoryClient _client;
    private readonly ILogger<InMemoryCommands> _logger;

    public InMemoryCommands(InMemoryClient client, ILogger<InMemoryCommands> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Configure the command line application
    /// </summary>
    public void Configure(CommandLineApplication app)
    {
        app.Name = "inmemory-client";
        app.Description = "InMemory Client CLI";
        app.HelpOption("-?|-h|--help");

        // Connect command
        app.Command("connect", command =>
        {
            command.Description = "Connect to InMemory server";
            var urlOption = command.Option("-u|--url <URL>", "Server URL (default: http://localhost:5000)", CommandOptionType.SingleValue);
            var groupOption = command.Option("-g|--group <GROUP>", "Group name (optional)", CommandOptionType.SingleValue);

            command.OnExecute(async () =>
            {
                var url = urlOption.HasValue() ? urlOption.Value() : "http://localhost:5000";
                var group = groupOption.HasValue() ? groupOption.Value() : null;

                try
                {
                    if (await _client.ConnectAsync(url, group))
                    {
                        Console.WriteLine($"Connected to server: {url}");
                        if (!string.IsNullOrEmpty(group))
                        {
                            Console.WriteLine($"Joined group: {group}");
                        }
                        return 0;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to connect to server: {url}");
                        return 1;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to server: {ex.Message}");
                    return 1;
                }
            });
        });

        // Disconnect command
        app.Command("disconnect", command =>
        {
            command.Description = "Disconnect from server";
            command.OnExecute(async () =>
            {
                try
                {
                    await _client.DisconnectAsync();
                    Console.WriteLine("Disconnected from server");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disconnecting from server: {ex.Message}");
                    return 1;
                }
            });
        });

        // Status command
        app.Command("status", command =>
        {
            command.Description = "Check connection status";
            command.OnExecute(() =>
            {
                Console.WriteLine($"Connection status: {(_client.IsConnected ? "Connected" : "Disconnected")}");
                return 0;
            });
        });

        // Get command
        app.Command("get", command =>
        {
            command.Description = "Get value by key";
            var keyArgument = command.Argument("key", "The key to get");

            command.OnExecute(async () =>
            {
                if (string.IsNullOrEmpty(keyArgument.Value))
                {
                    Console.WriteLine("Error: Key is required");
                    return 1;
                }

                try
                {
                    var value = await _client.GetAsync(keyArgument.Value);
                    if (value != null)
                    {
                        Console.WriteLine($"{keyArgument.Value} = {value}");
                    }
                    else
                    {
                        Console.WriteLine($"Key not found: {keyArgument.Value}");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            });
        });

        // More commands to be added...

        // Set default action
        app.OnExecute(() =>
        {
            app.ShowHelp();
            return 0;
        });
    }

    /// <summary>
    /// Run interactive mode
    /// </summary>
    public async Task InteractiveAsync()
    {
        Console.WriteLine("Interactive mode started. Type 'help' for a list of commands, 'exit' to quit.");

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
                        ShowHelp();
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

    private void ShowHelp()
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
        Console.WriteLine("  exit, quit             - Exit the program");
        Console.WriteLine("  help                   - Show this help");
    }
}
