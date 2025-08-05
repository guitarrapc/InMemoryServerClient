using Microsoft.Extensions.Logging;

namespace CliClient.Extensions;

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
