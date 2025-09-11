using WasmClient.Services;

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
    public string BattleId => Id; // Alias for backwards compatibility
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

    // 履歴バトル用プロパティ
    public bool IsHistoricalBattle { get; init; } = false;
    public BattleHistory? BattleHistory { get; init; }

    // バトル進行データ
    public List<BattleReplayData> ReplayData { get; } = new();
    public int TotalTurns { get; }
    private Lock _replayDataLock = new ();

    /// <summary>
    /// Raised when battle status changes
    /// </summary>
    public event Action<BattleStatus>? OnStatusChanged;

    /// <summary>
    /// Raised when battle is completed
    /// </summary>
    public event Action<BattleSessionModel, BattleResult>? OnBattleCompleted;

    public int ClientCount => _clients.Count;
    public bool IsFull => _clients.Count >= 5;

    public BattleSessionModel(IConnectionFactory connectionFactory, ILogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    /// Create historical battle session for replay viewing
    /// </summary>
    /// <param name="battleId">Battle ID</param>
    /// <param name="groupName">Group name</param>
    /// <param name="serverUrl">Server URL</param>
    /// <param name="clients">Historical clients</param>
    /// <param name="replayData">Replay data</param>
    /// <returns>Historical battle session</returns>
    public static BattleSessionModel CreateHistorical(
        string battleId,
        string groupName,
        string serverUrl,
        List<BattleClient> clients,
        List<BattleReplayData> replayData)
    {
        var session = new BattleSessionModel(null!, null!)
        {
            Id = battleId,
            GroupName = groupName,
            ServerUrl = serverUrl,
            IsHistoricalBattle = true,
            Status = BattleStatus.Completed
        };

        // Add clients
        foreach (var client in clients)
        {
            session._clients.Add(client);
        }

        // Add replay data
        session.ReplayData.AddRange(replayData);

        return session;
    }

    /// <summary>
    /// Add a client to this battle
    /// </summary>
    /// <param name="connectionInfo">Connection information</param>
    /// <returns>Created battle client</returns>
    public async Task<BattleClient> AddClientAsync(ConnectionInfo connectionInfo)
    {
        if (IsHistoricalBattle)
            throw new InvalidOperationException("Cannot add clients to historical battles");

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

                // Create battle result and trigger completion event
                var result = new BattleResult
                {
                    IsVictory = true, // TODO: Extract from actual battle data
                    PlayersSurvived = 0,   // TODO: Extract from actual battle data
                    EnemiesKilled = 0,   // TODO: Extract from actual battle data
                    VictoryCondition = "All enemies defeated" // TODO: Extract from actual battle data
                };

                OnBattleCompleted?.Invoke(this, result);
            };

            // Subscribe to replay data collection
            client.OnReplayDataReceived += (replayData) =>
            {
                lock (_replayDataLock)
                {
                    var count = ReplayData.Count;
                    if (count == replayData.TotalChunks)
                        return;

                    if (replayData.ChunkIndex + 1 == count)
                        return;

                    ReplayData.Add(replayData);
                    _logger.LogDebug("Added replay chunk {ChunkIndex} to battle {BattleId}", replayData.ChunkIndex, Id);
                }
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
        if (IsHistoricalBattle)
            return; // Cannot remove clients from historical battles

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

        if (!IsHistoricalBattle)
        {
            Status = BattleStatus.Completed;
        }
    }
}

/// <summary>
/// Battle client wrapper with real connection
/// </summary>
public class BattleClient : IAsyncDisposable
{
    private readonly IBattleConnection? _connection;
    private readonly ILogger? _logger;
    private List<BattleFieldData> _historicalTurnData = new();

    public string ConnectionId => _connection?.ConnectionId ?? "historical";
    public Shared.Models.ConnectionType Type => _connection?.Type ?? HistoricalConnectionType;
    public string? PlayerId { get; set; }
    public DateTime ConnectedAt { get; } = DateTime.Now;
    public BattleFieldData? CurrentField { get; set; }

    /// <summary>
    /// Connection type for historical clients (when no real connection exists)
    /// </summary>
    public Shared.Models.ConnectionType HistoricalConnectionType { get; set; } = Shared.Models.ConnectionType.SignalR;

    /// <summary>
    /// Get all historical turn data (for historical clients only)
    /// </summary>
    public IReadOnlyList<BattleFieldData> HistoricalTurnData => _historicalTurnData.AsReadOnly();

    public event Action<BattleFieldData>? OnBattleFieldUpdated;
    public event Action<BattleReplayData>? OnReplayDataReceived;
    public event Action? OnBattleComplete;

    public BattleClient(IBattleConnection? connection, ILogger? logger)
    {
        _connection = connection;
        _logger = logger;

        // Subscribe to battle replay data to update field visualization
        if (_connection != null)
        {
            _connection.OnBattleReplayReceived += HandleReplayReceived;
            _connection.OnConnectionsReady += HandleConnectionsReady;
            _connection.OnBattleStarted += HandleBattleStarted;
            _connection.OnBattleComplete += HandleBattleComplete;
        }
    }

    /// <summary>
    /// Create historical client for replay viewing (no real connection)
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <param name="groupName">Group name</param>
    /// <param name="replayData">Replay data</param>
    /// <param name="connectionType">Historical connection type for display</param>
    /// <returns>Historical client</returns>
    public static BattleClient CreateHistoricalClient(
        string playerId,
        string groupName,
        List<BattleReplayData> replayData,
        Shared.Models.ConnectionType connectionType = Shared.Models.ConnectionType.SignalR)
    {
        var client = new BattleClient(null!, null!)
        {
            PlayerId = playerId,
            HistoricalConnectionType = connectionType
        };

        // Convert all replay data to BattleFieldData for client
        var allFieldData = new List<BattleFieldData>();

        foreach (var chunk in replayData)
        {
            foreach (var turnData in chunk.TurnData)
            {
                var fieldData = client.ConvertToFieldData(turnData);
                allFieldData.Add(fieldData);

                // Update current field to latest turn
                client.CurrentField = fieldData;
            }
        }

        // Set all turn data for the client
        client.SetHistoricalTurnData(allFieldData);

        // Set initial field data to the first turn for display
        if (allFieldData.Any())
        {
            client.CurrentField = allFieldData[0];
        }

        return client;
    }

    /// <summary>
    /// Set historical turn data for replay viewing
    /// </summary>
    /// <param name="turnData">All turn data for this battle</param>
    public void SetHistoricalTurnData(List<BattleFieldData> turnData)
    {
        _historicalTurnData = new List<BattleFieldData>(turnData);
    }

    private void HandleReplayReceived(BattleReplayData replayData)
    {
        _logger?.LogInformation("Received replay chunk {ChunkIndex}/{TotalChunks} with {TurnCount} turns",
            replayData.ChunkIndex, replayData.TotalChunks, replayData.TurnData.Count);

        // Fire replay data event for collection
        OnReplayDataReceived?.Invoke(replayData);

        // Process each turn data individually for complete replay history
        foreach (var turnData in replayData.TurnData)
        {
            var fieldData = ConvertToFieldData(turnData);

            _logger?.LogDebug("Processing turn {Turn} with {EntityCount} entities",
                fieldData.Turn, fieldData.Entities.Count);

            // Always update current field for the latest turn
            CurrentField = fieldData;

            // Fire the event for each turn to build up the complete history
            OnBattleFieldUpdated?.Invoke(fieldData);
        }

        _logger?.LogInformation("Processed {TurnCount} turns from replay chunk {ChunkIndex}. Total chunks: {ChunkIndex}/{TotalChunks}",
            replayData.TurnData.Count, replayData.ChunkIndex, replayData.ChunkIndex + 1, replayData.TotalChunks);
    }

    private void HandleConnectionsReady(Shared.Models.ConnectionsReadyData data)
    {
        _logger?.LogInformation("Battle {BattleId} connections ready - Seed: {Seed}", data.BattleId, data.Seed);
    }

    private void HandleBattleStarted(Shared.Models.BattleStartedData data)
    {
        _logger?.LogInformation("Battle {BattleId} started with seed {Seed}", data.BattleId, data.Seed);
    }

    private void HandleBattleComplete(string message)
    {
        _logger?.LogInformation("Battle completed: {Message}", message);
        OnBattleComplete?.Invoke();
    }

    public BattleFieldData ConvertToFieldData(Shared.Battle.BattleStatus battleStatus)
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
        }));

        return new BattleFieldData
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
        _logger?.LogInformation("Disposing battle client {ConnectionId}", ConnectionId);

        if (_connection != null)
        {
            // Unsubscribe from events
            _connection.OnBattleReplayReceived -= HandleReplayReceived;
            _connection.OnConnectionsReady -= HandleConnectionsReady;
            _connection.OnBattleStarted -= HandleBattleStarted;
            _connection.OnBattleComplete -= HandleBattleComplete;

            await _connection.DisposeAsync();
        }
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
