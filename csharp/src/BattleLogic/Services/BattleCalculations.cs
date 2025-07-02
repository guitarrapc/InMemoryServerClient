namespace BattleLogic.Services;

/// <summary>
/// Battle calculation utilities and helper methods
/// </summary>
internal static class BattleCalculations
{
    /// <summary>
    /// Apply random flavor variation to attack power
    /// </summary>
    /// <param name="baseAttack">Base attack value</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Attack value with random flavor applied</returns>
    public static int ApplyAttackFlavor(int baseAttack, Random random)
    {
        var variation = Math.Max(BattleSystemDefines.MinAbsoluteFlavor, (int)(baseAttack * BattleSystemDefines.AttackFlavorPercent));
        var flavorRange = variation * 2 + 1; // -variation to +variation
        var flavor = random.Next(flavorRange) - variation;
        return Math.Max(1, baseAttack + flavor); // Ensure minimum 1 attack
    }

    /// <summary>
    /// Apply random flavor variation to defense power
    /// </summary>
    /// <param name="baseDefense">Base defense value</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Defense value with random flavor applied</returns>
    public static int ApplyDefenseFlavor(int baseDefense, Random random)
    {
        var variation = Math.Max(BattleSystemDefines.MinAbsoluteFlavor, (int)(baseDefense * BattleSystemDefines.DefenseFlavorPercent));
        var flavorRange = variation * 2 + 1; // -variation to +variation
        var flavor = random.Next(flavorRange) - variation;
        return Math.Max(0, baseDefense + flavor); // Defense can be 0
    }

    /// <summary>
    /// Apply random flavor variation to accuracy
    /// </summary>
    /// <param name="baseAccuracy">Base accuracy percentage</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Accuracy percentage with random flavor applied (minimum 0, no maximum limit)</returns>
    public static int ApplyAccuracyFlavor(int baseAccuracy, Random random)
    {
        var flavorRange = BattleSystemDefines.AccuracyFlavorRange * 2 + 1; // -range to +range
        var flavor = random.Next(flavorRange) - BattleSystemDefines.AccuracyFlavorRange;
        return Math.Max(0, baseAccuracy + flavor); // Keep minimum 0, but allow exceeding 100%
    }

    /// <summary>
    /// Apply random flavor variation to evasion
    /// </summary>
    /// <param name="baseEvasion">Base evasion percentage</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Evasion percentage with random flavor applied (minimum 0, no maximum limit)</returns>
    public static int ApplyEvasionFlavor(int baseEvasion, Random random)
    {
        var flavorRange = BattleSystemDefines.EvasionFlavorRange * 2 + 1; // -range to +range
        var flavor = random.Next(flavorRange) - BattleSystemDefines.EvasionFlavorRange;
        return Math.Max(0, baseEvasion + flavor); // Keep minimum 0, but allow exceeding 100%
    }
}
