using BattleLogic;
using Microsoft.Extensions.Logging;
using NSubstitute;
using BattleLogic.Models;
using BattleLogic.Interfaces;
using Shared;

namespace Tests;

/// <summary>
/// Tests for BattleState
/// </summary>
public class BattleStateTests
{
    private readonly ILogger<BattleState> _logger;

    private readonly IBattleGroupContext _mockGroup;
    private readonly IBattleReplayStorage _mockReplayStorage;
    private readonly IBattleNotificationService _mockNotificationService;

    public BattleStateTests()
    {
        _logger = Substitute.For<ILogger<BattleState>>();

        var groupInfo = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        _mockGroup = Substitute.For<IBattleGroupContext>();
        _mockGroup.Id.Returns(groupInfo.Id);
        _mockGroup.Name.Returns(groupInfo.Name);
        _mockGroup.ConnectedCount.Returns(groupInfo.ConnectionCount);
        _mockGroup.MaxClients.Returns(groupInfo.MaxConnections);
        _mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        _mockReplayStorage = Substitute.For<IBattleReplayStorage>();
        _mockNotificationService = Substitute.For<IBattleNotificationService>();
    }
    [Fact]
    public void BattleState_ShouldInitialize_WithProvidedGroup()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();

        // Act
        var battleState = new BattleState(battleId, _mockGroup, _logger, _mockReplayStorage, _mockNotificationService);
        var status = battleState.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(battleId, status.BattleId);
        Assert.Equal(_mockGroup.ConnectedCount, status.Players.Count);
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
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(2);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2" });

        // Act
        var battleState = new BattleState(battleId, mockGroup, _logger, _mockReplayStorage, _mockNotificationService);
        var status = battleState.GetStatus();

        // Assert
        Assert.Equal(2, status.Players.Count);

        foreach (var player in status.Players)
        {
            Assert.Equal(EntityType.Player, player.Type);
            // Players have job modifiers applied, so we need to check wider ranges
            Assert.True(player.MaxHp >= 150 && player.MaxHp <= 700, $"Player HP {player.MaxHp} is outside expected range");
            Assert.Equal(player.MaxHp, player.CurrentHp); // Should start at full health
            Assert.True(player.Attack >= 15 && player.Attack <= 60, $"Player Attack {player.Attack} is outside expected range");
            Assert.True(player.Defense >= 0 && player.Defense <= 50, $"Player Defense {player.Defense} is outside expected range");
            Assert.True(player.Speed >= 1 && player.Speed <= 8, $"Player Speed {player.Speed} is outside expected range");
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
        var mockReplayStorage = Substitute.For<IBattleReplayStorage>();
        var mockNotificationService = Substitute.For<IBattleNotificationService>();
        var battleState = new BattleState(battleId, group, _logger, mockReplayStorage, mockNotificationService);
        var status = battleState.GetStatus();

        // Assert
        Assert.True(status.Enemies.Count >= BattleBasicDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleBasicDefines.MaxEnemyCount);

        foreach (var enemy in status.Enemies)
        {
            // EntityType is an enum, not a string, so we use enemy.Type directly
            Assert.True(enemy.Type == EntityType.Enemy);

            // Since EntityInfo.Type is EntityType (not EnemyType), we cannot directly check EnemyType ranges
            // We'll verify basic HP/Attack/Defense/Speed ranges instead
            Assert.True(enemy.CurrentHp >= 50); // Minimum possible HP after job modifiers
            Assert.True(enemy.CurrentHp <= 500); // Maximum possible HP after job modifiers
            Assert.True(enemy.Attack >= 1);
            Assert.True(enemy.Defense >= 1);
            Assert.True(enemy.Speed >= 1);
        }
    }
}
