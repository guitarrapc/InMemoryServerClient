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
    public List<string> ClientIds { get; set; } = new(5); // Pre-allocate for max connections
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

