using Shared.Battle;

namespace Shared.Models;

/// <summary>
/// Server status information
/// </summary>
[MessagePackObject(true)]
public class ServerStatus
{
    /// <summary>
    /// Server uptime in seconds
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Total active connections
    /// </summary>
    public int TotalConnections { get; set; }

    /// <summary>
    /// Number of groups
    /// </summary>
    public int GroupCount { get; set; }

    /// <summary>
    /// Number of active battles
    /// </summary>
    public int ActiveBattleCount { get; set; }

    /// <summary>
    /// List of group summaries
    /// </summary>
    public List<GroupSummary> Groups { get; set; } = new(10); // Pre-allocate for typical group count

    /// <summary>
    /// List of active battle summaries
    /// </summary>
    public List<BattleSummary> ActiveBattles { get; set; } = new(5); // Pre-allocate for typical battle count
}
