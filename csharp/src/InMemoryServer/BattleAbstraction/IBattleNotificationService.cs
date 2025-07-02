namespace InMemoryServer.BattleAbstraction;

// IBattleGroupContext is now defined in Shared project

/// <summary>
/// Service for battle notifications and communication
/// </summary>
public interface IBattleNotificationService
{
    Task NotifyBattleStatusAsync(string groupId, object status);
    Task NotifyBattleProgressAsync(string groupId, object progress);
    Task SendReplayDataAsync(string groupId, object replayData);
}
