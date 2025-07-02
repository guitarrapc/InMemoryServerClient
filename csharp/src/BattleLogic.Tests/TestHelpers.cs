using BattleLogic.Infrastructures.BattleReplayWriter;
using Microsoft.Extensions.Logging;

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
}
