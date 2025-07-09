namespace BattleLogic.Infrastructures.BattleReplayWriter;

/// <summary>
/// Interface for writing and reading battle replay data
/// Supports different storage destinations (file, memory, cloud storage, etc.)
/// </summary>
public interface IBattleReplayWriter : IAsyncDisposable
{
    /// <summary>
    /// Initialize the writer for a specific battle
    /// </summary>
    /// <param name="battleId">The battle ID</param>
    /// <param name="seed">The battle seed (optional)</param>
    Task InitializeAsync(string battleId, int? seed = null);

    /// <summary>
    /// Write a single battle frame
    /// </summary>
    /// <param name="frame">The battle status frame to write</param>
    Task WriteFrameAsync(BattleStatus frame);

    /// <summary>
    /// Write all battle frames at once (for pre-calculated battles)
    /// </summary>
    /// <param name="frames">All battle frames to write</param>
    Task WriteAllFramesAsync(IEnumerable<BattleStatus> frames);

    /// <summary>
    /// Load battle replay data
    /// </summary>
    /// <param name="battleId">The battle ID to load</param>
    /// <returns>List of battle status frames</returns>
    Task<List<BattleStatus>> LoadReplayAsync(string battleId);

    /// <summary>
    /// Finalize the writing process
    /// </summary>
    Task FinalizeAsync();
}
