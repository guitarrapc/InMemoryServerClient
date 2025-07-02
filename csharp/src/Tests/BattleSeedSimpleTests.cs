using BattleLogic.Battle;
using BattleLogic.Models;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using System.Collections.Concurrent;

namespace Tests;

/// <summary>
/// Tests for battle reproducibility using battleId-based seeds (New Design)
/// </summary>
public class BattleSeedBattleIdTests
{
    private readonly ILogger<BattleState> _logger;

    public BattleSeedBattleIdTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<BattleState>();
    }

    [Fact]
    public void SameBattleId_ShouldProduceSameResults()
    {
        // Arrange
        const string testBattleId = "test-battle-12345";
        var mockGroup = new MockBattleGroupContext();

        // Act - Create two battles with the same battleId
        var battle1 = new BattleState(testBattleId, mockGroup, _logger);
        var battle2 = new BattleState(testBattleId, mockGroup, _logger);

        // Assert - Both battles should have the same seed
        Assert.Equal(battle1.BattleSeed.Seed, battle2.BattleSeed.Seed);
    }

    [Fact]
    public void DifferentBattleIds_ShouldProduceDifferentResults()
    {
        // Arrange
        const string battleId1 = "test-battle-12345";
        const string battleId2 = "test-battle-67890";
        var mockGroup = new MockBattleGroupContext();

        // Act
        var battle1 = new BattleState(battleId1, mockGroup, _logger);
        var battle2 = new BattleState(battleId2, mockGroup, _logger);

        // Assert - Different battle IDs should produce different seeds
        Assert.NotEqual(battle1.BattleSeed.Seed, battle2.BattleSeed.Seed);
    }

    [Fact]
    public void BattleSeed_WithBattleId_ShouldBeDeterministic()
    {
        // Arrange
        const string battleId = "test-battle-deterministic";

        // Act - Create multiple BattleSeeds with the same battleId
        var seed1 = new BattleSeed(battleId);
        var seed2 = new BattleSeed(battleId);
        var seed3 = new BattleSeed(battleId);

        // Assert - All should have the same seed value
        Assert.Equal(seed1.Seed, seed2.Seed);
        Assert.Equal(seed2.Seed, seed3.Seed);
        Assert.Equal(seed1.Seed, seed3.Seed);
    }

    [Fact]
    public void BattleSeed_WithEmptyBattleId_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BattleSeed(""));
        Assert.Throws<ArgumentException>(() => new BattleSeed((string)null!));
    }

    [Fact]
    public void BattleSeed_SameBattleId_ShouldGenerateSameGuids()
    {
        // Arrange
        const string battleId = "test-battle-guid-consistency";
        var seed1 = new BattleSeed(battleId);
        var seed2 = new BattleSeed(battleId);

        // Act - Generate multiple GUIDs
        var guids1 = new[] { seed1.NextGuid(), seed1.NextGuid(), seed1.NextGuid() };
        var guids2 = new[] { seed2.NextGuid(), seed2.NextGuid(), seed2.NextGuid() };

        // Assert - Same battleId should produce same GUID sequence
        for (int i = 0; i < guids1.Length; i++)
        {
            Assert.Equal(guids1[i], guids2[i]);
        }
    }

    [Fact]
    public void BattleSeed_DifferentBattleIds_ShouldGenerateDifferentGuids()
    {
        // Arrange
        const string battleId1 = "test-battle-guid-1";
        const string battleId2 = "test-battle-guid-2";
        var seed1 = new BattleSeed(battleId1);
        var seed2 = new BattleSeed(battleId2);

        // Act
        var guid1 = seed1.NextGuid();
        var guid2 = seed2.NextGuid();

        // Assert - Different battleIds should produce different GUIDs
        Assert.NotEqual(guid1, guid2);
    }

    [Fact]
    public void BattleState_SameBattleId_ShouldProduceSameInitialSetup()
    {
        // Arrange
        const string battleId = "test-initial-setup";
        var mockGroup = new MockBattleGroupContext();

        // Act
        var battle1 = new BattleState(battleId, mockGroup, _logger);
        var battle2 = new BattleState(battleId, mockGroup, _logger);

        var status1 = battle1.GetStatus();
        var status2 = battle2.GetStatus();

        // Assert - Same battleId should produce identical initial setup
        Assert.Equal(status1.Players.Count, status2.Players.Count);
        Assert.Equal(status1.Enemies.Count, status2.Enemies.Count);

        // Compare basic properties
        Assert.Equal(status1.CurrentTurn, status2.CurrentTurn);
        Assert.Equal(status1.IsInProgress, status2.IsInProgress);
    }

    [Fact]
    public void BattleState_DifferentBattleIds_MayProduceDifferentSetup()
    {
        // Arrange
        const string battleId1 = "test-setup-1";
        const string battleId2 = "test-setup-2";
        var mockGroup = new MockBattleGroupContext();

        // Act
        var battle1 = new BattleState(battleId1, mockGroup, _logger);
        var battle2 = new BattleState(battleId2, mockGroup, _logger);

        // Assert - Different battleIds may produce different setups
        // Note: We don't assert inequality because the setup could theoretically be the same
        // but the seed should definitely be different
        Assert.NotEqual(battle1.BattleSeed.Seed, battle2.BattleSeed.Seed);
    }

    /// <summary>
    /// Mock implementation for testing
    /// </summary>
    private class MockBattleGroupContext : IBattleGroupContext
    {
        public string Id => "test-group";
        public string Name => "Test Group";
        public IReadOnlyList<string> ClientIds => new List<string> { "client1", "client2", "client3", "client4", "client5" };
        public int ConnectedCount => ClientIds.Count;
        public int MaxClients => 5;
        public DateTime CreatedAt => DateTime.UtcNow;

        public Task SendToAllAsync(string method, object? arg1 = null, object? arg2 = null, object? arg3 = null)
        {
            return Task.CompletedTask;
        }

        public Task SendToAllAsync(object message)
        {
            return Task.CompletedTask;
        }
    }
}
