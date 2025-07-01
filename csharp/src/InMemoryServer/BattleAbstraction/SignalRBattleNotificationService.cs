using BattleLogic.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Shared;

namespace InMemoryServer.BattleAbstraction;

/// <summary>
/// SignalR-based implementation of IBattleNotificationService
/// </summary>
public class SignalRBattleNotificationService : IBattleNotificationService
{
    private readonly IHubContext<InMemoryHub> _hubContext;
    private readonly ILogger<SignalRBattleNotificationService> _logger;

    public SignalRBattleNotificationService(IHubContext<InMemoryHub> hubContext, ILogger<SignalRBattleNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Notify all clients in the group that all connections are ready
    /// </summary>
    public async Task NotifyAllConnectionsReadyAsync(IReadOnlyList<string> clientIds)
    {
        try
        {
            var tasks = clientIds.Select(clientId =>
                _hubContext.Clients.Client(clientId).SendAsync("AllConnectionsReady")
            );

            await Task.WhenAll(tasks);
            _logger.LogInformation("Notified {Count} clients that all connections are ready", clientIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify clients that all connections are ready");
            throw;
        }
    }

    /// <summary>
    /// Notify all clients in the group that battle is starting
    /// </summary>
    public async Task NotifyBattleStartingAsync(IReadOnlyList<string> clientIds, string battleId)
    {
        try
        {
            var tasks = clientIds.Select(clientId =>
                _hubContext.Clients.Client(clientId).SendAsync("BattleStarting", new { BattleId = battleId })
            );

            await Task.WhenAll(tasks);
            _logger.LogInformation("Notified {Count} clients that battle {BattleId} is starting", clientIds.Count, battleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify clients that battle {BattleId} is starting", battleId);
            throw;
        }
    }

    /// <summary>
    /// Send battle replay data to all clients in the group
    /// </summary>
    public async Task SendBattleReplayAsync(IReadOnlyList<string> clientIds, BattleReplayData replayData)
    {
        try
        {
            var tasks = clientIds.Select(clientId =>
                _hubContext.Clients.Client(clientId).SendAsync("BattleReplayData", replayData)
            );

            await Task.WhenAll(tasks);
            _logger.LogInformation("Sent battle replay chunk {ChunkIndex}/{TotalChunks} to {Count} clients",
                replayData.ChunkIndex, replayData.TotalChunks, clientIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send battle replay data to clients");
            throw;
        }
    }

    /// <summary>
    /// Notify battle status update
    /// </summary>
    public async Task NotifyBattleStatusAsync(string groupId, object status)
    {
        try
        {
            await _hubContext.Clients.Group(groupId).SendAsync("BattleStatus", status);
            _logger.LogInformation("Sent battle status to group {GroupId}", groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send battle status to group {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Notify battle progress update
    /// </summary>
    public async Task NotifyBattleProgressAsync(string groupId, object progress)
    {
        try
        {
            await _hubContext.Clients.Group(groupId).SendAsync("BattleProgress", progress);
            _logger.LogInformation("Sent battle progress to group {GroupId}", groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send battle progress to group {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Send replay data to clients
    /// </summary>
    public async Task SendReplayDataAsync(string groupId, object replayData)
    {
        try
        {
            await _hubContext.Clients.Group(groupId).SendAsync("BattleReplayData", replayData);
            _logger.LogInformation("Sent replay data to group {GroupId}", groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send replay data to group {GroupId}", groupId);
            throw;
        }
    }
}
