using MagicOnion;
using MagicOnion.Server;
using Shared.Contracts.MagicOnion;
using Shared.Models;
using InMemoryServer.Services;

namespace InMemoryServer.Http2Server;

/// <summary>
/// MagicOnion implementation of group management operations
/// </summary>
public class GroupService(
    ILogger<GroupService> logger,
    GroupManager groupManager) : ServiceBase<IGroupService>, IGroupService
{
    /// <summary>
    /// Join a group
    /// </summary>
    public async UnaryResult<string> JoinGroupAsync(string? groupName = null)
    {
        var connectionId = $"MagicOnion-{Context.CallContext.Peer}";
        logger.LogInformation("Client {ConnectionId} joining group: {GroupName}", connectionId, groupName ?? "auto-assigned");

        // Find or create group
        var group = await groupManager.JoinGroupAsync(connectionId, groupName);

        logger.LogInformation("Client {ConnectionId} joined group: {GroupName} (ID: {GroupId})",
            connectionId, group.Name, group.GroupId);

        // Note: Group member notifications would require streaming hub integration
        return group.GroupId;
    }

    /// <summary>
    /// Get all available groups
    /// </summary>
    public async UnaryResult<GroupInfo[]> GetGroupsAsync()
    {
        logger.LogInformation("Client requesting group list");
        return [.. groupManager.GetAllGroups()];
    }

    /// <summary>
    /// Get current group info
    /// </summary>
    public async UnaryResult<GroupInfo?> GetCurrentGroupAsync()
    {
        var connectionId = $"MagicOnion-{Context.CallContext.Peer}";
        var groupId = groupManager.GetGroupIdForConnection(connectionId);

        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("Client {ConnectionId} requested current group but is not in any group", connectionId);
            return null;
        }

        return groupManager.GetGroupInfo(groupId);
    }

    /// <summary>
    /// Manually extend a group's waiting time
    /// </summary>
    public async UnaryResult<bool> ExtendGroupAsync(string? groupName = null)
    {
        var connectionId = $"MagicOnion-{Context.CallContext.Peer}";
        var groupId = groupName != null ?
            groupManager.GetAllGroups().FirstOrDefault(g => g.Name == groupName)?.GroupId :
            groupManager.GetGroupIdForConnection(connectionId);

        if (string.IsNullOrEmpty(groupId))
        {
            logger.LogWarning("Client {ConnectionId} tried to extend group but is not in any group or group not found", connectionId);
            return false;
        }

        var success = groupManager.ExtendGroupWaitingTime(groupId);
        if (success)
        {
            var group = groupManager.GetGroupInfo(groupId);
            if (group != null)
            {
                logger.LogInformation("Group {GroupName} (ID: {GroupId}) extended by client {ConnectionId}",
                    group.Name, groupId, connectionId);
            }
        }

        return success;
    }
}
