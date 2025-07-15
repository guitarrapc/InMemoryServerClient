using MagicOnion;

namespace Shared.Contracts.MagicOnion;

/// <summary>
/// MagicOnion service interface for key-value operations
/// </summary>
public interface IKeyValueService : IService<IKeyValueService>
{
    /// <summary>
    /// Get value by key
    /// </summary>
    /// <param name="key">The key to retrieve</param>
    /// <returns>The value if found, null otherwise</returns>
    UnaryResult<string?> GetAsync(string key);

    /// <summary>
    /// Set key-value pair
    /// </summary>
    /// <param name="key">The key to set</param>
    /// <param name="value">The value to set</param>
    /// <returns>True if successful</returns>
    UnaryResult<bool> SetAsync(string key, string value);

    /// <summary>
    /// Delete key
    /// </summary>
    /// <param name="key">The key to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    UnaryResult<bool> DeleteAsync(string key);

    /// <summary>
    /// List keys matching pattern
    /// </summary>
    /// <param name="pattern">Pattern to match (default: "*")</param>
    /// <returns>Collection of matching keys</returns>
    UnaryResult<string[]> ListAsync(string pattern = "*");

    /// <summary>
    /// Watch key for changes
    /// </summary>
    /// <param name="key">The key to watch</param>
    /// <returns>True if watch was set up successfully</returns>
    UnaryResult<bool> WatchAsync(string key);
}
