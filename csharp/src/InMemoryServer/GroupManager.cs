using Shared.Constants;
using Shared.Models;
using System.Collections.Concurrent;

namespace InMemoryServer;

/// <summary>
/// Manages client groups
/// </summary>
public class GroupManager
{
    private readonly ILogger<GroupManager> _logger;
    private readonly ConcurrentDictionary<string, GroupInfo> _groups = new(Environment.ProcessorCount * 2, 10); // Pre-allocate for typical usage
    private readonly ConcurrentDictionary<string, string> _connectionToGroup = new(Environment.ProcessorCount * 2, 50); // Pre-allocate for typical connections

    public GroupManager(ILogger<GroupManager> logger)
    {
        _logger = logger;

        // Start group cleanup timer
        StartCleanupTimer();
    }

    /// <summary>
    /// Join a group, creating it if necessary
    /// </summary>
    public async Task<GroupInfo> JoinGroupAsync(string connectionId, string? groupName = null)
    {
        // If group name is specified, try to join that group
        if (!string.IsNullOrEmpty(groupName) && _groups.TryGetValue(groupName, out var existingGroup))
        {
            if (existingGroup.ConnectionCount < SystemDefines.MaxConnectionsPerGroup)
            {
                // Add connection to group
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

        // Find an available group with space
        var availableGroup = _groups.Values
            .Where(g => g.ConnectionCount < SystemDefines.MaxConnectionsPerGroup && string.IsNullOrEmpty(g.BattleId))
            .OrderByDescending(g => g.ConnectionCount) // Prefer groups with more connections to fill them up
            .FirstOrDefault();

        if (availableGroup != null)
        {
            // Add connection to group
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
                group.ConnectionCount--;
                group.ClientIds.Remove(connectionId);
                var newCount = group.ConnectionCount;
                _logger.LogInformation($"Connection {connectionId} left group {group.Name} (ID: {groupId}). New count: {newCount}");

                // Remove group if empty
                if (group.ConnectionCount <= 0)
                {
                    _groups.TryRemove(groupId, out _);
                    _logger.LogDebug($"Removed empty group {group.Name} (ID: {groupId})");
                    return (null, 0);
                }

                return (group, newCount);
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

    /// <summary>
    /// Dissolve a group and notify all members
    /// </summary>
    public async Task<List<string>> DissolveGroupAsync(string groupId, string reason = "Group disbanded due to timeout")
    {
        if (!_groups.TryRemove(groupId, out var group))
        {
            return new List<string>();
        }

        var clientIds = new List<string>(group.ClientIds);

        // Remove all connections from the group mapping
        foreach (var clientId in clientIds)
        {
            _connectionToGroup.TryRemove(clientId, out _);
        }

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
