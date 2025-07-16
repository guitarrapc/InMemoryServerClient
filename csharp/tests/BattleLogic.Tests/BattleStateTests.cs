using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Battle;

namespace BattleLogic.Tests;

/// <summary>
/// Tests for BattleState
/// </summary>
public class BattleStateTests
{
    private readonly ILogger<BattleState> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IBattleGroupContext _mockGroup;

    public BattleStateTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = Substitute.For<ILogger<BattleState>>();

        var groupInfo = new GroupInfo
        {
            GroupId = BattleSeed.NewTimestampId().ToString(), // Use GUID v7 for group ID
            Name = "test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        _mockGroup = Substitute.For<IBattleGroupContext>();
        _mockGroup.GroupId.Returns(groupInfo.GroupId);
        _mockGroup.Name.Returns(groupInfo.Name);
        _mockGroup.ConnectedCount.Returns(groupInfo.ConnectionCount);
        _mockGroup.MaxClients.Returns(groupInfo.MaxConnections);
        _mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });
    }

    [Fact]
    public void BattleState_ShouldInitialize_WithProvidedGroup()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var seed = 12345;

        // Act
        var battleState = TestHelpers.CreateBattleState(battleId, seed, _mockGroup, _logger, _loggerFactory);
        var status = battleState.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(battleId, status.BattleId);
        Assert.Equal(_mockGroup.ConnectedCount, status.Players.Count);
        Assert.True(status.Enemies.Count >= BattleSystemDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleSystemDefines.MaxEnemyCount);
        Assert.Equal(BattleSystemDefines.BattleFieldWidth, status.FieldWidth);
        Assert.Equal(BattleSystemDefines.BattleFieldHeight, status.FieldHeight);
        // Battle should not have a result when just initialized
        Assert.Null(status.IsPlayerVictory);
        Assert.True(status.IsInProgress);
    }

    [Fact]
    public async Task BattleState_AfterBattleCompletion_ShouldHaveVictoryResult()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId();
        var seed = 12345; // Use fixed seed for testing
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(5);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act
        var battleState = TestHelpers.CreateBattleState(battleId, seed, mockGroup, _logger, _loggerFactory);
        await battleState.RunBattleAsync();
        var status = battleState.GetStatus();
       
        // Assert
        Assert.False(status.IsInProgress); // Battle should be completed
        Assert.NotNull(status.IsPlayerVictory); // Should have a victory result
        Assert.True(status.IsPlayerVictory == true || status.IsPlayerVictory == false); // Should be either true or false

        Console.WriteLine($"Battle resut, IsPlayerVictory: {status.IsPlayerVictory}, Players alive: {status.Players.Count(p => p.CurrentHp > 0)}, Enemies alive: {status.Enemies.Count(e => e.CurrentHp > 0)}, TotalTurns: {status.TotalTurns}");

        // Additional checks based on the result
        if (status.IsPlayerVictory == true)
        {
            Assert.True(status.Players.Any(p => p.CurrentHp > 0), "If players won, at least one player should be alive");
            Assert.True(status.Enemies.All(e => e.CurrentHp <= 0), "If players won, all enemies should be defeated");
        }
        else
        {
            Assert.True(status.Players.All(p => p.CurrentHp <= 0), "If players lost, all players should be defeated");
        }

        // Clean up memory
        battleState.ClearBattleData();
    }

    [Fact]
    public void BattleStatus_ShouldHaveValidPlayers()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var seed = 12345; // Use fixed seed for testing
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(2);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2" });

        // Act
        var battleState = TestHelpers.CreateBattleState(battleId, seed, mockGroup, _logger, _loggerFactory);
        var status = battleState.GetStatus();

        // Assert
        Assert.Equal(2, status.Players.Count);

        foreach (var player in status.Players)
        {
            Assert.True(player.Type.IsPlayer);
            // Players have job modifiers applied, so we need to check wider ranges
            Assert.True(player.MaxHp >= 150 && player.MaxHp <= 1000, $"Player HP {player.MaxHp} is outside expected range");
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
        var battleId = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var seed = 12345; // Use fixed seed for testing
        var group = new GroupInfo
        {
            GroupId = BattleSeed.NewTimestampId().ToString(), // Use GUID v7 for group ID
            Name = "test_group",
            ConnectionCount = 1,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act
        var battleState = TestHelpers.CreateBattleState(battleId, seed, group, _logger, _loggerFactory);
        var status = battleState.GetStatus();

        // Assert
        Assert.True(status.Enemies.Count >= BattleSystemDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleSystemDefines.MaxEnemyCount);

        foreach (var enemy in status.Enemies)
        {
            // Check that enemy is indeed an enemy with specific size
            Assert.True(enemy.Type.IsEnemy);
            Assert.True(enemy.Type.EnemySize.HasValue);
            Assert.True(enemy.Type.EnemySize == EnemySize.Small ||
                       enemy.Type.EnemySize == EnemySize.Medium ||
                       enemy.Type.EnemySize == EnemySize.Large);

            // Verify basic HP/Attack/Defense/Speed ranges
            Assert.True(enemy.CurrentHp >= 8); // Minimum possible HP after job modifiers (Small + Assassin worst case)
            Assert.True(enemy.CurrentHp <= 600); // Maximum possible HP after job modifiers (Large + Guardian worst case)
            Assert.True(enemy.Attack >= 1, $"Enemy {enemy.Name} has Attack={enemy.Attack}, Job={enemy.EnemyJob}, Type={enemy.Type}");
            Assert.True(enemy.Defense >= 0); // Defense can be 0 after modifiers
            Assert.True(enemy.Speed >= 1);
        }
    }

    [Fact]
    public void BattleState_SameBattleId_ShouldProduceIdenticalResults()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var seed = 12345; // Use fixed seed for testing
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(5);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act - Create two battles with the same battleId
        var battle1 = TestHelpers.CreateBattleState(battleId, seed, mockGroup, _logger, _loggerFactory);
        var battle2 = TestHelpers.CreateBattleState(battleId, seed, mockGroup, _logger, _loggerFactory);

        var status1 = battle1.GetStatus();
        var status2 = battle2.GetStatus();

        // Assert - Both battles should produce identical results
        Assert.Equal(status1.Players.Count, status2.Players.Count);
        Assert.Equal(status1.Enemies.Count, status2.Enemies.Count);

        // Verify all players are identical
        for (int i = 0; i < status1.Players.Count; i++)
        {
            var player1 = status1.Players[i];
            var player2 = status2.Players[i];

            Assert.Equal(player1.EntityId, player2.EntityId); // Entity IDs should be identical
            Assert.Equal(player1.Name, player2.Name);
            Assert.Equal(player1.PlayerJob, player2.PlayerJob);
            Assert.Equal(player1.MaxHp, player2.MaxHp);
            Assert.Equal(player1.CurrentHp, player2.CurrentHp);
            Assert.Equal(player1.Attack, player2.Attack);
            Assert.Equal(player1.Defense, player2.Defense);
            Assert.Equal(player1.Speed, player2.Speed);
            Assert.Equal(player1.Accuracy, player2.Accuracy);
            Assert.Equal(player1.Evasion, player2.Evasion);
        }

        // Verify all enemies are identical
        for (int i = 0; i < status1.Enemies.Count; i++)
        {
            var enemy1 = status1.Enemies[i];
            var enemy2 = status2.Enemies[i];

            Assert.Equal(enemy1.EntityId, enemy2.EntityId); // Entity IDs should be identical
            Assert.Equal(enemy1.Name, enemy2.Name);
            Assert.Equal(enemy1.EnemyJob, enemy2.EnemyJob);
            Assert.Equal(enemy1.MaxHp, enemy2.MaxHp);
            Assert.Equal(enemy1.CurrentHp, enemy2.CurrentHp);
            Assert.Equal(enemy1.Attack, enemy2.Attack);
            Assert.Equal(enemy1.Defense, enemy2.Defense);
            Assert.Equal(enemy1.Speed, enemy2.Speed);
            Assert.Equal(enemy1.Accuracy, enemy2.Accuracy);
            Assert.Equal(enemy1.Evasion, enemy2.Evasion);
        }

        // Verify battle seeds are identical (user seed and deterministic seed should be same)
        Assert.Equal(battle1.BattleSeed.UserSeed, battle2.BattleSeed.UserSeed);
        Assert.Equal(battle1.BattleSeed.DeterministicSeed, battle2.BattleSeed.DeterministicSeed);
    }

    [Fact]
    public void BattleState_DifferentBattleIds_ShouldProduceDifferentResults()
    {
        // Arrange
        var battleId1 = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var battleId2 = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var seed = 12345; // Use fixed seed for testing
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(5);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act
        var battle1 = TestHelpers.CreateBattleState(battleId1, seed, mockGroup, _logger, _loggerFactory);
        var battle2 = TestHelpers.CreateBattleState(battleId2, seed, mockGroup, _logger, _loggerFactory);

        var status1 = battle1.GetStatus();
        var status2 = battle2.GetStatus();

        // Assert - Different battleIds should produce different deterministic seeds even with same user seed
        Assert.Equal(battle1.BattleSeed.UserSeed, battle2.BattleSeed.UserSeed); // Same user seed
        Assert.NotEqual(battle1.BattleSeed.DeterministicSeed, battle2.BattleSeed.DeterministicSeed); // Different deterministic seed

        // While counts might be the same due to random distribution,
        // at least some entities should be different
        bool hasDifferentPlayer = false;
        bool hasDifferentEnemy = false;

        // Check if any players are different
        for (int i = 0; i < Math.Min(status1.Players.Count, status2.Players.Count); i++)
        {
            var player1 = status1.Players[i];
            var player2 = status2.Players[i];

            if (player1.EntityId != player2.EntityId ||
                player1.PlayerJob != player2.PlayerJob ||
                player1.MaxHp != player2.MaxHp ||
                player1.Attack != player2.Attack ||
                player1.Defense != player2.Defense ||
                player1.Speed != player2.Speed ||
                player1.Accuracy != player2.Accuracy ||
                player1.Evasion != player2.Evasion)
            {
                hasDifferentPlayer = true;
                break;
            }
        }

        // Check if any enemies are different
        for (int i = 0; i < Math.Min(status1.Enemies.Count, status2.Enemies.Count); i++)
        {
            var enemy1 = status1.Enemies[i];
            var enemy2 = status2.Enemies[i];

            if (enemy1.EntityId != enemy2.EntityId ||
                enemy1.EnemyJob != enemy2.EnemyJob ||
                enemy1.MaxHp != enemy2.MaxHp ||
                enemy1.Attack != enemy2.Attack ||
                enemy1.Defense != enemy2.Defense ||
                enemy1.Speed != enemy2.Speed ||
                enemy1.Accuracy != enemy2.Accuracy ||
                enemy1.Evasion != enemy2.Evasion)
            {
                hasDifferentEnemy = true;
                break;
            }
        }

        // At least one should be different (either players or enemies or both)
        Assert.True(hasDifferentPlayer || hasDifferentEnemy,
            "Different battle IDs should produce at least some different entities");
    }

    [Fact]
    public async Task BattleState_SameBattleId_ShouldProduceIdenticalBattleExecution()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var seed = 12345; // Use fixed seed for testing
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(5);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act - Execute full battles with the same battleId
        var battle1 = TestHelpers.CreateBattleState(battleId, seed, mockGroup, _logger, _loggerFactory);
        var battle2 = TestHelpers.CreateBattleState(battleId, seed, mockGroup, _logger, _loggerFactory);

        // Execute the battles to completion
        await battle1.RunBattleAsync();
        await battle2.RunBattleAsync();

        var allTurnData1 = battle1.GetAllTurnData();
        var allTurnData2 = battle2.GetAllTurnData();

        // Assert - Both battles should produce identical execution results
        Assert.Equal(allTurnData1.Count, allTurnData2.Count);

        // Verify each turn is identical
        for (int turnIndex = 0; turnIndex < allTurnData1.Count; turnIndex++)
        {
            var turn1 = allTurnData1[turnIndex];
            var turn2 = allTurnData2[turnIndex];

            Assert.Equal(turn1.CurrentTurn, turn2.CurrentTurn);
            Assert.Equal(turn1.IsInProgress, turn2.IsInProgress);

            // Verify player states are identical for each turn
            Assert.Equal(turn1.Players.Count, turn2.Players.Count);
            for (int playerIndex = 0; playerIndex < turn1.Players.Count; playerIndex++)
            {
                var player1 = turn1.Players[playerIndex];
                var player2 = turn2.Players[playerIndex];

                Assert.Equal(player1.EntityId, player2.EntityId);
                Assert.Equal(player1.CurrentHp, player2.CurrentHp);
                Assert.Equal(player1.Position.X, player2.Position.X);
                Assert.Equal(player1.Position.Y, player2.Position.Y);
                Assert.Equal(player1.CurrentHp > 0, player2.CurrentHp > 0); // IsAlive equivalent
            }

            // Verify enemy states are identical for each turn
            Assert.Equal(turn1.Enemies.Count, turn2.Enemies.Count);
            for (int enemyIndex = 0; enemyIndex < turn1.Enemies.Count; enemyIndex++)
            {
                var enemy1 = turn1.Enemies[enemyIndex];
                var enemy2 = turn2.Enemies[enemyIndex];

                Assert.Equal(enemy1.EntityId, enemy2.EntityId);
                Assert.Equal(enemy1.CurrentHp, enemy2.CurrentHp);
                Assert.Equal(enemy1.Position.X, enemy2.Position.X);
                Assert.Equal(enemy1.Position.Y, enemy2.Position.Y);
                Assert.Equal(enemy1.CurrentHp > 0, enemy2.CurrentHp > 0); // IsAlive equivalent
            }
        }

        // Verify final battle results are identical
        var finalStatus1 = battle1.GetStatus();
        var finalStatus2 = battle2.GetStatus();
        Assert.Equal(finalStatus1.IsInProgress, finalStatus2.IsInProgress);
        Assert.Equal(finalStatus1.TotalTurns, finalStatus2.TotalTurns);
        Assert.Equal(finalStatus1.IsPlayerVictory, finalStatus2.IsPlayerVictory); // Battle outcomes should be identical
    }

    [Theory]
    [InlineData("0197e9ec-f33e-7787-9d91-c6a45876776e", "0197e9ec-f33e-7787-9d91-c6a45876776e", true)]   // Same battleId and seed should be identical
    [InlineData("0197e9ed-252a-7def-8a06-472cda6cf1e1", "0197e9ed-3be4-7096-98db-47a929334f06", false)]   // Different battleId should be different even with same seed
    public void BattleState_SeedReproducibility_ShouldMatchExpectedBehavior(
        Guid battleId1, Guid battleId2, bool shouldBeIdentical)
    {
        // Arrange
        var seed = 12345; // Use fixed seed for testing
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(5);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act
        var battle1 = TestHelpers.CreateBattleState(battleId1, seed, mockGroup, _logger, _loggerFactory);
        var battle2 = TestHelpers.CreateBattleState(battleId2, seed, mockGroup, _logger, _loggerFactory);

        var status1 = battle1.GetStatus();
        var status2 = battle2.GetStatus();

        // Assert
        if (shouldBeIdentical)
        {
            // Same seeds should produce identical entity arrangements
            Assert.Equal(status1.Players.Count, status2.Players.Count);
            Assert.Equal(status1.Enemies.Count, status2.Enemies.Count);

            // Verify entity IDs are identical
            for (int i = 0; i < status1.Players.Count; i++)
            {
                Assert.Equal(status1.Players[i].EntityId, status2.Players[i].EntityId);
            }
            for (int i = 0; i < status1.Enemies.Count; i++)
            {
                Assert.Equal(status1.Enemies[i].EntityId, status2.Enemies[i].EntityId);
            }
        }
        else
        {
            // Different seeds should produce different entity IDs
            bool playersAreDifferent = false;
            bool enemiesAreDifferent = false;

            for (int i = 0; i < Math.Min(status1.Players.Count, status2.Players.Count); i++)
            {
                if (status1.Players[i].EntityId != status2.Players[i].EntityId)
                {
                    playersAreDifferent = true;
                    break;
                }
            }

            for (int i = 0; i < Math.Min(status1.Enemies.Count, status2.Enemies.Count); i++)
            {
                if (status1.Enemies[i].EntityId != status2.Enemies[i].EntityId)
                {
                    enemiesAreDifferent = true;
                    break;
                }
            }

            // At least one should be different (players or enemies)
            Assert.True(playersAreDifferent || enemiesAreDifferent,
                "Different battle IDs should produce different entity arrangements");
        }
    }

    [Fact]
    public void BattleState_SameBattleIdDifferentSeed_ShouldProduceDifferentResults()
    {
        // Arrange
        var battleId = Guid.Parse("0197e9ec-f33e-7787-9d91-c6a45876776e");
        var seed1 = 12345;
        var seed2 = 54321;
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.ConnectedCount.Returns(5);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act
        var battle1 = TestHelpers.CreateBattleState(battleId, seed1, mockGroup, _logger, _loggerFactory);
        var battle2 = TestHelpers.CreateBattleState(battleId, seed2, mockGroup, _logger, _loggerFactory);

        var status1 = battle1.GetStatus();
        var status2 = battle2.GetStatus();

        // Assert - Same battleId but different user seeds should produce different deterministic seeds
        Assert.Equal(battle1.BattleSeed.BattleId, battle2.BattleSeed.BattleId); // Same battle ID
        Assert.NotEqual(battle1.BattleSeed.UserSeed, battle2.BattleSeed.UserSeed); // Different user seed
        Assert.NotEqual(battle1.BattleSeed.DeterministicSeed, battle2.BattleSeed.DeterministicSeed); // Different deterministic seed

        // Results should be different due to different deterministic seeds
        Assert.Equal(status1.Players.Count, status2.Players.Count); // Same number of players (5)
        // Enemy count may be different due to different random seeds - this is expected
        Assert.True(status1.Enemies.Count >= BattleSystemDefines.MinEnemyCount);
        Assert.True(status1.Enemies.Count <= BattleSystemDefines.MaxEnemyCount);
        Assert.True(status2.Enemies.Count >= BattleSystemDefines.MinEnemyCount);
        Assert.True(status2.Enemies.Count <= BattleSystemDefines.MaxEnemyCount);

        // At least some entity IDs should be different
        bool anyPlayerDifferent = false;
        for (int i = 0; i < status1.Players.Count; i++)
        {
            if (status1.Players[i].EntityId != status2.Players[i].EntityId)
            {
                anyPlayerDifferent = true;
                break;
            }
        }
        Assert.True(anyPlayerDifferent, "At least some player entity IDs should be different with different seeds");
    }

    [Fact]
    public void BattleSeed_CombinedSeedGeneration_ShouldBeConsistent()
    {
        // Arrange
        var battleId = Guid.Parse("0197e9ec-f33e-7787-9d91-c6a45876776e");
        var userSeed = 12345;

        // Act - Create multiple instances with same parameters
        var seed1 = new BattleSeed(battleId, userSeed);
        var seed2 = new BattleSeed(battleId, userSeed);

        // Assert - Should have identical deterministic seeds
        Assert.Equal(seed1.BattleId, seed2.BattleId);
        Assert.Equal(seed1.UserSeed, seed2.UserSeed);
        Assert.Equal(seed1.DeterministicSeed, seed2.DeterministicSeed);

        // Entity IDs should be identical when generated in same order
        var entityIds1 = new List<Guid>();
        var entityIds2 = new List<Guid>();

        for (int i = 0; i < 5; i++)
        {
            entityIds1.Add(seed1.NextEntityId());
            entityIds2.Add(seed2.NextEntityId());
        }

        Assert.Equal(entityIds1, entityIds2);
    }
}
