namespace Shared;

/// <summary>
/// Constants used in InMemoryServer
/// </summary>
public static class SystemDefines
{
    /// <summary>
    /// SignalR hub route
    /// </summary>
    public const string HubRoute = "/inmemoryhub";

    /// <summary>
    /// Default server port
    /// </summary>
    public const int DefaultServerPort = 5000;

    /// <summary>
    /// Maximum connections per group
    /// </summary>
    public const int MaxConnectionsPerGroup = 5;

    /// <summary>
    /// Group expiration time in minutes
    /// </summary>
    public const int GroupExpirationMinutes = 10;
}
