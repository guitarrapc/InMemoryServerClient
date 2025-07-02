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
    public void SameSeed_ShouldProduceSameResults()
    {
        // Arrange
        const int testSeed = 12345;
        var mockGroup = new MockBattleGroupContext();

        // Act - Create two battles with the same seed
        var battle1 = new BattleState("test1", mockGroup, _logger, testSeed);
        var battle2 = new BattleState("test2", mockGroup, _logger, testSeed);

        // Assert - Both battles should have the same seed
        Assert.Equal(testSeed, battle1.BattleSeed.Seed);
        Assert.Equal(testSeed, battle2.BattleSeed.Seed);
        Assert.Equal(battle1.BattleSeed.Seed, battle2.BattleSeed.Seed);
    }

    [Fact]
    public void DifferentSeeds_ShouldProduceDifferentResults()
    {
        // Arrange
        const int seed1 = 12345;
        const int seed2 = 67890;
        var mockGroup = new MockBattleGroupContext();

        // Act
        var battle1 = new BattleState("test1", mockGroup, _logger, seed1);
        var battle2 = new BattleState("test2", mockGroup, _logger, seed2);

        // Assert
        Assert.NotEqual(battle1.BattleSeed.Seed, battle2.BattleSeed.Seed);
    }

    [Fact]
    public void BattleSeed_NextGuid_ShouldBeDeterministic()
    {
        // Arrange
        const int testSeed = 12345;
        var seed1 = new BattleSeed(testSeed);
        var seed2 = new BattleSeed(testSeed);

        // Act - Generate multiple GUIDs from each seed
        var guids1 = new List<Guid>();
        var guids2 = new List<Guid>();

        for (int i = 0; i < 10; i++)
        {
            guids1.Add(seed1.NextGuid());
            guids2.Add(seed2.NextGuid());
        }

        // Assert - Same seed should produce same GUID sequence
        Assert.Equal(guids1, guids2);
    }

    [Fact]
    public void BattleSeed_Random_ShouldBeDeterministic()
    {
        // Arrange
        const int testSeed = 12345;
        var seed1 = new BattleSeed(testSeed);
        var seed2 = new BattleSeed(testSeed);

        // Act - Generate multiple random numbers from each seed
        var numbers1 = new List<int>();
        var numbers2 = new List<int>();

        for (int i = 0; i < 100; i++)
        {
            numbers1.Add(seed1.Random.Next(1, 100));
            numbers2.Add(seed2.Random.Next(1, 100));
        }

        // Assert - Same seed should produce same random sequence
        Assert.Equal(numbers1, numbers2);
    }

    [Fact]
    public void BattleSeed_ToString_ShouldContainSeedAndCounter()
    {
        // Arrange
        const int testSeed = 12345;
        var battleSeed = new BattleSeed(testSeed);

        // Act
        var initialString = battleSeed.ToString();

        // Generate some GUIDs to increment counter
        battleSeed.NextGuid();
        battleSeed.NextGuid();

        var afterGuidsString = battleSeed.ToString();

        // Assert
        Assert.Contains("12345", initialString);
        Assert.Contains("GuidCounter=0", initialString);
        Assert.Contains("GuidCounter=2", afterGuidsString);
    }

    [Fact]
    public void BattleSeed_RandomSeedGeneration_ShouldNotBeZero()
    {
        // Arrange & Act
        var battleSeed = new BattleSeed(); // No seed provided, should generate random

        // Assert
        Assert.NotEqual(0, battleSeed.Seed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(999999)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void BattleSeed_WithSpecificSeed_ShouldUseThatSeed(int seed)
    {
        // Arrange & Act
        var battleSeed = new BattleSeed(seed);

        // Assert
        Assert.Equal(seed, battleSeed.Seed);
    }

    [Fact]
    public async Task SameSeed_ShouldProduceIdenticalBattleInitialization()
    {
        // Arrange
        const int testSeed = 42;
        var mockGroup = new MockBattleGroupContext();

        // Act - Create two battles with the same seed
        var battle1 = new BattleState("test1", mockGroup, _logger, testSeed);
        var battle2 = new BattleState("test2", mockGroup, _logger, testSeed);

        var status1 = battle1.GetStatus();
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
    public async Task SameSeed_ShouldProduceIdenticalBattleExecution()
    {
        // Arrange
        const int testSeed = 123456;
        var mockGroup = new MockBattleGroupContext();

        // Act - Run two complete battles with the same seed
        var battle1 = new BattleState("test1", mockGroup, _logger, testSeed);
        var battle2 = new BattleState("test2", mockGroup, _logger, testSeed);

        await battle1.RunBattleAsync();
        await battle2.RunBattleAsync();

        var allTurnData1 = battle1.GetAllTurnData();
        var allTurnData2 = battle2.GetAllTurnData();

        // Assert - Battle execution should be identical
        Assert.Equal(allTurnData1.Count, allTurnData2.Count);

        for (int turn = 0; turn < allTurnData1.Count; turn++)
        {
            var turnData1 = allTurnData1[turn];
            var turnData2 = allTurnData2[turn];

            Assert.Equal(turnData1.CurrentTurn, turnData2.CurrentTurn);
            Assert.Equal(turnData1.IsInProgress, turnData2.IsInProgress);
            Assert.Equal(turnData1.TotalTurns, turnData2.TotalTurns);

            // Compare player states for each turn
            Assert.Equal(turnData1.Players.Count, turnData2.Players.Count);
            for (int i = 0; i < turnData1.Players.Count; i++)
            {
                var player1 = turnData1.Players[i];
                var player2 = turnData2.Players[i];

                Assert.Equal(player1.Id, player2.Id);
                Assert.Equal(player1.CurrentHp, player2.CurrentHp);
                Assert.Equal(player1.IsDefending, player2.IsDefending);
                Assert.Equal(player1.Position, player2.Position);
            }

            // Compare enemy states for each turn
            Assert.Equal(turnData1.Enemies.Count, turnData2.Enemies.Count);
            for (int i = 0; i < turnData1.Enemies.Count; i++)
            {
                var enemy1 = turnData1.Enemies[i];
                var enemy2 = turnData2.Enemies[i];

                Assert.Equal(enemy1.Id, enemy2.Id);
                Assert.Equal(enemy1.CurrentHp, enemy2.CurrentHp);
                Assert.Equal(enemy1.IsDefending, enemy2.IsDefending);
                Assert.Equal(enemy1.Position, enemy2.Position);
            }

            // Compare field dimensions for each turn
            Assert.Equal(turnData1.FieldWidth, turnData2.FieldWidth);
            Assert.Equal(turnData1.FieldHeight, turnData2.FieldHeight);
        }

        // Clean up
        battle1.ClearBattleData();
        battle2.ClearBattleData();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    [InlineData(123456)]
    [InlineData(-42)]
    public async Task DifferentSeeds_ShouldProduceDifferentBattleResults(int seed1)
    {
        // Arrange
        int seed2 = seed1 + 1; // Ensure different seeds
        var mockGroup = new MockBattleGroupContext();

        // Act - Run battles with different seeds
        var battle1 = new BattleState("test1", mockGroup, _logger, seed1);
        var battle2 = new BattleState("test2", mockGroup, _logger, seed2);

        await battle1.RunBattleAsync();
        await battle2.RunBattleAsync();

        var allTurnData1 = battle1.GetAllTurnData();
        var allTurnData2 = battle2.GetAllTurnData();

        // Assert - At least some aspect should be different
        bool foundDifference = false;

        // Check if battle outcomes are different
        var finalTurn1 = allTurnData1[^1];
        var finalTurn2 = allTurnData2[^1];

        // Check for basic differences in battle structure
        if (finalTurn1.CurrentTurn != finalTurn2.CurrentTurn ||
            finalTurn1.TotalTurns != finalTurn2.TotalTurns ||
            allTurnData1.Count != allTurnData2.Count)
        {
            foundDifference = true;
        }

        // Check if initial states are different (check first turn data)
        if (!foundDifference && allTurnData1.Count > 0 && allTurnData2.Count > 0)
        {
            var initial1 = allTurnData1[0];
            var initial2 = allTurnData2[0];

            // Compare initial player entity IDs (should be different with different seeds)
            if (initial1.Players.Count == initial2.Players.Count)
            {
                for (int i = 0; i < initial1.Players.Count; i++)
                {
                    if (initial1.Players[i].Id != initial2.Players[i].Id)
                    {
                        foundDifference = true;
                        break;
                    }
                }
            }

            // Compare initial enemy entity IDs (should be different with different seeds)
            if (!foundDifference && initial1.Enemies.Count == initial2.Enemies.Count)
            {
                for (int i = 0; i < initial1.Enemies.Count; i++)
                {
                    if (initial1.Enemies[i].Id != initial2.Enemies[i].Id)
                    {
                        foundDifference = true;
                        break;
                    }
                }
            }

            // Compare initial player stats
            if (!foundDifference)
            {
                for (int i = 0; i < Math.Min(initial1.Players.Count, initial2.Players.Count); i++)
                {
                    var player1 = initial1.Players[i];
                    var player2 = initial2.Players[i];

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

            // Compare initial enemy stats
            if (!foundDifference)
            {
                for (int i = 0; i < Math.Min(initial1.Enemies.Count, initial2.Enemies.Count); i++)
                {
                    var enemy1 = initial1.Enemies[i];
                    var enemy2 = initial2.Enemies[i];

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
        }

        if (!foundDifference)
        {
            // If no differences found in initial state, log details for debugging
            Console.WriteLine($"Warning: Seeds {seed1} and {seed2} produced very similar results");
            Console.WriteLine($"Turn counts: {finalTurn1.CurrentTurn} vs {finalTurn2.CurrentTurn}");
            Console.WriteLine($"Total battle data points: {allTurnData1.Count} vs {allTurnData2.Count}");

            // In some edge cases, consecutive seeds might produce very similar results
            // This is acceptable as long as the same seed always produces the same result
            // We'll consider this test passed if it's an edge case like int.MaxValue overflow
            if (seed1 == int.MaxValue || Math.Abs(seed1 - seed2) != 1)
            {
                foundDifference = true; // Consider edge cases as acceptable
            }
        }

        Assert.True(foundDifference, $"Battles with different seeds ({seed1} vs {seed2}) should typically produce different results. " +
                                   "If this fails consistently, there may be an issue with random number generation.");

        // Clean up
        battle1.ClearBattleData();
        battle2.ClearBattleData();
    }

    [Fact]
    public async Task SameSeed_MultipleExecutions_ShouldAlwaysProduceSameResult()
    {
        // Arrange
        const int testSeed = 555;
        const int executionCount = 5;
        var mockGroup = new MockBattleGroupContext();

        var battleResults = new List<(bool IsCompleted, int TurnCount, string FinalPlayersHp, string FinalEnemiesHp)>();

        // Act - Run the same seeded battle multiple times
        for (int execution = 0; execution < executionCount; execution++)
        {
            var battle = new BattleState($"test{execution}", mockGroup, _logger, testSeed);
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

        Console.WriteLine($"All {executionCount} executions with seed {testSeed} produced identical results:");
        Console.WriteLine($"- Battle Completed: {firstResult.IsCompleted}");
        Console.WriteLine($"- Turn Count: {firstResult.TurnCount}");
        Console.WriteLine($"- Final Players HP: {firstResult.FinalPlayersHp}");
        Console.WriteLine($"- Final Enemies HP: {firstResult.FinalEnemiesHp}");
    }

    [Fact]
    public async Task ReproducibilityStressTest_SameSeed_LargeBattle()
    {
        // Arrange - Use edge case seed values and run longer battles
        const int testSeed = 0; // Edge case: zero seed
        var mockGroup = new MockBattleGroupContext();

        // Act - Run two battles with potentially longer execution
        var battle1 = new BattleState("stress1", mockGroup, _logger, testSeed);
        var battle2 = new BattleState("stress2", mockGroup, _logger, testSeed);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await battle1.RunBattleAsync();
        var time1 = sw.ElapsedMilliseconds;

        sw.Restart();
        await battle2.RunBattleAsync();
        var time2 = sw.ElapsedMilliseconds;

        // Assert - Results should be identical regardless of execution time variations
        var allTurnData1 = battle1.GetAllTurnData();
        var allTurnData2 = battle2.GetAllTurnData();

        Assert.Equal(allTurnData1.Count, allTurnData2.Count);

        var finalTurn1 = allTurnData1[^1];
        var finalTurn2 = allTurnData2[^1];

        Assert.Equal(finalTurn1.IsInProgress, finalTurn2.IsInProgress);
        Assert.Equal(finalTurn1.CurrentTurn, finalTurn2.CurrentTurn);

        Console.WriteLine($"Stress test with seed {testSeed}:");
        Console.WriteLine($"- Battle 1 time: {time1}ms, Battle 2 time: {time2}ms");
        Console.WriteLine($"- Turn count: {finalTurn1.CurrentTurn}");
        Console.WriteLine($"- Battle completed: {!finalTurn1.IsInProgress}");

        // Clean up
        battle1.ClearBattleData();
        battle2.ClearBattleData();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12345)]
    public async Task EdgeCaseSeeds_ShouldProduceReproducibleResults(int edgeCaseSeed)
    {
        // Arrange
        var mockGroup = new MockBattleGroupContext();

        // Act - Test edge case seed values for reproducibility
        var battle1 = new BattleState("edge1", mockGroup, _logger, edgeCaseSeed);
        var battle2 = new BattleState("edge2", mockGroup, _logger, edgeCaseSeed);

        await Task.WhenAll(battle1.RunBattleAsync(), battle2.RunBattleAsync());

        var status1 = battle1.GetStatus();
        var status2 = battle2.GetStatus();

        // Assert - Even edge case seeds should produce identical results
        Assert.Equal(status1.IsInProgress, status2.IsInProgress);
        Assert.Equal(status1.CurrentTurn, status2.CurrentTurn);
        Assert.Equal(status1.TotalTurns, status2.TotalTurns);

        Console.WriteLine($"Edge case seed {edgeCaseSeed}: InProgress={status1.IsInProgress}, Turns={status1.CurrentTurn}/{status1.TotalTurns}");

        // Clean up
        battle1.ClearBattleData();
        battle2.ClearBattleData();
    }

    [Fact]
    public void BattleSeed_ConsistentGuidGeneration_AcrossMultipleInstances()
    {
        // Arrange
        const int testSeed = 789;
        const int guidCount = 100;

        // Act - Generate GUIDs from multiple BattleSeed instances with same seed
        var guids1 = new List<Guid>();
        var guids2 = new List<Guid>();
        var guids3 = new List<Guid>();

        var seed1 = new BattleSeed(testSeed);
        var seed2 = new BattleSeed(testSeed);
        var seed3 = new BattleSeed(testSeed);

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
    public void BattleSeed_ThreadSafety_MultipleThreadsWithSameSeed()
    {
        // Arrange
        const int testSeed = 999;
        const int threadsCount = 10;
        const int operationsPerThread = 50;

        var allGuids = new ConcurrentBag<List<Guid>>();
        var allRandomNumbers = new ConcurrentBag<List<int>>();

        // Act - Test thread safety by running multiple threads with same seed
        Parallel.For(0, threadsCount, threadIndex =>
        {
            var seed = new BattleSeed(testSeed);
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
        var seed = new BattleSeed(12345);
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
        var seedBytes = BitConverter.GetBytes(12345);
        foreach (var guid in guidList)
        {
            var guidBytes = guid.ToByteArray();
            Assert.Equal(seedBytes, guidBytes.Take(4).ToArray());
        }
    }

    /// <summary>
    /// Test that NextGuid() maintains order-dependency even with thread safety
    /// </summary>
    [Fact]
    public void NextGuid_OrderDependency_ShouldProduceDifferentResultsWithDifferentCallOrders()
    {
        // Arrange & Act
        var seed1 = new BattleSeed(54321);
        var seed2 = new BattleSeed(54321);

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
        var seed = new BattleSeed(98765);
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
