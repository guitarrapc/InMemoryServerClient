namespace Shared.Constants;

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
    /// Default server port (HTTP/1 - SignalR)
    /// </summary>
    public const int DefaultServerPort = 5000;

    /// <summary>
    /// Default server port for HTTP/2 (MagicOnion)
    /// </summary>
    public const int DefaultHttp2ServerPort = 5001;

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
