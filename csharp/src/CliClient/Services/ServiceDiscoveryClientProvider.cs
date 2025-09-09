using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Shared.ServiceDiscovery.Models;
using Shared.Models;
using CliClient.Clients;

namespace CliClient.Services;

/// <summary>
/// Service Discovery client provider for dynamic server discovery and connection
/// </summary>
public sealed class ServiceDiscoveryClientProvider(ILogger<ServiceDiscoveryClientProvider> logger, ILoggerFactory loggerFactory) : IDisposable
{
    private HubConnection? _hubConnection;

    /// <summary>
    /// Initialize connection to ServiceDiscovery server
    /// </summary>
    /// <param name="serviceDiscoveryUrl">ServiceDiscovery server URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InitializeAsync(string serviceDiscoveryUrl, CancellationToken cancellationToken = default)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            logger.LogDebug("Already connected to ServiceDiscovery server");
            return;
        }

        try
        {
            logger.LogInformation("Connecting to ServiceDiscovery server at {Url}", serviceDiscoveryUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{serviceDiscoveryUrl}/discoveryHub")
                .WithAutomaticReconnect()
                .Build();

            await _hubConnection.StartAsync(cancellationToken);

            logger.LogInformation("Successfully connected to ServiceDiscovery server");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to ServiceDiscovery server at {Url}", serviceDiscoveryUrl);
            throw;
        }
    }

    /// <summary>
    /// Create or find session for the specified group
    /// </summary>
    /// <param name="groupName">Group name</param>
    /// <param name="maxPlayers">Maximum players per session</param>
    /// <param name="mode">Session mode</param>
    /// <returns>Session creation response</returns>
    public async Task<SessionCreationResponse> CreateOrFindSessionAsync(string groupName, int maxPlayers = 5, SessionMode mode = SessionMode.Auto)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to ServiceDiscovery server. Call InitializeAsync first.");
        }

        try
        {
            logger.LogInformation("Requesting session for group {GroupName} with {MaxPlayers} players", groupName, maxPlayers);

            var request = new SessionCreationRequest
            {
                GroupName = groupName,
                MaxPlayers = maxPlayers,
                Mode = mode
            };

            var response = await _hubConnection.InvokeAsync<SessionCreationResponse>("CreateOrFindSessionAsync", request);

            if (response.IsSuccess && response.Session is not null)
            {
                logger.LogInformation("Successfully obtained session {SessionId} for group {GroupName}",
                    response.Session.Value.SessionId, groupName);
            }
            else
            {
                logger.LogWarning("Failed to obtain session for group {GroupName}: {Error}",
                    groupName, response.ErrorMessage);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating or finding session for group {GroupName}", groupName);
            throw;
        }
    }

    /// <summary>
    /// Get session information
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Session information</returns>
    public async Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to ServiceDiscovery server. Call InitializeAsync first.");
        }

        try
        {
            logger.LogDebug("Getting session info for session {SessionId}", sessionId);
            return await _hubConnection.InvokeAsync<SessionInfo?>("GetSessionInfoAsync", sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting session info for session {SessionId}", sessionId);
            return null;
        }
    }

    /// <summary>
    /// List all active sessions
    /// </summary>
    /// <returns>List of active sessions</returns>
    public async Task<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync()
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to ServiceDiscovery server. Call InitializeAsync first.");
        }

        try
        {
            logger.LogDebug("Listing active sessions");
            return await _hubConnection.InvokeAsync<IReadOnlyList<SessionInfo>>("ListActiveSessionsAsync");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing active sessions");
            return [];
        }
    }

    /// <summary>
    /// Get assigned battle server information
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Battle server information</returns>
    public async Task<BattleServerInfo?> GetAssignedServerAsync(string sessionId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to ServiceDiscovery server. Call InitializeAsync first.");
        }

        try
        {
            logger.LogDebug("Getting assigned server for session {SessionId}", sessionId);
            return await _hubConnection.InvokeAsync<BattleServerInfo?>("GetAssignedServerAsync", sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting assigned server for session {SessionId}", sessionId);
            return null;
        }
    }

    /// <summary>
    /// Create battle client and connect to assigned battle server
    /// </summary>
    /// <param name="connectionInfo">Battle server connection information</param>
    /// <param name="groupName">Group name to join after connection</param>
    /// <param name="connectionType">Preferred connection type</param>
    /// <returns>Connected battle client</returns>
    public async Task<IBattleClient> ConnectToBattleServerAsync(BattleServerConnectionInfo connectionInfo, string groupName, ConnectionType connectionType = ConnectionType.SignalR)
    {
        try
        {
            logger.LogInformation("Connecting to BattleServer {ServerId} at {Address}:{Port} using {ConnectionType}",
                connectionInfo.ServerId, connectionInfo.Address,
                connectionType == ConnectionType.SignalR ? connectionInfo.SignalRPort : connectionInfo.MagicOnionPort,
                connectionType);

            var battleClient = BattleClientFactory.Create(connectionType, loggerFactory);

            var serverUrl = connectionType switch
            {
                ConnectionType.SignalR => $"http://{connectionInfo.Address}:{connectionInfo.SignalRPort}",
                ConnectionType.MagicOnion => $"http://{connectionInfo.Address}:{connectionInfo.MagicOnionPort}",
                _ => throw new ArgumentException($"Unsupported connection type: {connectionType}")
            };

            // Connect to server and join the group
            var connected = await battleClient.ConnectAsync(serverUrl, groupName);
            if (!connected)
            {
                // 接続失敗時はServiceDiscoveryのプレイヤー数を減算
                // Note: sessionId needs to be passed from caller if needed for cleanup
                throw new InvalidOperationException($"Failed to connect to BattleServer {connectionInfo.ServerId}");
            }

            logger.LogInformation("Successfully connected to BattleServer {ServerId} and joined group {GroupName}", connectionInfo.ServerId, groupName);
            return battleClient;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to BattleServer {ServerId} at {Address}",
                connectionInfo.ServerId, connectionInfo.Address);
            throw;
        }
    }

    /// <summary>
    /// Terminate a session
    /// </summary>
    /// <param name="sessionId">Session ID to terminate</param>
    /// <returns>True if terminated successfully</returns>
    public async Task<bool> TerminateSessionAsync(string sessionId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to ServiceDiscovery server. Call InitializeAsync first.");
        }

        try
        {
            logger.LogInformation("Terminating session {SessionId}", sessionId);
            return await _hubConnection.InvokeAsync<bool>("TerminateSessionAsync", sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Remove player from session (for connection cleanup)
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>True if successfully removed</returns>
    public async Task<bool> RemovePlayerFromSessionAsync(string sessionId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to ServiceDiscovery server. Call InitializeAsync first.");
        }

        try
        {
            logger.LogDebug("Removing player from session {SessionId}", sessionId);
            return await _hubConnection.InvokeAsync<bool>("RemovePlayerFromSessionAsync", sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing player from session {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Full session creation and connection flow
    /// </summary>
    /// <param name="groupName">Group name</param>
    /// <param name="connectionType">Connection type</param>
    /// <param name="maxPlayers">Maximum players per session</param>
    /// <param name="mode">Session mode</param>
    /// <returns>Connected battle client with session information</returns>
    public async Task<(IBattleClient Client, SessionInfo Session)> CreateSessionAndConnectAsync(
        string groupName,
        ConnectionType connectionType = ConnectionType.SignalR,
        int maxPlayers = 5,
        SessionMode mode = SessionMode.Auto)
    {
        // Step 1: Create or find session
        var sessionResponse = await CreateOrFindSessionAsync(groupName, maxPlayers, mode);

        if (!sessionResponse.IsSuccess || sessionResponse.Session is null || sessionResponse.ConnectionInfo is null)
        {
            throw new InvalidOperationException($"Failed to create session: {sessionResponse.ErrorMessage}");
        }

        var session = sessionResponse.Session.Value;

        try
        {
            // Step 2: Connect to the assigned battle server
            var battleClient = await ConnectToBattleServerAsync(sessionResponse.ConnectionInfo.Value, groupName, connectionType);

            logger.LogInformation("Successfully completed session creation and connection flow for group {GroupName}", groupName);

            return (battleClient, session);
        }
        catch (Exception)
        {
            // Connection failed, remove player from session to free up the slot
            try
            {
                await RemovePlayerFromSessionAsync(session.SessionId);
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(cleanupEx, "Failed to remove player from session {SessionId} during error cleanup", session.SessionId);
            }
            throw;
        }
    }

    /// <summary>
    /// Create multiple session connections for batch battle testing
    /// </summary>
    /// <param name="groupName">Group name</param>
    /// <param name="count">Number of connections to create</param>
    /// <param name="connectionType">Connection type</param>
    /// <param name="maxPlayers">Maximum players per session</param>
    /// <param name="mode">Session mode</param>
    /// <returns>List of connected battle clients with session information</returns>
    public async Task<IReadOnlyList<(IBattleClient Client, SessionInfo Session)>> CreateMultipleSessionsAndConnectAsync(
        string groupName,
        int count,
        ConnectionType connectionType = ConnectionType.SignalR,
        int maxPlayers = 5,
        SessionMode mode = SessionMode.Auto)
    {
        logger.LogInformation("Creating {Count} session connections for group {GroupName}", count, groupName);

        var results = new List<(IBattleClient Client, SessionInfo Session)>();
        var exceptions = new List<Exception>();

        try
        {
            // Create connections concurrently
            var tasks = Enumerable.Range(0, count)
                .Select(async i =>
                {
                    try
                    {
                        return await CreateSessionAndConnectAsync(groupName, connectionType, maxPlayers, mode);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create session connection {Index} for group {GroupName}", i, groupName);
                        exceptions.Add(ex);
                        return (default(IBattleClient?), default(SessionInfo?));
                    }
                })
                .ToArray();

            var taskResults = await Task.WhenAll(tasks);

            // Filter successful connections
            foreach (var result in taskResults.Where(r => r.Item1 is not null && r.Item2.HasValue))
            {
                results.Add((result.Item1!, result.Item2!.Value));
            }

            if (results.Count == 0 && exceptions.Count > 0)
            {
                throw new AggregateException("Failed to create any session connections", exceptions);
            }

            logger.LogInformation("Successfully created {Successful}/{Total} session connections for group {GroupName}",
                results.Count, count, groupName);

            return results;
        }
        catch
        {
            // Clean up any successful connections on failure
            foreach (var (client, _) in results)
            {
                try
                {
                    await client.DisconnectAsync();
                }
                catch (Exception cleanupEx)
                {
                    logger.LogWarning(cleanupEx, "Error cleaning up battle client during exception handling");
                }
            }
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _hubConnection?.DisposeAsync().AsTask().Wait();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error disposing ServiceDiscoveryClientProvider");
        }
    }
}
