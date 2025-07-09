namespace InMemoryServer.Tests;

/// <summary>
/// Tests for GroupManager
/// </summary>
public class GroupManagerTests
{
    private readonly ILogger<GroupManager> _logger;
    private readonly GroupManager _groupManager;

    public GroupManagerTests()
    {
        _logger = Substitute.For<ILogger<GroupManager>>();
        _groupManager = new GroupManager(_logger);
    }

    [Fact]
    public async Task JoinGroupAsync_ShouldCreateNewGroup_WhenNoGroupsExist()
    {
        // Arrange
        const string connectionId = "test_connection";
        const string groupName = "test_group";

        // Act
        var result = await _groupManager.JoinGroupAsync(connectionId, groupName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(groupName, result.Name);
        Assert.Equal(1, result.ConnectionCount);
        Assert.Equal(SystemDefines.MaxConnectionsPerGroup, result.MaxConnections);
    }

    [Fact]
    public async Task JoinGroupAsync_ShouldJoinExistingGroup_WhenGroupHasSpace()
    {
        // Arrange
        const string connectionId1 = "test_connection_1";
        const string connectionId2 = "test_connection_2";
        const string groupName = "test_group";

        // Act
        var group1 = await _groupManager.JoinGroupAsync(connectionId1, groupName);
        var group2 = await _groupManager.JoinGroupAsync(connectionId2, groupName);

        // Assert
        Assert.Equal(group1.GroupId, group2.GroupId);
        Assert.Equal(2, group2.ConnectionCount);
    }

    [Fact]
    public async Task LeaveGroupAsync_ShouldReduceConnectionCount()
    {
        // Arrange
        const string connectionId = "test_connection";
        const string groupName = "test_group";

        // Act
        var group = await _groupManager.JoinGroupAsync(connectionId, groupName);
        await _groupManager.LeaveGroupAsync(connectionId);

        // Assert
        var groupInfo = _groupManager.GetGroupInfo(group.GroupId);
        Assert.Null(groupInfo); // Group should be removed when empty
    }

    [Fact]
    public void GetGroupIdForConnection_ShouldReturnNull_WhenConnectionNotInGroup()
    {
        // Arrange
        const string connectionId = "test_connection";

        // Act
        var result = _groupManager.GetGroupIdForConnection(connectionId);

        // Assert
        Assert.Null(result);
    }
}
