using CliClient.Extensions;
using CliClient.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CliClient.Tests;

/// <summary>
/// Test class for BattleLoggerExtensions
/// </summary>
public class BattleLoggerExtensionsTests
{
    private readonly ILogger _mockLogger = Substitute.For<ILogger>();

    [Fact]
    public void LogBattleInfo_ShouldCallLogInformationWithCorrectParameters()
    {
        // Arrange
        const string connectionId = "test-conn-123";
        const string groupName = "test-group";

        // Act
        _mockLogger.LogBattleInfo(svc => svc.FormatMemberJoined(connectionId, groupName));

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("New member joined")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogBattleWarning_ShouldCallLogWarningWithCorrectParameters()
    {
        // Arrange
        const string groupName = "test-group";
        const string groupId = "test-id";
        const string reason = "timeout";

        // Act
        _mockLogger.LogBattleWarning(svc => svc.FormatGroupDissolved(groupName, groupId, reason));

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("Group dissolved")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogBattleError_ShouldCallLogErrorWithCorrectParameters()
    {
        // Act
        _mockLogger.LogBattleError(svc => svc.FormatConnectionConfirmationFailed());

        // Assert
        _mockLogger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("Failed to confirm")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogBattle_ShouldCallLogWithSpecifiedLogLevel()
    {
        // Arrange
        const LogLevel customLogLevel = LogLevel.Debug;

        // Act
        _mockLogger.LogBattle(customLogLevel, svc => svc.FormatConnected());

        // Assert
        _mockLogger.Received(1).Log(
            customLogLevel,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("Connected to server")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
