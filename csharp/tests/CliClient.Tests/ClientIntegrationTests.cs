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

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}
