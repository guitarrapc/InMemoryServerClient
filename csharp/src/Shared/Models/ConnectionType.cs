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
    /// MagicOnion connection
    /// </summary>
    MagicOnion,

    /// <summary>
    /// Historical connection (no actual connection, for replay viewing)
    /// </summary>
    Historical
}
