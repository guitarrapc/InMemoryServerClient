namespace ServiceDiscoveryServer.Http1Server.Hubs;

/// <summary>
/// ServiceDiscovery SignalR Hub
/// </summary>
public sealed class ServiceDiscoveryHub : Hub
{
    private readonly ILogger<ServiceDiscoveryHub> _logger;
    private readonly ISessionManager _sessionManager;
    private readonly IBattleServerRegistry _serverRegistry;

    public ServiceDiscoveryHub(
        ILogger<ServiceDiscoveryHub> logger,
        ISessionManager sessionManager,
        IBattleServerRegistry serverRegistry)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _serverRegistry = serverRegistry;
    }

    /// <summary>
    /// Create or find session
    /// </summary>
    /// <param name="request">Session creation request</param>
    /// <returns>Session creation response</returns>
    public async Task<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request)
    {
        try
        {
            _logger.LogInformation("CreateOrFindSession request for group {GroupName} from connection {ConnectionId}",
                request.GroupName, Context.ConnectionId);

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

    /// <summary>
    /// Get session information
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Session information or null</returns>
    public async Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
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

    /// <summary>
    /// List active sessions
    /// </summary>
    /// <returns>List of active sessions</returns>
    public async Task<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync()
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

    /// <summary>
    /// Terminate session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>True if successfully terminated</returns>
    public async Task<bool> TerminateSessionAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation("Terminating session {SessionId} from connection {ConnectionId}",
                sessionId, Context.ConnectionId);

            return await _sessionManager.TerminateSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TerminateSessionAsync for session {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// List available servers
    /// </summary>
    /// <returns>List of available servers</returns>
    public async Task<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync()
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

    /// <summary>
    /// Get assigned server for session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Assigned server information or null</returns>
    public async Task<BattleServerInfo?> GetAssignedServerAsync(string sessionId)
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

    /// <summary>
    /// Register BattleServer (for BattleServer use)
    /// </summary>
    /// <param name="registration">Server registration information</param>
    /// <returns>True if successfully registered</returns>
    public async Task<bool> RegisterServerAsync(BattleServerRegistration registration)
    {
        try
        {
            _logger.LogInformation("Registering BattleServer {ServerId} from connection {ConnectionId}",
                registration.ServerId, Context.ConnectionId);

            return await _serverRegistry.RegisterServerAsync(registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RegisterServerAsync for server {ServerId}", registration.ServerId);
            return false;
        }
    }

    /// <summary>
    /// Update server status (for BattleServer use)
    /// </summary>
    /// <param name="serverId">Server ID</param>
    /// <param name="status">Updated status</param>
    /// <returns>True if successfully updated</returns>
    public async Task<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status)
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

    /// <summary>
    /// Unregister server (for BattleServer use)
    /// </summary>
    /// <param name="serverId">Server ID</param>
    /// <returns>Task</returns>
    public async Task UnregisterServerAsync(string serverId)
    {
        try
        {
            _logger.LogInformation("Unregistering BattleServer {ServerId} from connection {ConnectionId}",
                serverId, Context.ConnectionId);

            await _serverRegistry.UnregisterServerAsync(serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UnregisterServerAsync for server {ServerId}", serverId);
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }
}
