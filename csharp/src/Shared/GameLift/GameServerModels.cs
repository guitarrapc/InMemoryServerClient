namespace Shared.GameLift;

/// <summary>
/// Game server information
/// </summary>
/// <param name="GameServerId">Unique identifier for the game server</param>
/// <param name="FleetId">Fleet ID that the game server belongs to</param>
/// <param name="InstanceId">EC2 instance ID (if applicable)</param>
/// <param name="IpAddress">IP address of the game server</param>
/// <param name="Port">Port number of the game server</param>
/// <param name="Status">Current status of the game server</param>
/// <param name="ConnectionInfo">Connection information for the game server</param>
public readonly record struct GameServerInfo(
    string GameServerId,
    string FleetId,
    string InstanceId,
    string IpAddress,
    int Port,
    GameServerStatus Status,
    string ConnectionInfo)
{
    /// <summary>
    /// Empty game server info
    /// </summary>
    public static readonly GameServerInfo Empty = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        GameServerStatus.Unknown,
        string.Empty);
}

/// <summary>
/// Game server status enumeration
/// </summary>
public enum GameServerStatus
{
    /// <summary>
    /// Status is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// Game server is pending initialization
    /// </summary>
    Pending,

    /// <summary>
    /// Game server is active and available
    /// </summary>
    Active,

    /// <summary>
    /// Game server is terminating
    /// </summary>
    Terminating,

    /// <summary>
    /// Game server is terminated
    /// </summary>
    Terminated,

    /// <summary>
    /// Game server is available for game sessions
    /// </summary>
    Available,

    /// <summary>
    /// Game server is currently claimed for a game session
    /// </summary>
    Claimed,

    /// <summary>
    /// Game server is utilized and running a game session
    /// </summary>
    Utilized
}
