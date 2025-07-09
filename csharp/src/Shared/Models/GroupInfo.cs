using Shared.Contracts;

namespace Shared.Models;

/// <summary>
/// Group information
/// </summary>
public class GroupInfo : IBattleGroupContext
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
    /// Gets the maximum number of clients (alias for MaxConnections)
    /// </summary>
    public int MaxClients => MaxConnections;

    /// <summary>
    /// Gets the connected client count (alias for ConnectionCount)
    /// </summary>
    public int ConnectedCount => ConnectionCount;

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
    /// Number of extensions used for this group
    /// </summary>
    public int ExtensionCount { get; set; } = 0;

    /// <summary>
    /// Time when the group was last extended
    /// </summary>
    public DateTime? LastExtendedAt { get; set; }

    /// <summary>
    /// Client IDs in this group (not sent to clients)
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> ClientIds { get; set; } = new(5); // Pre-allocate for max connections

    /// <summary>
    /// Gets the readonly list of client IDs
    /// </summary>
    IReadOnlyList<string> IBattleGroupContext.ClientIds => ClientIds;
}
