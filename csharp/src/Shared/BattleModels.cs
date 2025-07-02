namespace Shared;

/// <summary>
/// Complete entity type information combining type and enemy size
/// </summary>
public readonly record struct EntityTypeInfo(EntityType Type, EnemySize? EnemySize = null)
{
    /// <summary>
    /// Gets whether this entity is a player
    /// </summary>
    public bool IsPlayer => Type == EntityType.Player;

    /// <summary>
    /// Gets whether this entity is an enemy
    /// </summary>
    public bool IsEnemy => Type == EntityType.Enemy;

    /// <summary>
    /// Creates a player entity type
    /// </summary>
    public static EntityTypeInfo Player => new(EntityType.Player);

    /// <summary>
    /// Creates a small enemy entity type
    /// </summary>
    public static EntityTypeInfo SmallEnemy => new(EntityType.Enemy, Shared.EnemySize.Small);

    /// <summary>
    /// Creates a medium enemy entity type
    /// </summary>
    public static EntityTypeInfo MediumEnemy => new(EntityType.Enemy, Shared.EnemySize.Medium);

    /// <summary>
    /// Creates a large enemy entity type
    /// </summary>
    public static EntityTypeInfo LargeEnemy => new(EntityType.Enemy, Shared.EnemySize.Large);

    /// <summary>
    /// Returns a string representation of the entity type
    /// </summary>
    public override string ToString() => Type switch
    {
        EntityType.Player => nameof(EntityType.Player),
        EntityType.Enemy when EnemySize.HasValue => $"{EnemySize}{nameof(EntityType.Enemy)}",
        EntityType.Enemy => nameof(EntityType.Enemy),
        _ => "Unknown"
    };
}

/// <summary>
/// Entity information for client-server communication
/// </summary>
public readonly record struct EntityInfo
{
    /// <summary>
    /// Entity unique identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Entity name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Entity type information (player or enemy with size)
    /// </summary>
    public required EntityTypeInfo Type { get; init; }

    /// <summary>
    /// Player job type (only set for players)
    /// </summary>
    public PlayerJob? PlayerJob { get; init; }

    /// <summary>
    /// Enemy job type (only set for enemies)
    /// </summary>
    public EnemyJob? EnemyJob { get; init; }

    /// <summary>
    /// Current HP
    /// </summary>
    public int CurrentHp { get; init; }

    /// <summary>
    /// Maximum HP
    /// </summary>
    public int MaxHp { get; init; }

    /// <summary>
    /// Attack power
    /// </summary>
    public int Attack { get; init; }

    /// <summary>
    /// Defense power
    /// </summary>
    public int Defense { get; init; }

    /// <summary>
    /// Movement speed
    /// </summary>
    public int Speed { get; init; }

    /// <summary>
    /// Accuracy (hit rate, 0-100)
    /// </summary>
    public int Accuracy { get; init; }

    /// <summary>
    /// Evasion (dodge rate, 0-100)
    /// </summary>
    public int Evasion { get; init; }

    /// <summary>
    /// Position on the battle field
    /// </summary>
    public Vector2 Position { get; init; }

    /// <summary>
    /// Is defending (damage reduction)
    /// </summary>
    public bool IsDefending { get; init; }
}

/// <summary>
/// Battle status for client-server communication
/// </summary>
public class BattleStatus
{
    /// <summary>
    /// Battle unique identifier
    /// </summary>
    public string? BattleId { get; set; }

    /// <summary>
    /// Is battle in progress
    /// </summary>
    public bool IsInProgress { get; set; }

    /// <summary>
    /// Current turn number
    /// </summary>
    public int CurrentTurn { get; set; }

    /// <summary>
    /// Total turns in battle
    /// </summary>
    public int TotalTurns { get; set; }

    /// <summary>
    /// Players in battle
    /// </summary>
    public List<EntityInfo> Players { get; set; } = [];

    /// <summary>
    /// Enemies in battle
    /// </summary>
    public List<EntityInfo> Enemies { get; set; } = [];

    /// <summary>
    /// Field dimensions (for client-side rendering)
    /// </summary>
    public int FieldWidth { get; set; } = 20; // Default battle field width

    /// <summary>
    /// Field dimensions (for client-side rendering)
    /// </summary>
    public int FieldHeight { get; set; } = 20; // Default battle field height

    /// <summary>
    /// Recent battle logs
    /// </summary>
    public List<string> RecentLogs { get; set; } = new(10); // Pre-allocate for recent logs

    /// <summary>
    /// Clears all references to reduce memory pressure
    /// </summary>
    public void Clear()
    {
        Players.Clear();
        Enemies.Clear();
        RecentLogs.Clear();
    }
}

/// <summary>
/// Battle replay data for chunked transmission
/// </summary>
public readonly struct BattleReplayData
{
    /// <summary>
    /// Battle ID
    /// </summary>
    public required string BattleId { get; init; }

    /// <summary>
    /// Turn data for this chunk
    /// </summary>
    public required List<BattleStatus> TurnData { get; init; }

    /// <summary>
    /// Current chunk index (0-based)
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Total number of chunks
    /// </summary>
    public int TotalChunks { get; init; }

    /// <summary>
    /// Whether this is the last chunk
    /// </summary>
    public bool IsLastChunk { get; init; }
}

/// <summary>
/// Battle summary information
/// </summary>
public readonly struct BattleSummary
{
    /// <summary>
    /// Battle ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Associated group ID
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Current turn
    /// </summary>
    public int CurrentTurn { get; init; }

    /// <summary>
    /// Number of players
    /// </summary>
    public int PlayerCount { get; init; }

    /// <summary>
    /// Number of enemies
    /// </summary>
    public int EnemyCount { get; init; }

    /// <summary>
    /// Battle started time
    /// </summary>
    public DateTime StartedAt { get; init; }
}



/// <summary>
/// Battle log item
/// </summary>
public readonly struct BattleLogItem
{
    /// <summary>
    /// Log message
    /// </summary>
    public readonly string Message { get; init; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public readonly DateTime Timestamp { get; init; }

    /// <summary>
    /// Creates a new battle log item
    /// </summary>
    public BattleLogItem(string message)
    {
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}
