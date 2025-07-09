using BattleLogic.Models;
using Microsoft.Extensions.Logging;

namespace BattleLogic.Infrastructures.BattleReplayWriter;

/// <summary>
/// Null implementation of IBattleReplayWriter
/// Does not store any battle replay data - for performance testing
/// </summary>
internal class NullBattleReplayWriter : IBattleReplayWriter
{
    private readonly ILogger<NullBattleReplayWriter> _logger;
    private readonly bool _enableLogging;

    public NullBattleReplayWriter(BattleReplayOptions options, ILogger<NullBattleReplayWriter> logger)
    {
        _enableLogging = options.EnableLogging;
        _logger = logger;
    }

    public Task InitializeAsync(string battleId, int? seed = null)
    {
        if (_enableLogging)
        {
            _logger.LogDebug("Null battle replay writer initialized - BattleId: {BattleId}, Seed: {Seed} (no output)", battleId, seed);
        }

        return Task.CompletedTask;
    }

    public Task WriteFrameAsync(BattleStatus frame)
    {
        // Do nothing - this is the null implementation
        return Task.CompletedTask;
    }

    public Task WriteAllFramesAsync(IEnumerable<BattleStatus> frames)
    {
        // Do nothing - this is the null implementation
        return Task.CompletedTask;
    }

    public Task<List<BattleStatus>> LoadReplayAsync(string battleId)
    {
        // Return empty list - no data is stored
        return Task.FromResult(new List<BattleStatus>());
    }

    public Task FinalizeAsync()
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
