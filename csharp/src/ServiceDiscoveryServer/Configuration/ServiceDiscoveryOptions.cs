namespace ServiceDiscoveryServer.Configuration;

/// <summary>
/// ServiceDiscovery server configuration options
/// </summary>
public class ServiceDiscoveryOptions
{
    public const string SectionName = "ServiceDiscovery";
    public static readonly ServiceDiscoveryOptions Default = new()
    {
        Session = SessionOptions.Default,
        BattleServer = BattleServerOptions.Default,
        GameLift = GameLiftOptions.Default,
    };

    public required SessionOptions Session { get; init; }
    public required BattleServerOptions BattleServer { get; init; }
    public required GameLiftOptions GameLift { get; init; }
}

/// <summary>
/// Session management configuration options
/// </summary>
public class SessionOptions
{
    public static readonly SessionOptions Default = new()
    {
        DefaultMaxPlayers = 5,
        SessionTimeoutMinutes = 30,
        CleanupIntervalMinutes = 5,
        MaxConcurrentSessions = 100,
    };
    public required int DefaultMaxPlayers { get; init; }
    public required int SessionTimeoutMinutes { get; init; }
    public required int CleanupIntervalMinutes { get; init; }
    public required int MaxConcurrentSessions { get; init; }
}

/// <summary>
/// BattleServer management configuration options
/// </summary>
public class BattleServerOptions
{
    public static readonly BattleServerOptions Default = new()
    {
        HeartbeatIntervalSeconds = 30,
        HealthCheckTimeoutSeconds = 10,
        UnhealthyThresholdCount = 3,
        RemoveUnhealthyAfterMinutes = 5,
    };
    public required int HeartbeatIntervalSeconds { get; init; }
    public required int HealthCheckTimeoutSeconds { get; init; }
    public required int UnhealthyThresholdCount { get; init; }
    public required int RemoveUnhealthyAfterMinutes { get; init; }
}

/// <summary>
/// GameLift integration configuration options
/// </summary>
public class GameLiftOptions
{
    public static readonly GameLiftOptions Default = new()
    {
        Mode = "Disabled",
        Anywhere = new GameLiftAnywhereOptions()
        {
            FleetId = string.Empty,
            CustomLocation = string.Empty,
            Region = "ap-northeast-1",
            MaxGameSessionsPerFleet = 50,
        }
    };
    public required string Mode { get; init; }
    public required GameLiftAnywhereOptions Anywhere { get; init; }
}

/// <summary>
/// GameLift Anywhere configuration options
/// </summary>
public class GameLiftAnywhereOptions
{
    public required string FleetId { get; init; }
    public required string CustomLocation { get; init; }
    public required string Region { get; init; }
    public required int MaxGameSessionsPerFleet { get; init; }
}
