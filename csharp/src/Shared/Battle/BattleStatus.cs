namespace Shared.Battle;

/// <summary>
/// Battle status for client-server communication
/// </summary>
public class BattleStatus
{
    /// <summary>
    /// Battle unique identifier
    /// </summary>
    public Guid? BattleId { get; set; }

    /// <summary>
    /// Is battle in progress
    /// </summary>
    public bool IsInProgress { get; set; }

    /// <summary>
    /// Current turn number
    /// </summary>
    public int CurrentTurn { get; set; }

    /// <summary>
    /// Total turns in battle
    /// </summary>
    public int TotalTurns { get; set; }

    /// <summary>
    /// Players in battle
    /// </summary>
    public List<EntityInfo> Players { get; set; } = [];

    /// <summary>
    /// Enemies in battle
    /// </summary>
    public List<EntityInfo> Enemies { get; set; } = [];

    /// <summary>
    /// Field dimensions (for client-side rendering)
    /// </summary>
    public int FieldWidth { get; set; } = 20; // Default battle field width

    /// <summary>
    /// Field dimensions (for client-side rendering)
    /// </summary>
    public int FieldHeight { get; set; } = 20; // Default battle field height

    /// <summary>
    /// Recent battle logs
    /// </summary>
    public List<string> RecentLogs { get; set; } = new(10); // Pre-allocate for recent logs

    /// <summary>
    /// Battle result (null if battle is still in progress)
    /// </summary>
    public bool? IsPlayerVictory { get; set; }

    /// <summary>
    /// Clears all references to reduce memory pressure
    /// </summary>
    public void Clear()
    {
        Players.Clear();
        Enemies.Clear();
        RecentLogs.Clear();
    }
}
