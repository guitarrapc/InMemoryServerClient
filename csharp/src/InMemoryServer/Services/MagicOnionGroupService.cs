using Cysharp.Runtime.Multicast;
using Shared.Contracts.MagicOnion;

namespace InMemoryServer.Services;

/// <summary>
/// MagicOnion application-managed group service
/// Based on: https://cysharp.github.io/MagicOnion/ja/streaminghub/group-application-managed
/// </summary>
public class MagicOnionGroupService : IDisposable
{
    private readonly ILogger<MagicOnionGroupService> logger;
    private readonly IMulticastGroupProvider groupProvider;
    private readonly Dictionary<string, IMulticastSyncGroup<Guid, IInMemoryHubReceiver>> _groups = new();
    private readonly Lock _lock = new();

    public MagicOnionGroupService(IMulticastGroupProvider groupProvider, ILogger<MagicOnionGroupService> logger)
    {
        this.groupProvider = groupProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Get or create a group for the specified group ID
    /// </summary>
    public IMulticastSyncGroup<Guid, IInMemoryHubReceiver> GetOrCreateGroup(string groupId)
    {
        lock (_lock)
        {
            if (!_groups.TryGetValue(groupId, out var group))
            {
                group = groupProvider.GetOrAddSynchronousGroup<Guid, IInMemoryHubReceiver>(groupId);
                _groups[groupId] = group;
                logger.LogInformation("Created new MagicOnion group: {GroupId}", groupId);
            }
            return group;
        }
    }

    /// <summary>
    /// Add a client to a group
    /// </summary>
    public void AddClientToGroup(string groupId, Guid connectionId, IInMemoryHubReceiver client)
    {
        var group = GetOrCreateGroup(groupId);
        group.Add(connectionId, client);
        logger.LogInformation("Added client {ConnectionId} to MagicOnion group {GroupId}", connectionId, groupId);
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
                logger.LogInformation("Removed client {ConnectionId} from MagicOnion group {GroupId}", connectionId, groupId);
            }
        }
    }

    /// <summary>
    /// Send a message to all clients in a group
    /// </summary>
    public void SendToAll(string groupId, Action<IInMemoryHubReceiver> action)
    {
        lock (_lock)
        {
            if (_groups.TryGetValue(groupId, out var group))
            {
                action(group.All);
            }
        }
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
                logger.LogInformation("Removed MagicOnion group: {GroupId}", groupId);
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
