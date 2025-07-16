namespace InMemoryServer.Models;

/// <summary>
/// Represents a client connection with protocol information
/// </summary>
public class ConnectionInfo
{
    /// <summary>
    /// Unique connection identifier (normalized across protocols)
    /// </summary>
    public required string ConnectionId { get; init; }

    /// <summary>
    /// Original connection identifier from the protocol
    /// </summary>
    public required string OriginalConnectionId { get; init; }

    /// <summary>
    /// Connection protocol type
    /// </summary>
    public required ConnectionProtocol Protocol { get; init; }

    /// <summary>
    /// Connection timestamp
    /// </summary>
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Connection protocol types
/// </summary>
public enum ConnectionProtocol
{
    SignalR,
    MagicOnion
}
