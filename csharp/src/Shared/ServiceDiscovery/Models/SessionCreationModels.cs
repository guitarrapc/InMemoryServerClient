namespace Shared.ServiceDiscovery.Models;

/// <summary>
/// Session creation request model
/// </summary>
public readonly record struct SessionCreationRequest
{
    public required string GroupName { get; init; }
    public int MaxPlayers { get; init; } = 5;
    public SessionMode Mode { get; init; } = SessionMode.Auto;
    public string? PreferredRegion { get; init; }

    public SessionCreationRequest()
    {
        GroupName = string.Empty;
    }
}

/// <summary>
/// Session creation response model
/// </summary>
public readonly record struct SessionCreationResponse
{
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public SessionInfo? Session { get; init; }
    public BattleServerConnectionInfo? ConnectionInfo { get; init; }
}
