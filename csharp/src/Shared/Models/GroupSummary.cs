namespace Shared.Models;

/// <summary>
/// Group summary information
/// </summary>
public readonly struct GroupSummary
{
    /// <summary>
    /// Group ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Group name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current connection count
    /// </summary>
    public int ConnectionCount { get; init; }

    /// <summary>
    /// Battle ID if battle is in progress
    /// </summary>
    public string? BattleId { get; init; }
}
