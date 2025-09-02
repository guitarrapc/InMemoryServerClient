using MagicOnion;
using MagicOnion.Server;

namespace ServiceDiscoveryServer.Http2Server.Services;

/// <summary>
/// ServiceDiscovery MagicOnion Service
/// </summary>
public sealed class ServiceDiscoveryService(ILogger<ServiceDiscoveryService> logger, ISessionManager sessionManager, IBattleServerRegistry serverRegistry) : ServiceBase<IServiceDiscoveryService>, IServiceDiscoveryService
{
    public async UnaryResult<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request)
    {
        try
        {
            logger.LogInformation("CreateOrFindSession request for group {GroupName} via MagicOnion", request.GroupName);

            var response = await sessionManager.CreateOrFindSessionAsync(request);

            logger.LogInformation("CreateOrFindSession response: {IsSuccess} for group {GroupName}",
                response.IsSuccess, request.GroupName);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in CreateOrFindSessionAsync for group {GroupName}", request.GroupName);
            return new SessionCreationResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Internal server error: {ex.Message}"
            };
        }
    }

    public async UnaryResult<SessionInfo?> GetSessionInfoAsync(string sessionId)
    {
        try
        {
            return await sessionManager.GetSessionInfoAsync(sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetSessionInfoAsync for session {SessionId}", sessionId);
            return null;
        }
    }

    public async UnaryResult<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync()
    {
        try
        {
            return await sessionManager.ListActiveSessionsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ListActiveSessionsAsync");
            return [];
        }
    }

    public async UnaryResult<bool> TerminateSessionAsync(string sessionId)
    {
        try
        {
            logger.LogInformation("Terminating session {SessionId} via MagicOnion", sessionId);
            return await sessionManager.TerminateSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in TerminateSessionAsync for session {SessionId}", sessionId);
            return false;
        }
    }

    public async UnaryResult<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync()
    {
        try
        {
            return await serverRegistry.ListAvailableServersAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ListAvailableServersAsync");
            return [];
        }
    }

    public async UnaryResult<BattleServerInfo?> GetAssignedServerAsync(string sessionId)
    {
        try
        {
            return await serverRegistry.GetAssignedServerAsync(sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetAssignedServerAsync for session {SessionId}", sessionId);
            return null;
        }
    }

    public async UnaryResult<bool> RegisterServerAsync(BattleServerRegistration registration)
    {
        try
        {
            logger.LogInformation("Registering BattleServer {ServerId} via MagicOnion", registration.ServerId);
            return await serverRegistry.RegisterServerAsync(registration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in RegisterServerAsync for server {ServerId}", registration.ServerId);
            return false;
        }
    }

    public async UnaryResult<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status)
    {
        try
        {
            return await serverRegistry.UpdateServerStatusAsync(serverId, status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in UpdateServerStatusAsync for server {ServerId}", serverId);
            return false;
        }
    }

    public async UnaryResult<bool> UnregisterServerAsync(string serverId)
    {
        try
        {
            logger.LogInformation("Unregistering BattleServer {ServerId} via MagicOnion", serverId);
            return await serverRegistry.UnregisterServerAsync(serverId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in UnregisterServerAsync for server {ServerId}", serverId);
            return false;
        }
    }
}

/// <summary>
/// ServiceDiscovery MagicOnion service interface
/// </summary>
public interface IServiceDiscoveryService : IService<IServiceDiscoveryService>
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
