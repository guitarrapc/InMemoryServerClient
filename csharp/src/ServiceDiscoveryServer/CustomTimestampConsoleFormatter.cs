using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using System.Globalization;

namespace ServiceDiscoveryServer;

/// <summary>
/// Custom timestamp console formatter for ServiceDiscoveryServer
/// </summary>
public sealed class CustomTimestampConsoleFormatter : ConsoleFormatter, IDisposable
{
    private readonly IDisposable? _optionsReloadToken;
    private CustomTimestampConsoleFormatterOptions _formatterOptions;

    public CustomTimestampConsoleFormatter(IOptionsMonitor<CustomTimestampConsoleFormatterOptions> options)
        : base("custom-timestamp")
    {
        _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
        _formatterOptions = options.CurrentValue;
    }

    private void ReloadLoggerOptions(CustomTimestampConsoleFormatterOptions options)
    {
        _formatterOptions = options;
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (message is null)
        {
            return;
        }

        var timestamp = _formatterOptions.UseUtcTimestamp
            ? DateTime.UtcNow
            : DateTime.Now;

        var formattedTimestamp = timestamp.ToString(_formatterOptions.TimestampFormat, CultureInfo.InvariantCulture);
        var logLevel = GetLogLevelString(logEntry.LogLevel);
        var categoryName = logEntry.Category;

        textWriter.Write($"{formattedTimestamp} [{logLevel}] {categoryName}: {message}");

        if (logEntry.Exception is not null)
        {
            textWriter.Write($" {logEntry.Exception}");
        }

        textWriter.WriteLine();
    }

    private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRCE",
        LogLevel.Debug => "DBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "FAIL",
        LogLevel.Critical => "CRIT",
        _ => "UNKN"
    };

    public void Dispose()
    {
        _optionsReloadToken?.Dispose();
    }
}

/// <summary>
/// Custom timestamp console formatter options
/// </summary>
public class CustomTimestampConsoleFormatterOptions : ConsoleFormatterOptions
{
    public new string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";
    public new bool UseUtcTimestamp { get; set; } = true;
}
