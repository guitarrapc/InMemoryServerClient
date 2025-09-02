namespace Shared.Constants;

/// <summary>
/// Constants used in InMemoryServer
/// </summary>
public static class SystemDefines
{
    /// <summary>
    /// SignalR hub route
    /// </summary>
    public const string BattleHubRoute = "/inmemoryhub";
    public const string ServiceDiscoveryHubRoute = "/discoveryhub";

    /// <summary>
    /// Maximum connections per group
    /// </summary>
    public const int MaxConnectionsPerGroup = 5;

    /// <summary>
    /// Group expiration time in minutes
    /// </summary>
    public const int GroupExpirationMinutes = 10;

    /// <summary>
    /// Group waiting timeout in minutes (time to wait for members to join before auto-dissolve)
    /// </summary>
    public const int GroupWaitingTimeoutMinutes = 3;

    /// <summary>
    /// Maximum number of extensions allowed for a group before auto-dissolve
    /// </summary>
    public const int MaxGroupExtensions = 3;

    /// <summary>
    /// Extension duration in minutes
    /// </summary>
    public const int GroupExtensionMinutes = 2;
}
