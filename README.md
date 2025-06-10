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
exit, quit             - Exit
help                   - Show help
```

## Battle System

### Battle Start Conditions
- Automatically starts when 5 clients connect to a group

### Battle Features
- **Field**: 20x20 grid
- **Entities**: Players (HP 200 fixed) and enemies (Small: HP 100, Medium: HP 200, Large: HP 300)
- **Stats**: Attack power (10-30), Defense (5-15), Movement speed (1-3) randomly generated
- **Actions**: Move, Attack, Defend (3 types)
- **Turn-based**: Actions ordered by movement speed
- **Duration**: Completes in 100-300 turns

### Battle Replay
- Saved in JSON LINE format in `./battle_replay/` directory
- Records the state of each turn

## Configuration

### Server Configuration
- **Port**: 5000 (default)
- **SignalR Endpoint**: `/inmemoryhub`
- **Health Check**: `/health`

### Environment Variables
- `ASPNETCORE_URLS`: Server URL configuration
- `Logging__LogLevel__Default`: Log level configuration

## Development Information

### Coding Standards
- TreatWarningsAsErrors enabled
- Nullable Reference Types enabled
- XML documentation comments for all public APIs

### Test Coverage
- InMemoryState: Basic operation tests
- GroupManager: Group management tests
- BattleState: Battle logic tests

## License

This project is published under the MIT License.

## Contributing

1. Fork the project
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Future Expansion Plans

- JWT authentication implementation
- gRPC (MagicOnion) support
- Go language implementation
- Web-based client
- More complex battle system
