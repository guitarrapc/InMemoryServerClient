namespace Shared.Battle;

/// <summary>
/// Battle replay data for chunked transmission
/// </summary>
public readonly struct BattleReplayData
{
    /// <summary>
    /// Battle ID
    /// </summary>
    public required string BattleId { get; init; }

    /// <summary>
    /// Turn data for this chunk
    /// </summary>
    public required List<BattleStatus> TurnData { get; init; }

    /// <summary>
    /// Current chunk index (0-based)
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Total number of chunks
    /// </summary>
    public int TotalChunks { get; init; }

    /// <summary>
    /// Whether this is the last chunk
    /// </summary>
    public bool IsLastChunk { get; init; }
}
