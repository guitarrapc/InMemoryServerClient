using CliClient.Clients;

namespace CliClient.Tests;

/// <summary>
/// Tests for MagicOnionBattleClient
/// These tests focus on client behavior without requiring an actual server connection
/// </summary>
public class MagicOnionBattleClientTests
{
    private readonly ILogger<MagicOnionBattleClient> _logger;

    public MagicOnionBattleClientTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<MagicOnionBattleClient>();
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Assert
        Assert.NotNull(client);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Constructor_WithNullLogger_DoesNotThrowInConstructor()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        // Note: The actual implementation does not perform null checks in constructor
        var client = new MagicOnionBattleClient(null!, cts.Token);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void IsConnected_InitialState_ReturnsFalse()
    {
        // Arrange & Act
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Assert
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidUrl_ReturnsFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act
        var result = await client.ConnectAsync("invalid-url");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectAsync_WithEmptyUrl_ReturnsFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act
        var result = await client.ConnectAsync("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConnectAsync_WithNullUrl_ReturnsFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act
        var result = await client.ConnectAsync(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await client.DisconnectAsync(); // Should not throw
    }

    [Fact]
    public async Task GetAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("test-key"));
    }

    [Fact]
    public async Task SetAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetAsync("test-key", "test-value"));
    }

    [Fact]
    public async Task DeleteAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeleteAsync("test-key"));
    }

    [Fact]
    public async Task ListKeysAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListKeysAsync());
    }

    [Fact]
    public async Task JoinGroupAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.JoinGroupAsync("test-group"));
    }

    [Fact]
    public async Task BroadcastMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.BroadcastMessageAsync("test-message"));
    }

    [Fact]
    public async Task GetGroupsAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetGroupsAsync());
    }

    [Fact]
    public async Task GetCurrentGroupAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetCurrentGroupAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task GetAsync_WithInvalidKey_ThrowsInvalidOperationException(string? key)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(key!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SetAsync_WithInvalidKey_ThrowsInvalidOperationException(string? key)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetAsync(key!, "value"));
    }

    [Fact]
    public async Task ListAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListAsync("*"));
    }

    [Fact]
    public async Task BroadcastAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.BroadcastAsync("test-message"));
    }

    [Fact]
    public async Task GetMyGroupAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetMyGroupAsync());
    }

    [Fact]
    public async Task GetBattleReplayAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        var battleId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetBattleReplayAsync(battleId));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var client = new MagicOnionBattleClient(_logger, cts.Token);

        // Act & Assert
        await client.DisposeAsync(); // Should not throw even when not implemented
    }
}
