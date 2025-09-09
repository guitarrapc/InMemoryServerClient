using CliClient.Clients;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Models;

namespace CliClient;

/// <summary>
/// Connection configuration for battle reproduction
/// </summary>
internal readonly record struct ConnectionOptions
{
    public string ServerUrl { get; init; }
    public string? GroupName { get; init; }
}

/// <summary>
/// CLI commands for InMemory server
/// Public Method will be automatically registered as commands.
/// ListFooAsync will be registered as list-foo command.
/// </summary>
public class ConsoleCommand(MultiBattleClientManager multiClientManager, ILoggerFactory loggerFactory, ILogger<ConsoleCommand> logger)
{
    /// <summary>Connect multiple sessions to the server with the same group</summary>
    /// <param name="url">-u, Server URL</param>
    /// <param name="group">-g, Group name</param>
    /// <param name="count">-c, Number of sessions to connect (default: 5)</param>
    /// <param name="connectionType">-t, Connection type (default: SignalR)</param>
    [Command("connect-battle")]
    public async Task ConnectMultipleAsync(string url = "http://localhost:5000", string group = "battle-group", int count = 5, ConnectionType connectionType = ConnectionType.SignalR)
    {
        if (count <= 0 || count > 10)
        {
            logger.LogError("接続数は1から10の間で指定してください");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrEmpty(group))
        {
            logger.LogInformation("Error: Group name is required for multiple connections");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            logger.LogInformation($"Connecting {count} sessions to server: {url}");
            logger.LogInformation($"Group name: {group}");

            // 新しいMultiClientManagerを使用
            if (await multiClientManager.ConnectMultipleAsync(count, url, group, connectionType))
            {
                logger.LogInformation($"Successfully connected {count} clients to group: {group}");
                logger.LogInformation($"If this completes the group (5 sessions), a battle should start automatically!");

                // バトルの完了を待機
                logger.LogInformation("Waiting for battle complete...");
                await multiClientManager.WaitForBattleCompletionAsync();

                logger.LogInformation("Battle completed successfully!");
            }
            else
            {
                logger.LogInformation($"Failed to connect {count} clients to server");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation($"Error connecting multiple clients: {ex.Message}");
            Environment.ExitCode = 1;
        }
        finally
        {
            // 最後に必ずクリーンアップを実行
            try
            {
                await multiClientManager.CleanupClientsAsync();
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Error during cleanup: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// <summary>Reproduce a battle with specific battle ID and seed value</summary>
    /// </summary>
    /// <param name="battleId">-b, BattleId to get reproduce result</param>
    /// <param name="seed">-s, Battle Seed to specify reproduce result</param>
    /// <param name="count">-c, Number of sessions to connect (default: 5)</param>
    /// <param name="groupName">-g, Group name (optional)</param>
    /// <param name="serverUrl">-u, Server URL</param>
    /// <param name="connectionType">-t, Connection type (default: SignalR)</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [Command("battle-reproduce")]
    public async Task ReproduceBattleAsync(
        string battleId,
        string seed,
        int count = 5,
        string groupName = "test-group",
        string serverUrl = "http://localhost:5000",
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        // Validate parameters
        if (!Guid.TryParse(battleId, out var parsedBattleId))
        {
            logger.LogError("無効なバトルIDです: {BattleId}", battleId);
            Environment.ExitCode = 1;
            return;
        }

        if (!int.TryParse(seed, out var parsedSeed))
        {
            logger.LogError("無効なシードです: {Seed}", seed);
            Environment.ExitCode = 1;
            return;
        }

        if (count <= 0 || count > 10)
        {
            logger.LogError("接続数は1から10の間で指定してください");
            Environment.ExitCode = 1;
            return;
        }

        logger.LogInformation("指定されたバトルID '{BattleId}' とシード値 '{SeedValue}' でバトルを再現します...", battleId, seed);

        logger.LogInformation("{Count}つの接続を作成中...", count);

        var connections = new List<IBattleClient>();
        var connectionFailures = 0;

        try
        {
            // Generate unique group name for this reproduction
            logger.LogInformation("グループ名: {GroupName}", groupName);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var connection = BattleClientFactory.Create(connectionType, loggerFactory);

                    var success = await connection.ConnectAsync(serverUrl, groupName);
                    if (!success)
                    {
                        throw new InvalidOperationException("Failed to connect to server");
                    }

                    // Call the server's ReproduceBattleAsync method to start reproduction
                    var reproduced = await connection.ReproduceBattleAsync(parsedBattleId, parsedSeed, groupName);
                    if (!reproduced)
                    {
                        logger.LogWarning("サーバーでのバトル再現リクエストが失敗しました");
                    }

                    connections.Add(connection);

                    logger.LogInformation("接続 {Current}/{Total} 完了", i + 1, count);
                    await Task.Delay(100); // Avoid overwhelming the server
                }
                catch (Exception ex)
                {
                    connectionFailures++;
                    logger.LogWarning("接続 {Current} が失敗: {Message}", i + 1, ex.Message);

                    if (connectionFailures > count / 2)
                    {
                        logger.LogError("接続失敗が多すぎるため、処理を中断します");
                        Environment.ExitCode = 1;
                        return;
                    }
                }
            }

            if (connections.Count == 0)
            {
                logger.LogError("接続できたクライアントがありません");
                Environment.ExitCode = 1;
                return;
            }

            logger.LogInformation("有効な接続数: {ConnectionCount}/{RequestedCount}", connections.Count, count);
            logger.LogInformation("バトルID '{BattleId}' とシード値 '{SeedValue}' でバトルが再現されます。", battleId, seed);
            logger.LogInformation("バトル完了まで待機中...");

            // Wait for battle completion with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(10));
            var completedTask = await Task.WhenAny(
                Task.Delay(TimeSpan.FromMinutes(5)), // バトル待機
                timeoutTask
            );

            if (completedTask == timeoutTask)
            {
                logger.LogWarning("バトル完了のタイムアウトが発生しました");
            }
            else
            {
                logger.LogInformation("バトルID '{BattleId}' とシード値 '{SeedValue}' のバトル再現が正常に完了しました", battleId, seed);
            }
        }
        finally
        {
            logger.LogInformation("接続をクリーンアップ中...");
            foreach (var connection in connections)
            {
                try
                {
                    await connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning("接続のクリーンアップ中にエラーが発生: {Message}", ex.Message);
                }
            }
        }
    }
}
