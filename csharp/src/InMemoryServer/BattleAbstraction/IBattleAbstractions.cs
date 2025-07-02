namespace InMemoryServer.BattleAbstraction;

// IBattleGroupContext is now defined in Shared project

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
