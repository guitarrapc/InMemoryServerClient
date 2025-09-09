namespace Shared.ServiceDiscovery.Models;

/// <summary>
/// Battle result information
/// </summary>
public readonly record struct BattleResult
{
    /// <summary>
    /// Session ID
    /// </summary>
    public string SessionId { get; init; }

    /// <summary>
    /// Battle outcome
    /// </summary>
    public BattleOutcome Outcome { get; init; }

    /// <summary>
    /// Battle duration
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Player results
    /// </summary>
    public IReadOnlyList<PlayerResult> PlayerResults { get; init; }

    /// <summary>
    /// When the battle was completed
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Create battle result
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="outcome">Battle outcome</param>
    /// <param name="duration">Duration</param>
    /// <param name="playerResults">Player results</param>
    /// <param name="completedAt">Completion time</param>
    public BattleResult(string sessionId, BattleOutcome outcome, TimeSpan duration,
        IReadOnlyList<PlayerResult> playerResults, DateTime completedAt)
    {
        SessionId = sessionId;
        Outcome = outcome;
        Duration = duration;
        PlayerResults = playerResults ?? Array.Empty<PlayerResult>();
        CompletedAt = completedAt;
    }
}

/// <summary>
/// Player result in a battle
/// </summary>
public readonly record struct PlayerResult
{
    /// <summary>
    /// Player ID
    /// </summary>
    public string PlayerId { get; init; }

    /// <summary>
    /// Whether the player won
    /// </summary>
    public bool IsWinner { get; init; }

    /// <summary>
    /// Player's score
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Create player result
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <param name="isWinner">Is winner</param>
    /// <param name="score">Score</param>
    public PlayerResult(string playerId, bool isWinner, int score)
    {
        PlayerId = playerId;
        IsWinner = isWinner;
        Score = score;
    }
}

/// <summary>
/// Battle outcome enumeration
/// </summary>
public enum BattleOutcome
{
    /// <summary>
    /// Players won the battle
    /// </summary>
    Victory,

    /// <summary>
    /// Players lost the battle
    /// </summary>
    Defeat,

    /// <summary>
    /// Battle ended in a draw
    /// </summary>
    Draw,

    /// <summary>
    /// Battle was aborted before completion
    /// </summary>
    Aborted,

    /// <summary>
    /// Battle ended due to an error
    /// </summary>
    Error
}

/// <summary>
/// Session termination reason
/// </summary>
public enum TerminationReason
{
    /// <summary>
    /// Normal completion
    /// </summary>
    Normal,

    /// <summary>
    /// Player disconnected
    /// </summary>
    PlayerDisconnect,

    /// <summary>
    /// Server error occurred
    /// </summary>
    ServerError,

    /// <summary>
    /// Session timed out
    /// </summary>
    Timeout,

    /// <summary>
    /// Terminated by administrator
    /// </summary>
    AdminAction
}
