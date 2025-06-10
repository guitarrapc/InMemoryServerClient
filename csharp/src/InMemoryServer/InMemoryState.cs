using System.Collections.Concurrent;

namespace InMemoryServer;

/// <summary>
/// State container for the in-memory server
/// </summary>
public class InMemoryState
{
    /// <summary>
    /// Key-value store
    /// </summary>
    public ConcurrentDictionary<string, string> KeyValueStore { get; } = new ConcurrentDictionary<string, string>();

    /// <summary>
    /// Key watchers (key -> set of connection IDs)
    /// </summary>
    public ConcurrentDictionary<string, HashSet<string>> KeyWatchers { get; } = new ConcurrentDictionary<string, HashSet<string>>();

    /// <summary>
    /// Battle states (battle ID -> battle state)
    /// </summary>
    public ConcurrentDictionary<string, BattleState> BattleStates { get; } = new ConcurrentDictionary<string, BattleState>();
}
