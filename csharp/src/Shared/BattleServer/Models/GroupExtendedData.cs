namespace Shared.BattleServer.Models;

/// <summary>
/// Data for GroupExtended event
/// </summary>
[MessagePackObject(true)]
public readonly record struct GroupExtendedData
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
    /// Current extension count
    /// </summary>
    public required int ExtensionCount { get; init; }

    /// <summary>
    /// Maximum allowed extensions
    /// </summary>
    public required int MaxExtensions { get; init; }

    /// <summary>
    /// New expiry time after extension
    /// </summary>
    public required DateTime NewExpiryTime { get; init; }
}
