using InMemoryServer.Models;
using InMemoryServer.Http1Server;
using InMemoryServer.Services;
using Microsoft.AspNetCore.SignalR;
using Shared.Models;
using Shared.Battle;

namespace InMemoryServer.Services;

/// <summary>
/// Service for sending notifications across different protocols (SignalR and MagicOnion)
/// </summary>
public class CrossProtocolNotificationService
{
    private readonly ILogger<CrossProtocolNotificationService> _logger;
    private readonly ConnectionManager _connectionManager;
    private readonly IHubContext<InMemoryHub> _signalRHubContext;
    private readonly MagicOnionGroupService _magicOnionGroupService;

    public CrossProtocolNotificationService(
        ILogger<CrossProtocolNotificationService> logger,
        ConnectionManager connectionManager,
        IHubContext<InMemoryHub> signalRHubContext,
        MagicOnionGroupService magicOnionGroupService)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _signalRHubContext = signalRHubContext;
        _magicOnionGroupService = magicOnionGroupService;
    }

    /// <summary>
    /// Send a notification to all clients in a group across all protocols
    /// </summary>
    /// <param name="groupId">Group ID</param>
    /// <param name="connectionIds">List of normalized connection IDs in the group</param>
    /// <param name="notificationAction">Action to send the notification</param>
    public async Task NotifyGroupAsync<T>(string groupId, IEnumerable<string> connectionIds,
        string methodName, T data)
    {
        var signalRConnections = new List<string>();
        var magicOnionConnections = new List<Models.ConnectionInfo>();

        // Categorize connections by protocol
        foreach (var normalizedConnectionId in connectionIds)
        {
            var connectionInfo = _connectionManager.GetConnectionInfo(normalizedConnectionId);
            if (connectionInfo == null) continue;

            switch (connectionInfo.Protocol)
            {
                case ConnectionProtocol.SignalR:
                    signalRConnections.Add(connectionInfo.OriginalConnectionId);
                    break;
                case ConnectionProtocol.MagicOnion:
                    magicOnionConnections.Add(connectionInfo);
                    break;
            }
        }

        // Send to SignalR clients
        if (signalRConnections.Count > 0)
        {
            try
            {
                await _signalRHubContext.Clients.Clients(signalRConnections)
                    .SendAsync(methodName, data);
                _logger.LogDebug("Sent {MethodName} to {Count} SignalR clients in group {GroupId}",
                    methodName, signalRConnections.Count, groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending {MethodName} to SignalR clients in group {GroupId}",
                    methodName, groupId);
            }
        }

        // Send to MagicOnion clients
        if (magicOnionConnections.Count > 0)
        {
            try
            {
                // Use MagicOnion group service to send to all clients in the group
                _magicOnionGroupService.SendToAll(groupId, receiver =>
                {
                    switch (methodName)
                    {
                        case "MemberJoined":
                            if (data is MemberJoinedData memberJoinedData)
                                receiver.OnMemberJoined(memberJoinedData);
                            break;
                        case "MemberLeft":
                            if (data is MemberLeftData memberLeftData)
                                receiver.OnMemberLeft(memberLeftData);
                            break;
                        case "ConnectionsReady":
                            if (data is ConnectionsReadyData connectionsReadyData)
                                receiver.OnConnectionsReady(connectionsReadyData);
                            break;
                        case "BattleStarted":
                            if (data is BattleStartedData battleStartedData)
                                receiver.OnBattleStarted(battleStartedData);
                            break;
                        case "BattleReplayData":
                            if (data is BattleReplayData battleReplayData)
                                receiver.OnBattleReplayData(battleReplayData);
                            break;
                        case "GroupDissolved":
                            if (data is GroupDissolvedData groupDissolvedData)
                                receiver.OnGroupDissolved(groupDissolvedData);
                            break;
                        case "GroupExtended":
                            if (data is GroupExtendedData groupExtendedData)
                                receiver.OnGroupExtended(groupExtendedData);
                            break;
                        case "GroupMessage":
                            if (data is GroupMessageData groupMessageData)
                                receiver.OnGroupMessage(groupMessageData.SenderId, groupMessageData.Message);
                            break;
                    }
                });
                _logger.LogDebug("Sent {MethodName} to {Count} MagicOnion clients in group {GroupId}",
                    methodName, magicOnionConnections.Count, groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending {MethodName} to MagicOnion clients in group {GroupId}",
                    methodName, groupId);
            }
        }
    }

    /// <summary>
    /// Send a notification to a specific client
    /// </summary>
    /// <param name="normalizedConnectionId">Normalized connection ID</param>
    /// <param name="methodName">Method name to call</param>
    /// <param name="data">Data to send</param>
    public async Task NotifyClientAsync<T>(string normalizedConnectionId, string methodName, T data)
    {
        var connectionInfo = _connectionManager.GetConnectionInfo(normalizedConnectionId);
        if (connectionInfo == null)
        {
            _logger.LogWarning("Connection not found for normalized ID: {ConnectionId}", normalizedConnectionId);
            return;
        }

        try
        {
            switch (connectionInfo.Protocol)
            {
                case ConnectionProtocol.SignalR:
                    await _signalRHubContext.Clients.Client(connectionInfo.OriginalConnectionId)
                        .SendAsync(methodName, data);
                    break;
                case ConnectionProtocol.MagicOnion:
                    // For MagicOnion, we would need to find the specific client in the group
                    // This is more complex and might require additional tracking
                    _logger.LogWarning("Individual MagicOnion client notification not yet implemented for method {MethodName}", methodName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending {MethodName} to client {ConnectionId} ({Protocol})",
                methodName, normalizedConnectionId, connectionInfo.Protocol);
        }
    }
}

/// <summary>
/// Data model for group message notifications
/// </summary>
public class GroupMessageData
{
    public required string SenderId { get; init; }
    public required string Message { get; init; }
}
