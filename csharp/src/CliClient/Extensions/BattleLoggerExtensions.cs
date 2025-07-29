using Microsoft.Extensions.Logging;
using CliClient.Services;

namespace CliClient.Extensions;

/// <summary>
/// Extension methods for ILogger to provide standardized battle logging
/// </summary>
public static class BattleLoggerExtensions
{
    /// <summary>
    /// Log a standardized battle message using the message service
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="logLevel">The log level</param>
    /// <param name="messageSelector">Function to select the appropriate message from the service</param>
    public static void LogBattle(this ILogger logger, LogLevel logLevel,
        Func<IBattleLogMessageService, (string message, object?[] args)> messageSelector)
    {
        var service = new BattleLogMessageService();
        var (message, args) = messageSelector(service);
        logger.Log(logLevel, message, args);
    }

    /// <summary>
    /// Log a standardized battle information message
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="messageSelector">Function to select the appropriate message from the service</param>
    public static void LogBattleInfo(this ILogger logger,
        Func<IBattleLogMessageService, (string message, object?[] args)> messageSelector)
    {
        logger.LogBattle(LogLevel.Information, messageSelector);
    }

    /// <summary>
    /// Log a standardized battle warning message
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="messageSelector">Function to select the appropriate message from the service</param>
    public static void LogBattleWarning(this ILogger logger,
        Func<IBattleLogMessageService, (string message, object?[] args)> messageSelector)
    {
        logger.LogBattle(LogLevel.Warning, messageSelector);
    }

    /// <summary>
    /// Log a standardized battle error message
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="messageSelector">Function to select the appropriate message from the service</param>
    public static void LogBattleError(this ILogger logger,
        Func<IBattleLogMessageService, (string message, object?[] args)> messageSelector)
    {
        logger.LogBattle(LogLevel.Error, messageSelector);
    }
}
