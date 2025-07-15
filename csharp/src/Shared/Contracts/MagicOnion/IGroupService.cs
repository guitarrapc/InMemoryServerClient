using MagicOnion;
using Shared.Models;

namespace Shared.Contracts.MagicOnion;

/// <summary>
/// MagicOnion service interface for group management operations
/// </summary>
public interface IGroupService : IService<IGroupService>
{
    /// <summary>
    /// Join a group
    /// </summary>
    /// <param name="groupName">Optional group name to join</param>
    /// <returns>The group ID that was joined</returns>
    UnaryResult<string> JoinGroupAsync(string? groupName = null);

    /// <summary>
    /// Get all available groups
    /// </summary>
    /// <returns>Collection of group information</returns>
    UnaryResult<GroupInfo[]> GetGroupsAsync();

    /// <summary>
    /// Get current group info
    /// </summary>
    /// <returns>Current group information if in a group, null otherwise</returns>
    UnaryResult<GroupInfo?> GetCurrentGroupAsync();

    /// <summary>
    /// Manually extend a group's waiting time
    /// </summary>
    /// <param name="groupName">Optional group name to extend</param>
    /// <returns>True if extension was successful</returns>
    UnaryResult<bool> ExtendGroupAsync(string? groupName = null);
}
