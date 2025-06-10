# InMemoryServerClient

*[日本語版はこちら / Japanese version](README_jp.md)*

A C# implementation of an in-memory stateful server and CLI client project. The server maintains state in memory and provides an interface for clients to interact with this state.

## Features

### Server Features
- **Basic Key-Value Store Operations**
  - GET/SET/DELETE/LIST operations
  - Key change monitoring functionality
- **Group Management**
  - Group creation and management identified by UUIDv4
  - Maximum connection limit per group (max 5 sessions)
  - Automatic group expiration management (10 minutes)
- **Battle System**
  - Automatic battle start when group is full (5 sessions)
  - Turn-based RPG-style battles on 20x20 pseudo field
  - Fully automated battle system
  - Battle replay saved in JSON LINE format

### Client Features
- **Interactive Mode**: Interactive command line
- **Batch Mode**: Single command execution
- **Group Operations**: Join groups, send messages
- **Battle Monitoring**: Real-time battle status display

## Architecture

### Technology Stack
- **.NET 9**: Latest .NET Runtime
- **SignalR**: Real-time communication
- **xUnit + NSubstitute**: Testing framework
- **ConsoleAppFramework**: CLI framework

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
docker run -p 5000:5000 inmemory-server
```

### Client Usage

#### Interactive Mode
```bash
cd csharp/src/CliClient
dotnet run
```

#### Single Command Examples
```bash
# Connect to server
dotnet run -- connect -u http://localhost:5000

# Connect multiple sessions for battle testing
dotnet run -- connect-battle -u http://localhost:5000 -g battle-group -c 5

# Key-value operations
dotnet run -- set mykey "Hello World"
dotnet run -- get mykey
dotnet run -- delete mykey
dotnet run -- list "*"

# Group operations
dotnet run -- join mygroup
dotnet run -- broadcast "Hello everyone!"
dotnet run -- groups
dotnet run -- my-group

# Battle features
dotnet run -- battle-status
dotnet run -- battle-replay <battle_id>
```

#### Interactive Mode Commands
```
connect [url] [group]  - Connect to server
disconnect             - Disconnect from server
status                 - Show connection status
get <key>              - Get key
set <key> <value>      - Set key
delete <key>           - Delete key
list [pattern]         - List keys (pattern optional)
watch <key>            - Watch key changes
join <group_name>      - Join group
broadcast <message>    - Send message to group
groups                 - List groups
mygroup                - Current group info
battle-status          - Check battle status
battle-replay <id>     - Show replay data for a battle
exit, quit             - Exit
help                   - Show help
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
   > connect http://localhost:5000
   Connected to server: http://localhost:5000

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

8. **If your group reaches 5 members, a battle will automatically start**

9. **Check current battle status during battle:**
   ```
   > battle-status
   [BATTLE] ========== Battle Status ==========
   [BATTLE] Battle ID: 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
   [BATTLE] Turn: 45/231
   [BATTLE] Players alive: 5/5
   ...
   ```

10. **After battle completes, view the replay:**
    ```
    > battle-replay 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
    Battle replay for battle 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212:
    ...
    ```
