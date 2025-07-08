namespace Shared.Models;

/// <summary>
/// Connection type for protocol selection
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// SignalR connection
    /// </summary>
    SignalR,

    /// <summary>
    /// MagicOnion connection (future implementation)
    /// </summary>
    MagicOnion
}
