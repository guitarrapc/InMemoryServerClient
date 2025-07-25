namespace BattleLogic.Models;

/// <summary>
/// Damage calculation formula types
/// </summary>
public enum DamageCalculationFormula
{
    /// <summary>
    /// Current formula: (Attack - Defense/2) with critical hit doubling damage
    /// </summary>
    Standard,

    /// <summary>
    /// Percentage-based formula: Attack * (100 - DefenseReduction%) / 100
    /// </summary>
    PercentageBased,

    /// <summary>
    /// Square root formula: Attack - sqrt(Defense * AttackPower)
    /// </summary>
    SquareRoot,

    /// <summary>
    /// Logarithmic formula: Attack * log(Attack/Defense + 1)
    /// </summary>
    Logarithmic,

    /// <summary>
    /// Linear scaling: Attack * (1 - Defense / (Defense + 100))
    /// </summary>
    LinearScaling,

    /// <summary>
    /// Dragon Quest formula: (Attack/2 - Defense/4) + random variance
    /// Classic JRPG formula with random damage variance
    /// </summary>
    DragonQuest
}
