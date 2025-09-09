namespace WasmClient.Constants;

/// <summary>
/// Battle replay configuration constants shared with CliClient
/// </summary>
public static class BattleReplayDefines
{
    /// <summary>
    /// Replay frame rate (5 FPS for smooth visual updates)
    /// </summary>
    public const int ReplayFps = 5;

    /// <summary>
    /// Time in milliseconds between replay frames
    /// </summary>
    public const int ReplayFrameTimeMs = 1000 / ReplayFps; // 200ms

    /// <summary>
    /// Maximum number of entities to display at once
    /// </summary>
    public const int MaxEntitiesPerField = 50;

    /// <summary>
    /// Battle field size (20x20 game coordinates)
    /// </summary>
    public const int BattleFieldSize = 20;
}
