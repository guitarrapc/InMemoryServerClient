using Microsoft.Extensions.Logging;
using Shared.Constants;
using Shared.Contracts;
using Shared.Models;

namespace CliClient.Clients;

/// <summary>
/// Service to manage multiple independent client instances
/// Protocol-independent implementation
/// </summary>
public class MultiBattleClientManager(ILoggerFactory loggerFactory)
{
    private readonly ILogger<MultiBattleClientManager> _logger = loggerFactory.CreateLogger<MultiBattleClientManager>();
    private readonly List<IBattleClient> _clients = [];

    /// <summary>
    /// Connect multiple independent client instances to the same group
    /// </summary>
    public async Task<bool> ConnectMultipleAsync(
        int clientCount,
        string serverUrl,
        CancellationToken cancellationToken,
        string? groupName = null,
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        _logger.LogInformation("Creating {ClientCount} independent client instances for group '{GroupName}'", clientCount, groupName);

        if (clientCount <= 0)
        {
            _logger.LogWarning("Invalid session count: {ClientCount}, must be greater than 0", clientCount);
            return false;
        }

        // Clear existing clients
        await CleanupClientsAsync();

        // Create clients and connect them to the specified group
        for (int i = 0; i < clientCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Ctrl+C requested. Stopping connect client to server, and cleaning up existing connections.");
                await CleanupClientsAsync();
                return false;
            }

            try
            {
                var client = BattleClientFactory.Create(connectionType, loggerFactory, cancellationToken);

                _clients.Add(client);

                // Connect to the server
                var success = await client.ConnectAsync(serverUrl, groupName);
                if (!success)
                {
                    _logger.LogError("Client {ClientIndex}: Failed to connect", i);
                    await CleanupClientsAsync();
                    return false;
                }

                _logger.LogInformation("Client {ClientIndex}: Connected successfully to group '{GroupName}'", i, groupName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client {ClientIndex}: Exception during connection", i);
                await CleanupClientsAsync();
                return false;
            }

            // Wait a moment to spread the load of simultaneous connections
            if (i < clientCount - 1)
            {
                await Task.Delay(100);
            }
        }

        _logger.LogInformation("All {ClientCount} clients connected successfully to group '{GroupName}'", clientCount, groupName);
        return true;
    }

    /// <summary>
    /// Battle reproduction with specific seed
    /// </summary>
    public async Task<bool> ReproduceBattleAsync(
        string serverUrl,
        string seed,
        ConnectionType connectionType = ConnectionType.SignalR,
        CancellationToken cancellationToken = default)
    {
        // Fullfill group to start battle reproduction
        var groupName = $"battle-reproduce-{seed}";
        return await ConnectMultipleAsync(SystemDefines.MaxConnectionsPerGroup, serverUrl, cancellationToken, groupName, connectionType);
    }

    /// <summary>
    /// Wait for all battles to complete
    /// </summary>
    public async Task WaitForBattleCompletionAsync()
    {
        if (_clients.Count == 0)
        {
            _logger.LogWarning("No clients available for battle completion waiting");
            return;
        }

        _logger.LogInformation("Waiting for battle completion from {ClientCount} clients...", _clients.Count);

        var completionTasks = new List<Task>();

        foreach (var client in _clients)
        {
            // Use the BattleCompletionSource from the interface for protocol-independent implementation
            completionTasks.Add(client.BattleCompletionSource.Task);
        }

        if (completionTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(completionTasks);
                _logger.LogInformation("All battles completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for battle completion");
            }
        }

        await CleanupClientsAsync();
    }

    /// <summary>
    /// Get connected client count
    /// </summary>
    public int ConnectedClientCount => _clients.Count(c => c.IsConnected);

    /// <summary>
    /// Clean up all clients
    /// </summary>
    public async Task CleanupClientsAsync()
    {
        if (_clients.Count == 0) return;

        _logger.LogInformation("Cleaning up {ClientCount} clients...", _clients.Count);

        // Create a copy to avoid collection modification issues
        var clientsToDispose = _clients.ToList();

        foreach (var client in clientsToDispose)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing client");
            }
        }

        _clients.Clear();
        _logger.LogInformation("Client cleanup completed");
    }

    /// <summary>
    /// Dispose all resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await CleanupClientsAsync();
    }
}
