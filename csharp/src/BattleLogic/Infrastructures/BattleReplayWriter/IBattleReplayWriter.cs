using Shared.BattleLogic.Models;

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
    Task InitializeAsync(Guid battleId, int seed);

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
    Task<List<BattleStatus>> LoadReplayAsync(Guid battleId);

    /// <summary>
    /// Finalize the writing process
    /// </summary>
    Task FinalizeAsync();
}

public readonly struct WriterMetadata
{
    public Guid BattleId { get; }
    public int Seed { get; }
    public DateTime Timestamp { get; }
    public string Type => "BattleMetadata";

    public WriterMetadata(Guid battleId, int seed, DateTime timestamp)
    {
        BattleId = battleId;
        Seed = seed;
        Timestamp = timestamp;
    }
}
