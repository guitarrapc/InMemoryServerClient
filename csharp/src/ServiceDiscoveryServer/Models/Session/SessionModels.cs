namespace ServiceDiscoveryServer.Models.Session;

/// <summary>
/// Session information model
/// </summary>
public readonly record struct SessionInfo
{
    public required string SessionId { get; init; }
    public required string GroupName { get; init; }
    public SessionMode Mode { get; init; }
    public SessionStatus Status { get; init; }
    public required string AssignedServerId { get; init; }
    public int CurrentPlayers { get; init; }
    public int MaxPlayers { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    // GameLift mode only
    public string? GameSessionId { get; init; }
    public string? FleetId { get; init; }
}

/// <summary>
/// Session mode enumeration
/// </summary>
public enum SessionMode
{
    /// <summary>Server selects optimal mode</summary>
    Auto,
    /// <summary>Force GameLift Anywhere usage</summary>
    GameLift,
    /// <summary>Force Direct connection usage</summary>
    Direct
}

/// <summary>
/// Session status enumeration
/// </summary>
public enum SessionStatus
{
    Creating,
    Active,
    InBattle,
    Completed,
    Terminated,
    Error
}
