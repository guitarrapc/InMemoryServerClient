using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Text;

namespace InMemoryServer;

/// <summary>
/// タイムスタンプを付与するカスタムコンソールフォーマッタのオプション
/// </summary>
public class CustomTimestampConsoleFormatterOptions : ConsoleFormatterOptions
{
// ConsoleFormatterOptionsを継承しているので、TimestampFormatとIncludeScopesは既に持っています
}

/// <summary>
/// タイムスタンプを付与するカスタムコンソールフォーマッタ
/// </summary>
public class CustomTimestampConsoleFormatter : ConsoleFormatter
{
private readonly IDisposable? _optionsReloadToken;
private CustomTimestampConsoleFormatterOptions _formatterOptions;

public CustomTimestampConsoleFormatter(IOptionsMonitor<CustomTimestampConsoleFormatterOptions> options)
    : base("customTimestamp")
{
    _optionsReloadToken = options.OnChange(ReloadOptions);
    _formatterOptions = options.CurrentValue;
}

private void ReloadOptions(CustomTimestampConsoleFormatterOptions options)
{
    _formatterOptions = options;
}

public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
{
    string? message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
    if (message == null)
    {
        return;
    }

    var sb = new StringBuilder();

    // タイムスタンプを追加
    if (!string.IsNullOrEmpty(_formatterOptions.TimestampFormat))
    {
        string timestamp = DateTime.Now.ToString(_formatterOptions.TimestampFormat);
        sb.Append(timestamp);
    }

    // ログレベル
    sb.Append(GetLogLevelString(logEntry.LogLevel));
    sb.Append(": ");

    // カテゴリ名
    sb.Append(logEntry.Category);
    sb.Append("[0] ");

    // スコープ情報を追加
    if (_formatterOptions.IncludeScopes && scopeProvider != null)
    {
        scopeProvider.ForEachScope((scope, state) =>
        {
            state.Append(" => ");
            state.Append(scope);
        }, sb);
        sb.Append(" ");
    }

    // メッセージ
    sb.AppendLine(message);

    // 例外
    if (logEntry.Exception != null)
    {
        sb.AppendLine(logEntry.Exception.ToString());
    }        textWriter.Write(sb.ToString());
}

private static string GetLogLevelString(LogLevel logLevel)
{
    return logLevel switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRITICAL",
        _ => "UNKNOWN"
    };
}
}
