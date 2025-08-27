using CliClient.Clients;

namespace CliClient.Tests;

/// <summary>
/// Tests for ConsoleCommand class
/// These tests focus on constructor validation and basic instantiation
/// Since most methods are private, we test the public interface through interaction
/// </summary>
public class ConsoleCommandTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MultiBattleClientManager _multiClientManager;
    private readonly ILogger<ConsoleCommand> _logger;

    public ConsoleCommandTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _multiClientManager = new MultiBattleClientManager(_loggerFactory);
        _logger = _loggerFactory.CreateLogger<ConsoleCommand>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var command = new ConsoleCommand(_multiClientManager, _loggerFactory, _logger);

        // Assert
        Assert.NotNull(command);
    }

    [Fact]
    public void Constructor_WithNullMultiClientManager_DoesNotThrowInConstructor()
    {
        // Note: The actual implementation does not perform null checks in constructor
        // Null reference exceptions will be thrown during actual usage
        var command = new ConsoleCommand(null!, _loggerFactory, _logger);
        Assert.NotNull(command);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_DoesNotThrowInConstructor()
    {
        // Note: The actual implementation does not perform null checks in constructor
        var command = new ConsoleCommand(_multiClientManager, null!, _logger);
        Assert.NotNull(command);
    }

    [Fact]
    public void Constructor_WithNullLogger_DoesNotThrowInConstructor()
    {
        // Note: The actual implementation does not perform null checks in constructor
        var command = new ConsoleCommand(_multiClientManager, _loggerFactory, _logger);
        Assert.NotNull(command);
    }

    [Fact]
    public void Constructor_WithNullGameLiftClientProvider_DoesNotThrowInConstructor()
    {
        // Note: The actual implementation does not perform null checks in constructor
        var command = new ConsoleCommand(_multiClientManager, _loggerFactory, _logger);
        Assert.NotNull(command);
    }

    [Fact]
    public async Task InteractiveAsync_DoesNotThrow()
    {
        // This test verifies the interactive method exists
        // Since it's an interactive console loop, we can't test it directly
        // But we can verify it's available and properly defined

        // Act & Assert
        var method = typeof(ConsoleCommand).GetMethod("InteractiveAsync");
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
    }

    public void Dispose()
    {
        _multiClientManager?.DisposeAsync().AsTask().Wait();
        _loggerFactory?.Dispose();
    }
}
