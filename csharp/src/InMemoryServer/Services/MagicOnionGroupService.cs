using Cysharp.Runtime.Multicast;
using Shared.Contracts.Http2Server;

namespace InMemoryServer.Services;

/// <summary>
/// MagicOnion application-managed group service
/// Based on: https://cysharp.github.io/MagicOnion/ja/streaminghub/group-application-managed
/// </summary>
public class MagicOnionGroupService(IMulticastGroupProvider groupProvider, ILogger<MagicOnionGroupService> logger) : IDisposable
{
    private readonly Dictionary<string, IMulticastSyncGroup<Guid, IMagicOnionBattleHubReceiver>> _groups = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Get or create a group for the specified group ID
    /// </summary>
    public IMulticastSyncGroup<Guid, IMagicOnionBattleHubReceiver> GetOrCreateGroup(string groupId)
    {
        lock (_lock)
        {
            if (!_groups.TryGetValue(groupId, out var group))
            {
                group = groupProvider.GetOrAddSynchronousGroup<Guid, IMagicOnionBattleHubReceiver>(groupId);
                _groups[groupId] = group;
                logger.LogDebug("Created new MagicOnion group: {GroupId}", groupId);
            }
            return group;
        }
    }

    /// <summary>
    /// Add a client to a group
    /// </summary>
    public void AddClientToGroup(string groupId, Guid connectionId, IMagicOnionBattleHubReceiver client)
    {
        var group = GetOrCreateGroup(groupId);
        group.Add(connectionId, client);
        logger.LogDebug("Added client {ConnectionId} to MagicOnion group {GroupId}", connectionId, groupId);
    }

    /// <summary>
    /// Remove a client from a group
    /// </summary>
    public void RemoveClientFromGroup(string groupId, Guid connectionId)
    {
        lock (_lock)
        {
            if (_groups.TryGetValue(groupId, out var group))
            {
                group.Remove(connectionId);
                logger.LogDebug("Removed client {ConnectionId} from MagicOnion group {GroupId}", connectionId, groupId);
            }
        }
    }

    /// <summary>
    /// Send a message to all clients in a group
    /// </summary>
    public void SendToAll(string groupId, Action<IMagicOnionBattleHubReceiver> action)
    {
        logger.LogDebug("MagicOnionGroupService.SendToAll called for group {GroupId}", groupId);
        lock (_lock)
        {
            if (_groups.TryGetValue(groupId, out var group))
            {
                logger.LogDebug("MagicOnionGroupService.SendToAll group found for group {GroupId}, calling action", groupId);

                try
                {
                    // Call action directly - MagicOnion StreamingHub requires synchronous calls
                    action(group.All);
                    logger.LogDebug("MagicOnionGroupService.SendToAll action completed for group {GroupId}", groupId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "MagicOnionGroupService.SendToAll action failed for group {GroupId}", groupId);
                }
            }
            else
            {
                logger.LogWarning("MagicOnionGroupService.SendToAll group not found for group {GroupId}", groupId);
            }
        }
        logger.LogDebug("MagicOnionGroupService.SendToAll exiting for group {GroupId}", groupId);
    }

    /// <summary>
    /// Remove a group when it's no longer needed
    /// </summary>
    public void RemoveGroup(string groupId)
    {
        lock (_lock)
        {
            if (_groups.TryGetValue(groupId, out var group))
            {
                group.Dispose();
                _groups.Remove(groupId);
                logger.LogDebug("Removed MagicOnion group: {GroupId}", groupId);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var group in _groups.Values)
            {
                group.Dispose();
            }
            _groups.Clear();
        }
    }
}
