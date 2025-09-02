namespace Shared.BattleServer.Models;

/// <summary>
/// Data for GroupDissolved event
/// </summary>
[MessagePackObject(true)]
public readonly record struct GroupDissolvedData
{
    /// <summary>
    /// Group ID
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Group name
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// Reason for dissolution
    /// </summary>
    public required string Reason { get; init; }
}
