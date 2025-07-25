using BattleLogic.Constans;
using static BattleLogic.Constans.BattleSystemDefines;

namespace BattleLogic.Services;

/// <summary>
/// Service for calculating damage using different formulas
/// </summary>
public static class DamageCalculationService
{
    /// <summary>
    /// Calculate damage using the specified formula
    /// </summary>
    /// <param name="formula">The damage calculation formula to use</param>
    /// <param name="attackPower">Attacker's attack power (after flavor)</param>
    /// <param name="defensePower">Target's defense power (after flavor)</param>
    /// <param name="criticalRate">Attacker's critical hit rate</param>
    /// <param name="isDefending">Whether target is defending</param>
    /// <param name="random">Random instance for critical hit calculation</param>
    /// <returns>Calculated damage and whether it was a critical hit</returns>
    public static (int damage, bool isCriticalHit) CalculateDamage(
        DamageCalculationFormula formula,
        int attackPower,
        int defensePower,
        int criticalRate,
        bool isDefending,
        Random random)
    {
        // Critical hit check (common for all formulas)
        bool isCriticalHit = false;
        int criticalRoll = random.Next(1, 101);
        if (criticalRoll <= criticalRate)
        {
            isCriticalHit = true;
        }

        // Calculate base damage using the specified formula
        int baseDamage = formula switch
        {
            DamageCalculationFormula.Standard => CalculateStandardDamage(attackPower, defensePower),
            DamageCalculationFormula.PercentageBased => CalculatePercentageBasedDamage(attackPower, defensePower),
            DamageCalculationFormula.SquareRoot => CalculateSquareRootDamage(attackPower, defensePower),
            DamageCalculationFormula.Logarithmic => CalculateLogarithmicDamage(attackPower, defensePower),
            DamageCalculationFormula.LinearScaling => CalculateLinearScalingDamage(attackPower, defensePower),
            _ => throw new ArgumentOutOfRangeException(nameof(formula), formula, "Unknown damage calculation formula")
        };

        // Apply critical hit multiplier
        if (isCriticalHit)
        {
            baseDamage *= 2;
        }

        // Apply defense damage reduction if defending
        int finalDamage = baseDamage;
        if (isDefending)
        {
            finalDamage = finalDamage * (100 - BattleSystemDefines.DefenseDamageReductionPercent) / 100;
        }

        return (Math.Max(1, finalDamage), isCriticalHit);
    }

    /// <summary>
    /// Standard formula: Attack - Defense/2, minimum 1 damage
    /// </summary>
    private static int CalculateStandardDamage(int attackPower, int defensePower)
    {
        return Math.Max(1, attackPower - defensePower / 2);
    }

    /// <summary>
    /// Percentage-based formula: Attack * (100 - DefenseReduction%) / 100
    /// Defense reduces damage by a percentage based on defense value
    /// </summary>
    private static int CalculatePercentageBasedDamage(int attackPower, int defensePower)
    {
        // Defense reduces damage by (Defense / 2)% up to maximum 80%
        int defenseReduction = Math.Min(80, defensePower / 2);
        int damage = attackPower * (100 - defenseReduction) / 100;
        return Math.Max(1, damage);
    }

    /// <summary>
    /// Square root formula: Attack - sqrt(Defense * AttackPower)
    /// Non-linear defense scaling
    /// </summary>
    private static int CalculateSquareRootDamage(int attackPower, int defensePower)
    {
        double defenseDamage = Math.Sqrt(defensePower * attackPower);
        int damage = (int)(attackPower - defenseDamage);
        return Math.Max(1, damage);
    }

    /// <summary>
    /// Logarithmic formula: Attack * log(Attack/Defense + 1)
    /// High attack vs low defense gets bonus damage
    /// </summary>
    private static int CalculateLogarithmicDamage(int attackPower, int defensePower)
    {
        double ratio = Math.Max(0.1, attackPower / Math.Max(1, (double)defensePower));
        double multiplier = Math.Log(ratio + 1);
        int damage = (int)(attackPower * multiplier);
        return Math.Max(1, damage);
    }

    /// <summary>
    /// Linear scaling formula: Attack * (1 - Defense / (Defense + ScalingFactor))
    /// Diminishing returns on defense
    /// </summary>
    private static int CalculateLinearScalingDamage(int attackPower, int defensePower)
    {
        const int scalingFactor = 100;
        double damageMultiplier = 1.0 - ((double)defensePower / (defensePower + scalingFactor));
        int damage = (int)(attackPower * damageMultiplier);
        return Math.Max(1, damage);
    }
}
