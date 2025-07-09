namespace Shared.Battle;

/// <summary>
/// Battle summary information
/// </summary>
public readonly struct BattleSummary
{
    /// <summary>
    /// Battle ID
    /// </summary>
    public required string BattleId { get; init; }

    /// <summary>
    /// Associated group ID
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Current turn
    /// </summary>
    public int CurrentTurn { get; init; }

    /// <summary>
    /// Number of players
    /// </summary>
    public int PlayerCount { get; init; }

    /// <summary>
    /// Number of enemies
    /// </summary>
    public int EnemyCount { get; init; }

    /// <summary>
    /// Battle started time
    /// </summary>
    public DateTime StartedAt { get; init; }
}
