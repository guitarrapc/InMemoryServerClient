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
    private readonly SettingsService _settings;
    private BattleStatus _status = BattleStatus.Waiting;

    /// <summary>
    /// Session ID (client-generated UUID for this battle session)
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
    /// <summary>
    /// Battle ID provided by the server
    /// </summary>
    public string BattleId { get; private set; } = string.Empty;
    /// <summary>
    /// Gets the battle seed for reproducibility
    /// </summary>
    public int Seed { get; private set; }

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

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
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

    public BattleSessionModel(IConnectionFactory connectionFactory, ILogger logger, SettingsService settings)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _settings = settings;
    }

    /// <summary>
    /// Create historical battle session for replay viewing
    /// </summary>
    /// <param name="battleHistory">Battle history from IndexedDB</param>
    /// <returns>Historical battle session</returns>
    public static BattleSessionModel CreateHistorical(BattleHistory battleHistory)
    {
        var session = new BattleSessionModel(null!, null!, null!)
        {
            SessionId = battleHistory.SessionId, // Note: battleHistory.BattleId is actually the session ID
            BattleId = battleHistory.BattleId,
            GroupName = battleHistory.GroupName,
            ServerUrl = battleHistory.ServerUrl,
            IsHistoricalBattle = true,
            Status = BattleStatus.Completed,
            BattleHistory = battleHistory
        };

        // Set seed if available in battle history
        if (battleHistory.Seed != 0)
        {
            session.Seed = battleHistory.Seed;
        }

        // Create historical clients based on stored client history
        foreach (var clientHistory in battleHistory.ParticipatingClients)
        {
            var client = BattleClient.CreateHistoricalClient(
                clientHistory.PlayerId,
                clientHistory.ConnectionId,
                battleHistory.GroupName,
                battleHistory.ReplayData,
                clientHistory.ConnectionType,
                clientHistory.ConnectedAt);

            session._clients.Add(client);
        }

        return session;
    }

    /// <summary>
    /// Add a client to this battle
    /// </summary>
    /// <param name="connectionType">Connection type for this client</param>
    /// <returns>Created battle client</returns>
    public async Task<BattleClient> AddClientAsync(Shared.Models.ConnectionType connectionType)
    {
        if (IsHistoricalBattle)
            throw new InvalidOperationException("Cannot add clients to historical battles");

        if (_clients.Count >= 5)
            throw new InvalidOperationException("Battle is full (max 5 clients)");

        try
        {
            // Create connection info with the specified type and appropriate URL
            var fullServerUrl = connectionType switch
            {
                ConnectionType.SignalR => $"{ServerUrl}:{_settings.SignalRPort}",
                ConnectionType.MagicOnion => $"{ServerUrl}:{_settings.MagicOnionPort}",
                _ => throw new ArgumentException($"Unsupported connection type: {connectionType}")
            };

            var connectionInfo = new ConnectionInfo
            {
                Type = connectionType,
                GroupName = GroupName,
                ServerUrl = fullServerUrl
            };

            // Create connection using factory
            var connection = connectionType switch
            {
                ConnectionType.SignalR => await _connectionFactory.CreateSignalRConnectionAsync(connectionInfo),
                ConnectionType.MagicOnion => await _connectionFactory.CreateMagicOnionConnectionAsync(connectionInfo),
                _ => throw new ArgumentException($"Unsupported connection type: {connectionType}")
            };

            var client = new BattleClient(connection, _logger);

            // Set player ID if not already set
            client.PlayerId = Shared.Common.PlayerNameGenerator.GenerateShortName();

            // Subscribe to connection events to capture server battle data
            client.OnConnectionsReady += (data) =>
            {
                BattleId = data.BattleId.ToString();
                Seed = data.Seed;
                _logger.LogInformation("Session {SessionId}: Received server BattleId: {ServerBattleId}, Seed: {Seed}",
                    SessionId, BattleId, Seed);
            };

            client.OnBattleStarted += (data) =>
            {
                // Backup assignment if not set in ConnectionsReady
                if (string.IsNullOrEmpty(BattleId))
                {
                    BattleId = data.BattleId.ToString();
                }
                if (Seed == 0)
                {
                    Seed = data.Seed;
                }
                _logger.LogInformation("Session {SessionId}: Battle started with BattleId: {ServerBattleId}, Seed: {Seed}",
                    SessionId, BattleId, Seed);
            };

            // Subscribe to battle completion events
            client.OnBattleComplete += () =>
            {
                Status = BattleStatus.Completed;
                _logger.LogInformation("Battle session {SessionId} status updated to Completed", SessionId);

                // Extract actual battle result from the final turn data
                var result = CalculateBattleResult(client);

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
                    _logger.LogDebug("Added replay chunk {ChunkIndex} to battle session {SessionId}", replayData.ChunkIndex, SessionId);
                }
            };

            _clients.Add(client);

            _logger.LogInformation("Added client {ConnectionId} ({Type}) to battle session {SessionId}",
                connection.ConnectionId, connection.Type, SessionId);

            // Check if battle should start (5 clients connected)
            if (_clients.Count == 5 && Status == BattleStatus.Waiting)
            {
                Status = BattleStatus.InProgress;
                _logger.LogInformation("Battle session {SessionId} is starting with 5 clients", SessionId);
            }

            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add client to battle session {SessionId}", SessionId);
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
            _logger.LogInformation("Removing client {ConnectionId} from battle session {SessionId}",
                client.ConnectionId, SessionId);
            await client.DisposeAsync();
        }
    }

    /// <summary>
    /// Calculate battle result from the final turn data
    /// </summary>
    private BattleResult CalculateBattleResult(BattleClient client)
    {
        try
        {
            var finalTurnData = client.CurrentField;
            if (finalTurnData != null)
            {
                var alivePlayers = finalTurnData.Entities.Count(e => e.Type == EntityType.Player && e.Health > 0);
                var aliveEnemies = finalTurnData.Entities.Count(e => e.Type != EntityType.Player && e.Health > 0);
                var totalPlayers = finalTurnData.Entities.Count(e => e.Type == EntityType.Player);
                var totalEnemies = finalTurnData.Entities.Count(e => e.Type != EntityType.Player);

                bool isVictory;
                string victoryCondition;

                if (aliveEnemies == 0)
                {
                    isVictory = true;
                    victoryCondition = "All enemies defeated";
                }
                else if (alivePlayers == 0)
                {
                    isVictory = false;
                    victoryCondition = "All players defeated";
                }
                else
                {
                    // Turn limit case - determine by survivor count
                    isVictory = alivePlayers > aliveEnemies;
                    victoryCondition = $"Turn limit reached - {(isVictory ? "Players" : "Enemies")} have more survivors";
                }

                return new BattleResult
                {
                    IsVictory = isVictory,
                    PlayersSurvived = alivePlayers,
                    EnemiesKilled = totalEnemies - aliveEnemies,
                    TotalTurns = finalTurnData.Turn,
                    VictoryCondition = victoryCondition
                };
            }
            else
            {
                _logger.LogWarning("No final turn data available for battle result calculation");
                return new BattleResult
                {
                    IsVictory = false,
                    PlayersSurvived = 0,
                    EnemiesKilled = 0,
                    TotalTurns = 0,
                    VictoryCondition = "Unknown - no final turn data"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating battle result");
            return new BattleResult
            {
                IsVictory = false,
                PlayersSurvived = 0,
                EnemiesKilled = 0,
                TotalTurns = 0,
                VictoryCondition = "Error calculating result"
            };
        }
    }

    /// <summary>
    /// Dispose all clients and clean up resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing battle session {SessionId} with {ClientCount} clients", SessionId, _clients.Count);

        var disposeTasks = _clients.Select(c => c.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);
        _clients.Clear();

        if (!IsHistoricalBattle)
        {
            Status = BattleStatus.Completed;
        }
    }

    private void HandleConnectionsReady(Shared.Models.ConnectionsReadyData data)
    {
        Seed = data.Seed; // Save seed for history
        _logger?.LogInformation("Battle {BattleId} connections ready - Seed: {Seed}", data.BattleId, data.Seed);
    }

    private void HandleBattleStarted(Shared.Models.BattleStartedData data)
    {
        // Update seed if it wasn't set in ConnectionsReady (backup)
        if (Seed == 0)
        {
            Seed = data.Seed;
        }
        _logger?.LogInformation("Battle {BattleId} started with seed {Seed}", data.BattleId, data.Seed);
    }

    private void HandleBattleComplete(string message)
    {
        _logger?.LogInformation("Battle completed: {Message}", message);
        // This is handled at the session level, not individual client level
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

    // Unified connection information (used for both real and historical connections)
    private readonly string _connectionId;
    private readonly Shared.Models.ConnectionType _connectionType;
    private readonly DateTime _connectedAt;

    public string ConnectionId => _connectionId;
    public Shared.Models.ConnectionType Type => _connectionType;
    public string? PlayerId { get; set; }
    public DateTime ConnectedAt => _connectedAt;
    public BattleFieldData? CurrentField { get; set; }

    /// <summary>
    /// Get all historical turn data (for historical clients only)
    /// </summary>
    public IReadOnlyList<BattleFieldData> HistoricalTurnData => _historicalTurnData.AsReadOnly();

    public event Action<BattleFieldData>? OnBattleFieldUpdated;
    public event Action<BattleReplayData>? OnReplayDataReceived;
    public event Action? OnBattleComplete;

    // Events for server data
    public event Action<Shared.Models.ConnectionsReadyData>? OnConnectionsReady;
    public event Action<Shared.Models.BattleStartedData>? OnBattleStarted;

    public BattleClient(IBattleConnection? connection, ILogger? logger)
    {
        _connection = connection;
        _logger = logger;

        // Initialize connection information for real connections
        _connectionId = _connection?.ConnectionId ?? "unknown";
        _connectionType = _connection?.Type ?? Shared.Models.ConnectionType.SignalR;
        _connectedAt = DateTime.UtcNow;

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
    /// Private constructor for historical clients
    /// </summary>
    private BattleClient(
        string connectionId,
        Shared.Models.ConnectionType connectionType,
        DateTime connectedAt,
        ILogger? logger = null)
    {
        _connection = null;
        _logger = logger;
        _connectionId = connectionId;
        _connectionType = connectionType;
        _connectedAt = connectedAt;
    }

    /// <summary>
    /// Create historical client for replay viewing (no real connection)
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <param name="connectionId">Connection ID</param>
    /// <param name="groupName">Group name</param>
    /// <param name="replayData">Replay data</param>
    /// <param name="connectionType">Historical connection type for display</param>
    /// <param name="connectedAt">Original connection time</param>
    /// <returns>Historical client</returns>
    public static BattleClient CreateHistoricalClient(
        string playerId,
        string connectionId,
        string groupName,
        List<BattleReplayData> replayData,
        Shared.Models.ConnectionType connectionType = Shared.Models.ConnectionType.SignalR,
        DateTime connectedAt = default)
    {
        var client = new BattleClient(
            connectionId,
            connectionType,
            connectedAt == default ? DateTime.UtcNow : connectedAt)
        {
            PlayerId = playerId
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
        // Simple log - parent session manages seed
        _logger?.LogInformation("Battle client connections ready - BattleId: {BattleId}, Seed: {Seed}", data.BattleId, data.Seed);

        // Fire event for parent session to capture
        OnConnectionsReady?.Invoke(data);
    }

    private void HandleBattleStarted(Shared.Models.BattleStartedData data)
    {
        // Simple log - parent session manages seed
        _logger?.LogInformation("Battle client started - BattleId: {BattleId}, Seed: {Seed}", data.BattleId, data.Seed);

        // Fire event for parent session to capture
        OnBattleStarted?.Invoke(data);
    }

    private void HandleBattleComplete(string message)
    {
        _logger?.LogInformation("Battle client completed: {Message}", message);
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
