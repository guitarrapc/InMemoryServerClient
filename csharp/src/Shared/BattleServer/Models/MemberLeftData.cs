namespace Shared.BattleServer.Models;

/// <summary>
/// Data for MemberLeft SignalR event
/// </summary>
[MessagePackObject(true)]
public readonly record struct MemberLeftData
{
    /// <summary>
    /// Member connection ID
    /// </summary>
    public required string ConnectionId { get; init; }

    /// <summary>
    /// Group ID
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Group name
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// Current member count after leave
    /// </summary>
    public required int CurrentMemberCount { get; init; }

    /// <summary>
    /// Maximum group capacity
    /// </summary>
    public required int MaxMembers { get; init; }
}
