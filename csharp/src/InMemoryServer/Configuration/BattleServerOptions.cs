namespace InMemoryServer.Configuration;

/// <summary>
/// BattleServer configuration options
/// </summary>
public class BattleServerOptions
{
    public const string SectionName = "BattleServer";

    public static readonly BattleServerOptions Default = new()
    {
        ServiceDiscovery = ServiceDiscoveryOptions.Default,
        Server = ServerOptions.Default,
    };

    public static string ServerId { get; } = Environment.MachineName;
    public required ServiceDiscoveryOptions ServiceDiscovery { get; init; }
    public required ServerOptions Server { get; init; }
}

/// <summary>
/// ServiceDiscovery connection options
/// </summary>
public class ServiceDiscoveryOptions
{
    public static readonly ServiceDiscoveryOptions Default = new()
    {
        SignalREndpoint = "http://localhost:5010",
        MagicOnionEndpoint = "http://localhost:5011",
        RegistrationIntervalSeconds = 10,
        HeartbeatIntervalSeconds = 30,
    };

    public required string SignalREndpoint { get; init; }
    public required string MagicOnionEndpoint { get; init; }
    public required int RegistrationIntervalSeconds { get; init; }
    public required int HeartbeatIntervalSeconds { get; init; }
}

/// <summary>
/// Server configuration options
/// </summary>
public class ServerOptions
{
    public static readonly ServerOptions Default = new()
    {
        MaxConcurrentSessions = 3,
    };

    public required int MaxConcurrentSessions { get; init; }
}
