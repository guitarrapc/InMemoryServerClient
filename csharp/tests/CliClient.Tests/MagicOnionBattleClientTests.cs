using CliClient.Clients;

namespace CliClient.Tests;

/// <summary>
/// Tests for MagicOnionBattleClient
/// These tests focus on client behavior without requiring an actual server connection
/// </summary>
public class MagicOnionBattleClientTests : IDisposable
{
    private readonly ILogger<MagicOnionBattleClient> _logger;
    private readonly MagicOnionBattleClient _client;

    public MagicOnionBattleClientTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<MagicOnionBattleClient>();
        _client = new MagicOnionBattleClient(_logger);
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        var client = new MagicOnionBattleClient(_logger);

        // Assert
        Assert.NotNull(client);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Constructor_WithNullLogger_DoesNotThrowInConstructor()
    {
        // Note: The actual implementation does not perform null checks in constructor
        var client = new MagicOnionBattleClient(null!);
        Assert.NotNull(client);
    }

    [Fact]
    public void IsConnected_InitialState_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(_client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidUrl_ReturnsFalse()
    {
        // Act
        var result = await _client.ConnectAsync("invalid-url");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectAsync_WithEmptyUrl_ReturnsFalse()
    {
        // Act
        var result = await _client.ConnectAsync("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectAsync_WithNullUrl_ReturnsFalse()
    {
        // Act
        var result = await _client.ConnectAsync(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        // Act & Assert
        await _client.DisconnectAsync(); // Should not throw
    }

    [Fact]
    public async Task GetAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetAsync("test-key"));
    }

    [Fact]
    public async Task SetAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.SetAsync("test-key", "test-value"));
    }

    [Fact]
    public async Task DeleteAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.DeleteAsync("test-key"));
    }

    [Fact]
    public async Task ListKeysAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.ListKeysAsync());
    }

    [Fact]
    public async Task JoinGroupAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.JoinGroupAsync("test-group"));
    }

    [Fact]
    public async Task BroadcastMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.BroadcastMessageAsync("test-message"));
    }

    [Fact]
    public async Task GetGroupsAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetGroupsAsync());
    }

    [Fact]
    public async Task GetCurrentGroupAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetCurrentGroupAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task GetAsync_WithInvalidKey_ThrowsInvalidOperationException(string? key)
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetAsync(key!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SetAsync_WithInvalidKey_ThrowsInvalidOperationException(string? key)
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.SetAsync(key!, "value"));
    }

    [Fact]
    public async Task ListAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.ListAsync("*"));
    }

    [Fact]
    public async Task BroadcastAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.BroadcastAsync("test-message"));
    }

    [Fact]
    public async Task GetMyGroupAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetMyGroupAsync());
    }

    [Fact]
    public async Task GetBattleReplayAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var battleId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetBattleReplayAsync(battleId));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Act & Assert
        await _client.DisposeAsync(); // Should not throw even when not implemented
    }

    public void Dispose()
    {
        _client?.DisposeAsync().AsTask().Wait();
    }
}
