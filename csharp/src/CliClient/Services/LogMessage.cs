using Microsoft.Extensions.Logging;

namespace CliClient.Services;

/// <summary>
/// Extension methods for ILogger to provide type-safe battle logging functionality.
/// Uses struct-based message passing to avoid object allocations and ensure type safety.
/// </summary>
public static class BattleLoggerExtensions
{
    /// <summary>
    /// Logs battle information with type-safe struct-based message
    /// </summary>
    /// <typeparam name="T">The struct type containing log data</typeparam>
    /// <param name="logger">The logger instance</param>
    /// <param name="logData">The struct containing log data</param>
    public static void LogBattleInfo<T>(this ILogger logger, in T logData) where T : struct, IFormattable
    {
        if (!logger.IsEnabled(LogLevel.Information))
            return;

        var message = logData.ToString(null, null);
        logger.LogInformation("[BATTLE] {Message}", message);
    }

    /// <summary>
    /// Logs battle warning with type-safe struct-based message
    /// </summary>
    /// <typeparam name="T">The struct type containing log data</typeparam>
    /// <param name="logger">The logger instance</param>
    /// <param name="logData">The struct containing log data</param>
    public static void LogBattleWarning<T>(this ILogger logger, in T logData) where T : struct, IFormattable
    {
        if (!logger.IsEnabled(LogLevel.Warning))
            return;

        var message = logData.ToString(null, null);
        logger.LogWarning("[BATTLE] ⚠️ {Message}", message);
    }

    /// <summary>
    /// Logs battle error with type-safe struct-based message
    /// </summary>
    /// <typeparam name="T">The struct type containing log data</typeparam>
    /// <param name="logger">The logger instance</param>
    /// <param name="logData">The struct containing log data</param>
    public static void LogBattleError<T>(this ILogger logger, in T logData) where T : struct, IFormattable
    {
        if (!logger.IsEnabled(LogLevel.Error))
            return;

        var message = logData.ToString(null, null);
        logger.LogError("[BATTLE] ❌ {Message}", message);
    }

    /// <summary>
    /// Logs battle message with custom log level and type-safe struct-based message
    /// </summary>
    /// <typeparam name="T">The struct type containing log data</typeparam>
    /// <param name="logger">The logger instance</param>
    /// <param name="logLevel">The log level</param>
    /// <param name="logData">The struct containing log data</param>
    public static void LogBattle<T>(this ILogger logger, LogLevel logLevel, in T logData) where T : struct, IFormattable
    {
        if (!logger.IsEnabled(logLevel))
            return;

        var message = logData.ToString(null, null);
        var prefix = logLevel switch
        {
            LogLevel.Error => "[BATTLE] ❌",
            LogLevel.Warning => "[BATTLE] ⚠️",
            LogLevel.Information => "[BATTLE]",
            LogLevel.Debug => "[BATTLE] 🐛",
            _ => "[BATTLE]"
        };

        logger.Log(logLevel, "{Prefix} {Message}", prefix, message);
    }
}

/// <summary>
/// Represents a log message with strongly typed arguments
/// </summary>
/// <typeparam name="T">The type of the arguments</typeparam>
public readonly record struct LogMessage<T>
{
    /// <summary>
    /// The message template with placeholders
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The strongly typed arguments for the message template
    /// </summary>
    public required T Args { get; init; }
}

/// <summary>
/// Represents a log message with no arguments
/// </summary>
public readonly record struct LogMessage
{
    /// <summary>
    /// The message template
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Creates a LogMessage with no arguments
    /// </summary>
    /// <param name="message">The message template</param>
    /// <returns>A LogMessage with no arguments</returns>
    public static LogMessage Create(string message) => new() { Message = message };
}
