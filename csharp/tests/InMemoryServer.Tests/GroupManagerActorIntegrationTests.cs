using InMemoryServer.Services;

namespace InMemoryServer.Tests;

/// <summary>
/// Integration tests for Actor-based Group Manager
/// </summary>
public class GroupManagerActorIntegrationTests : IDisposable
{
    private GroupManagerActor _actor = null!;
    private IGroupManager _groupManager = null!;
    private ILogger<GroupManagerActor> _logger = null!;
    private MockConnectionManager _connectionManager = null!;

    public GroupManagerActorIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<GroupManagerActor>();
        _connectionManager = new MockConnectionManager();

        _actor = new GroupManagerActor(_logger, _connectionManager);
        _groupManager = new GroupManagerAdapter(_actor);
    }

    public void Dispose()
    {
        _actor?.Dispose();
    }

    [Fact(Timeout = 15000)]
    public async Task JoinGroup_ShouldCreateNewGroup_WhenNoGroupExists()
    {
        // Arrange
        var connectionId = "test_conn_1";
        var groupName = "test_group";

        // Act
        var result = await _groupManager.JoinGroupAsync(connectionId, groupName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(groupName, result.Name);
        Assert.Equal(1, result.ConnectionCount);
        Assert.Contains(connectionId, result.ClientIds);
    }

    [Fact(Timeout = 15000)]
    public async Task JoinGroup_ShouldJoinExistingGroup_WhenGroupHasSpace()
    {
        // Arrange
        var conn1 = "test_conn_1";
        var conn2 = "test_conn_2";
        var groupName = "test_group";

        // Act
        var result1 = await _groupManager.JoinGroupAsync(conn1, groupName);
        var result2 = await _groupManager.JoinGroupAsync(conn2, groupName);

        // Assert
        Assert.Equal(result2.GroupId, result1.GroupId);
        Assert.Equal(2, result2.ConnectionCount);
        Assert.Equal(2, result2.ClientIds.Count);
        Assert.Contains(conn1, result2.ClientIds);
        Assert.Contains(conn2, result2.ClientIds);
    }

    [Fact(Timeout = 15000)]
    public async Task LeaveGroup_ShouldRemoveConnectionFromGroup()
    {
        // Arrange
        var conn1 = "test_conn_1";
        var conn2 = "test_conn_2";
        var groupName = "test_group";

        await _groupManager.JoinGroupAsync(conn1, groupName);
        await _groupManager.JoinGroupAsync(conn2, groupName);

        // Act
        var (group, newCount) = await _groupManager.LeaveGroupAsync(conn1);

        // Assert
        Assert.NotNull(group);
        Assert.Equal(1, newCount);
        Assert.Single(group!.ClientIds);
        Assert.Contains(conn2, group.ClientIds);
        Assert.DoesNotContain(conn1, group.ClientIds);
    }

    [Fact(Timeout = 15000)]
    public async Task LeaveGroup_ShouldRemoveGroupWhenEmpty()
    {
        // Arrange
        var connectionId = "test_conn_1";
        var groupName = "test_group";

        await _groupManager.JoinGroupAsync(connectionId, groupName);

        // Act
        var (group, newCount) = await _groupManager.LeaveGroupAsync(connectionId);

        // Assert
        Assert.Null(group);
        Assert.Equal(0, newCount);

        // Verify group is removed
        var allGroups = await _groupManager.GetAllGroupsAsync();
        Assert.Empty(allGroups);
    }

    [Fact(Timeout = 15000)]
    public async Task GetGroupInfo_ShouldReturnCorrectGroup()
    {
        // Arrange
        var connectionId = "test_conn_1";
        var groupName = "test_group";

        var createdGroup = await _groupManager.JoinGroupAsync(connectionId, groupName);

        // Act
        var retrievedGroup = await _groupManager.GetGroupInfoAsync(createdGroup.GroupId);

        // Assert
        Assert.NotNull(retrievedGroup);
        Assert.Equal(createdGroup.GroupId, retrievedGroup!.GroupId);
        Assert.Equal(groupName, retrievedGroup.Name);
        Assert.Equal(1, retrievedGroup.ConnectionCount);
    }

    [Fact(Timeout = 15000)]
    public async Task GetGroupIdForConnection_ShouldReturnCorrectGroupId()
    {
        // Arrange
        var connectionId = "test_conn_1";
        var groupName = "test_group";

        var group = await _groupManager.JoinGroupAsync(connectionId, groupName);

        // Act
        var groupId = await _groupManager.GetGroupIdForConnectionAsync(connectionId);

        // Assert
        Assert.Equal(group.GroupId, groupId);
    }

    [Fact(Timeout = 15000)]
    public async Task ExtendGroupWaitingTime_ShouldExtendGroup()
    {
        // Arrange
        var connectionId = "test_conn_1";
        var groupName = "test_group";

        var group = await _groupManager.JoinGroupAsync(connectionId, groupName);
        var originalExpiresAt = group.ExpiresAt;

        // Act
        var success = await _groupManager.ExtendGroupWaitingTimeAsync(group.GroupId);

        // Assert
        Assert.True(success);

        // Verify the extension
        var updatedGroup = await _groupManager.GetGroupInfoAsync(group.GroupId);
        Assert.NotNull(updatedGroup);
        Assert.True(updatedGroup!.ExpiresAt > originalExpiresAt);
        Assert.Equal(1, updatedGroup.ExtensionCount);
    }

    [Fact(Timeout = 15000)]
    public async Task DissolveGroup_ShouldRemoveGroupAndReturnClientIds()
    {
        // Arrange
        var conn1 = "test_conn_1";
        var conn2 = "test_conn_2";
        var groupName = "test_group";

        var group = await _groupManager.JoinGroupAsync(conn1, groupName);
        await _groupManager.JoinGroupAsync(conn2, groupName);

        // Act
        var clientIds = await _groupManager.DissolveGroupAsync(group.GroupId, "Test dissolution");

        // Assert
        Assert.Equal(2, clientIds.Count);
        Assert.Contains(conn1, clientIds);
        Assert.Contains(conn2, clientIds);

        // Verify group is removed
        var dissolvedGroup = await _groupManager.GetGroupInfoAsync(group.GroupId);
        Assert.Null(dissolvedGroup);
    }

    [Fact(Timeout = 15000)]
    public async Task ConcurrentOperations_ShouldHandleCorrectly()
    {
        // Arrange
        const int connectionCount = 100;
        var connectionIds = Enumerable.Range(0, connectionCount).Select(i => $"conn_{i}").ToArray();

        // Act - Concurrent joins with different group names to avoid group size limits
        var joinTasks = connectionIds.Select((id, index) =>
            _groupManager.JoinGroupAsync(id, $"concurrent_group_{index % 20}")).ToArray();
        var joinResults = await Task.WhenAll(joinTasks);

        // Assert - All joins successful
        Assert.Equal(connectionCount, joinResults.Length);
        Assert.True(joinResults.All(r => r != null));

        // Verify that multiple groups were created due to group size limits
        var allGroups = await _groupManager.GetAllGroupsAsync();
        var totalConnections = allGroups.Sum(g => g.ConnectionCount);
        Assert.Equal(connectionCount, totalConnections);

        // Verify each group respects the max connection limit
        Assert.True(allGroups.All(g => g.ConnectionCount <= SystemDefines.MaxConnectionsPerGroup));

        // Act - Concurrent leaves
        var leaveTasks = connectionIds.Select(id => _groupManager.LeaveGroupAsync(id)).ToArray();
        var leaveResults = await Task.WhenAll(leaveTasks);

        // Assert - All groups should be removed after all leave
        var allGroupsAfterLeave = await _groupManager.GetAllGroupsAsync();
        Assert.Empty(allGroupsAfterLeave);
    }

    [Fact(Timeout = 15000)]
    public async Task GroupDissolutionEvent_ShouldBeFired()
    {
        // Arrange
        var connectionId = "test_conn_1";
        var groupName = "test_group";
        var eventFired = false;
        string? dissolvedGroupId = null;
        string? dissolvedGroupName = null;
        List<string>? affectedClients = null;
        string? reason = null;

        _groupManager.OnGroupDissolved += (groupId, groupName, clientIds, dissolveReason) =>
        {
            eventFired = true;
            dissolvedGroupId = groupId;
            dissolvedGroupName = groupName;
            affectedClients = clientIds;
            reason = dissolveReason;
        };

        var group = await _groupManager.JoinGroupAsync(connectionId, groupName);

        // Act
        await _groupManager.DissolveGroupAsync(group.GroupId, "Test event");

        // Allow some time for event to be fired
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(eventFired);
        Assert.Equal(group.GroupId, dissolvedGroupId);
        Assert.Equal(groupName, dissolvedGroupName);
        Assert.Contains(connectionId, affectedClients ?? new List<string>());
        Assert.Equal("Test event", reason);
    }
}

/// <summary>
/// Mock connection manager for testing
/// </summary>
public class MockConnectionManager : ConnectionManager
{
    public MockConnectionManager() : base(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ConnectionManager>())
    {
    }

    public override async Task<bool> IsConnectionActiveAsync(string connectionId)
    {
        // Always return true for testing
        return await Task.FromResult(true);
    }
}
