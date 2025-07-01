namespace Shared;

/// <summary>
/// Vector2 for positions
/// </summary>
/// <remarks>
/// Creates a new Vector2
/// </remarks>
[System.Text.Json.Serialization.JsonConverter(typeof(Vector2JsonConverter))]
public readonly record struct Vector2
{
    /// <summary>
    /// X coordinate
    /// </summary>
    public readonly int X { get; init; }

    /// <summary>
    /// Y coordinate
    /// </summary>
    public readonly int Y { get; init; }

    /// <summary>
    /// Creates a new Vector2
    /// </summary>
    public Vector2(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString() => $"({X}, {Y})";

    public static Vector2 InvalidPosition { get; } = new Vector2(-1, -1);
}

/// <summary>
/// Custom JSON converter for Vector2 to avoid property name conflicts
/// </summary>
public class Vector2JsonConverter : System.Text.Json.Serialization.JsonConverter<Vector2>
{
    public override Vector2 Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.StartObject)
        {
            throw new System.Text.Json.JsonException();
        }

        int x = 0;
        int y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)
            {
                return new Vector2(x, y);
            }

            if (reader.TokenType != System.Text.Json.JsonTokenType.PropertyName)
            {
                throw new System.Text.Json.JsonException();
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "X":
                    x = reader.GetInt32();
                    break;
                case "Y":
                    y = reader.GetInt32();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new System.Text.Json.JsonException();
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, Vector2 value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Group information
/// </summary>
public class GroupInfo
{
    /// <summary>
    /// Group unique identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Group name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Current connection count
    /// </summary>
    public int ConnectionCount { get; set; }

    /// <summary>
    /// Maximum allowed connections
    /// </summary>
    public int MaxConnections { get; set; }

    /// <summary>
    /// Group creation time
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Group expiration time
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Battle ID if battle is in progress
    /// </summary>
    public string? BattleId { get; set; }

    /// <summary>
    /// Client IDs in this group (not sent to clients)
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> ClientIds { get; set; } = new(5); // Pre-allocate for max connections
}

/// <summary>
/// Battle status
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
    public int FieldWidth { get; set; } = BattleBasicDefines.BattleFieldWidth;

    /// <summary>
    /// Field dimensions (for client-side rendering)
    /// </summary>
    public int FieldHeight { get; set; } = BattleBasicDefines.BattleFieldHeight;

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
/// Entity information (player or enemy)
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
    /// Entity type (player or enemy)
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Player job (only for players)
    /// </summary>
    public PlayerJob? Job { get; init; }

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
    /// Position on the battle field
    /// </summary>
    public Vector2 Position { get; init; }

    /// <summary>
    /// Is defending (damage reduction)
    /// </summary>
    public bool IsDefending { get; init; }
}

/// <summary>
/// Battle field information
/// </summary>
public readonly struct BattleFieldInfo
{
    /// <summary>
    /// Field width
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Field height
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Field cells
    /// </summary>
    public ReadOnlyMemory<ReadOnlyMemory<string?>> Cells { get; init; }
}

/// <summary>
/// Server status information
/// </summary>
public class ServerStatus
{
    /// <summary>
    /// Server uptime
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Total active connections
    /// </summary>
    public int TotalConnections { get; set; }

    /// <summary>
    /// Number of groups
    /// </summary>
    public int GroupCount { get; set; }

    /// <summary>
    /// Number of active battles
    /// </summary>
    public int ActiveBattleCount { get; set; }

    /// <summary>
    /// List of group summaries
    /// </summary>
    public List<GroupSummary> Groups { get; set; } = new(10); // Pre-allocate for typical group count

    /// <summary>
    /// List of active battle summaries
    /// </summary>
    public List<BattleSummary> ActiveBattles { get; set; } = new(5); // Pre-allocate for typical battle count
}

/// <summary>
/// Group summary information
/// </summary>
public readonly struct GroupSummary
{
    /// <summary>
    /// Group ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Group name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current connection count
    /// </summary>
    public int ConnectionCount { get; init; }

    /// <summary>
    /// Battle ID if battle is in progress
    /// </summary>
    public string? BattleId { get; init; }
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
/// Battle replay data sent to clients
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
