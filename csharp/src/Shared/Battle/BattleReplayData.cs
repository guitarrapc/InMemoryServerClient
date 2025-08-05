namespace Shared.Battle;

/// <summary>
/// Battle replay data for chunked transmission
/// </summary>
[MessagePackObject(true)]
public readonly struct BattleReplayData
{
    /// <summary>
    /// Battle ID
    /// </summary>
    public required Guid BattleId { get; init; }

    /// <summary>
    /// Battle seed for reproducibility
    /// </summary>
    public int Seed { get; init; }

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

    /// <summary>
    /// Battle summary metadata (only available in the last chunk)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleReplaySummary? Summary { get; init; }
}
