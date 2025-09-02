using System.Collections.Concurrent;
using BattleLogic.Battle;

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

    /// <summary>
    /// Get value by key
    /// </summary>
    public string? GetValue(string key)
    {
        return KeyValueStore.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Set key-value pair
    /// </summary>
    public void SetValue(string key, string value)
    {
        KeyValueStore[key] = value;
    }

    /// <summary>
    /// Delete a key
    /// </summary>
    public bool DeleteValue(string key)
    {
        return KeyValueStore.TryRemove(key, out _);
    }

    /// <summary>
    /// List keys matching pattern
    /// </summary>
    public IEnumerable<string> ListKeys(string? pattern = null)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return KeyValueStore.Keys;
        }

        // Simple wildcard pattern matching
        if (pattern.Contains('*'))
        {
            var regex = new System.Text.RegularExpressions.Regex(
                "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return KeyValueStore.Keys.Where(key => regex.IsMatch(key));
        }

        return KeyValueStore.Keys.Where(key => key.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Add watcher for a key
    /// </summary>
    public void AddWatcher(string connectionId, string key)
    {
        KeyWatchers.AddOrUpdate(key,
            new HashSet<string> { connectionId },
            (k, existing) =>
            {
                lock (existing)
                {
                    existing.Add(connectionId);
                    return existing;
                }
            });
    }

    /// <summary>
    /// Remove watcher for a key
    /// </summary>
    public void RemoveWatcher(string connectionId, string key)
    {
        if (KeyWatchers.TryGetValue(key, out var watchers))
        {
            lock (watchers)
            {
                watchers.Remove(connectionId);
                if (watchers.Count == 0)
                {
                    KeyWatchers.TryRemove(key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Remove all watchers for a connection
    /// </summary>
    public void RemoveAllWatchers(string connectionId)
    {
        var keysToRemove = new List<string>();

        foreach (var kvp in KeyWatchers)
        {
            lock (kvp.Value)
            {
                kvp.Value.Remove(connectionId);
                if (kvp.Value.Count == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var key in keysToRemove)
        {
            KeyWatchers.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Get watchers for a key
    /// </summary>
    public IEnumerable<string> GetWatchers(string key)
    {
        if (KeyWatchers.TryGetValue(key, out var watchers))
        {
            lock (watchers)
            {
                return watchers.ToList();
            }
        }
        return [];
    }

    /// <summary>
    /// Get total key count
    /// </summary>
    public int GetKeyCount()
    {
        return KeyValueStore.Count;
    }
}
