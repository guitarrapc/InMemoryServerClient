namespace CliClient.Services;

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
