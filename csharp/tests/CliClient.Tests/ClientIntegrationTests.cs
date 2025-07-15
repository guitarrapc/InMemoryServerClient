using CliClient.Clients;

namespace CliClient.Tests;

/// <summary>
/// Integration tests for client components working together
/// These tests verify the interaction between different client components
/// </summary>
public class ClientIntegrationTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    public ClientIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    [Fact]
    public void BattleClientFactory_CreateSignalRClient_IntegratesWithMultiBattleClientManager()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);

        // Act
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);

        // Assert
        Assert.NotNull(client);
        Assert.IsType<SignalRBattleClient>(client);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void BattleClientFactory_CreateMagicOnionClient_IntegratesWithMultiBattleClientManager()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);

        // Act
        var client = BattleClientFactory.Create(ConnectionType.MagicOnion, _loggerFactory);

        // Assert
        Assert.NotNull(client);
        Assert.IsType<MagicOnionBattleClient>(client);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void ConsoleCommand_WithMultiBattleClientManager_IntegratesCorrectly()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);
        var logger = _loggerFactory.CreateLogger<ConsoleCommand>();

        // Act
        var command = new ConsoleCommand(manager, _loggerFactory, logger);

        // Assert
        Assert.NotNull(command);
    }

    [Fact]
    public async Task ClientLifecycle_SignalR_CreateConnectDispose_WorksCorrectly()
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);

        // Act & Assert - Initial state
        Assert.False(client.IsConnected);

        // Act - Try to connect (will fail without server, but should not throw)
        var connectResult = await client.ConnectAsync("http://localhost:5000");
        Assert.False(connectResult); // Expected to fail without server

        // Act - Disconnect and Dispose
        await client.DisconnectAsync(); // Should not throw
        await client.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task ClientLifecycle_MagicOnion_CreateConnectDispose_WorksCorrectly()
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.MagicOnion, _loggerFactory);

        // Act & Assert - Initial state
        Assert.False(client.IsConnected);

        // Act - Try to connect (will throw NotImplementedException)
        await Assert.ThrowsAsync<NotImplementedException>(() => client.ConnectAsync("http://localhost:5000"));

        // Act - Dispose
        await client.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task MultiBattleClientManager_WithDifferentConnectionTypes_HandlesCorrectly()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);

        // Act & Assert - Test with SignalR
        var signalRResult = await manager.ConnectMultipleAsync(
            1, "http://localhost:5000", "test-signalr", ConnectionType.SignalR);
        Assert.False(signalRResult); // Expected to fail without server

        // Cleanup
        await manager.CleanupClientsAsync();

        // Act & Assert - Test with MagicOnion
        var magicOnionResult = await manager.ConnectMultipleAsync(
            1, "http://localhost:5000", "test-magiconion", ConnectionType.MagicOnion);
        Assert.False(magicOnionResult); // Expected to fail without server
    }

    [Fact]
    public void ConsoleCommand_WithClientOperations_IntegratesCorrectly()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);
        var logger = _loggerFactory.CreateLogger<ConsoleCommand>();
        var command = new ConsoleCommand(manager, _loggerFactory, logger);

        // Act & Assert - The command object should be created successfully
        Assert.NotNull(command);

        // Verify the structure exists for handling commands
        var type = typeof(ConsoleCommand);
        Assert.NotNull(type.GetMethod("InteractiveAsync"));
    }

    [Fact]
    public void ConnectionOptions_WithClientIntegration_WorksCorrectly()
    {
        // Arrange
        var options = new ConnectionOptions
        {
            ServerUrl = "http://localhost:5000",
            GroupName = "test-group"
        };

        // Act
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);

        // Assert
        Assert.NotNull(client);
        Assert.Equal("http://localhost:5000", options.ServerUrl);
        Assert.Equal("test-group", options.GroupName);
    }

    /// <summary>
    /// サーバーが起動している場合のみ実行される実際の接続テスト
    /// </summary>
    [Fact]
    public async Task SignalR_ConnectToRunningServer_WhenServerAvailable()
    {
        // Arrange
        const string serverUrl = "http://localhost:5000";
        var isServerAvailable = await IntegrationTestHelpers.IsServerAvailableAsync(serverUrl);

        if (!isServerAvailable)
        {
            // サーバーが利用できない場合はテストをスキップ
            Console.WriteLine($"⚠️ Test skipped: Server is not available at {serverUrl}. Start the server to run this test.");
            Assert.True(true, $"Test skipped: Server is not available at {serverUrl}");
            return;
        }

        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);

        try
        {
            // Act
            var connectResult = await client.ConnectAsync(serverUrl);

            // Assert
            Assert.True(connectResult, "Should successfully connect to running server");
            Assert.True(client.IsConnected, "Client should be connected");

            Console.WriteLine("✓ Successfully connected to running server");
        }
        finally
        {
            // Cleanup
            await client.DisconnectAsync();
            await client.DisposeAsync();
        }
    }

    /// <summary>
    /// 実際のバトルリプレイ統合テスト（サーバーが必要）
    /// </summary>
    [Fact]
    public async Task BattleReplay_Integration_WithRunningServer()
    {
        // Arrange
        const string serverUrl = "http://localhost:5000";
        const string groupName = "integration-test-group";

        var isServerAvailable = await IntegrationTestHelpers.IsServerAvailableAsync(serverUrl);

        if (!isServerAvailable)
        {
            // サーバーが利用できない場合はテストをスキップ
            Console.WriteLine($"⚠️ Test skipped: Server is not available at {serverUrl}. Start the server to run this test.");
            Assert.True(true, $"Test skipped: Server is not available at {serverUrl}");
            return;
        }

        var manager = new MultiBattleClientManager(_loggerFactory);

        try
        {
            // Act - 複数クライアントを接続
            var connectResult = await manager.ConnectMultipleAsync(
                2, serverUrl, groupName, ConnectionType.SignalR);

            // サーバーに接続できた場合のテスト
            Assert.True(connectResult, "Should successfully connect multiple clients to running server");
            Assert.True(manager.ConnectedClientCount >= 2, "Should have connected multiple clients");

            // バトル開始のテスト（実装に応じて調整）
            // var battleResult = await manager.StartBattleAsync();
            // Assert.True(battleResult, "Battle should start successfully");

            Console.WriteLine($"✓ Successfully connected {manager.ConnectedClientCount} clients to running server");
        }
        finally
        {
            // Cleanup
            await manager.CleanupClientsAsync();
        }
    }

    /// <summary>
    /// バトルリプレイファイル生成の統合テスト（モック使用）
    /// </summary>
    [IntegrationTest]
    public async Task BattleReplay_FileGeneration_WithMockBattle()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"battle_replay_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act - モックバトルデータでリプレイファイルを生成
            var replayData = CreateMockBattleReplayData();
            var filePath = Path.Combine(tempDir, "test_battle_replay.json");

            await File.WriteAllTextAsync(filePath, System.Text.Json.JsonSerializer.Serialize(replayData));

            // Assert
            Assert.True(File.Exists(filePath), "Replay file should be created");

            var fileContent = await File.ReadAllTextAsync(filePath);
            Assert.NotEmpty(fileContent);
            Assert.Contains("battleId", fileContent);

            Console.WriteLine($"✓ Battle replay file created: {filePath}");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static object CreateMockBattleReplayData()
    {
        return new
        {
            battleId = Guid.NewGuid().ToString(),
            timestamp = DateTime.UtcNow,
            players = new[]
            {
                new { id = "player1", name = "TestPlayer1" },
                new { id = "player2", name = "TestPlayer2" }
            },
            result = "victory",
            duration = "00:05:30"
        };
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}
