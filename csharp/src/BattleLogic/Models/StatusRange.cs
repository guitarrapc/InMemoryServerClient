namespace BattleLogic.Models;

/// <summary>
/// Status range for defining min/max values
/// </summary>
public readonly record struct StatusRange
{
    public int Min { get; init; }
    public int Max { get; init; }

    public StatusRange(int min, int max)
    {
        Min = min;
        Max = max;
    }
}
