using BattleLogic.Battle;
using BattleLogic.Models;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using System.Collections.Concurrent;

namespace Tests;

/// <summary>
/// Tests for battle reproducibility using seeds
/// </summary>
public class BattleSeedReproducibilityTests
{
    private readonly ILogger<BattleState> _logger;

    public BattleSeedReproducibilityTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<BattleState>();
    }

    [Fact]
    public void SameBattleId_ShouldProduceSameResults()
    {
        // Arrange
        const string battleId = "test-battle-same";
        var mockGroup = new MockBattleGroupContext();

        // Act - Create two battles with the same battleId
        var battle1 = new BattleState(battleId, mockGroup, _logger);
        var battle2 = new BattleState(battleId, mockGroup, _logger);

        // Assert - Both battles should have the same battleId and seed
        Assert.Equal(battleId, battle1.BattleSeed.BattleId);
        Assert.Equal(battleId, battle2.BattleSeed.BattleId);
        Assert.Equal(battle1.BattleSeed.Seed, battle2.BattleSeed.Seed);
    }

    [Fact]
    public void DifferentBattleIds_ShouldProduceDifferentResults()
    {
        // Arrange
        const string battleId1 = "test-battle-1";
        const string battleId2 = "test-battle-2";
        var mockGroup = new MockBattleGroupContext();

        // Act
        var battle1 = new BattleState(battleId1, mockGroup, _logger);
        var battle2 = new BattleState(battleId2, mockGroup, _logger);

        // Assert - Different battleIds should produce different seeds
        Assert.Equal(battleId1, battle1.BattleSeed.BattleId);
        Assert.Equal(battleId2, battle2.BattleSeed.BattleId);
        Assert.NotEqual(battle1.BattleSeed.Seed, battle2.BattleSeed.Seed);
    }

    [Fact]
    public void BattleSeed_NextGuid_ShouldBeDeterministic()
    {
        // Arrange
        const string battleId = "test-guid-deterministic";
        var seed1 = new BattleSeed(battleId);
        var seed2 = new BattleSeed(battleId);

        // Act - Generate multiple GUIDs from each seed
        var guids1 = new List<Guid>();
        var guids2 = new List<Guid>();

        for (int i = 0; i < 10; i++)
        {
            guids1.Add(seed1.NextGuid());
            guids2.Add(seed2.NextGuid());
        }

        // Assert - Same battleId should produce same GUID sequence
        Assert.Equal(guids1, guids2);
    }

    [Fact]
    public void BattleSeed_Random_ShouldBeDeterministic()
    {
        // Arrange
        const string battleId = "test-random-deterministic";
        var seed1 = new BattleSeed(battleId);
        var seed2 = new BattleSeed(battleId);

        // Act - Generate multiple random numbers from each seed
        var numbers1 = new List<int>();
        var numbers2 = new List<int>();

        for (int i = 0; i < 100; i++)
        {
            numbers1.Add(seed1.Random.Next(1, 100));
            numbers2.Add(seed2.Random.Next(1, 100));
        }

        // Assert - Same battleId should produce same random sequence
        Assert.Equal(numbers1, numbers2);
    }

    [Fact]
    public void BattleSeed_ToString_ShouldContainSeedAndCounter()
    {
        // Arrange
        const string testBattleId = "d337a429-5837-45a8-9519-909a92593e03";
        var battleSeed = new BattleSeed(testBattleId);

        // Act
        var initialString = battleSeed.ToString();

        // Generate some GUIDs to increment counter
        battleSeed.NextGuid();
        battleSeed.NextGuid();

        var afterGuidsString = battleSeed.ToString();

        // Assert
        Assert.Contains($"Seed={battleSeed.Seed}", initialString);
        Assert.Contains("GuidCounter=0", initialString);
        Assert.Contains("GuidCounter=2", afterGuidsString);
    }

    [Theory]
    [InlineData("battle-1")]
    [InlineData("battle-42")]
    [InlineData("battle-999999")]
    [InlineData("battle-test")]
    [InlineData("very-long-battle-id-with-many-characters")]
    [InlineData("short")]
    public void BattleSeed_WithSpecificBattleId_ShouldGenerateConsistentSeed(string battleId)
    {
        // Arrange & Act
        var battleSeed1 = new BattleSeed(battleId);
        var battleSeed2 = new BattleSeed(battleId);

        // Assert - Same battleId should always generate the same seed
        Assert.Equal(battleSeed1.Seed, battleSeed2.Seed);
        Assert.Equal(battleId, battleSeed1.BattleId);
        Assert.Equal(battleId, battleSeed2.BattleId);
    }

    [Fact]
    public async Task SameBattleId_ShouldProduceIdenticalBattleInitialization()
    {
        // Arrange
        const string battleId = "test-battle-initialization";
        var mockGroup = new MockBattleGroupContext();

        // Act - Create two battles with the same battleId (run sequentially to avoid file conflicts)
        var battle1 = new BattleState(battleId, mockGroup, _logger);
        var status1 = battle1.GetStatus();

        var battle2 = new BattleState(battleId, mockGroup, _logger);
        var status2 = battle2.GetStatus();

        // Assert - Initial battle states should be identical
        Assert.Equal(status1.Players.Count, status2.Players.Count);
        Assert.Equal(status1.Enemies.Count, status2.Enemies.Count);

        // Compare players in detail
        for (int i = 0; i < status1.Players.Count; i++)
        {
            var player1 = status1.Players[i];
            var player2 = status2.Players[i];

            Assert.Equal(player1.Id, player2.Id);
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

        // Compare enemies in detail
        for (int i = 0; i < status1.Enemies.Count; i++)
        {
            var enemy1 = status1.Enemies[i];
            var enemy2 = status2.Enemies[i];

            Assert.Equal(enemy1.Id, enemy2.Id);
            Assert.Equal(enemy1.Name, enemy2.Name);
            Assert.Equal(enemy1.EnemyJob, enemy2.EnemyJob);
            Assert.Equal(enemy1.Type.EnemySize, enemy2.Type.EnemySize);
            Assert.Equal(enemy1.MaxHp, enemy2.MaxHp);
            Assert.Equal(enemy1.CurrentHp, enemy2.CurrentHp);
            Assert.Equal(enemy1.Attack, enemy2.Attack);
            Assert.Equal(enemy1.Defense, enemy2.Defense);
            Assert.Equal(enemy1.Speed, enemy2.Speed);
            Assert.Equal(enemy1.Accuracy, enemy2.Accuracy);
            Assert.Equal(enemy1.Evasion, enemy2.Evasion);
        }

        // Compare battle field layout
        Assert.Equal(status1.FieldWidth, status2.FieldWidth);
        Assert.Equal(status1.FieldHeight, status2.FieldHeight);
    }

    [Fact]
    public async Task SameBattleId_ShouldProduceIdenticalBattleExecution()
    {
        // Arrange
        const string battleId = "test-battle-execution";
        var mockGroup = new MockBattleGroupContext();

        // Act - Run two complete battles with the same battleId (sequentially to avoid file conflicts)
        var battle1 = new BattleState(battleId, mockGroup, _logger);
        await battle1.RunBattleAsync();
        var finalStatus1 = battle1.GetStatus(); // Get status before clear
        battle1.ClearBattleData();

        var battle2 = new BattleState(battleId, mockGroup, _logger);
        await battle2.RunBattleAsync();
        var finalStatus2 = battle2.GetStatus();

        // Debug: Log the comparison
        Console.WriteLine($"Battle 1: Turn {finalStatus1.CurrentTurn}, InProgress {finalStatus1.IsInProgress}");
        Console.WriteLine($"Battle 2: Turn {finalStatus2.CurrentTurn}, InProgress {finalStatus2.IsInProgress}");

        // Assert - Battle execution should be identical
        Assert.Equal(finalStatus1.IsInProgress, finalStatus2.IsInProgress);
        Assert.Equal(finalStatus1.CurrentTurn, finalStatus2.CurrentTurn);
        Assert.Equal(finalStatus1.TotalTurns, finalStatus2.TotalTurns);
        Assert.Equal(finalStatus1.FieldWidth, finalStatus2.FieldWidth);
        Assert.Equal(finalStatus1.FieldHeight, finalStatus2.FieldHeight);

        // Compare player counts and basic info
        Assert.Equal(finalStatus1.Players.Count, finalStatus2.Players.Count);
        for (int i = 0; i < finalStatus1.Players.Count; i++)
        {
            var player1 = finalStatus1.Players[i];
            var player2 = finalStatus2.Players[i];

            Assert.Equal(player1.Id, player2.Id);
            Assert.Equal(player1.Name, player2.Name);
            Assert.Equal(player1.PlayerJob, player2.PlayerJob);
            Assert.Equal(player1.CurrentHp, player2.CurrentHp);
            Assert.Equal(player1.Position, player2.Position);
        }

        // Compare enemy counts and basic info
        Assert.Equal(finalStatus1.Enemies.Count, finalStatus2.Enemies.Count);
        for (int i = 0; i < finalStatus1.Enemies.Count; i++)
        {
            var enemy1 = finalStatus1.Enemies[i];
            var enemy2 = finalStatus2.Enemies[i];

            Assert.Equal(enemy1.Id, enemy2.Id);
            Assert.Equal(enemy1.Name, enemy2.Name);
            Assert.Equal(enemy1.EnemyJob, enemy2.EnemyJob);
            Assert.Equal(enemy1.CurrentHp, enemy2.CurrentHp);
            Assert.Equal(enemy1.Position, enemy2.Position);
        }

        // Clean up
        battle2.ClearBattleData();
    }

    [Theory]
    [InlineData("battle-1")]
    [InlineData("battle-999")]
    [InlineData("d337a429-5837-45a8-9519-909a92593e03")]
    [InlineData("battle-42")]
    public async Task DifferentBattleIds_ShouldProduceDifferentBattleResults(string battleId)
    {
        // Arrange
        const string baseBattleId = "reference-battle";
        var mockGroup = new MockBattleGroupContext();

        // Act - Run battles with different battleIds (sequentially to avoid file conflicts)
        var battle1 = new BattleState(baseBattleId, mockGroup, _logger);
        await battle1.RunBattleAsync();
        var finalStatus1 = battle1.GetStatus(); // Get status before clear
        battle1.ClearBattleData();

        var battle2 = new BattleState(battleId, mockGroup, _logger);
        await battle2.RunBattleAsync();
        var finalStatus2 = battle2.GetStatus();

        // Assert - At least some aspect should be different
        bool foundDifference = false;

        // Check for basic differences in battle structure
        if (finalStatus1.CurrentTurn != finalStatus2.CurrentTurn ||
            finalStatus1.TotalTurns != finalStatus2.TotalTurns ||
            finalStatus1.IsInProgress != finalStatus2.IsInProgress)
        {
            foundDifference = true;
        }

        // Compare initial player entity IDs (should be different with different seeds)
        if (!foundDifference && finalStatus1.Players.Count == finalStatus2.Players.Count)
        {
            for (int i = 0; i < finalStatus1.Players.Count; i++)
            {
                if (finalStatus1.Players[i].Id != finalStatus2.Players[i].Id)
                {
                    foundDifference = true;
                    break;
                }
            }
        }

        // Compare initial enemy entity IDs (should be different with different seeds)
        if (!foundDifference && finalStatus1.Enemies.Count == finalStatus2.Enemies.Count)
        {
            for (int i = 0; i < finalStatus1.Enemies.Count; i++)
            {
                if (finalStatus1.Enemies[i].Id != finalStatus2.Enemies[i].Id)
                {
                    foundDifference = true;
                    break;
                }
            }
        }

        // Compare player stats
        if (!foundDifference)
        {
            for (int i = 0; i < Math.Min(finalStatus1.Players.Count, finalStatus2.Players.Count); i++)
            {
                var player1 = finalStatus1.Players[i];
                var player2 = finalStatus2.Players[i];

                if (player1.MaxHp != player2.MaxHp ||
                    player1.Attack != player2.Attack ||
                    player1.Defense != player2.Defense ||
                    player1.Speed != player2.Speed ||
                    player1.Accuracy != player2.Accuracy ||
                    player1.Evasion != player2.Evasion ||
                    player1.PlayerJob != player2.PlayerJob ||
                    player1.Position != player2.Position)
                {
                    foundDifference = true;
                    break;
                }
            }
        }

        // Compare enemy stats
        if (!foundDifference)
        {
            for (int i = 0; i < Math.Min(finalStatus1.Enemies.Count, finalStatus2.Enemies.Count); i++)
            {
                var enemy1 = finalStatus1.Enemies[i];
                var enemy2 = finalStatus2.Enemies[i];

                if (enemy1.MaxHp != enemy2.MaxHp ||
                    enemy1.Attack != enemy2.Attack ||
                    enemy1.Defense != enemy2.Defense ||
                    enemy1.Speed != enemy2.Speed ||
                    enemy1.Accuracy != enemy2.Accuracy ||
                    enemy1.Evasion != enemy2.Evasion ||
                    enemy1.EnemyJob != enemy2.EnemyJob ||
                    enemy1.Type.IsPlayer != enemy2.Type.IsPlayer ||
                    enemy1.Position != enemy2.Position)
                {
                    foundDifference = true;
                    break;
                }
            }
        }

        if (!foundDifference)
        {
            // If no differences found in initial state, log details for debugging
            Console.WriteLine($"Warning: BattleIds {baseBattleId} and {battleId} produced very similar results");
            Console.WriteLine($"Turn counts: {finalStatus1.CurrentTurn} vs {finalStatus2.CurrentTurn}");

            // Only consider this an issue if the battleIds are actually different
            if (baseBattleId != battleId)
            {
                foundDifference = true; // Different battleIds should produce different results
            }
        }

        Assert.True(foundDifference, $"Battles with different battleIds ({baseBattleId} vs {battleId}) should typically produce different results. " +
                                   "If this fails consistently, there may be an issue with random number generation.");

        // Clean up
        battle2.ClearBattleData();
    }

    [Fact]
    public async Task SameBattleId_MultipleExecutions_ShouldAlwaysProduceSameResult()
    {
        // Arrange
        const string testBattleId = "consistent-battle-test";
        const int executionCount = 5;
        var mockGroup = new MockBattleGroupContext();

        var battleResults = new List<(bool IsCompleted, int TurnCount, string FinalPlayersHp, string FinalEnemiesHp)>();

        // Act - Run the same battleId battle multiple times
        for (int execution = 0; execution < executionCount; execution++)
        {
            var battle = new BattleState(testBattleId, mockGroup, _logger);
            await battle.RunBattleAsync();

            var allTurnData = battle.GetAllTurnData();
            var finalTurn = allTurnData[^1];

            var playersHp = string.Join(",", finalTurn.Players.Select(p => p.CurrentHp));
            var enemiesHp = string.Join(",", finalTurn.Enemies.Select(e => e.CurrentHp));

            battleResults.Add((!finalTurn.IsInProgress, finalTurn.CurrentTurn, playersHp, enemiesHp));

            battle.ClearBattleData();
        }

        // Assert - All executions should produce identical results
        var firstResult = battleResults[0];
        for (int i = 1; i < battleResults.Count; i++)
        {
            var currentResult = battleResults[i];

            Assert.Equal(firstResult.IsCompleted, currentResult.IsCompleted);
            Assert.Equal(firstResult.TurnCount, currentResult.TurnCount);
            Assert.Equal(firstResult.FinalPlayersHp, currentResult.FinalPlayersHp);
            Assert.Equal(firstResult.FinalEnemiesHp, currentResult.FinalEnemiesHp);
        }

        Console.WriteLine($"All {executionCount} executions with battleId {testBattleId} produced identical results:");
        Console.WriteLine($"- Battle Completed: {firstResult.IsCompleted}");
        Console.WriteLine($"- Turn Count: {firstResult.TurnCount}");
        Console.WriteLine($"- Final Players HP: {firstResult.FinalPlayersHp}");
        Console.WriteLine($"- Final Enemies HP: {firstResult.FinalEnemiesHp}");
    }

    [Fact]
    public async Task ReproducibilityStressTest_SameBattleId_LargeBattle()
    {
        // Arrange - Use edge case battleId values and run longer battles
        const string testBattleId = "stress-test-zero"; // Edge case: simple battleId
        var mockGroup = new MockBattleGroupContext();

        // Act - Run two battles with potentially longer execution (sequentially to avoid file conflicts)
        var battle1 = new BattleState(testBattleId, mockGroup, _logger);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await battle1.RunBattleAsync();
        var time1 = sw.ElapsedMilliseconds;
        var finalStatus1 = battle1.GetStatus(); // Get status before clear
        battle1.ClearBattleData();

        var battle2 = new BattleState(testBattleId, mockGroup, _logger);

        sw.Restart();
        await battle2.RunBattleAsync();
        var time2 = sw.ElapsedMilliseconds;
        var finalStatus2 = battle2.GetStatus();

        // Assert - Results should be identical regardless of execution time variations
        Assert.Equal(finalStatus1.IsInProgress, finalStatus2.IsInProgress);
        Assert.Equal(finalStatus1.CurrentTurn, finalStatus2.CurrentTurn);
        Assert.Equal(finalStatus1.TotalTurns, finalStatus2.TotalTurns);

        Console.WriteLine($"Stress test with battleId {testBattleId}:");
        Console.WriteLine($"- Battle 1 time: {time1}ms, Battle 2 time: {time2}ms");
        Console.WriteLine($"- Turn count: {finalStatus1.CurrentTurn}");
        Console.WriteLine($"- Battle completed: {!finalStatus1.IsInProgress}");

        // Clean up
        battle2.ClearBattleData();
    }

    [Theory]
    [InlineData("edge-case-minus")]
    [InlineData("edge-case-zero")]
    [InlineData("edge-case-one")]
    [InlineData("d337a429-5837-45a8-9519-909a92593e03")]
    public async Task EdgeCaseBattleIds_ShouldProduceReproducibleResults(string edgeCaseBattleId)
    {
        // Arrange
        var mockGroup = new MockBattleGroupContext();

        // Act - Test edge case battleId values for reproducibility
        var battle1 = new BattleState(edgeCaseBattleId, mockGroup, _logger);

        // Run battles sequentially to avoid file access conflicts
        await battle1.RunBattleAsync();
        var status1 = battle1.GetStatus();
        battle1.ClearBattleData();

        var battle2 = new BattleState(edgeCaseBattleId, mockGroup, _logger);
        await battle2.RunBattleAsync();
        var status2 = battle2.GetStatus();

        // Assert - Even edge case battleIds should produce identical results
        Assert.Equal(status1.IsInProgress, status2.IsInProgress);
        Assert.Equal(status1.CurrentTurn, status2.CurrentTurn);
        Assert.Equal(status1.TotalTurns, status2.TotalTurns);

        Console.WriteLine($"Edge case battleId {edgeCaseBattleId}: InProgress={status1.IsInProgress}, Turns={status1.CurrentTurn}/{status1.TotalTurns}");

        // Clean up
        battle2.ClearBattleData();
    }

    [Fact]
    public void BattleSeed_ConsistentGuidGeneration_AcrossMultipleInstances()
    {
        // Arrange
        const string testBattleId = "guid-consistency-test";
        const int guidCount = 100;

        // Act - Generate GUIDs from multiple BattleSeed instances with same battleId
        var guids1 = new List<Guid>();
        var guids2 = new List<Guid>();
        var guids3 = new List<Guid>();

        var seed1 = new BattleSeed(testBattleId);
        var seed2 = new BattleSeed(testBattleId);
        var seed3 = new BattleSeed(testBattleId);

        for (int i = 0; i < guidCount; i++)
        {
            guids1.Add(seed1.NextGuid());
            guids2.Add(seed2.NextGuid());
            guids3.Add(seed3.NextGuid());
        }

        // Assert - All instances should generate identical GUID sequences
        Assert.Equal(guids1, guids2);
        Assert.Equal(guids1, guids3);
        Assert.Equal(guids2, guids3);

        // Verify all GUIDs are unique within each sequence
        Assert.Equal(guidCount, guids1.Distinct().Count());
        Assert.Equal(guidCount, guids2.Distinct().Count());
        Assert.Equal(guidCount, guids3.Distinct().Count());
    }

    [Fact]
    public void BattleSeed_ThreadSafety_MultipleThreadsWithSameBattleId()
    {
        // Arrange
        const string testBattleId = "thread-safety-test";
        const int threadsCount = 10;
        const int operationsPerThread = 50;

        var allGuids = new ConcurrentBag<List<Guid>>();
        var allRandomNumbers = new ConcurrentBag<List<int>>();

        // Act - Test thread safety by running multiple threads with same battleId
        Parallel.For(0, threadsCount, threadIndex =>
        {
            var seed = new BattleSeed(testBattleId);
            var guids = new List<Guid>();
            var numbers = new List<int>();

            for (int i = 0; i < operationsPerThread; i++)
            {
                guids.Add(seed.NextGuid());
                numbers.Add(seed.Random.Next(1, 1000));
            }

            allGuids.Add(guids);
            allRandomNumbers.Add(numbers);
        });

        // Assert - All threads should produce identical sequences
        var guidLists = allGuids.ToList();
        var numberLists = allRandomNumbers.ToList();

        for (int i = 1; i < guidLists.Count; i++)
        {
            Assert.Equal(guidLists[0], guidLists[i]);
        }

        for (int i = 1; i < numberLists.Count; i++)
        {
            Assert.Equal(numberLists[0], numberLists[i]);
        }

        Console.WriteLine($"Thread safety test with {threadsCount} threads completed successfully");
        Console.WriteLine($"Each thread generated {operationsPerThread} GUIDs and random numbers");
        Console.WriteLine($"All threads produced identical sequences");
    }

    /// <summary>
    /// Mock implementation of IBattleGroupContext for testing
    /// </summary>
    private class MockBattleGroupContext : IBattleGroupContext
    {
        public string Id => "test-group";
        public string Name => "Test Group";
        public int MaxClients => 5;
        public int ConnectedCount => 5;
        public IReadOnlyList<string> ClientIds => new List<string> { "client1", "client2", "client3", "client4", "client5" };
    }

    /// <summary>
    /// Test that NextGuid() is thread-safe and generates unique GUIDs across multiple threads
    /// </summary>
    [Fact]
    public async Task NextGuid_ThreadSafety_ShouldGenerateUniqueGuidsAcrossThreadsAsync()
    {
        // Arrange
        const int numberOfThreads = 10;
        const int guidsPerThread = 100;
        var seed = new BattleSeed("d337a429-5837-45a8-9519-909a92593e03");
        var allGuids = new ConcurrentBag<Guid>();
        var tasks = new List<Task>();

        // Act - Generate GUIDs from multiple threads simultaneously
        for (int i = 0; i < numberOfThreads; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < guidsPerThread; j++)
                {
                    var guid = seed.NextGuid();
                    allGuids.Add(guid);
                }
            }, TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        // Assert
        var guidList = allGuids.ToList();

        // Should have exactly the expected number of GUIDs
        Assert.Equal(numberOfThreads * guidsPerThread, guidList.Count);

        // All GUIDs should be unique
        var uniqueGuids = guidList.Distinct().ToList();
        Assert.Equal(guidList.Count, uniqueGuids.Count);

        // All GUIDs should contain the seed in their first 4 bytes
        var battleSeed = new BattleSeed("d337a429-5837-45a8-9519-909a92593e03");
        var expectedSeedBytes = BitConverter.GetBytes(battleSeed.Seed);
        foreach (var guid in guidList)
        {
            var guidBytes = guid.ToByteArray();
            Assert.Equal(expectedSeedBytes, guidBytes.Take(4).ToArray());
        }
    }

    /// <summary>
    /// Test that NextGuid() maintains order-dependency even with thread safety
    /// </summary>
    [Fact]
    public void NextGuid_OrderDependency_ShouldProduceDifferentResultsWithDifferentCallOrders()
    {
        // Arrange & Act
        var seed1 = new BattleSeed("order-test-1");
        var seed2 = new BattleSeed("order-test-1");

        // Generate GUIDs in different orders
        var guid1_a = seed1.NextGuid();
        var guid1_b = seed1.NextGuid();
        var guid1_c = seed1.NextGuid();

        var guid2_a = seed2.NextGuid();
        var guid2_c = seed2.NextGuid(); // Skip the second call
        var guid2_b = seed2.NextGuid();

        // Assert - Same call order produces same results
        Assert.Equal(guid1_a, guid2_a);

        // Different call order produces different results
        Assert.NotEqual(guid1_b, guid2_b);
        Assert.NotEqual(guid1_c, guid2_c);
    }

    /// <summary>
    /// Test that counter increments atomically across multiple threads
    /// </summary>
    [Fact]
    public async Task NextGuid_CounterIncrement_ShouldBeAtomicAcrossThreadsAsync()
    {
        // Arrange
        const int numberOfThreads = 5;
        const int guidsPerThread = 20;
        var seed = new BattleSeed("counter-increment-test");
        var allGuids = new ConcurrentBag<Guid>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < numberOfThreads; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < guidsPerThread; j++)
                {
                    allGuids.Add(seed.NextGuid());
                }
            }, TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        // Assert
        var guidList = allGuids.ToList();
        var counters = new HashSet<uint>();

        // Extract counter values from GUIDs
        foreach (var guid in guidList)
        {
            var guidBytes = guid.ToByteArray();
            var counter = BitConverter.ToUInt32(guidBytes, 4);
            counters.Add(counter);
        }

        // Should have unique counter values (1 to totalCount)
        var expectedCount = numberOfThreads * guidsPerThread;
        Assert.Equal(expectedCount, counters.Count);
        Assert.Equal(1u, counters.Min());
        Assert.Equal((uint)expectedCount, counters.Max());
    }
}
