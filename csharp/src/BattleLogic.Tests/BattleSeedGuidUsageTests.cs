using BattleLogic.Models;
using System.Collections.Concurrent;

namespace Tests;

/// <summary>
/// Tests for BattleSeed GUID generation with purpose-specific usage patterns
/// Validates proper GUID v4 (deterministic entity IDs) vs GUID v7 (timestamp-ordered) separation
/// </summary>
public class BattleSeedGuidUsageTests
{
    [Fact]
    public void NextEntityId_ShouldGenerateDeterministicGuidV4()
    {
        // Arrange
        const string battleId = "test-entity-deterministic";
        var seed1 = new BattleSeed(battleId);
        var seed2 = new BattleSeed(battleId);

        // Act - Generate multiple entity IDs from each seed
        var entityIds1 = new List<Guid>();
        var entityIds2 = new List<Guid>();

        for (int i = 0; i < 10; i++)
        {
            entityIds1.Add(seed1.NextEntityId());
            entityIds2.Add(seed2.NextEntityId());
        }

        // Assert - Same battleId should produce identical entity ID sequences
        Assert.Equal(entityIds1, entityIds2);

        // Verify all generated GUIDs are version 4
        foreach (var guid in entityIds1)
        {
            var version = ExtractVersion(guid);
            Assert.Equal(4, version); // GUID v4
        }
    }

    [Fact]
    public async Task NextEntityId_ShouldBeThreadSafe()
    {
        // Arrange
        const string battleId = "test-thread-safety";
        var seed = new BattleSeed(battleId);
        var allGuids = new ConcurrentBag<Guid>();
        const int threadsCount = 10;
        const int guidsPerThread = 100;

        // Act - Generate GUIDs from multiple threads
        var tasks = Enumerable.Range(0, threadsCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < guidsPerThread; i++)
                {
                    allGuids.Add(seed.NextEntityId());
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - All GUIDs should be unique
        var uniqueGuids = allGuids.Distinct().ToList();
        Assert.Equal(threadsCount * guidsPerThread, uniqueGuids.Count);
    }

    [Fact]
    public void NewTimestampId_ShouldGenerateUniqueGuidV7()
    {
        // Arrange & Act
        var timestampIds = new List<Guid>();

        for (int i = 0; i < 100; i++)
        {
            timestampIds.Add(BattleSeed.NewTimestampId());
            // Small delay to ensure timestamp progression
            if (i % 10 == 9) Thread.Sleep(1);
        }

        // Assert - All GUIDs should be unique
        var uniqueIds = timestampIds.Distinct().ToList();
        Assert.Equal(timestampIds.Count, uniqueIds.Count);

        // Verify all generated GUIDs are version 7
        foreach (var guid in timestampIds)
        {
            var version = ExtractVersion(guid);
            Assert.Equal(7, version); // GUID v7
        }
    }

    [Fact]
    public void NewTimestampId_ShouldHaveTimestampOrdering()
    {
        // Arrange & Act
        var firstBatch = new List<Guid>();
        var secondBatch = new List<Guid>();

        // Generate first batch
        for (int i = 0; i < 10; i++)
        {
            firstBatch.Add(BattleSeed.NewTimestampId());
        }

        // Wait to ensure timestamp progression
        Thread.Sleep(10);

        // Generate second batch
        for (int i = 0; i < 10; i++)
        {
            secondBatch.Add(BattleSeed.NewTimestampId());
        }

        // Assert - Second batch should generally have later timestamps than first batch
        // GUID v7 stores timestamp in first 48 bits
        var firstTimestamps = firstBatch.Select(ExtractTimestamp).ToList();
        var secondTimestamps = secondBatch.Select(ExtractTimestamp).ToList();

        var maxFirstTimestamp = firstTimestamps.Max();
        var minSecondTimestamp = secondTimestamps.Min();

        Assert.True(minSecondTimestamp >= maxFirstTimestamp,
            "Second batch should have later or equal timestamps than first batch");
    }

    [Fact]
    public void EntityIdVsTimestampId_ShouldHaveDifferentVersions()
    {
        // Arrange
        var battleSeed = new BattleSeed("test-version-difference");

        // Act
        var entityId = battleSeed.NextEntityId();
        var timestampId = BattleSeed.NewTimestampId();

        // Assert - Different versions
        var entityIdVersion = ExtractVersion(entityId);
        var timestampIdVersion = ExtractVersion(timestampId);

        Assert.Equal(4, entityIdVersion); // Entity IDs should be GUID v4
        Assert.Equal(7, timestampIdVersion); // Timestamp IDs should be GUID v7
    }

    [Fact]
    public void BattleSeed_ToString_ShouldShowCorrectCounterForEntityIds()
    {
        // Arrange
        const string testBattleId = "d337a429-5837-45a8-9519-909a92593e03";
        var battleSeed = new BattleSeed(testBattleId);

        // Act
        var initialString = battleSeed.ToString();

        // Generate some entity IDs to increment counter
        battleSeed.NextEntityId();
        battleSeed.NextEntityId();

        var afterEntityIdsString = battleSeed.ToString();

        // Assert
        Assert.Contains($"Seed={battleSeed.Seed}", initialString);
        Assert.Contains("GuidCounter=0", initialString);
        Assert.Contains("GuidCounter=2", afterEntityIdsString);
    }

    [Fact]
    public void BattleSeed_SameBattleId_ShouldProduceSameEntityIdSequence()
    {
        // Arrange
        const string battleId1 = "battle-reproducibility-test-1";
        const string battleId2 = "battle-reproducibility-test-1"; // Same ID
        const string battleId3 = "battle-reproducibility-test-2"; // Different ID

        var seed1 = new BattleSeed(battleId1);
        var seed2 = new BattleSeed(battleId2);
        var seed3 = new BattleSeed(battleId3);

        // Act
        var entityIds1 = new List<Guid>();
        var entityIds2 = new List<Guid>();
        var entityIds3 = new List<Guid>();

        for (int i = 0; i < 10; i++)
        {
            entityIds1.Add(seed1.NextEntityId());
            entityIds2.Add(seed2.NextEntityId());
            entityIds3.Add(seed3.NextEntityId());
        }

        // Assert
        Assert.Equal(entityIds1, entityIds2); // Same battle ID = same sequence
        Assert.NotEqual(entityIds1, entityIds3); // Different battle ID = different sequence
    }

    /// <summary>
    /// Extract timestamp from GUID v7 (first 48 bits)
    /// </summary>
    private static long ExtractTimestamp(Guid guid)
    {
        var bytes = guid.ToByteArray();

        // GUID v7 stores timestamp in bytes 0-5 (48 bits)
        // Convert to big-endian for timestamp extraction
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes, 0, 4);
            Array.Reverse(bytes, 4, 2);
        }

        long timestamp = 0;
        for (int i = 0; i < 6; i++)
        {
            timestamp = (timestamp << 8) | bytes[i];
        }

        return timestamp;
    }

    /// <summary>
    /// Extract version from GUID using string representation (RFC-compliant method)
    /// This avoids byte-order complications in .NET's ToByteArray()
    /// </summary>
    private static int ExtractVersion(Guid guid)
    {
        // Get the string representation and parse the version from time_hi_and_version field
        var guidStr = guid.ToString();
        var parts = guidStr.Split('-');
        var timeHiAndVersion = parts[2]; // Third part: time_hi_and_version

        // Version is the first hex digit of time_hi_and_version
        return int.Parse(timeHiAndVersion[0].ToString(), System.Globalization.NumberStyles.HexNumber);
    }
}
