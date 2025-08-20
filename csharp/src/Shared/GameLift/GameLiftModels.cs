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
    string ServiceSdkEndpoint,
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
public readonly record struct GameSessionInfo(
    string GameSessionId,
    string FleetId,
    int MaxPlayers,
    Dictionary<string, string> GameProperties)
{
    /// <summary>
    /// Empty game session info
    /// </summary>
    public static readonly GameSessionInfo Empty = new(string.Empty, string.Empty, 0, new Dictionary<string, string>());
}
