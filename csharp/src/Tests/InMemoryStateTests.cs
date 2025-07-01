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
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(battleId, status.BattleId);
        Assert.Equal(group.ConnectionCount, status.Players.Count);
        Assert.True(status.Enemies.Count >= BattleBasicDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleBasicDefines.MaxEnemyCount);
        Assert.Equal(BattleBasicDefines.BattleFieldWidth, status.FieldWidth);
        Assert.Equal(BattleBasicDefines.BattleFieldHeight, status.FieldHeight);
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
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.Equal(2, status.Players.Count);

        foreach (var player in status.Players)
        {
            Assert.Equal("Player", player.Type);
            Assert.True(player.MaxHp >= BattleBasicDefines.PlayerHp.Min && player.MaxHp <= BattleBasicDefines.PlayerHp.Max);
            Assert.Equal(player.MaxHp, player.CurrentHp); // Should start at full health
            Assert.True(player.Attack >= BattleBasicDefines.PlayerAttackPower.Min && player.Attack <= BattleBasicDefines.PlayerAttackPower.Max);
            Assert.True(player.Defense >= BattleBasicDefines.PlayerDefencePower.Min && player.Defense <= BattleBasicDefines.PlayerDefencePower.Max);
            Assert.True(player.Speed >= BattleBasicDefines.PlayerMoveSpeed.Min && player.Speed <= BattleBasicDefines.PlayerMoveSpeed.Max);
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
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.True(status.Enemies.Count >= BattleBasicDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleBasicDefines.MaxEnemyCount);

        foreach (var enemy in status.Enemies)
        {
            var enemyType = Enum.Parse<EnemyType>(enemy.Type);
            Assert.True(BattleBasicDefines.EnemyHpByType.ContainsKey(enemyType));

            Assert.True(enemy.MaxHp >= BattleBasicDefines.EnemyHpByType[enemyType].Min && enemy.MaxHp <= BattleBasicDefines.EnemyHpByType[enemyType].Max);
            Assert.Equal(enemy.MaxHp, enemy.CurrentHp); // Should start at full health
            Assert.True(enemy.Attack >= BattleBasicDefines.EnemyAttackPower[enemyType].Min && enemy.Attack <= BattleBasicDefines.EnemyAttackPower[enemyType].Max);
            Assert.True(enemy.Defense >= BattleBasicDefines.EnemyDefencePower[enemyType].Min - 2 && enemy.Defense <= BattleBasicDefines.EnemyDefencePower[enemyType].Max);
            Assert.True(enemy.Speed >= BattleBasicDefines.EnemyMoveSpeed[enemyType].Min && enemy.Speed <= BattleBasicDefines.EnemyMoveSpeed[enemyType].Max);
        }
    }
}
