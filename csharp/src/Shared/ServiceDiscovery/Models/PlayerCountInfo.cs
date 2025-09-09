namespace Shared.ServiceDiscovery.Models;

/// <summary>
/// Player count information for a session
/// </summary>
public readonly record struct PlayerCountInfo
{
    /// <summary>
    /// Session ID
    /// </summary>
    public string SessionId { get; init; }

    /// <summary>
    /// Current number of players in the session
    /// </summary>
    public int CurrentPlayers { get; init; }

    /// <summary>
    /// Maximum number of players allowed in the session
    /// </summary>
    public int MaxPlayers { get; init; }

    /// <summary>
    /// Whether the session is full
    /// </summary>
    public bool IsFull => CurrentPlayers >= MaxPlayers;

    /// <summary>
    /// Last time this information was updated
    /// </summary>
    public DateTime LastUpdated { get; init; }

    /// <summary>
    /// Create player count info
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="currentPlayers">Current players</param>
    /// <param name="maxPlayers">Max players</param>
    /// <param name="lastUpdated">Last updated time</param>
    public PlayerCountInfo(string sessionId, int currentPlayers, int maxPlayers, DateTime lastUpdated)
    {
        SessionId = sessionId;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
        LastUpdated = lastUpdated;
    }
}
