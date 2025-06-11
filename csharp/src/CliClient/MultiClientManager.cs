using Microsoft.Extensions.Logging;

namespace CliClient;

/// <summary>
/// Service to manage multiple independent client instances
/// </summary>
public class MultiClientManager
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MultiClientManager> _logger;
    private readonly List<InMemoryClient> _clients = new();
    private readonly List<TaskCompletionSource<bool>> _battleCompletionSources = new();

    public MultiClientManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MultiClientManager>();
    }

    /// <summary>
    /// Connect multiple independent client instances to the same group
    /// </summary>
    public async Task<bool> ConnectMultipleClientsAsync(string serverUrl, string groupName, int count)
    {
        _logger.LogInformation($"Creating {count} independent client instances for group '{groupName}'");

        if (count <= 0)
        {
            _logger.LogWarning($"Invalid session count: {count}, must be greater than 0");
            return false;
        }

        // クリーンアップを行う
        await CleanupClientsAsync();

        // 各クライアントを作成して接続
        for (int i = 0; i < count; i++)
        {
            try
            {
                var clientLogger = _loggerFactory.CreateLogger<InMemoryClient>();
                var client = new InMemoryClient(clientLogger);
                var completionSource = new TaskCompletionSource<bool>();

                // バトル完了のモニタリングを設定
                SetupBattleCompletionMonitoring(client, completionSource, i);

                // クライアントをリストに追加
                _clients.Add(client);
                _battleCompletionSources.Add(completionSource);

                // 接続
                var success = await client.ConnectAsync(serverUrl, groupName);
                if (!success)
                {
                    _logger.LogError($"Client {i}: Failed to connect");
                    await CleanupClientsAsync();
                    return false;
                }

                _logger.LogInformation($"Client {i}: Connected successfully to group '{groupName}'");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create client {i}: {ex.Message}");
                await CleanupClientsAsync();
                return false;
            }
        }

        _logger.LogInformation($"Successfully connected {count} clients to group '{groupName}'");
        return true;
    }

    /// <summary>
    /// Wait for all battles to complete
    /// </summary>
    public async Task<bool> WaitForBattleCompletionAsync(TimeSpan timeout)
    {
        if (_battleCompletionSources.Count == 0)
        {
            _logger.LogWarning("No clients to wait for");
            return false;
        }

        _logger.LogInformation($"Waiting for battle to complete on {_battleCompletionSources.Count} clients (timeout: {timeout})");

        try
        {
            // すべてのクライアントの完了を待機
            var allTasks = _battleCompletionSources.Select(source => source.Task).ToArray();
            var timeoutTask = Task.Delay(timeout);

            var completedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask);

            if (completedTask != timeoutTask)
            {
                _logger.LogInformation("Battle completed successfully on all clients");
                return true;
            }
            else
            {
                _logger.LogWarning("Timed out waiting for battle completion");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while waiting for battle completion: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Cleanup all clients
    /// </summary>
    public async Task CleanupClientsAsync()
    {
        foreach (var client in _clients)
        {
            try
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disconnecting client: {ex.Message}");
            }
        }

        _clients.Clear();
        _battleCompletionSources.Clear();
    }

    /// <summary>
    /// Setup monitoring for battle completion
    /// </summary>
    private void SetupBattleCompletionMonitoring(InMemoryClient client, TaskCompletionSource<bool> completionSource, int clientIndex)
    {
        // BattleCompleted イベントをモニタリングする
        client.SetBattleCompletedCallback(() =>
        {
            _logger.LogInformation($"Client {clientIndex}: Battle completed callback triggered");
            completionSource.TrySetResult(true);
        });
    }
}
