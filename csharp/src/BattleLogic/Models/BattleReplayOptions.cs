using BattleLogic.Constants;

namespace BattleLogic.Models;

/// <summary>
/// Configuration options for battle replay system
/// </summary>
public class BattleReplayOptions
{
    public static BattleReplayOptions Defaults = new BattleReplayOptions
    {
        Mode = BattleReplayMode.File,
        FileOutputDirectory = BattleSystemDefines.BattleReplayDirectory,
        EnableLogging = true,
    };

    /// <summary>
    /// The mode for battle replay output
    /// </summary>
    public required BattleReplayMode Mode { get; init; }

    /// <summary>
    /// Directory path for file output (used when Mode is File)
    /// </summary>
    public required string FileOutputDirectory { get; init; }

    /// <summary>
    /// Whether to enable detailed logging for replay operations
    /// </summary>
    public required bool EnableLogging { get; init; }
}

/// <summary>
/// Available modes for battle replay output
/// </summary>
public enum BattleReplayMode
{
    /// <summary>
    /// Write replay data to files (production default)
    /// </summary>
    File = 0,

    /// <summary>
    /// Store replay data in memory only (testing)
    /// </summary>
    Memory = 1,

    /// <summary>
    /// No replay data output (performance testing)
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// Future: Cloud storage output (S3, Azure Blob, etc.)
    /// </summary>
    Cloud = 3
}
