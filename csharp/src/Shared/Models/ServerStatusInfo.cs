namespace Shared.Models;

/// <summary>
/// Server status information for client abstraction
/// </summary>
public readonly record struct ServerStatusInfo(
    TimeSpan Uptime,
    int TotalConnections,
    int ActiveGroups,
    int ActiveBattles,
    IReadOnlyList<ClientGroupInfo> Groups
);

/// <summary>
/// Group information for client abstraction
/// </summary>
public readonly record struct ClientGroupInfo(
    string GroupId,
    string GroupName,
    int MemberCount,
    int MaxMembers,
    TimeSpan RemainingTime
);
