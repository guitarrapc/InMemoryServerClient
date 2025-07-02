namespace Shared.Battle;

/// <summary>
/// Battle log item
/// </summary>
public readonly struct BattleLogItem
{
    /// <summary>
    /// Log message
    /// </summary>
    public readonly string Message { get; init; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public readonly DateTime Timestamp { get; init; }

    /// <summary>
    /// Creates a new battle log item
    /// </summary>
    public BattleLogItem(string message)
    {
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}
