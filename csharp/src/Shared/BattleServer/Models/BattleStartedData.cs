namespace Shared.BattleServer.Models;

/// <summary>
/// Data for BattleStarted event
/// </summary>
[MessagePackObject(true)]
public readonly record struct BattleStartedData
{
    /// <summary>
    /// Battle ID
    /// </summary>
    public required Guid BattleId { get; init; }

    /// <summary>
    /// Battle seed for reproducibility
    /// </summary>
    public required int Seed { get; init; }
}
