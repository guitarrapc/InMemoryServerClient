namespace Shared;

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
    /// Job type (unified for players and enemies)
    /// </summary>
    public JobType? Job { get; init; }

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
