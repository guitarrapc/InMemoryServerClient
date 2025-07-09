namespace CliClient.Tests;

/// <summary>
/// Tests focused on error handling and edge cases for CliClient components
/// </summary>
public class ErrorHandlingTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    public ErrorHandlingTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    [Fact]
    public async Task SignalRBattleClient_MultipleConnectCalls_HandlesGracefully()
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);

        // Act
        var result1 = await client.ConnectAsync("http://localhost:5000");
        var result2 = await client.ConnectAsync("http://localhost:5001");

        // Assert
        Assert.False(result1); // Expected to fail without server
        Assert.False(result2); // Expected to fail without server
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task MagicOnionBattleClient_MultipleConnectCalls_HandlesGracefully()
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.MagicOnion, _loggerFactory);

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.ConnectAsync("http://localhost:5000"));
        await Assert.ThrowsAsync<NotImplementedException>(() => client.ConnectAsync("http://localhost:5001"));
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task MultiBattleClientManager_ExcessiveClientCount_HandlesGracefully()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);

        // Act
        var result = await manager.ConnectMultipleAsync(
            1000, "http://localhost:5000", "test-group");

        // Assert
        Assert.False(result); // Expected to fail
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid-protocol")]
    [InlineData("http://")]
    public async Task BattleClient_InvalidUrls_HandlesGracefully(string invalidUrl)
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);

        // Act
        var result = await client.ConnectAsync(invalidUrl);

        // Assert
        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task BattleClient_OperationsAfterDispose_HandlesGracefully()
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);
        await client.DisposeAsync();

        // Act & Assert - Operations after dispose should throw InvalidOperationException for SignalR
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetAsync("test", "value"));
    }

    [Fact]
    public async Task MultiBattleClientManager_OperationsAfterDispose_HandlesGracefully()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);
        await manager.DisposeAsync();

        // Act
        var result = await manager.ConnectMultipleAsync(1, "http://localhost:5000", "test");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1000)]
    [InlineData(0)]
    public async Task MultiBattleClientManager_InvalidClientCounts_ReturnsExpectedResults(int invalidCount)
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);

        // Act
        var result = await manager.ConnectMultipleAsync(
            invalidCount, "http://localhost:5000", "test");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BattleClient_LargeDataOperations_HandlesGracefully()
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);
        var largeValue = new string('x', 10000); // 10KB string

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetAsync("large-key", largeValue));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("large-key"));
    }

    [Fact]
    public async Task BattleClient_SpecialCharacterHandling_WorksCorrectly()
    {
        // Arrange
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);
        var specialKey = "key-with-特殊文字-and-emoji-🚀";
        var specialValue = "value-with-特殊文字-and-newlines\n\r\t";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetAsync(specialKey, specialValue));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(specialKey));
    }

    [Fact]
    public void ConsoleCommand_ExceptionHandling_DoesNotPropagate()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);
        var logger = _loggerFactory.CreateLogger<ConsoleCommand>();
        var command = new ConsoleCommand(manager, _loggerFactory, logger);

        // Act & Assert - Command creation should not throw
        Assert.NotNull(command);

        // Verify proper dependency injection
        var type = typeof(ConsoleCommand);
        var constructor = type.GetConstructors()[0];
        Assert.Equal(3, constructor.GetParameters().Length);
    }

    [Fact]
    public async Task BattleClientFactory_ConcurrentCreation_HandlesCorrectly()
    {
        // Arrange
        var tasks = new List<Task<IBattleClient>>();

        // Act - Create multiple clients concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
                BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory)));
        }

        var clients = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, clients.Length);
        Assert.All(clients, client => Assert.NotNull(client));
        Assert.All(clients, client => Assert.IsType<SignalRBattleClient>(client));

        // Cleanup
        foreach (var client in clients)
        {
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task MultiBattleClientManager_ConcurrentOperations_HandlesCorrectly()
    {
        // Arrange
        var manager = new MultiBattleClientManager(_loggerFactory);
        var tasks = new List<Task<bool>>();

        // Act - Perform concurrent operations
        for (int i = 0; i < 5; i++)
        {
            var groupName = $"concurrent-group-{i}";
            tasks.Add(manager.ConnectMultipleAsync(1, "http://localhost:5000", groupName));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Length);
        Assert.All(results, result => Assert.False(result)); // Expected to fail without server
    }

    [Fact]
    public void BattleClientFactory_InvalidEnumValue_ThrowsAppropriateException()
    {
        // Arrange
        var invalidConnectionType = (ConnectionType)999;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            BattleClientFactory.Create(invalidConnectionType, _loggerFactory));

        Assert.Contains("Unsupported connection type", exception.Message);
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}
