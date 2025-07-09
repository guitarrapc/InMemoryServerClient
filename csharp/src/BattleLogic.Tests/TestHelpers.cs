using BattleLogic.Infrastructures.BattleReplayWriter;
using Microsoft.Extensions.Logging;
using Shared.Contracts;

namespace BattleLogic.Tests;

internal static class TestHelpers
{
    /// <summary>
    /// Creates a memory-based BattleReplayWriterFactory for testing
    /// </summary>
    public static BattleReplayWriterFactory CreateMemoryReplayWriterFactory(ILoggerFactory _loggerFactory)
    {
        var options = new BattleReplayOptions
        {
            Mode = BattleReplayMode.Memory,
            FileOutputDirectory = string.Empty,
            EnableLogging = false
        };
        return new BattleReplayWriterFactory(options, _loggerFactory);
    }

    /// <summary>
    /// Creates a BattleState instance for testing with default seed
    /// </summary>
    public static BattleState CreateBattleState(IBattleGroupContext group, ILogger<BattleState> logger, ILoggerFactory loggerFactory, int seed = 12345)
    {
        var battleId = BattleSeed.NewTimestampId();
        return new BattleState(battleId, seed, group, logger, CreateMemoryReplayWriterFactory(loggerFactory));
    }

    /// <summary>
    /// Creates a BattleState instance for testing with specific battleId and seed
    /// </summary>
    public static BattleState CreateBattleState(Guid battleId, int seed, IBattleGroupContext group, ILogger<BattleState> logger, ILoggerFactory loggerFactory)
    {
        return new BattleState(battleId, seed, group, logger, CreateMemoryReplayWriterFactory(loggerFactory));
    }
}
