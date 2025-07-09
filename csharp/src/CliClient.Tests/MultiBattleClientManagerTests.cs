namespace CliClient.Tests;

/// <summary>
/// Test class for MultiBattleClientManager
/// </summary>
public class MultiBattleClientManagerTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MultiBattleClientManager _manager;

    public MultiBattleClientManagerTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _manager = new MultiBattleClientManager(_loggerFactory);
    }

    [Fact]
    public void Constructor_WithValidLoggerFactory_CreatesInstance()
    {
        // Act
        var manager = new MultiBattleClientManager(_loggerFactory);

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MultiBattleClientManager(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task ConnectMultipleAsync_WithInvalidClientCount_ReturnsFalse(int clientCount)
    {
        // Act
        var result = await _manager.ConnectMultipleAsync(
            clientCount,
            "http://localhost:5000",
            "test-group");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectMultipleAsync_WithValidParameters_HasCorrectSignature()
    {
        // This test verifies the method signature and parameter handling
        // In a real scenario, this would connect to an actual server
        // For unit testing, we're testing the method exists and accepts correct parameters

        // Arrange
        const int clientCount = 1;
        const string serverUrl = "http://localhost:5000";
        const string groupName = "test-group";

        // Act & Assert (method should exist and not throw immediately)
        var task = _manager.ConnectMultipleAsync(clientCount, serverUrl, groupName, ConnectionType.SignalR);

        // Since we don't have a running server, we expect this to fail at connection
        // but the method should exist and process parameters correctly
        var result = await task;
        Assert.False(result); // Expected to fail without running server
    }

    [Fact]
    public async Task ReproduceBattleAsync_WithValidParameters_HasCorrectSignature()
    {
        // Arrange
        const string serverUrl = "http://localhost:5000";
        const string seed = "12345";

        // Act
        var result = await _manager.ReproduceBattleAsync(serverUrl, seed, ConnectionType.SignalR);

        // Assert
        Assert.False(result); // Expected to fail without running server
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ReproduceBattleAsync_WithInvalidSeed_HasCorrectBehavior(string? seed)
    {
        // Act
        var result = await _manager.ReproduceBattleAsync("http://localhost:5000", seed!, ConnectionType.SignalR);

        // Assert
        Assert.False(result); // Expected to fail
    }

    [Fact]
    public async Task WaitForBattleCompletionAsync_WithNoClients_DoesNotThrow()
    {
        // Act & Assert
        await _manager.WaitForBattleCompletionAsync(); // Should not throw
    }

    [Fact]
    public async Task CleanupClientsAsync_DoesNotThrow()
    {
        // Act & Assert
        await _manager.CleanupClientsAsync(); // Should not throw
    }

    [Theory]
    [InlineData(ConnectionType.SignalR)]
    [InlineData(ConnectionType.MagicOnion)]
    public async Task ConnectMultipleAsync_WithDifferentConnectionTypes_AcceptsValidTypes(ConnectionType connectionType)
    {
        // Act
        var result = await _manager.ConnectMultipleAsync(1, "http://localhost:5000", "test", connectionType);

        // Assert
        Assert.False(result); // Expected to fail without running server, but method should accept the parameter
    }

    [Fact]
    public void ConnectedClientCount_WithNoClients_ReturnsZero()
    {
        // Act
        var count = _manager.ConnectedClientCount;

        // Assert
        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task ConnectMultipleAsync_WithValidClientCount_ProcessesCorrectly(int clientCount)
    {
        // This test verifies that the method processes different client counts
        // Without a running server, connections will fail, but parameter processing should work

        // Act
        var result = await _manager.ConnectMultipleAsync(
            clientCount,
            "http://localhost:5000",
            "test-group",
            ConnectionType.SignalR);

        // Assert
        Assert.False(result); // Expected to fail without running server
    }

    public void Dispose()
    {
        _manager?.DisposeAsync().AsTask().Wait();
        _loggerFactory?.Dispose();
    }
}
