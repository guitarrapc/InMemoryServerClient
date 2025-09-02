using MagicOnion;

namespace ServiceDiscoveryServer.Http2Server.Services;

/// <summary>
/// ServiceDiscovery MagicOnion service interface
/// </summary>
public interface IMagicOnionServiceDiscoveryService : IService<IMagicOnionServiceDiscoveryService>
{
    // Session management API
    UnaryResult<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request);
    UnaryResult<SessionInfo?> GetSessionInfoAsync(string sessionId);
    UnaryResult<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync();
    UnaryResult<bool> TerminateSessionAsync(string sessionId);

    // Server management API
    UnaryResult<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync();
    UnaryResult<BattleServerInfo?> GetAssignedServerAsync(string sessionId);

    // BattleServer API
    UnaryResult<bool> RegisterServerAsync(BattleServerRegistration registration);
    UnaryResult<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status);
    UnaryResult<bool> UnregisterServerAsync(string serverId);
}
