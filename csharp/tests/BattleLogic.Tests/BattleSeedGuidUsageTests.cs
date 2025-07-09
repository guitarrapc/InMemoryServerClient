using System.Collections.Concurrent;

namespace BattleLogic.Tests;

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
        const int userSeed = 12345;
        var battleId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"); // Fixed battleId for test
        var seed1 = new BattleSeed(battleId, userSeed);
        var seed2 = new BattleSeed(battleId, userSeed);

        // Act - Generate multiple entity IDs from each seed
        var entityIds1 = new List<Guid>();
        var entityIds2 = new List<Guid>();

        for (int i = 0; i < 10; i++)
        {
            entityIds1.Add(seed1.NextEntityId());
            entityIds2.Add(seed2.NextEntityId());
        }

        // Assert - Same battleId and userSeed should produce identical entity ID sequences
        Assert.Equal(entityIds1, entityIds2);

        // Verify all generated GUIDs are version 4
        foreach (var guid in entityIds1)
        {
            // Guid.ToByteArray is little-endian, version is in byte 7
            var guidBytes = guid.ToByteArray();
            var version = (guidBytes[7] & 0xF0) >> 4;
            Assert.Equal(4, version); // GUID v4
        }
    }

    [Fact]
    public async Task NextEntityId_ShouldBeThreadSafe()
    {
        // Arrange
        const int userSeed = 12345;
        var battleId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"); // Fixed battleId for test
        var seed = new BattleSeed(battleId, userSeed);
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
            // Guid.ToByteArray is little-endian, version is in byte 7
            var guidBytes = guid.ToByteArray();
            var version = (guidBytes[7] & 0xF0) >> 4;
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
        var battleId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var battleSeed = new BattleSeed(battleId, 12345);

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
        const int testSeed = 12345;
        var battleId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var battleSeed = new BattleSeed(battleId, testSeed);

        // Act
        var initialString = battleSeed.ToString();

        // Generate some entity IDs to increment counter
        battleSeed.NextEntityId();
        battleSeed.NextEntityId();

        var afterEntityIdsString = battleSeed.ToString();

        // Assert
        Assert.Contains($"UserSeed={battleSeed.UserSeed}", initialString);
        Assert.Contains($"DeterministicSeed={battleSeed.DeterministicSeed}", initialString);
        Assert.Contains("GuidCounter=0", initialString);
        Assert.Contains("GuidCounter=2", afterEntityIdsString);
    }

    [Fact]
    public void BattleSeed_DifferentBattleIdSameSeed_ShouldProduceDifferentEntityIdSequence()
    {
        // Arrange
        const int userSeed = 12345; // Same user seed
        var battleId1 = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var battleId2 = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"); // Different battleId
        var battleId3 = Guid.Parse("550e8400-e29b-41d4-a716-446655440002"); // Different battleId

        var seed1 = new BattleSeed(battleId1, userSeed);
        var seed2 = new BattleSeed(battleId2, userSeed);
        var seed3 = new BattleSeed(battleId3, userSeed);

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
        Assert.NotEqual(entityIds1, entityIds2); // Different battleId + same userSeed = different sequence
        Assert.NotEqual(entityIds1, entityIds3); // Different battleId + same userSeed = different sequence
        Assert.NotEqual(entityIds2, entityIds3); // Different battleId + same userSeed = different sequence
    }

    [Fact]
    public void BattleSeed_SameBattleIdAndSeed_ShouldProduceSameEntityIdSequence()
    {
        // Arrange
        const int userSeed = 12345;
        var battleId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

        var seed1 = new BattleSeed(battleId, userSeed);
        var seed2 = new BattleSeed(battleId, userSeed);

        // Act
        var entityIds1 = new List<Guid>();
        var entityIds2 = new List<Guid>();

        for (int i = 0; i < 10; i++)
        {
            entityIds1.Add(seed1.NextEntityId());
            entityIds2.Add(seed2.NextEntityId());
        }

        // Assert
        Assert.Equal(entityIds1, entityIds2); // Same battleId + same userSeed = same sequence
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
    /// Extract version from GUID
    /// </summary>
    private static int ExtractVersion(Guid guid)
    {
        var bytes = guid.ToByteArray();
        // GUID version is always at byte[7] (time-hi-and-version field, upper 4 bits)
        // due to little-endian byte ordering in .NET's ToByteArray()
        // GUID: "0197e9ec-f33e-7787-9d91-c6a45876776e"
        // バイト配列: [ec,e9,97,01, 3e,f3, 87,77, 9d,91, c6,a4,58,76,77,6e]
        //             0  1  2  3   4  5   6  7   8  9   10 11 12 13 14 15
        //                                     ↑
        //                                 バージョン位置
        return (bytes[7] & 0xF0) >> 4;
    }
}
