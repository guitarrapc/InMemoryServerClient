using InMemoryServer;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;

namespace Tests;

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
            // Players have job modifiers applied, so we need to check wider ranges
            Assert.True(player.MaxHp >= 200 && player.MaxHp <= 700, $"Player HP {player.MaxHp} is outside expected range");
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
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.True(status.Enemies.Count >= BattleBasicDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleBasicDefines.MaxEnemyCount);

        foreach (var enemy in status.Enemies)
        {
            var enemyType = Enum.Parse<EnemyType>(enemy.Type);
            Assert.True(BattleBasicDefines.EnemyHpByType.ContainsKey(enemyType));

            // Enemies also have job modifiers applied, so we check wider ranges
            var baseHpRange = BattleBasicDefines.EnemyHpByType[enemyType];
            var baseAttackRange = BattleBasicDefines.EnemyAttackPower[enemyType];
            var baseDefenseRange = BattleBasicDefines.EnemyDefencePower[enemyType];
            var baseSpeedRange = BattleBasicDefines.EnemyMoveSpeed[enemyType];

            // Job modifiers can multiply values by up to 1.4x and add up to +100, or reduce by 0.6x and subtract up to -30
            var expectedMinHp = Math.Max(1, (int)(baseHpRange.Min * 0.6f - 30));
            var expectedMaxHp = (int)(baseHpRange.Max * 1.4f + 100);
            var expectedMinAttack = Math.Max(1, (int)(baseAttackRange.Min * 0.6f - 5));
            var expectedMaxAttack = (int)(baseAttackRange.Max * 1.4f + 10);
            var expectedMinDefense = Math.Max(0, (int)(baseDefenseRange.Min * 0.6f - 5));
            var expectedMaxDefense = (int)(baseDefenseRange.Max * 1.6f + 10);
            var expectedMinSpeed = Math.Max(1, (int)(baseSpeedRange.Min * 0.6f - 1));
            var expectedMaxSpeed = (int)(baseSpeedRange.Max * 1.5f + 1);

            Assert.True(enemy.MaxHp >= expectedMinHp && enemy.MaxHp <= expectedMaxHp,
                $"Enemy HP {enemy.MaxHp} is outside expected range [{expectedMinHp}-{expectedMaxHp}]");
            Assert.Equal(enemy.MaxHp, enemy.CurrentHp); // Should start at full health
            Assert.True(enemy.Attack >= expectedMinAttack && enemy.Attack <= expectedMaxAttack,
                $"Enemy Attack {enemy.Attack} is outside expected range [{expectedMinAttack}-{expectedMaxAttack}]");
            Assert.True(enemy.Defense >= expectedMinDefense && enemy.Defense <= expectedMaxDefense,
                $"Enemy Defense {enemy.Defense} is outside expected range [{expectedMinDefense}-{expectedMaxDefense}]");
            Assert.True(enemy.Speed >= expectedMinSpeed && enemy.Speed <= expectedMaxSpeed,
                $"Enemy Speed {enemy.Speed} is outside expected range [{expectedMinSpeed}-{expectedMaxSpeed}]");
        }
    }
}
