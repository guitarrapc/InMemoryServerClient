[![Build](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/build.yaml)
[![Release](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/release.yaml/badge.svg)](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/release.yaml)

[![Docker Pulls](https://img.shields.io/docker/pulls/guitarrapc/inmemoryserverclient.svg?maxAge=3600)](https://hub.docker.com/r/guitarrapc/inmemoryserverclient/)
![Static Badge](https://img.shields.io/badge/ghcr.io-inmemoryserverclient?style=flat&logo=github&logoColor=white&color=2088FF&link=https%3A%2F%2Fgithub.com%2Fguitarrapc%2FInMemoryServerClient%2Fpkgs%2Fcontainer%2Finmemoryserverclient)

# InMemoryServerClient

*[日本語版はこちら / Japanese version](README_jp.md)*

A C# implementation of an in-memory stateful server and CLI client project. The server maintains state in memory and provides an interface for clients to interact with this state. The system supports real-time communication, group management, and an automated battle system with replay capabilities.

## Features

### Server Features
- **Basic Key-Value Store Operations**
  - GET/SET/DELETE/LIST operations
  - Key change monitoring functionality
- **Group Management**
  - Group creation and management identified by UUIDv4
  - Client-specified or auto-assigned group names
  - Maximum connection limit per group (max 5 sessions)
  - Automatic group expiration management (10 minutes)
- **Battle System**
  - Automatic battle start when group is full (5 sessions)
  - Turn-based RPG-style battles on 20x20 pseudo field
  - Pre-computed battle simulation for efficient processing
  - Battle actions: movement, attack, defense
  - Victory condition: defeating all enemies
  - Battle replay saved in JSON LINE format
  - Memory-optimized implementation with chunked data transmission

### Client Features
- **Interactive Mode**: Interactive command line with real-time responses
- **Batch Mode**: Single command execution for automation
- **Connection Management**: Connect multiple sessions for battle testing
- **Group Operations**: Join groups, broadcast messages, check group status
- **Battle Visualization**: Display battle status and replay battles at 5fps
- **Server Status**: Monitor server statistics and resource usage

## Architecture

### Technology Stack
- **.NET 9**: Latest .NET Runtime for modern C# features
- **SignalR**: Real-time bidirectional communication
- **Minimal API**: Lightweight server implementation
- **xUnit + NSubstitute**: Comprehensive testing framework
- **ConsoleAppFramework**: Powerful CLI framework for client commands

### Project Structure
```
csharp/
├── src/
│   ├── InMemoryServer/     # Server implementation
│   ├── CliClient/          # CLI client
│   ├── Shared/             # Shared library
│   └── Tests/              # Test project
├── Dockerfile              # Server containerization
└── Directory.Build.props   # Build configuration
```

## Getting Started

### Prerequisites
- .NET 9 SDK
- Docker (for container execution)

### Build
```bash
cd csharp
dotnet build
```

### Run Tests
```bash
cd csharp
dotnet test
```

### Start Server

#### Local Execution
```bash
cd csharp/src/InMemoryServer
dotnet run
```

#### Docker Execution
```bash
cd csharp
docker build -t inmemory-server .
docker run -p 5001:5001 inmemory-server
```

### Client Usage

#### Interactive Mode
```bash
cd csharp/src/CliClient
dotnet run
```

#### Multi-Client Battle Testing

To test a battle with multiple clients using a single command:
```bash
cd csharp/src/CliClient

# SignalR
dotnet run -- connect-battle -u wss://localhost:5001 -g test-battle -t SignalR -c 5

# MagicOnion
dotnet run -- connect-battle -u https://localhost:5001 -g test-battle -t MagicOnion -c 5
```

This will create 5 client connections in the same group to trigger an automatic battle.

#### Single Command Examples

SignalR:

```bash
# Connect single sessions for battle testing
dotnet run -- connect-battle -u wss://localhost:5001 -g battle-group -t SignalR -c 1

# Connect multiple sessions for battle testing
dotnet run -- connect-battle -u wss://localhost:5001 -g battle-group -t SignalR -c 4
```

MagicOnion:

```bash
# Connect single sessions for battle testing
dotnet run -- connect-battle -u https://localhost:5001 -g battle-group -t MagicOnion -c 1

# Connect multiple sessions for battle testing
dotnet run -- connect-battle -u https://localhost:5001 -g battle-group -t MagicOnion -c 4
```

#### Interactive Mode Commands
```
connect [url] [group]                - Connect to server
connect-battle [url] [group] [count] - Connect multiple clients for battle testing
disconnect                           - Disconnect from server
status                               - Show connection status
get <key>                            - Get key
set <key> <value>                    - Set key
delete <key>                         - Delete key
list [pattern]                       - List keys (pattern optional)
watch <key>                          - Watch key changes
join <group_name>                    - Join group
broadcast <message>                  - Send message to group
groups                               - List groups
mygroup                              - Current group info
battle-status                        - Check battle status
battle-replay <id>                   - Show replay data for a battle
battle-complete                      - Signal replay viewing completion
server-status                        - Show server statistics
exit, quit                           - Exit
help                                 - Show help
```

#### Example: Group Session Workflow

Here's an example of a typical group session workflow:

1. **Start the server:**
   ```bash
   cd csharp/src/InMemoryServer
   dotnet run
   ```

2. **Start multiple clients in separate terminals:**
   ```bash
   cd csharp/src/CliClient
   dotnet run
   ```

3. **Connect to the server and check available groups:**
   ```
   > connect https://localhost:5001
   Connected to server: https://localhost:5001

   > groups
   Available groups:
     3f7e8d2c-9a6b-4c5d-8e7f-1a2b3c4d5e6f
   ```

4. **Join an existing group or create a new one:**
   ```
   > join my-team
   Joined group: my-team
   ```

5. **Check your current group information:**
   ```
   > mygroup
   Current group: 7b8c9d0e-1f2a-3b4c-5d6e-7f8a9b0c1d2e
   ```

6. **Send a message to everyone in your group:**
   ```
   > broadcast Hello teammates! Ready for battle?
   Message broadcasted: Hello teammates! Ready for battle?
   ```

7. **You'll receive messages from other group members:**
   ```
   [GROUP] Message from a4b5c6d7-e8f9-0a1b-2c3d-4e5f6a7b8c9d: I'm ready!
   ```

8. **If your group reaches 5 members, a battle will automatically start:**
   ```
   [BATTLE] ========== Connections Ready! ==========
   [BATTLE] Battle ID: 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
   [BATTLE] Group is full! All clients connected.
   [BATTLE] Confirming connection ready status...
   [BATTLE] ========================================
   ```

9. **During the battle, the server pre-computes all turns and sends replay data to clients in chunks:**
   ```
   [BATTLE] Received replay chunk 1/3 with 50 turns
   [BATTLE] Received replay chunk 2/3 with 50 turns
   [BATTLE] Received replay chunk 3/3 with 45 turns
   [BATTLE] All replay chunks received! Starting replay with 145 turns
   ```

10. **The battle is replayed at 5fps, displaying turn-by-turn updates:**
    ```
    [BATTLE] Turn 1: Player1 moved to (10,16)
    [BATTLE] Turn 1: MediumEnemy3 attacked Player2 for 12 damage
    ...
    ```

11. **After the replay completes, notify the server and check the final status:**
    ```
    > battle-complete
    Battle replay viewing completed, notified server.

    > battle-status
    [BATTLE] ========== Battle Status ==========
    [BATTLE] Battle ID: 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
    [BATTLE] Result: Victory! All enemies defeated.
    [BATTLE] ======================================
    ```

12. **For automation, use the connect-battle command to test with multiple clients:**
    ```bash
    dotnet run -- connect-battle -u https://localhost:5001 -g test-battle -c 5
    ```
    This will create 5 client connections in the same group, trigger a battle, and display the replay automatically.

## Technical Details

### Memory Optimization

The server uses several techniques to optimize memory usage:

1. **Value Types**: Immutable structs (`readonly struct` and `readonly record struct`) for entities, positions, and other small data structures.

2. **Pre-allocation**: Collections are initialized with expected capacity to avoid resizing:
   ```csharp
   private readonly List<EntityInfo> _players = new(5); // Pre-allocate for max players
   private readonly List<EntityInfo> _enemies = new(15); // Pre-allocate for max enemies
   ```

3. **Chunked Data Transmission**: Battle replay data is split into manageable chunks (50 turns per chunk) to avoid sending large payloads:
   ```csharp
   public required List<BattleStatus> TurnData { get; set; } = new(50);
   ```

4. **Memory Cleanup**: Explicit memory management after data is no longer needed:
   ```csharp
   public void ClearBattleData()
   {
       _allTurnData.Clear();
       _players.Clear();
       _enemies.Clear();
       _battleLogs.Clear();
       // ...
   }
   ```

5. **Efficient Field Representation**: Using 2D arrays and reference types efficiently for the battle field grid.

## GameLift Server Integration

You need GameLift Server SDK to run the server, which can be installed via local NuGet Package build via yourself.

1. Download Server SDK: https://github.com/amazon-gamelift/amazon-gamelift-servers-go-server-sdk
2. Build NuPkg by `dotnet pack -c Release -o .artifacts -p:Version=5.3.0 -o .artifacts GameLiftServerSDK.sln`
3. Place the artifact in the `csharp/LocalPackages` directory, and ensure the `NuGet.config` file points to this directory.
4. Now you can restore sln.

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.
