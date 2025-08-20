namespace Shared.GameLift;

/// <summary>
/// GameLift configuration options
/// </summary>
public class GameLiftOptions
{
    /// <summary>
    /// GameLift operation mode
    /// </summary>
    public GameLiftMode Mode { get; set; } = GameLiftMode.Direct;

    /// <summary>
    /// GameLift Anywhere specific configuration
    /// </summary>
    public GameLiftAnywhereOptions Anywhere { get; set; } = new();

    /// <summary>
    /// GameLift FleetIQ specific configuration
    /// </summary>
    public GameLiftFleetIQOptions FleetIQ { get; set; } = new();

    /// <summary>
    /// AWS configuration
    /// </summary>
    public AWSOptions AWS { get; set; } = new();

    /// <summary>
    /// Client-specific configuration
    /// </summary>
    public ClientOptions Client { get; set; } = new();
}

/// <summary>
/// GameLift operation modes
/// </summary>
public enum GameLiftMode
{
    /// <summary>
    /// Direct connection without GameLift
    /// </summary>
    Direct,

    /// <summary>
    /// GameLift Fleet Anywhere
    /// </summary>
    Anywhere,

    /// <summary>
    /// GameLift FleetIQ
    /// </summary>
    FleetIQ
}

/// <summary>
/// GameLift Anywhere configuration options
/// </summary>
public class GameLiftAnywhereOptions
{
    /// <summary>
    /// GameLift Fleet ID
    /// </summary>
    public string FleetId { get; set; } = string.Empty;

    /// <summary>
    /// Compute name for GameLift Anywhere
    /// </summary>
    public string ComputeName { get; set; } = string.Empty;

    /// <summary>
    /// Custom location for GameLift Anywhere
    /// </summary>
    public string CustomLocation { get; set; } = string.Empty;

    // Control plane settings
    /// <summary>
    /// Host ID for GameLift server registration
    /// </summary>
    public string HostId { get; set; } = Environment.MachineName;

    /// <summary>
    /// Host IP Address for GameLift server registration
    /// </summary>
    public string IpAddress { get; set; } = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();

    /// <summary>
    /// Process ID for GameLift server registration
    /// </summary>
    public string ProcessId { get; set; } = Environment.ProcessId.ToString();

    /// <summary>
    /// Auth token refresh interval
    /// </summary>
    public TimeSpan AuthTokenRefreshInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Cleanup threshold for old compute instances
    /// </summary>
    public TimeSpan ComputeCleanupThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to cleanup compute instance on application startup
    /// </summary>
    public bool CleanupComputeOnStartup { get; set; } = false;

    /// <summary>
    /// Whether to cleanup compute instance on application shutdown
    /// </summary>
    public bool CleanupComputeOnShutdown { get; set; } = false;

    // Server SDK settings
    /// <summary>
    /// Health check timeout
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum concurrent game sessions
    /// </summary>
    public int MaxConcurrentGameSessions { get; set; } = 3;

    /// <summary>
    /// Game session idle timeout
    /// </summary>
    public TimeSpan GameSessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Delay before cleaning up completed game sessions
    /// </summary>
    public TimeSpan GameSessionCleanupDelay { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// GameLift FleetIQ configuration options
/// </summary>
public class GameLiftFleetIQOptions
{
    /// <summary>
    /// Game server group name
    /// </summary>
    public string GameServerGroupName { get; set; } = string.Empty;

    /// <summary>
    /// Game server ID
    /// </summary>
    public string GameServerId { get; set; } = string.Empty;

    /// <summary>
    /// Instance ID for FleetIQ
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;
}

/// <summary>
/// AWS configuration options
/// </summary>
public class AWSOptions
{
    /// <summary>
    /// AWS region
    /// </summary>
    public string Region { get; set; } = "us-west-2";

    /// <summary>
    /// AWS CLI Profile name (recommended)
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// AWS Identity Center SSO Session name (recommended)
    /// </summary>
    public string SsoSessionName { get; set; } = string.Empty;

    /// <summary>
    /// AWS Access Key ID (deprecated: for development/testing only)
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// AWS Secret Access Key (deprecated: for development/testing only)
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

/// <summary>
/// AWS Session Token (for STS token usage)
/// </summary>
public string SessionToken { get; set; } = string.Empty;
}

/// <summary>
/// Client-specific configuration options
/// </summary>
public class ClientOptions
{
    /// <summary>
    /// Default server URL for direct connections
    /// </summary>
    public string DefaultServerUrl { get; set; } = "wss://localhost:5001/battlehub";

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30000;
}
