using Shared.Constants;
using Shared.Models;
using System.Collections.Concurrent;

namespace InMemoryServer.Services;

/// <summary>
/// Manages client groups across different protocols
/// </summary>
public class GroupManager
{
    private readonly ILogger<GroupManager> _logger;
    private readonly ConnectionManager _connectionManager;
    private readonly ConcurrentDictionary<string, GroupInfo> _groups = new(Environment.ProcessorCount * 2, 10); // Pre-allocate for typical usage (GroupId -> GroupInfo)
    private readonly ConcurrentDictionary<string, string> _connectionToGroup = new(Environment.ProcessorCount * 2, 50); // Pre-allocate for typical connections (ConnectionId -> GroupId)
    private readonly ConcurrentDictionary<string, Lock> _groupLocks = new(Environment.ProcessorCount * 2, 10); // Lock objects for each group (GroupId -> Lock)
    private readonly ConcurrentDictionary<string, string> _groupNameToId = new(Environment.ProcessorCount * 2, 10); // Group name to ID mapping (GroupName -> GroupId)
    private readonly ConcurrentDictionary<string, Lock> _groupNameLocks = new(Environment.ProcessorCount * 2, 10); // Lock objects for group name operations (GroupName -> Lock)

    public GroupManager(ILogger<GroupManager> logger, ConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;

        // Start group cleanup timer
        StartCleanupTimer();
    }

    /// <summary>
    /// Join a group, creating it if necessary
    /// </summary>
    public async Task<GroupInfo> JoinGroupAsync(string connectionId, string? groupName = null)
    {
        // If group name is specified, try to join that group
        if (!string.IsNullOrEmpty(groupName))
        {
            var groupNameLock = _groupNameLocks.GetOrAdd(groupName, _ => new Lock());
            lock (groupNameLock)
            {
                // Check if group exists after acquiring the name lock
                if (_groupNameToId.TryGetValue(groupName, out var existingGroupId) && _groups.TryGetValue(existingGroupId, out var existingGroup))
                {
                    var groupLock = _groupLocks.GetOrAdd(existingGroup.GroupId, _ => new Lock());
                    lock (groupLock)
                    {
                        if (existingGroup.ConnectionCount < SystemDefines.MaxConnectionsPerGroup)
                        {
                            // Add connection to group (thread-safe operations)
                            existingGroup.ConnectionCount++;
                            _connectionToGroup[connectionId] = existingGroup.GroupId;
                            existingGroup.ClientIds.Add(connectionId);
                            _logger.LogInformation($"Connection {connectionId} joined existing group {existingGroup.Name} (ID: {existingGroup.GroupId})");

                            // Check if group is full for battle start
                            if (existingGroup.ConnectionCount == SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(existingGroup.BattleId))
                            {
                                _logger.LogInformation($"Group {existingGroup.Name} is now full and ready for battle!");
                            }

                            return existingGroup;
                        }
                        else
                        {
                            _logger.LogWarning($"Group {groupName} is full, connection {connectionId} will be assigned to a new group");
                        }
                    }
                }
                else
                {
                    // Create new group with the specified name (still holding the name lock)
                    var namedGroupId = Guid.CreateVersion7().ToString(); // Use GUID v7 for timestamp ordering
                    var namedGroup = new GroupInfo
                    {
                        GroupId = namedGroupId,
                        Name = groupName,
                        ConnectionCount = 1,
                        MaxConnections = SystemDefines.MaxConnectionsPerGroup,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes),
                        ClientIds = new List<string> { connectionId }
                    };

                    _groups[namedGroupId] = namedGroup;
                    _connectionToGroup[connectionId] = namedGroupId;
                    _groupLocks.GetOrAdd(namedGroupId, _ => new Lock()); // Initialize lock for new group
                    _groupNameToId[groupName] = namedGroupId; // Map group name to ID

                    _logger.LogInformation($"Created new group {groupName} (ID: {namedGroupId}) for connection {connectionId}");
                    return namedGroup;
                }
            }
        }

        // Find an available group with space
        var availableGroup = _groups.Values
            .Where(g => g.ConnectionCount < SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(g.BattleId))
            .OrderByDescending(g => g.ConnectionCount) // Prefer groups with more connections to fill them up
            .FirstOrDefault();

        if (availableGroup != null)
        {
            var groupLock = _groupLocks.GetOrAdd(availableGroup.GroupId, _ => new Lock());
            lock (groupLock)
            {
                // Double-check the condition inside the lock to prevent race conditions
                if (availableGroup.ConnectionCount < SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(availableGroup.BattleId))
                {
                    // Add connection to group (thread-safe operations)
                    availableGroup.ConnectionCount++;
                    _connectionToGroup[connectionId] = availableGroup.GroupId;
                    availableGroup.ClientIds.Add(connectionId);
                    _logger.LogInformation($"Connection {connectionId} joined available group {availableGroup.Name} (ID: {availableGroup.GroupId})");

                    // Check if group is full for battle start
                    if (availableGroup.ConnectionCount == SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(availableGroup.BattleId))
                    {
                        _logger.LogInformation($"Group {availableGroup.Name} is now full and ready for battle!");
                    }

                    return availableGroup;
                }
                // Group became full while we were waiting for the lock, fall through to create a new group
            }
        }

        // Create a new group
        var newGroupId = Guid.CreateVersion7().ToString(); // Use GUID v7 for timestamp ordering
        var newGroupName = !string.IsNullOrEmpty(groupName) ? groupName : $"Group-{newGroupId[..8]}";
        var newGroup = new GroupInfo
        {
            GroupId = newGroupId,
            Name = newGroupName,
            ConnectionCount = 1,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes),
            ClientIds = new List<string> { connectionId }
        };

        _groups[newGroupId] = newGroup;
        _connectionToGroup[connectionId] = newGroupId;
        _groupLocks.GetOrAdd(newGroupId, _ => new Lock()); // Initialize lock for new group
        _groupNameToId[newGroupName] = newGroupId; // Map group name to ID

        _logger.LogInformation($"Created new group {newGroupName} (ID: {newGroupId}) for connection {connectionId}");
        return newGroup;
    }

    /// <summary>
    /// Leave current group
    /// </summary>
    public async Task<(GroupInfo? group, int newCount)> LeaveGroupAsync(string connectionId)
    {
        if (_connectionToGroup.TryRemove(connectionId, out var groupId))
        {
            if (_groups.TryGetValue(groupId, out var group))
            {
                var groupLock = _groupLocks.GetOrAdd(groupId, _ => new Lock());
                lock (groupLock)
                {
                    group.ConnectionCount--;
                    group.ClientIds.Remove(connectionId);
                    var newCount = group.ConnectionCount;
                    _logger.LogInformation($"Connection {connectionId} left group {group.Name} (ID: {groupId}). New count: {newCount}");

                    // Remove group if empty
                    if (group.ConnectionCount <= 0)
                    {
                        _groups.TryRemove(groupId, out _);
                        _groupLocks.TryRemove(groupId, out _); // Clean up the lock as well
                        _groupNameToId.TryRemove(group.Name, out _); // Clean up the name mapping
                        _groupNameLocks.TryRemove(group.Name, out _); // Clean up the name lock as well
                        _logger.LogDebug($"Removed empty group {group.Name} (ID: {groupId})");
                        return (null, 0);
                    }

                    return (group, newCount);
                }
            }
        }

        return (null, 0);
    }

    /// <summary>
    /// Get all available groups
    /// </summary>
    public IEnumerable<GroupInfo> GetAllGroups()
    {
        return _groups.Values;
    }

    /// <summary>
    /// Get group info by ID
    /// </summary>
    public GroupInfo? GetGroupInfo(string groupId)
    {
        return _groups.TryGetValue(groupId, out var group) ? group : null;
    }

    /// <summary>
    /// Get group ID for a connection
    /// </summary>
    public string? GetGroupIdForConnection(string connectionId)
    {
        return _connectionToGroup.TryGetValue(connectionId, out var groupId) ? groupId : null;
    }

    /// <summary>
    /// Extend group waiting time if possible
    /// </summary>
    public bool ExtendGroupWaitingTime(string groupId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
        {
            return false;
        }

        var groupLock = _groupLocks.GetOrAdd(groupId, _ => new Lock());
        lock (groupLock)
        {
            // Check if extension is allowed
            if (group.ExtensionCount >= SystemDefines.MaxGroupExtensions)
            {
                _logger.LogWarning($"Group {group.Name} (ID: {groupId}) has reached maximum extensions ({SystemDefines.MaxGroupExtensions})");
                return false;
            }

            // Extend the group
            group.ExtensionCount++;
            group.LastExtendedAt = DateTime.UtcNow;
            group.ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExtensionMinutes);

            _logger.LogInformation($"Extended group {group.Name} (ID: {groupId}) for {SystemDefines.GroupExtensionMinutes} minutes. Extension count: {group.ExtensionCount}/{SystemDefines.MaxGroupExtensions}");
            return true;
        }
    }

    /// <summary>
    /// Dissolve a group and notify all members
    /// </summary>
    public async Task<List<string>> DissolveGroupAsync(string groupId, string reason = "Group disbanded due to timeout")
    {
        if (!_groups.TryRemove(groupId, out var group))
        {
            return new List<string>();
        }

        var groupLock = _groupLocks.GetOrAdd(groupId, _ => new Lock());
        List<string> clientIds;
        lock (groupLock)
        {
            clientIds = new List<string>(group.ClientIds);

            // Remove all connections from the group mapping
            foreach (var clientId in clientIds)
            {
                _connectionToGroup.TryRemove(clientId, out _);
            }
        }

        // Clean up the lock after the group is dissolved
        _groupLocks.TryRemove(groupId, out _);
        _groupNameToId.TryRemove(group.Name, out _); // Clean up the name mapping
        _groupNameLocks.TryRemove(group.Name, out _); // Clean up the name lock as well

        _logger.LogInformation($"Dissolved group {group.Name} (ID: {groupId}). Reason: {reason}. Affected clients: {clientIds.Count}");
        return clientIds;
    }

    /// <summary>
    /// Get groups that need extension or dissolution
    /// </summary>
    public IEnumerable<GroupInfo> GetGroupsNeedingAttention()
    {
        var now = DateTime.UtcNow;
        return _groups.Values.Where(g =>
            g.ConnectionCount > 0 &&
            g.ConnectionCount < SystemDefines.MaxConnectionsPerGroup &&
            string.IsNullOrEmpty(g.BattleId) &&
            g.ExpiresAt <= now.AddMinutes(1) // Groups expiring within 1 minute
        );
    }

    /// <summary>
    /// Start timer to clean up expired groups and handle extensions
    /// </summary>
    private void StartCleanupTimer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                // Check every 30 seconds for more responsive handling
                await Task.Delay(TimeSpan.FromSeconds(30));

                var now = DateTime.UtcNow;

                // Handle empty expired groups
                var expiredEmptyGroups = _groups.Values.Where(g => g.ExpiresAt < now && g.ConnectionCount == 0).ToList();
                foreach (var group in expiredEmptyGroups)
                {
                    if (_groups.TryRemove(group.GroupId, out _))
                    {
                        _logger.LogInformation($"Removed expired empty group {group.Name} (ID: {group.GroupId})");
                    }
                }

                // Handle groups that need attention (extension or dissolution)
                var groupsNeedingAttention = GetGroupsNeedingAttention().ToList();
                foreach (var group in groupsNeedingAttention)
                {
                    if (group.ExpiresAt <= now)
                    {
                        // Group has expired, attempt extension or dissolve
                        if (group.ExtensionCount < SystemDefines.MaxGroupExtensions)
                        {
                            // Extend the group
                            ExtendGroupWaitingTime(group.GroupId);
                            _logger.LogInformation($"Auto-extended group {group.Name} (ID: {group.GroupId}) due to timeout. Members: {group.ConnectionCount}/{SystemDefines.MaxConnectionsPerGroup}");
                        }
                        else
                        {
                            // Dissolve the group - maximum extensions reached
                            var clientIds = await DissolveGroupAsync(group.GroupId, "Maximum extensions reached");
                            _logger.LogWarning($"Auto-dissolved group {group.Name} (ID: {group.GroupId}) - maximum extensions reached. Affected clients: {clientIds.Count}");

                            // Notify the hub about the dissolution
                            OnGroupDissolved?.Invoke(group.GroupId, group.Name, clientIds, "Maximum extensions reached");
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Event fired when a group is dissolved
    /// </summary>
    public event Action<string, string, List<string>, string>? OnGroupDissolved;
}
