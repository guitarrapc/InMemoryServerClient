namespace Shared.Battle;

/// <summary>
/// Battle replay summary metadata
/// </summary>
[MessagePackObject(true)]
public readonly struct BattleReplaySummary
{
    /// <summary>
    /// Final turn when the battle ended
    /// </summary>
    public required int FinalTurn { get; init; }

    /// <summary>
    /// Maximum turns that were allocated for this battle
    /// </summary>
    public required int TotalTurns { get; init; }

    /// <summary>
    /// Whether players won the battle
    /// </summary>
    public required bool IsVictory { get; init; }

    /// <summary>
    /// Whether the battle ended due to turn limit
    /// </summary>
    public required bool IsEndedByTurnLimit { get; init; }

    /// <summary>
    /// Number of surviving players
    /// </summary>
    public required int SurvivingPlayers { get; init; }

    /// <summary>
    /// Total number of players
    /// </summary>
    public required int TotalPlayers { get; init; }

    /// <summary>
    /// Number of surviving enemies
    /// </summary>
    public required int SurvivingEnemies { get; init; }

    /// <summary>
    /// Total number of enemies
    /// </summary>
    public required int TotalEnemies { get; init; }

    /// <summary>
    /// Battle duration in the real world (how long the server took to compute)
    /// </summary>
    public TimeSpan BattleDuration { get; init; }
}
