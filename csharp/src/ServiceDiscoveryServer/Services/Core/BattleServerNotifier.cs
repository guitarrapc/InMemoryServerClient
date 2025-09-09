using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace ServiceDiscoveryServer.Services.Core;

/// <summary>
/// Service for notifying BattleServers about session events
/// </summary>
public sealed class BattleServerNotifier(
    ILogger<BattleServerNotifier> logger,
    IBattleServerRegistry battleServerRegistry) : IBattleServerNotifier, IDisposable
{
    private readonly ConcurrentDictionary<string, HubConnection> _connections = new();

    public async Task<bool> NotifyBattleReadyAsync(string serverId, SessionInfo sessionInfo)
    {
        try
        {
            var connection = await GetOrCreateConnectionAsync(serverId);
            if (connection?.State == HubConnectionState.Connected)
            {
                await connection.InvokeAsync("NotifyBattleReadyAsync", sessionInfo.SessionId, sessionInfo);
                logger.LogInformation("Notified BattleServer {ServerId} that session {SessionId} is ready for battle",
                    serverId, sessionInfo.SessionId);
                return true;
            }

            logger.LogWarning("Failed to notify BattleServer {ServerId} - connection not available", serverId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error notifying BattleServer {ServerId} about session {SessionId}",
                serverId, sessionInfo.SessionId);
            return false;
        }
    }

    private async Task<HubConnection?> GetOrCreateConnectionAsync(string serverId)
    {
        if (_connections.TryGetValue(serverId, out var existingConnection) &&
            existingConnection.State == HubConnectionState.Connected)
        {
            return existingConnection;
        }

        var serverInfo = await battleServerRegistry.GetServerInfoAsync(serverId);
        if (serverInfo is null)
        {
            logger.LogWarning("BattleServer {ServerId} not found in registry", serverId);
            return null;
        }

        try
        {
            var connection = new HubConnectionBuilder()
                .WithUrl($"http://{serverInfo.Value.Address}:{serverInfo.Value.SignalRPort}/battlehub")
                .WithAutomaticReconnect()
                .Build();

            connection.Closed += async (error) =>
            {
                if (error != null)
                {
                    logger.LogWarning("Connection to BattleServer {ServerId} closed with error: {Error}", serverId, error.Message);
                }
                _connections.TryRemove(serverId, out _);
            };

            await connection.StartAsync();
            _connections[serverId] = connection;

            logger.LogDebug("Established connection to BattleServer {ServerId}", serverId);
            return connection;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create connection to BattleServer {ServerId}", serverId);
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            try
            {
                connection.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error disposing BattleServer connection");
            }
        }
        _connections.Clear();
    }
}
