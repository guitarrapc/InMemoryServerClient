using WasmClient.Services;
using Shared.Battle;

namespace WasmClient.Models;

/// <summary>
/// Battle session model with real connection management
/// </summary>
public class BattleSessionModel : IAsyncDisposable
{
    private readonly List<BattleClient> _clients = new();
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger _logger;
    private BattleStatus _status = BattleStatus.Waiting;

    public string Id { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string ServerUrl { get; init; } = string.Empty;

    public BattleStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnStatusChanged?.Invoke(_status);
            }
        }
    }

    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public IReadOnlyList<BattleClient> Clients => _clients.AsReadOnly();

    /// <summary>
    /// Raised when battle status changes
    /// </summary>
    public event Action<BattleStatus>? OnStatusChanged;

    public int ClientCount => _clients.Count;
    public bool IsFull => _clients.Count >= 5;

    public BattleSessionModel(IConnectionFactory connectionFactory, ILogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    /// Add a client to this battle
    /// </summary>
    /// <param name="connectionInfo">Connection information</param>
    /// <returns>Created battle client</returns>
    public async Task<BattleClient> AddClientAsync(ConnectionInfo connectionInfo)
    {
        if (_clients.Count >= 5)
            throw new InvalidOperationException("Battle is full (max 5 clients)");

        try
        {
            // Create connection using factory
            var connection = connectionInfo.Type switch
            {
                ConnectionType.SignalR => await _connectionFactory.CreateSignalRConnectionAsync(connectionInfo),
                ConnectionType.MagicOnion => await _connectionFactory.CreateMagicOnionConnectionAsync(connectionInfo),
                _ => throw new ArgumentException($"Unsupported connection type: {connectionInfo.Type}")
            };

            var client = new BattleClient(connection, _logger);

            // Subscribe to battle completion events
            client.OnBattleComplete += () =>
            {
                Status = BattleStatus.Completed;
                _logger.LogInformation("Battle {BattleId} status updated to Completed", Id);
            };

            _clients.Add(client);

            _logger.LogInformation("Added client {ConnectionId} ({Type}) to battle {BattleId}",
                connection.ConnectionId, connection.Type, Id);

            // Check if battle should start (5 clients connected)
            if (_clients.Count == 5 && Status == BattleStatus.Waiting)
            {
                Status = BattleStatus.InProgress;
                _logger.LogInformation("Battle {BattleId} is starting with 5 clients", Id);
            }

            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add client to battle {BattleId}", Id);
            throw;
        }
    }

    /// <summary>
    /// Remove a client from this battle
    /// </summary>
    /// <param name="client">Client to remove</param>
    public async Task RemoveClientAsync(BattleClient client)
    {
        if (_clients.Remove(client))
        {
            _logger.LogInformation("Removing client {ConnectionId} from battle {BattleId}",
                client.ConnectionId, Id);
            await client.DisposeAsync();
        }
    }

    /// <summary>
    /// Dispose all clients and clean up resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing battle {BattleId} with {ClientCount} clients", Id, _clients.Count);

        var disposeTasks = _clients.Select(c => c.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);
        _clients.Clear();

        Status = BattleStatus.Completed;
    }
}

/// <summary>
/// Battle client wrapper with real connection
/// </summary>
public class BattleClient : IAsyncDisposable
{
    private readonly IBattleConnection _connection;
    private readonly ILogger _logger;

    public string ConnectionId => _connection.ConnectionId;
    public ConnectionType Type => _connection.Type;
    public string? PlayerId { get; set; }
    public DateTime ConnectedAt { get; } = DateTime.Now;
    public BattleFieldData? CurrentField { get; private set; }

    public event Action<BattleFieldData>? OnBattleFieldUpdated;
    public event Action? OnBattleComplete;

    public BattleClient(IBattleConnection connection, ILogger logger)
    {
        _connection = connection;
        _logger = logger;

        // Subscribe to battle replay data to update field visualization
        _connection.OnBattleReplayReceived += HandleReplayReceived;
        _connection.OnConnectionsReady += HandleConnectionsReady;
        _connection.OnBattleStarted += HandleBattleStarted;
        _connection.OnBattleComplete += HandleBattleComplete;
    }

    private void HandleReplayReceived(BattleReplayData replayData)
    {
        _logger.LogInformation("Received replay chunk {ChunkIndex}/{TotalChunks} with {TurnCount} turns",
            replayData.ChunkIndex, replayData.TotalChunks, replayData.TurnData.Count);

        // Convert the latest turn data to field data for visualization
        if (replayData.TurnData.Any())
        {
            var latestTurn = replayData.TurnData.Last();
            CurrentField = ConvertToFieldData(latestTurn);
            OnBattleFieldUpdated?.Invoke(CurrentField);
        }
    }

    private void HandleConnectionsReady(Shared.Models.ConnectionsReadyData data)
    {
        _logger.LogInformation("Battle {BattleId} connections ready - Seed: {Seed}", data.BattleId, data.Seed);
    }

    private void HandleBattleStarted(Shared.Models.BattleStartedData data)
    {
        _logger.LogInformation("Battle {BattleId} started with seed {Seed}", data.BattleId, data.Seed);
    }

    private void HandleBattleComplete(string message)
    {
        _logger.LogInformation("Battle completed: {Message}", message);
        OnBattleComplete?.Invoke();
    }

    private BattleFieldData ConvertToFieldData(Shared.Battle.BattleStatus battleStatus)
    {
        var allEntities = new List<EntityData>();

        // Add players
        allEntities.AddRange(battleStatus.Players.Select(p => new EntityData
        {
            Id = p.EntityId.ToString(),
            Type = EntityType.Player,
            Position = new Position(p.Position.X, p.Position.Y),
            Health = p.CurrentHp,
            MaxHealth = p.MaxHp
        }));

        // Add enemies
        allEntities.AddRange(battleStatus.Enemies.Select(e => new EntityData
        {
            Id = e.EntityId.ToString(),
            Type = GetEnemyType(e),
            Position = new Position(e.Position.X, e.Position.Y),
            Health = e.CurrentHp,
            MaxHealth = e.MaxHp
        }));        return new BattleFieldData
        {
            Turn = battleStatus.CurrentTurn,
            Entities = allEntities
        };
    }

    private EntityType GetEnemyType(Shared.Battle.EntityInfo entity)
    {
        if (!entity.Type.IsEnemy || !entity.Type.EnemySize.HasValue)
            return EntityType.Small; // Default fallback

        return entity.Type.EnemySize.Value switch
        {
            Shared.Battle.EnemySize.Small => EntityType.Small,
            Shared.Battle.EnemySize.Medium => EntityType.Medium,
            Shared.Battle.EnemySize.Large => EntityType.Large,
            _ => EntityType.Small
        };
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing battle client {ConnectionId}", ConnectionId);

        // Unsubscribe from events
        _connection.OnBattleReplayReceived -= HandleReplayReceived;
        _connection.OnConnectionsReady -= HandleConnectionsReady;
        _connection.OnBattleStarted -= HandleBattleStarted;
        _connection.OnBattleComplete -= HandleBattleComplete;

        await _connection.DisposeAsync();
    }
}

public enum BattleStatus
{
    Waiting,
    InProgress,
    Completed
}

public record BattleFieldData
{
    public int Turn { get; init; }
    public List<EntityData> Entities { get; init; } = new();
}

public record EntityData
{
    public string Id { get; init; } = string.Empty;
    public EntityType Type { get; init; }
    public Position Position { get; init; } = new(0, 0);
    public int Health { get; init; }
    public int MaxHealth { get; init; }
}

public enum EntityType
{
    Player,
    Small,
    Medium,
    Large
}

public record Position(int X, int Y);
