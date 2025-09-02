namespace Shared.GameLift;

/// <summary>
/// Request model for server-side GameSession creation via SignalR
/// </summary>
public class GameSessionCreationRequest
{
    /// <summary>
    /// Fleet ID to create the session in
    /// </summary>
    public string FleetId { get; init; } = string.Empty;

    /// <summary>
    /// Location/region for the GameSession
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Name for the GameSession (typically group name)
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Maximum number of players allowed
    /// </summary>
    public int MaxPlayers { get; init; } = 5;

    /// <summary>
    /// Optional game session data
    /// </summary>
    public string? GameSessionData { get; init; }

    /// <summary>
    /// Client ID requesting the session (for tracking)
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Create request for auto-battle session
    /// </summary>
    public static GameSessionCreationRequest ForAutoBattle(string fleetId, string location, string groupName, string clientId) => new()
    {
        FleetId = fleetId,
        Location = location,
        Name = groupName,
        MaxPlayers = 5,
        GameSessionData = groupName,
        ClientId = clientId
    };
}

/// <summary>
/// Response model for server-side GameSession creation
/// </summary>
public class GameSessionCreationResponse
{
    /// <summary>
    /// Created or found GameSession information
    /// </summary>
    public GameSessionInfo GameSession { get; init; } = GameSessionInfo.Empty;

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Connection endpoint for the client
    /// </summary>
    public string ConnectionEndpoint { get; init; } = string.Empty;

    /// <summary>
    /// Whether this is a newly created session or existing one
    /// </summary>
    public bool IsNewSession { get; init; }

    /// <summary>
    /// Failed response
    /// </summary>
    public static GameSessionCreationResponse Failed(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };

    /// <summary>
    /// Successful response for new session
    /// </summary>
    public static GameSessionCreationResponse Success(GameSessionInfo gameSession, string connectionEndpoint, bool isNewSession = true) =>
        new()
        {
            GameSession = gameSession,
            ConnectionEndpoint = connectionEndpoint,
            IsSuccess = true,
            IsNewSession = isNewSession
        };
}

/// <summary>
/// Request model for PlayerSession creation via SignalR
/// </summary>
public class PlayerSessionCreationRequest
{
    /// <summary>
    /// GameSession ID to join
    /// </summary>
    public string GameSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Player ID
    /// </summary>
    public string PlayerId { get; init; } = string.Empty;

    /// <summary>
    /// Client ID requesting the player session
    /// </summary>
    public string ClientId { get; init; } = string.Empty;
}

/// <summary>
/// Response model for PlayerSession creation
/// </summary>
public class PlayerSessionCreationResponse
{
    /// <summary>
    /// Created PlayerSession information
    /// </summary>
    public PlayerSessionInfo PlayerSession { get; init; } = PlayerSessionInfo.Empty;

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Failed response
    /// </summary>
    public static PlayerSessionCreationResponse Failed(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };

    /// <summary>
    /// Successful response
    /// </summary>
    public static PlayerSessionCreationResponse Success(PlayerSessionInfo playerSession) =>
        new() { PlayerSession = playerSession, IsSuccess = true };
}
