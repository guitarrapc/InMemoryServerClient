namespace Shared.BattleServer.Models;

/// <summary>
/// Represents battle group context information
/// </summary>
public interface IBattleGroupContext
{
    string GroupId { get; }
    string Name { get; }
    int MaxClients { get; }
    int ConnectedCount { get; }
    IReadOnlyList<string> ClientIds { get; }
}

/// <summary>
/// Group information
/// </summary>
[MessagePackObject(true, AllowPrivate = true)]
public class BattleGroupContext : IBattleGroupContext
{
    /// <summary>
    /// Group unique identifier
    /// </summary>
    public required string GroupId { get; set; }

    /// <summary>
    /// Group name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Private field for thread-safe connection count operations
    /// </summary>
    [IgnoreMember]
    private int _connectionCount;

    /// <summary>
    /// Current connection count
    /// Prefer using IncrementConnectionCount() and DecrementConnectionCount() for thread-safe operations
    /// </summary>
    public int ConnectionCount
    {
        get => _connectionCount;
        init => _connectionCount = value; // For initialization and serialization only
    }

    /// <summary>
    /// Maximum allowed connections
    /// </summary>
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
    [JsonIgnore]
    [IgnoreMember]
    public List<string> ClientIds { get; set; } = new(5); // Pre-allocate for max connections

    /// <summary>
    /// Gets the readonly list of client IDs
    /// </summary>
    [IgnoreMember]
    IReadOnlyList<string> IBattleGroupContext.ClientIds => ClientIds;

    /// <summary>
    /// Thread-safe increment of connection count
    /// </summary>
    /// <returns>The new connection count after increment</returns>
    public int IncrementConnectionCount()
    {
        return Interlocked.Increment(ref _connectionCount);
    }

    /// <summary>
    /// Thread-safe decrement of connection count
    /// </summary>
    /// <returns>The new connection count after decrement</returns>
    public int DecrementConnectionCount()
    {
        return Interlocked.Decrement(ref _connectionCount);
    }

    /// <summary>
    /// Thread-safe check if the group is full
    /// </summary>
    /// <returns>True if the group has reached maximum connections</returns>
    public bool IsFull()
    {
        return Interlocked.CompareExchange(ref _connectionCount, 0, 0) >= MaxConnections;
    }
}
