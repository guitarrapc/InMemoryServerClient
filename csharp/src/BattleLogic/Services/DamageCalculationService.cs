using BattleLogic.Constants;
using BattleLogic.Models;

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
            DamageCalculationFormula.Standard => CalculateStandardDamage(attackPower, defensePower, random),
            DamageCalculationFormula.PercentageBased => CalculatePercentageBasedDamage(attackPower, defensePower, random),
            DamageCalculationFormula.SquareRoot => CalculateSquareRootDamage(attackPower, defensePower, random),
            DamageCalculationFormula.Logarithmic => CalculateLogarithmicDamage(attackPower, defensePower, random),
            DamageCalculationFormula.LinearScaling => CalculateLinearScalingDamage(attackPower, defensePower, random),
            DamageCalculationFormula.DragonQuest => CalculateDragonQuestDamage(attackPower, defensePower, random),
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
    /// Standard formula: (Attack - Defense/2) + random variance, minimum 1 damage
    /// </summary>
    private static int CalculateStandardDamage(int attackPower, int defensePower, Random random)
    {
        int baseDamage = attackPower - defensePower / 2;
        // Add ±10% random variance
        int variance = random.Next(-attackPower / 10, attackPower / 10 + 1);
        return Math.Max(1, baseDamage + variance);
    }

    /// <summary>
    /// Percentage-based formula: Attack * (100 - DefenseReduction%) / 100 + random variance
    /// Defense reduces damage by a percentage based on defense value
    /// </summary>
    private static int CalculatePercentageBasedDamage(int attackPower, int defensePower, Random random)
    {
        // Defense reduces damage by (Defense / 2)% up to maximum 80%
        int defenseReduction = Math.Min(80, defensePower / 2);
        int baseDamage = attackPower * (100 - defenseReduction) / 100;
        // Add ±8% random variance
        int variance = random.Next(-baseDamage / 12, baseDamage / 12 + 1);
        return Math.Max(1, baseDamage + variance);
    }

    /// <summary>
    /// Square root formula: Attack - sqrt(Defense * AttackPower) + random variance
    /// Non-linear defense scaling with random variance
    /// </summary>
    private static int CalculateSquareRootDamage(int attackPower, int defensePower, Random random)
    {
        double defenseDamage = Math.Sqrt(defensePower * attackPower);
        int baseDamage = (int)(attackPower - defenseDamage);
        // Add ±12% random variance
        int variance = random.Next(-attackPower / 8, attackPower / 8 + 1);
        return Math.Max(1, baseDamage + variance);
    }

    /// <summary>
    /// Logarithmic formula: Attack * log(Attack/Defense + 1) + random variance
    /// High attack vs low defense gets bonus damage with random variance
    /// </summary>
    private static int CalculateLogarithmicDamage(int attackPower, int defensePower, Random random)
    {
        double ratio = Math.Max(0.1, attackPower / Math.Max(1, (double)defensePower));
        double multiplier = Math.Log(ratio + 1);
        int baseDamage = (int)(attackPower * multiplier);
        // Add ±15% random variance
        int variance = random.Next(-baseDamage / 6, baseDamage / 6 + 1);
        return Math.Max(1, baseDamage + variance);
    }

    /// <summary>
    /// Linear scaling formula: Attack * (1 - Defense / (Defense + ScalingFactor)) + random variance
    /// Diminishing returns on defense with random variance
    /// </summary>
    private static int CalculateLinearScalingDamage(int attackPower, int defensePower, Random random)
    {
        const int scalingFactor = 100;
        double damageMultiplier = 1.0 - ((double)defensePower / (defensePower + scalingFactor));
        int baseDamage = (int)(attackPower * damageMultiplier);
        // Add ±8% random variance
        int variance = random.Next(-baseDamage / 12, baseDamage / 12 + 1);
        return Math.Max(1, baseDamage + variance);
    }

    /// <summary>
    /// Dragon Quest formula: (Attack/2 - Defense/4) + random variance
    /// Classic JRPG formula with random damage variance
    /// </summary>
    private static int CalculateDragonQuestDamage(int attackPower, int defensePower, Random random)
    {
        int baseDamage = attackPower / 2 - defensePower / 4;
        // Dragon Quest style random variance: ±10% of attack power
        int maxVariance = attackPower / 10;
        int variance = random.Next(-maxVariance, maxVariance + 1);
        return Math.Max(1, baseDamage + variance);
    }
}
