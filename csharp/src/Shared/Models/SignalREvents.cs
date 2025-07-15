namespace Shared.Models;

/// <summary>
/// Data for ConnectionsReady event
/// </summary>
[MessagePackObject(true)]
public readonly record struct ConnectionsReadyData
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

/// <summary>
/// Data for MemberJoined SignalR event
/// </summary>
[MessagePackObject(true)]
public readonly record struct MemberJoinedData
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
    /// Current member count after join
    /// </summary>
    public required int CurrentMemberCount { get; init; }

    /// <summary>
    /// Maximum group capacity
    /// </summary>
    public required int MaxMembers { get; init; }
}

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
