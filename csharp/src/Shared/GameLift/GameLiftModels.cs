namespace Shared.GameLift;

/// <summary>
/// Compute information for GameLift Anywhere
/// </summary>
public readonly record struct ComputeInfo(
    string ComputeName,
    string FleetId,
    string CustomLocation,
    string ComputeArn,
    ComputeStatus Status)
{
    /// <summary>
    /// Empty compute info
    /// </summary>
    public static readonly ComputeInfo Empty = new(string.Empty, string.Empty, string.Empty, string.Empty, ComputeStatus.Unknown);
}

/// <summary>
/// Compute status enumeration
/// </summary>
public enum ComputeStatus
{
    Unknown,
    Pending,
    Active,
    Terminating,
    Terminated
}

/// <summary>
/// Auth token information for GameLift server SDK
/// </summary>
public readonly record struct AuthTokenInfo(
    string AuthToken,
    string WebSocketUrl,
    DateTime ExpirationTime)
{
    /// <summary>
    /// Empty auth token info
    /// </summary>
    public static readonly AuthTokenInfo Empty = new(string.Empty, string.Empty, DateTime.MinValue);

    /// <summary>
    /// Checks if the auth token is expired or will expire soon
    /// </summary>
    /// <param name="buffer">Buffer time before expiration</param>
    /// <returns>True if token needs refresh</returns>
    public readonly bool NeedsRefresh(TimeSpan buffer) => DateTime.UtcNow.Add(buffer) >= ExpirationTime;
}

/// <summary>
/// Game session information
/// </summary>
public class GameSessionInfo
{
    public string GameSessionId { get; init; } = string.Empty;
    public string FleetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int CurrentPlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public string? IpAddress { get; init; }
    public int Port { get; init; }
    public string? GameSessionData { get; init; }
    public DateTime CreationTime { get; init; }

    /// <summary>
    /// Empty game session info
    /// </summary>
    public static readonly GameSessionInfo Empty = new();
}

/// <summary>
/// Game server information for client discovery
/// </summary>
public class GameServerInfo
{
    public string ServerId { get; init; } = string.Empty;
    public string FleetId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string ConnectionEndpoint { get; init; } = string.Empty;

    /// <summary>
    /// Empty game server info
    /// </summary>
    public static readonly GameServerInfo Empty = new();
}

/// <summary>
/// GameSession creation request
/// </summary>
public class CreateGameSessionRequest
{
    public string FleetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int MaxPlayers { get; init; } = 5;
    public string? GameSessionData { get; init; }

    /// <summary>
    /// Default request for this game (5 players, auto-battle)
    /// </summary>
    public static CreateGameSessionRequest ForAutoBattle(string fleetId, string creatorId = "auto-client") =>
        new()
        {
            FleetId = fleetId,
            Name = $"AutoBattle-{creatorId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            MaxPlayers = 5,
            GameSessionData = "auto-battle"
        };
}

/// <summary>
/// GameSession creation response
/// </summary>
public class CreateGameSessionResponse
{
    public GameSessionInfo GameSession { get; init; } = GameSessionInfo.Empty;
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Failed response
    /// </summary>
    public static CreateGameSessionResponse Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };

    /// <summary>
    /// Successful response
    /// </summary>
    public static CreateGameSessionResponse CreateSuccessful(GameSessionInfo gameSession) =>
        new() { GameSession = gameSession, Success = true };
}

/// <summary>
/// Player session information
/// </summary>
public class PlayerSessionInfo
{
    public string PlayerSessionId { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string GameSessionId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreationTime { get; init; }
    public string? IpAddress { get; init; }
    public int Port { get; init; }

    /// <summary>
    /// Empty player session info
    /// </summary>
    public static readonly PlayerSessionInfo Empty = new();
}

/// <summary>
/// Game session status values
/// </summary>
public static class GameSessionStatus
{
    public const string Activating = "ACTIVATING";
    public const string Active = "ACTIVE";
    public const string Terminating = "TERMINATING";
    public const string Terminated = "TERMINATED";
    public const string Error = "ERROR";
    public const string Unknown = "UNKNOWN";
}

/// <summary>
/// Player session status values
/// </summary>
public static class PlayerSessionStatus
{
    public const string Reserved = "RESERVED";
    public const string Active = "ACTIVE";
    public const string Completed = "COMPLETED";
    public const string TimedOut = "TIMEDOUT";
    public const string Unknown = "UNKNOWN";
}
