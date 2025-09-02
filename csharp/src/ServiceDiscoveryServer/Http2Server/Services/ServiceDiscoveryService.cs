using MagicOnion;
using MagicOnion.Server;

namespace ServiceDiscoveryServer.Http2Server.Services;

/// <summary>
/// ServiceDiscovery MagicOnion Service
/// </summary>
public sealed class ServiceDiscoveryService : ServiceBase<IServiceDiscoveryService>, IServiceDiscoveryService
{
    private readonly ILogger<ServiceDiscoveryService> _logger;
    private readonly ISessionManager _sessionManager;
    private readonly IBattleServerRegistry _serverRegistry;

    public ServiceDiscoveryService(
        ILogger<ServiceDiscoveryService> logger,
        ISessionManager sessionManager,
        IBattleServerRegistry serverRegistry)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _serverRegistry = serverRegistry;
    }

    public async UnaryResult<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request)
    {
        try
        {
            _logger.LogInformation("CreateOrFindSession request for group {GroupName} via MagicOnion", request.GroupName);

            var response = await _sessionManager.CreateOrFindSessionAsync(request);

            _logger.LogInformation("CreateOrFindSession response: {IsSuccess} for group {GroupName}",
                response.IsSuccess, request.GroupName);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateOrFindSessionAsync for group {GroupName}", request.GroupName);
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
            return await _sessionManager.GetSessionInfoAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetSessionInfoAsync for session {SessionId}", sessionId);
            return null;
        }
    }

    public async UnaryResult<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync()
    {
        try
        {
            return await _sessionManager.ListActiveSessionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListActiveSessionsAsync");
            return Array.Empty<SessionInfo>();
        }
    }

    public async UnaryResult<bool> TerminateSessionAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation("Terminating session {SessionId} via MagicOnion", sessionId);
            return await _sessionManager.TerminateSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TerminateSessionAsync for session {SessionId}", sessionId);
            return false;
        }
    }

    public async UnaryResult<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync()
    {
        try
        {
            return await _serverRegistry.ListAvailableServersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListAvailableServersAsync");
            return Array.Empty<BattleServerInfo>();
        }
    }

    public async UnaryResult<BattleServerInfo?> GetAssignedServerAsync(string sessionId)
    {
        try
        {
            return await _serverRegistry.GetAssignedServerAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAssignedServerAsync for session {SessionId}", sessionId);
            return null;
        }
    }

    public async UnaryResult<bool> RegisterServerAsync(BattleServerRegistration registration)
    {
        try
        {
            _logger.LogInformation("Registering BattleServer {ServerId} via MagicOnion", registration.ServerId);
            return await _serverRegistry.RegisterServerAsync(registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RegisterServerAsync for server {ServerId}", registration.ServerId);
            return false;
        }
    }

    public async UnaryResult<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status)
    {
        try
        {
            return await _serverRegistry.UpdateServerStatusAsync(serverId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateServerStatusAsync for server {ServerId}", serverId);
            return false;
        }
    }

    public async UnaryResult<bool> UnregisterServerAsync(string serverId)
    {
        try
        {
            _logger.LogInformation("Unregistering BattleServer {ServerId} via MagicOnion", serverId);
            return await _serverRegistry.UnregisterServerAsync(serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UnregisterServerAsync for server {ServerId}", serverId);
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
