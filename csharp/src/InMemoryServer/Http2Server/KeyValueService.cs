using MagicOnion;
using MagicOnion.Server;
using Shared.Contracts.MagicOnion;
using System.Text.RegularExpressions;

namespace InMemoryServer.Http2Server;

/// <summary>
/// MagicOnion implementation of key-value operations
/// </summary>
public class KeyValueService(
    ILogger<KeyValueService> logger,
    InMemoryState state) : ServiceBase<IKeyValueService>, IKeyValueService
{
    /// <summary>
    /// Get value by key
    /// </summary>
    public async UnaryResult<string?> GetAsync(string key)
    {
        logger.LogInformation("Client requested value for key: {Key}", key);
        return state.KeyValueStore.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Set key-value pair
    /// </summary>
    public async UnaryResult<bool> SetAsync(string key, string value)
    {
        logger.LogInformation("Client setting key: {Key} to value: {Value}", key, value);
        state.KeyValueStore[key] = value;

        // Note: Key watchers notification would require streaming hub integration
        // For now, this only updates the store
        return true;
    }

    /// <summary>
    /// Delete key
    /// </summary>
    public async UnaryResult<bool> DeleteAsync(string key)
    {
        logger.LogInformation("Client deleting key: {Key}", key);
        var result = state.KeyValueStore.TryRemove(key, out _);

        // Note: Key watchers notification would require streaming hub integration
        // For now, this only removes from the store
        return result;
    }

    /// <summary>
    /// List keys matching pattern
    /// </summary>
    public async UnaryResult<string[]> ListAsync(string pattern = "*")
    {
        logger.LogInformation("Client listing keys with pattern: {Pattern}", pattern);

        // Simple pattern matching, replace * with .* for regex
        if (pattern == "*")
        {
            return [.. state.KeyValueStore.Keys];
        }
        else
        {
            var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
            return state.KeyValueStore.Keys
                .Where(k => Regex.IsMatch(k, regexPattern))
                .ToArray();
        }
    }

    /// <summary>
    /// Watch key for changes
    /// </summary>
    public async UnaryResult<bool> WatchAsync(string key)
    {
        logger.LogInformation("Client watching key: {Key}", key);

        // Note: Full watch implementation would require streaming hub integration
        // For now, this is a placeholder
        logger.LogWarning("Key watching is not fully implemented in unary service. Use streaming hub for real-time notifications.");
        return false;
    }
}
