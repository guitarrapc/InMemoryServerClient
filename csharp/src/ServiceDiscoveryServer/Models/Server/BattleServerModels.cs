namespace ServiceDiscoveryServer.Models.Server;

/// <summary>
/// BattleServer connection information
/// </summary>
public readonly record struct BattleServerConnectionInfo
{
    public required string ServerId { get; init; }
    public required string Address { get; init; }
    public int SignalRPort { get; init; }
    public int MagicOnionPort { get; init; }
    public string SignalRHubPath { get; init; } = "/battlehub";
    public ConnectionType SupportedTypes { get; init; }

    public BattleServerConnectionInfo()
    {
        ServerId = string.Empty;
        Address = string.Empty;
    }
}

/// <summary>
/// BattleServer registration information
/// </summary>
public readonly record struct BattleServerRegistration
{
    public required string ServerId { get; init; }
    public required string Address { get; init; }
    public int SignalRPort { get; init; }
    public int MagicOnionPort { get; init; }
    public int MaxConcurrentSessions { get; init; } = 3;
    public IReadOnlyList<string> SupportedModes { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    public BattleServerRegistration()
    {
        ServerId = string.Empty;
        Address = string.Empty;
    }
}

/// <summary>
/// BattleServer status information
/// </summary>
public readonly record struct BattleServerStatus
{
    public required string ServerId { get; init; }
    public ServerHealth Health { get; init; } = ServerHealth.Healthy;
    public int ActiveSessions { get; init; }
    public int MaxSessions { get; init; }
    public double CpuUsage { get; init; }
    public double MemoryUsage { get; init; }
    public DateTime LastHeartbeat { get; init; }

    public BattleServerStatus()
    {
        ServerId = string.Empty;
    }
}

/// <summary>
/// BattleServer information
/// </summary>
public readonly record struct BattleServerInfo
{
    public required string ServerId { get; init; }
    public required string Address { get; init; }
    public int SignalRPort { get; init; }
    public int MagicOnionPort { get; init; }
    public ServerHealth Health { get; init; }
    public int ActiveSessions { get; init; }
    public int MaxSessions { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public double LoadScore { get; init; }
}

/// <summary>
/// Server health enumeration
/// </summary>
public enum ServerHealth
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

/// <summary>
/// Connection type flags
/// </summary>
[Flags]
public enum ConnectionType
{
    None = 0,
    SignalR = 1,
    MagicOnion = 2,
    Both = SignalR | MagicOnion
}
