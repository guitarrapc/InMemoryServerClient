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

/// <summary>
/// Job stat modifier
/// </summary>
public readonly record struct JobStatModifier
{
    /// <summary>
    /// HP multiplier
    /// </summary>
    public float HpMultiplier { get; init; }

    /// <summary>
    /// Attack multiplier
    /// </summary>
    public float AttackMultiplier { get; init; }

    /// <summary>
    /// Defense multiplier
    /// </summary>
    public float DefenseMultiplier { get; init; }

    /// <summary>
    /// Speed multiplier
    /// </summary>
    public float SpeedMultiplier { get; init; }

    /// <summary>
    /// Accuracy multiplier
    /// </summary>
    public float AccuracyMultiplier { get; init; }

    /// <summary>
    /// Evasion multiplier
    /// </summary>
    public float EvasionMultiplier { get; init; }

    /// <summary>
    /// HP bonus (flat addition)
    /// </summary>
    public int HpBonus { get; init; }

    /// <summary>
    /// Attack bonus (flat addition)
    /// </summary>
    public int AttackBonus { get; init; }

    /// <summary>
    /// Defense bonus (flat addition)
    /// </summary>
    public int DefenseBonus { get; init; }

    /// <summary>
    /// Speed bonus (flat addition)
    /// </summary>
    public int SpeedBonus { get; init; }

    /// <summary>
    /// Accuracy bonus (flat addition)
    /// </summary>
    public int AccuracyBonus { get; init; }

    /// <summary>
    /// Evasion bonus (flat addition)
    /// </summary>
    public int EvasionBonus { get; init; }
}

/// <summary>
/// Battle field information
/// </summary>
public readonly struct BattleFieldInfo
{
    /// <summary>
    /// Field width
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Field height
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Field cells
    /// </summary>
    public ReadOnlyMemory<ReadOnlyMemory<string?>> Cells { get; init; }
}
