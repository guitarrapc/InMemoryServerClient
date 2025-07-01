namespace BattleLogic.Interfaces;

/// <summary>
/// Represents battle group context information
/// </summary>
public interface IBattleGroupContext
{
    string Id { get; }
    string Name { get; }
    int MaxClients { get; }
    int ConnectedCount { get; }
    IReadOnlyList<string> ClientIds { get; }
}

/// <summary>
/// Service for battle replay storage operations
/// </summary>
public interface IBattleReplayStorage
{
    Task SaveBattleReplayAsync(string battleId, IEnumerable<string> replayData);
}

/// <summary>
/// Service for battle notifications and communication
/// </summary>
public interface IBattleNotificationService
{
    Task NotifyBattleStatusAsync(string groupId, object status);
    Task NotifyBattleProgressAsync(string groupId, object progress);
    Task SendReplayDataAsync(string groupId, object replayData);
}
