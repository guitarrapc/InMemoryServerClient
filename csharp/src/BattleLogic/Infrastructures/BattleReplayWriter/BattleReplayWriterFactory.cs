using BattleLogic.Models;
using Microsoft.Extensions.Logging;

namespace BattleLogic.Infrastructures.BattleReplayWriter;

/// <summary>
/// Factory for creating IBattleReplayWriter instances based on configuration
/// </summary>
public class BattleReplayWriterFactory
{
    private readonly BattleReplayOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public BattleReplayWriterFactory(ILoggerFactory loggerFactory) : this(BattleReplayOptions.Defaults, loggerFactory)
    {
    }

    public BattleReplayWriterFactory(BattleReplayOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Create a new IBattleReplayWriter instance based on current configuration
    /// </summary>
    public IBattleReplayWriter Create(string battleId)
    {
        return _options.Mode switch
        {
            BattleReplayMode.File => new FileBattleReplayWriter(_options, _loggerFactory.CreateLogger<FileBattleReplayWriter>()),
            BattleReplayMode.Memory => new MemoryBattleReplayWriter(_options, _loggerFactory.CreateLogger<MemoryBattleReplayWriter>()),
            BattleReplayMode.Disabled => new NullBattleReplayWriter(_options, _loggerFactory.CreateLogger<NullBattleReplayWriter>()),
            BattleReplayMode.Cloud => throw new NotImplementedException("Cloud storage replay writer is not yet implemented"),
            _ => throw new ArgumentOutOfRangeException(nameof(_options.Mode), _options.Mode, "Unknown battle replay mode")
        };
    }
}
