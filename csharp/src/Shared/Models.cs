namespace Shared;
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
    public List<string> ClientIds { get; set; } = [];
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
    /// Battle field information
    /// </summary>
    public required BattleFieldInfo Field { get; set; }

    /// <summary>
    /// Recent battle logs
    /// </summary>
    public List<string> RecentLogs { get; set; } = [];
}    /// <summary>
/// Entity information (player or enemy)
/// </summary>
public class EntityInfo
{
    /// <summary>
    /// Entity unique identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Entity name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Entity type (player or enemy)
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Current HP
    /// </summary>
    public int CurrentHp { get; set; }

    /// <summary>
    /// Maximum HP
    /// </summary>
    public int MaxHp { get; set; }

    /// <summary>
    /// Attack power
    /// </summary>
    public int Attack { get; set; }

    /// <summary>
    /// Defense power
    /// </summary>
    public int Defense { get; set; }

    /// <summary>
    /// Movement speed
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    /// Position X
    /// </summary>
    public int PositionX { get; set; }

    /// <summary>
    /// Position Y
    /// </summary>
    public int PositionY { get; set; }

    /// <summary>
    /// Is defending (damage reduction)
    /// </summary>
    public bool IsDefending { get; set; }
}

/// <summary>
/// Battle field information
/// </summary>
public class BattleFieldInfo
{
    /// <summary>
    /// Field width
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Field height
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Field cells
    /// </summary>
    public List<List<string>> Cells { get; set; } = [];
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
    public List<GroupSummary> Groups { get; set; } = [];

    /// <summary>
    /// List of active battle summaries
    /// </summary>
    public List<BattleSummary> ActiveBattles { get; set; } = [];
}

/// <summary>
/// Group summary information
/// </summary>
public class GroupSummary
{
    /// <summary>
    /// Group ID
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
    /// Battle ID if battle is in progress
    /// </summary>
    public string? BattleId { get; set; }
}

/// <summary>
/// Battle summary information
/// </summary>
public class BattleSummary
{
    /// <summary>
    /// Battle ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Associated group ID
    /// </summary>
    public required string GroupId { get; set; }

    /// <summary>
    /// Current turn
    /// </summary>
    public int CurrentTurn { get; set; }

    /// <summary>
    /// Number of players
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// Number of enemies
    /// </summary>
    public int EnemyCount { get; set; }

    /// <summary>
    /// Battle started time
    /// </summary>
    public DateTime StartedAt { get; set; }
}
