using Shared.Contracts;

namespace Shared.Models;

/// <summary>
/// Group information
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GroupInfo : IBattleGroupContext
{
    /// <summary>
    /// Group unique identifier
    /// </summary>
    [Key(0)]
    public required string GroupId { get; set; }

    /// <summary>
    /// Group name
    /// </summary>
    [Key(1)]
    public required string Name { get; set; }

    /// <summary>
    /// Current connection count
    /// </summary>
    [Key(2)]
    public int ConnectionCount { get; set; }

    /// <summary>
    /// Maximum allowed connections
    /// </summary>
    [Key(3)]
    public int MaxConnections { get; set; }

    /// <summary>
    /// Gets the maximum number of clients (alias for MaxConnections)
    /// </summary>
    [IgnoreMember]
    public int MaxClients => MaxConnections;

    /// <summary>
    /// Gets the connected client count (alias for ConnectionCount)
    /// </summary>
    [IgnoreMember]
    public int ConnectedCount => ConnectionCount;

    /// <summary>
    /// Group creation time
    /// </summary>
    [Key(4)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Group expiration time
    /// </summary>
    [Key(5)]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Battle ID if battle is in progress
    /// </summary>
    [Key(6)]
    public string? BattleId { get; set; }

    /// <summary>
    /// Number of extensions used for this group
    /// </summary>
    [Key(7)]
    public int ExtensionCount { get; set; } = 0;

    /// <summary>
    /// Time when the group was last extended
    /// </summary>
    [Key(8)]
    public DateTime? LastExtendedAt { get; set; }

    /// <summary>
    /// Client IDs in this group (not sent to clients)
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [IgnoreMember]
    public List<string> ClientIds { get; set; } = new(5); // Pre-allocate for max connections

    /// <summary>
    /// Gets the readonly list of client IDs
    /// </summary>
    [IgnoreMember]
    IReadOnlyList<string> IBattleGroupContext.ClientIds => ClientIds;
}
