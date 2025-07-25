namespace BattleLogic.Models;

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
    /// Critical rate multiplier
    /// </summary>
    public float CriticalRateMultiplier { get; init; }

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

    /// <summary>
    /// Critical rate bonus (flat addition)
    /// </summary>
    public int CriticalRateBonus { get; init; }
}
