using System.Collections.Concurrent;

namespace InMemoryServer;

/// <summary>
/// State container for the in-memory server
/// </summary>
public class InMemoryState
{
    /// <summary>
    /// Server start time
    /// </summary>
    public DateTime StartTime { get; } = DateTime.UtcNow;

    /// <summary>
    /// Key-value store
    /// </summary>
    public ConcurrentDictionary<string, string> KeyValueStore { get; } = new(Environment.ProcessorCount * 2, 100); // Pre-allocate for typical usage

    /// <summary>
    /// Key watchers (key -> set of connection IDs)
    /// </summary>
    public ConcurrentDictionary<string, HashSet<string>> KeyWatchers { get; } = new(Environment.ProcessorCount * 2, 50); // Pre-allocate for watchers

    /// <summary>
    /// Battle states (battle ID -> battle state)
    /// </summary>
    public ConcurrentDictionary<string, BattleState> BattleStates { get; } = new(Environment.ProcessorCount * 2, 10); // Pre-allocate for battles

    /// <summary>
    /// Total connection count
    /// </summary>
    public int ConnectionCount { get; set; } = 0;
}
