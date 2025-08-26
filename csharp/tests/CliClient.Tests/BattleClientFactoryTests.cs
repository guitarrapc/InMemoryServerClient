using CliClient.Clients;

namespace CliClient.Tests;

/// <summary>
/// Test class for BattleClientFactory
/// </summary>
public class BattleClientFactoryTests
{
    private readonly ILoggerFactory _loggerFactory;

    public BattleClientFactoryTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    [Fact]
    public void Create_WithSignalRConnectionType_ReturnsSignalRBattleClient()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory, ct);

        // Assert
        Assert.NotNull(client);
        Assert.IsType<SignalRBattleClient>(client);
    }

    [Fact]
    public void Create_WithMagicOnionConnectionType_ReturnsMagicOnionBattleClient()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        var client = BattleClientFactory.Create(ConnectionType.MagicOnion, _loggerFactory, ct);

        // Assert
        Assert.NotNull(client);
        Assert.IsType<MagicOnionBattleClient>(client);
    }

    [Fact]
    public void Create_WithUnsupportedConnectionType_ThrowsArgumentException()
    {
        // Arrange
        var unsupportedType = (ConnectionType)999;
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => BattleClientFactory.Create(unsupportedType, _loggerFactory, ct));

        Assert.Contains("Unsupported connection type", exception.Message);
    }

    [Fact]
    public void Create_WithDefaultConnectionType_ReturnsSignalRBattleClient()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        var client = BattleClientFactory.Create(_loggerFactory, ct);

        // Assert
        Assert.NotNull(client);
        Assert.IsType<SignalRBattleClient>(client);
    }

    [Fact]
    public void Create_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => BattleClientFactory.Create(ConnectionType.SignalR, null!, ct));
    }

    [Theory]
    [InlineData(ConnectionType.SignalR)]
    [InlineData(ConnectionType.MagicOnion)]
    public void Create_WithValidConnectionTypes_ReturnsNonNullClient(ConnectionType connectionType)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        var client = BattleClientFactory.Create(connectionType, _loggerFactory, ct);

        // Assert
        Assert.NotNull(client);
        Assert.IsAssignableFrom<IBattleClient>(client);
    }

    [Fact]
    public void Create_MultipleCallsWithSameParameters_ReturnsDifferentInstances()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        var client1 = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory, ct);
        var client2 = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory, ct);

        // Assert
        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.NotSame(client1, client2);
    }
}
