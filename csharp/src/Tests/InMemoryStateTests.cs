using InMemoryServer;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;

namespace Tests;

/// <summary>
/// Tests for InMemoryState
/// </summary>
public class InMemoryStateTests
{
    [Fact]
    public void KeyValueStore_ShouldBeEmpty_Initially()
    {
        // Arrange & Act
        var state = new InMemoryState();

        // Assert
        Assert.Empty(state.KeyValueStore);
        Assert.Empty(state.KeyWatchers);
        Assert.Empty(state.BattleStates);
    }

    [Fact]
    public void KeyValueStore_ShouldStore_KeyValuePairs()
    {
        // Arrange
        var state = new InMemoryState();
        const string key = "test_key";
        const string value = "test_value";

        // Act
        state.KeyValueStore[key] = value;

        // Assert
        Assert.True(state.KeyValueStore.ContainsKey(key));
        Assert.Equal(value, state.KeyValueStore[key]);
    }
}

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
        Assert.Equal(Constants.MaxConnectionsPerGroup, result.MaxConnections);
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
        Assert.Equal(group1.Id, group2.Id);
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
        var groupInfo = _groupManager.GetGroupInfo(group.Id);
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

/// <summary>
/// Tests for BattleState
/// </summary>
public class BattleStateTests
{
    private readonly ILogger<BattleState> _logger;

    public BattleStateTests()
    {
        _logger = Substitute.For<ILogger<BattleState>>();
    }
    [Fact]
    public void BattleState_ShouldInitialize_WithProvidedGroup()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test_group",
            ConnectionCount = 3,
            MaxConnections = Constants.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(Constants.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(battleId, status.BattleId);
        Assert.Equal(group.ConnectionCount, status.Players.Count);
        Assert.True(status.Enemies.Count >= Constants.MinEnemyCount);
        Assert.True(status.Enemies.Count <= Constants.MaxEnemyCount);
        Assert.Equal(Constants.BattleFieldWidth, status.Field.Width);
        Assert.Equal(Constants.BattleFieldHeight, status.Field.Height);
    }

    [Fact]
    public void BattleStatus_ShouldHaveValidPlayers()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test_group",
            ConnectionCount = 2,
            MaxConnections = Constants.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(Constants.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.Equal(2, status.Players.Count);
        foreach (var player in status.Players)
        {
            Assert.Equal("Player", player.Type);
            Assert.Equal(Constants.PlayerHp, player.MaxHp);
            Assert.Equal(Constants.PlayerHp, player.CurrentHp);
            Assert.True(player.Attack >= Constants.MinAttackPower && player.Attack <= Constants.MaxAttackPower);
            Assert.True(player.Defense >= Constants.MinDefensePower && player.Defense <= Constants.MaxDefensePower);
            Assert.True(player.Speed >= Constants.MinMovementSpeed && player.Speed <= Constants.MaxMovementSpeed);
        }
    }

    [Fact]
    public void BattleStatus_ShouldHaveValidEnemies()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test_group",
            ConnectionCount = 1,
            MaxConnections = Constants.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(Constants.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.True(status.Enemies.Count >= Constants.MinEnemyCount);
        Assert.True(status.Enemies.Count <= Constants.MaxEnemyCount);

        foreach (var enemy in status.Enemies)
        {
            Assert.True(Constants.EnemyHpByType.ContainsKey(enemy.Type));
            Assert.Equal(Constants.EnemyHpByType[enemy.Type], enemy.MaxHp);
            Assert.Equal(Constants.EnemyHpByType[enemy.Type], enemy.CurrentHp);
            Assert.True(enemy.Attack >= Constants.MinAttackPower && enemy.Attack <= Constants.MaxAttackPower);
            Assert.True(enemy.Defense >= Constants.MinDefensePower && enemy.Defense <= Constants.MaxDefensePower);
            Assert.True(enemy.Speed >= Constants.MinMovementSpeed && enemy.Speed <= Constants.MaxMovementSpeed);
        }
    }
}
