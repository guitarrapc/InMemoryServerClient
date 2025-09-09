namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// Interface for notifying BattleServers about session events
/// </summary>
public interface IBattleServerNotifier
{
    /// <summary>
    /// Notify BattleServer that a session is ready to start battle
    /// </summary>
    /// <param name="serverId">BattleServer ID</param>
    /// <param name="sessionInfo">Session information</param>
    /// <returns>True if notification was sent successfully</returns>
    Task<bool> NotifyBattleReadyAsync(string serverId, SessionInfo sessionInfo);
}
