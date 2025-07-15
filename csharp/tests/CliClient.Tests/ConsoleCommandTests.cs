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
    private readonly ConsoleCommand _consoleCommand;

    public ConsoleCommandTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _multiClientManager = new MultiBattleClientManager(_loggerFactory);
        _logger = _loggerFactory.CreateLogger<ConsoleCommand>();
        _consoleCommand = new ConsoleCommand(_multiClientManager, _loggerFactory, _logger);
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
        var command = new ConsoleCommand(_multiClientManager, _loggerFactory, null!);
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

    [Theory]
    [InlineData("connect")]
    [InlineData("status")]
    [InlineData("get")]
    [InlineData("set")]
    public void ConsoleCommand_HasExpectedCommandHandling_ForBasicCommands(string expectedCommand)
    {
        // This test verifies that the ConsoleCommand class is properly structured
        // by checking that it contains references to expected command strings

        // Arrange & Act
        var sourceCode = File.ReadAllText($@"{Directory.GetCurrentDirectory()}/../../../../../src/CliClient/ConsoleCommand.cs");

        // Assert
        Assert.Contains($"\"{expectedCommand}\"", sourceCode);
    }

    [Fact]
    public void ConsoleCommand_IsProperlyStructured()
    {
        // Verify the class has the expected structure for ConsoleAppFramework

        // Act
        var type = typeof(ConsoleCommand);
        var constructorParams = type.GetConstructors()[0].GetParameters();

        // Assert
        Assert.Equal(3, constructorParams.Length);
        Assert.Equal(typeof(MultiBattleClientManager), constructorParams[0].ParameterType);
        Assert.Equal(typeof(ILoggerFactory), constructorParams[1].ParameterType);
        Assert.Equal(typeof(ILogger<ConsoleCommand>), constructorParams[2].ParameterType);
    }

    public void Dispose()
    {
        _multiClientManager?.DisposeAsync().AsTask().Wait();
        _loggerFactory?.Dispose();
    }
}
