namespace ServiceDiscoveryServer.Configuration;

/// <summary>
/// ServiceDiscovery server configuration options
/// </summary>
public class ServiceDiscoveryOptions
{
    public const string SectionName = "ServiceDiscovery";

    public ServerOptions Server { get; set; } = new();
    public SessionOptions Session { get; set; } = new();
    public BattleServerOptions BattleServer { get; set; } = new();
    public GameLiftOptions GameLift { get; set; } = new();
}

/// <summary>
/// Server configuration options
/// </summary>
public class ServerOptions
{
    public int SignalRPort { get; set; } = 5010;
    public int MagicOnionPort { get; set; } = 5011;
    public int HealthCheckPort { get; set; } = 5012;
    public string[] AllowedOrigins { get; set; } = ["*"];
}

/// <summary>
/// Session management configuration options
/// </summary>
public class SessionOptions
{
    public int DefaultMaxPlayers { get; set; } = 5;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int CleanupIntervalMinutes { get; set; } = 5;
    public int MaxConcurrentSessions { get; set; } = 100;
}

/// <summary>
/// BattleServer management configuration options
/// </summary>
public class BattleServerOptions
{
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int HealthCheckTimeoutSeconds { get; set; } = 10;
    public int UnhealthyThresholdCount { get; set; } = 3;
    public int RemoveUnhealthyAfterMinutes { get; set; } = 5;
}

/// <summary>
/// GameLift integration configuration options
/// </summary>
public class GameLiftOptions
{
    public string Mode { get; set; } = "Auto"; // "Disabled", "Auto", "Anywhere"
    public GameLiftAnywhereOptions Anywhere { get; set; } = new();
    public GameLiftAwsOptions AWS { get; set; } = new();
}

/// <summary>
/// GameLift Anywhere configuration options
/// </summary>
public class GameLiftAnywhereOptions
{
    public string FleetId { get; set; } = string.Empty;
    public string CustomLocation { get; set; } = string.Empty;
    public string Region { get; set; } = "ap-northeast-1";
    public int MaxGameSessionsPerFleet { get; set; } = 50;
}

/// <summary>
/// GameLift AWS configuration options
/// </summary>
public class GameLiftAwsOptions
{
    public string Profile { get; set; } = string.Empty;
    public string Region { get; set; } = "ap-northeast-1";
}
