using Microsoft.Extensions.Logging;
using Shared;
using System.Collections.Concurrent;

namespace InMemoryServer;

/// <summary>
/// Manages client groups
/// </summary>
public class GroupManager
{
    private readonly ILogger<GroupManager> _logger;
    private readonly ConcurrentDictionary<string, GroupInfo> _groups = new ConcurrentDictionary<string, GroupInfo>();
    private readonly ConcurrentDictionary<string, string> _connectionToGroup = new ConcurrentDictionary<string, string>();

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
            if (existingGroup.ConnectionCount < Constants.MaxConnectionsPerGroup)
            {
                // Add connection to group
                existingGroup.ConnectionCount++;
                _connectionToGroup[connectionId] = existingGroup.Id;
                _logger.LogInformation($"Connection {connectionId} joined existing group {existingGroup.Name} (ID: {existingGroup.Id})");
                return existingGroup;
            }
            else
            {
                _logger.LogWarning($"Group {groupName} is full, connection {connectionId} will be assigned to a new group");
            }
        }

        // Find an available group with space
        var availableGroup = _groups.Values
            .Where(g => g.ConnectionCount < Constants.MaxConnectionsPerGroup && string.IsNullOrEmpty(g.BattleId))
            .OrderByDescending(g => g.ConnectionCount) // Prefer groups with more connections to fill them up
            .FirstOrDefault();

        if (availableGroup != null)
        {
            // Add connection to group
            availableGroup.ConnectionCount++;
            _connectionToGroup[connectionId] = availableGroup.Id;
            _logger.LogInformation($"Connection {connectionId} joined available group {availableGroup.Name} (ID: {availableGroup.Id})");
            return availableGroup;
        }

        // Create a new group
        var newGroupId = Guid.NewGuid().ToString();
        var newGroupName = !string.IsNullOrEmpty(groupName) ? groupName : $"Group-{newGroupId.Substring(0, 8)}";

        var newGroup = new GroupInfo
        {
            Id = newGroupId,
            Name = newGroupName,
            ConnectionCount = 1,
            MaxConnections = Constants.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(Constants.GroupExpirationMinutes)
        };

        _groups[newGroupId] = newGroup;
        _connectionToGroup[connectionId] = newGroupId;

        _logger.LogInformation($"Created new group {newGroupName} (ID: {newGroupId}) for connection {connectionId}");
        return newGroup;
    }

    /// <summary>
    /// Leave current group
    /// </summary>
    public async Task LeaveGroupAsync(string connectionId)
    {
        if (_connectionToGroup.TryRemove(connectionId, out var groupId))
        {
            if (_groups.TryGetValue(groupId, out var group))
            {
                group.ConnectionCount--;
                _logger.LogInformation($"Connection {connectionId} left group {group.Name} (ID: {groupId})");

                // Remove group if empty
                if (group.ConnectionCount <= 0)
                {
                    _groups.TryRemove(groupId, out _);
                    _logger.LogInformation($"Removed empty group {group.Name} (ID: {groupId})");
                }
            }
        }
    }

    /// <summary>
    /// Get all available groups
    /// </summary>
    public IEnumerable<GroupInfo> GetAllGroups()
    {
        return _groups.Values;
    }        /// <summary>
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
    /// Start timer to clean up expired groups
    /// </summary>
    private void StartCleanupTimer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                // Check once a minute
                await Task.Delay(TimeSpan.FromMinutes(1));

                var now = DateTime.UtcNow;
                var expiredGroups = _groups.Values.Where(g => g.ExpiresAt < now && g.ConnectionCount == 0).ToList();

                foreach (var group in expiredGroups)
                {
                    if (_groups.TryRemove(group.Id, out _))
                    {
                        _logger.LogInformation($"Removed expired group {group.Name} (ID: {group.Id})");
                    }
                }
            }
        });
    }
}
