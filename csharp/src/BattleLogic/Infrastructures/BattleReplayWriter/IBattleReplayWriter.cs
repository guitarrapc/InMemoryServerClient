namespace BattleLogic.Infrastructures.BattleReplayWriter;

/// <summary>
/// Interface for writing battle replay data
/// Supports different output destinations (file, memory, cloud storage, etc.)
/// </summary>
public interface IBattleReplayWriter : IAsyncDisposable
{
    /// <summary>
    /// Initialize the writer for a specific battle
    /// </summary>
    /// <param name="battleId">The battle ID</param>
    Task InitializeAsync(string battleId);

    /// <summary>
    /// Write a single battle frame
    /// </summary>
    /// <param name="frame">The battle status frame to write</param>
    Task WriteFrameAsync(BattleStatus frame);

    /// <summary>
    /// Finalize the writing process
    /// </summary>
    Task FinalizeAsync();
}
