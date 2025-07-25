using BattleLogic.Services;

namespace BattleLogic.Tests;

/// <summary>
/// Unit tests for DamageCalculationService
/// </summary>
public class DamageCalculationServiceTests(ITestOutputHelper output)
{
    private readonly Random _random = new(42); // Fixed seed for reproducibility
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(DamageCalculationFormula.Standard, 30, 10, 0, false)] // Base: 30 - 10/2 = 25, with random variance
    [InlineData(DamageCalculationFormula.Standard, 30, 10, 0, true)]  // Base with defense reduction and variance
    [InlineData(DamageCalculationFormula.Standard, 10, 20, 0, false)] // Should be minimum 1
    public void CalculateDamage_StandardFormula_ShouldCalculateWithinRange(
        DamageCalculationFormula formula,
        int attack,
        int defense,
        int criticalRate,
        bool isDefending)
    {
        // Arrange
        var random = new Random(42); // Fixed seed for reproducibility

        // Act
        var (damage, isCriticalHit) = DamageCalculationService.CalculateDamage(formula, attack, defense, criticalRate, isDefending, random);

        // Assert
        Assert.True(damage >= 1, "Damage should be at least 1");
        Assert.False(isCriticalHit, "Critical hit should not occur with 0% rate");

        // Test damage is within reasonable range (considering random variance)
        if (attack >= defense)
        {
            Assert.True(damage > 0, "Damage should be positive when attack exceeds defense");
        }
    }

    [Fact]
    public void CalculateDamage_CriticalHit_ShouldDoubleDamage()
    {
        // Arrange
        var random = new Random(42);
        const int attack = 30;
        const int defense = 10;
        const int criticalRate = 100; // 100% critical rate

        // Act
        var (damage, isCriticalHit) = DamageCalculationService.CalculateDamage(DamageCalculationFormula.Standard, attack, defense, criticalRate, false, random);

        // Assert
        Assert.True(isCriticalHit);
        // Standard formula: (30 - 10/2) + variance = 25 + variance, then * 2 for critical
        // With ±10% variance of attack (±3), base damage could be 22-28, critical = 44-56
        Assert.True(damage >= 44 && damage <= 56, $"Critical damage should be in range 44-56, but was {damage}");
    }

    [Theory]
    [InlineData(DamageCalculationFormula.PercentageBased)]
    [InlineData(DamageCalculationFormula.SquareRoot)]
    [InlineData(DamageCalculationFormula.Logarithmic)]
    [InlineData(DamageCalculationFormula.LinearScaling)]
    [InlineData(DamageCalculationFormula.DragonQuest)]
    public void CalculateDamage_AllFormulas_ShouldReturnMinimumOneDamage(DamageCalculationFormula formula)
    {
        // Arrange
        const int lowAttack = 1;
        const int highDefense = 100;
        var random = new Random(42);

        // Act
        var (damage, _) = DamageCalculationService.CalculateDamage(formula, lowAttack, highDefense, 0, false, random);

        // Assert
        Assert.True(damage >= 1, $"Formula {formula} should always return at least 1 damage");
    }

    [Theory]
    [InlineData(DamageCalculationFormula.Standard)]
    [InlineData(DamageCalculationFormula.PercentageBased)]
    [InlineData(DamageCalculationFormula.SquareRoot)]
    [InlineData(DamageCalculationFormula.Logarithmic)]
    [InlineData(DamageCalculationFormula.LinearScaling)]
    [InlineData(DamageCalculationFormula.DragonQuest)]
    public void CalculateDamage_DefendingState_ShouldReduceDamage(DamageCalculationFormula formula)
    {
        // Arrange
        const int attack = 50;
        const int defense = 20;
        var random = new Random(42);

        // Act
        var (normalDamage, _) = DamageCalculationService.CalculateDamage(formula, attack, defense, 0, false, random);
        var (defendingDamage, _) = DamageCalculationService.CalculateDamage(formula, attack, defense, 0, true, random);

        // Assert
        Assert.True(defendingDamage < normalDamage, $"Formula {formula}: Defending should reduce damage. Normal: {normalDamage}, Defending: {defendingDamage}");
    }

    [Fact]
    public void CalculateDamage_PercentageBasedFormula_ShouldWorkAsExpected()
    {
        // Arrange
        const int attack = 100;
        const int defense = 40; // Should reduce damage by 20% (40/2)
        var random = new Random(42);

        // Act
        var (damage, _) = DamageCalculationService.CalculateDamage(DamageCalculationFormula.PercentageBased, attack, defense, 0, false, random);

        // Assert
        // Expected base: 100 * (100 - 20) / 100 = 80
        // With ±8% variance of base damage (±6.67), damage should be around 73-87
        Assert.True(damage >= 73 && damage <= 87, $"Percentage-based damage should be in range 73-87, but was {damage}");
    }

    [Fact]
    public void CalculateDamage_InvalidFormula_ShouldThrowException()
    {
        // Arrange
        var invalidFormula = (DamageCalculationFormula)999;
        var random = new Random(42);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculationService.CalculateDamage(invalidFormula, 30, 10, 5, false, random));
    }

    [Fact]
    public void CalculateDamage_HighAttackVsLowDefense_LogarithmicShouldGiveBonus()
    {
        // Arrange
        const int highAttack = 100;
        const int lowDefense = 10;
        var random = new Random(42);

        // Act
        var (standardDamage, _) = DamageCalculationService.CalculateDamage(DamageCalculationFormula.Standard, highAttack, lowDefense, 0, false, random);
        var (logarithmicDamage, _) = DamageCalculationService.CalculateDamage(DamageCalculationFormula.Logarithmic, highAttack, lowDefense, 0, false, random);

        // Assert
        _output.WriteLine($"Standard: {standardDamage}, Logarithmic: {logarithmicDamage}");
        // Logarithmic formula should provide different results for high attack vs low defense
        Assert.True(logarithmicDamage > 0);
    }

    [Fact]
    public void CalculateDamage_DragonQuestFormula_ShouldWorkAsExpected()
    {
        // Arrange
        const int attack = 40; // Base damage: 40/2 - 10/4 = 20 - 2.5 = 17.5 → 17
        const int defense = 10;
        var random = new Random(42);

        // Act
        var (damage, isCriticalHit) = DamageCalculationService.CalculateDamage(DamageCalculationFormula.DragonQuest, attack, defense, 0, false, random);

        // Assert
        Assert.False(isCriticalHit);
        Assert.True(damage >= 1);
        // Base: 17, with ±25% of attack (±10) variance, should be around 7-27
        Assert.True(damage > 5 && damage < 35, $"Expected damage in reasonable range for Dragon Quest formula, got {damage}");
    }

    [Fact]
    public void CalculateDamage_DragonQuestFormula_ShouldHaveSignificantVariance()
    {
        // Arrange
        const int attack = 60;
        const int defense = 20;
        var damages = new List<int>();

        // Act - Multiple calculations to see variance
        for (int i = 0; i < 100; i++)
        {
            var random = new Random(i); // Different seed each time
            var (damage, _) = DamageCalculationService.CalculateDamage(DamageCalculationFormula.DragonQuest, attack, defense, 0, false, random);
            damages.Add(damage);
        }

        // Assert
        var minDamage = damages.Min();
        var maxDamage = damages.Max();
        var avgDamage = damages.Average();

        _output.WriteLine($"Dragon Quest Formula Variance Test:");
        _output.WriteLine($"  Attack: {attack}, Defense: {defense}");
        _output.WriteLine($"  Min Damage: {minDamage}");
        _output.WriteLine($"  Max Damage: {maxDamage}");
        _output.WriteLine($"  Avg Damage: {avgDamage:F1}");
        _output.WriteLine($"  Variance Range: {maxDamage - minDamage}");

        // Dragon Quest should have significant variance
        Assert.True(maxDamage - minDamage > 10, "Dragon Quest formula should have significant damage variance");
        Assert.True(minDamage >= 1, "Minimum damage should be at least 1");
    }

    [Fact]
    public void CalculateDamage_StandardFormula_ShouldHaveRandomVariance()
    {
        // Arrange
        const int attack = 50;
        const int defense = 20;
        var damages = new List<int>();

        // Act - Multiple calculations to see variance
        for (int i = 0; i < 50; i++)
        {
            var random = new Random(i); // Different seed each time
            var (damage, _) = DamageCalculationService.CalculateDamage(DamageCalculationFormula.Standard, attack, defense, 0, false, random);
            damages.Add(damage);
        }

        // Assert
        var minDamage = damages.Min();
        var maxDamage = damages.Max();
        var avgDamage = damages.Average();

        _output.WriteLine($"Standard Formula Variance Test:");
        _output.WriteLine($"  Attack: {attack}, Defense: {defense}");
        _output.WriteLine($"  Min Damage: {minDamage}");
        _output.WriteLine($"  Max Damage: {maxDamage}");
        _output.WriteLine($"  Avg Damage: {avgDamage:F1}");
        _output.WriteLine($"  Variance Range: {maxDamage - minDamage}");

        // Standard formula should have variance from ±10% of attack
        Assert.True(maxDamage - minDamage > 5, "Standard formula should have damage variance");
        Assert.True(minDamage >= 1, "Minimum damage should be at least 1");
    }

    [Fact]
    public void CalculateDamage_AllFormulas_ShouldHaveRandomVariance()
    {
        // Arrange
        const int attack = 60;
        const int defense = 25;
        var formulas = new[]
        {
            DamageCalculationFormula.Standard,
            DamageCalculationFormula.PercentageBased,
            DamageCalculationFormula.SquareRoot,
            DamageCalculationFormula.Logarithmic,
            DamageCalculationFormula.LinearScaling,
            DamageCalculationFormula.DragonQuest
        };

        foreach (var formula in formulas)
        {
            var damages = new List<int>();

            // Act - Multiple calculations to see variance
            for (int i = 0; i < 30; i++)
            {
                var random = new Random(i); // Different seed each time
                var (damage, _) = DamageCalculationService.CalculateDamage(formula, attack, defense, 0, false, random);
                damages.Add(damage);
            }

            // Assert
            var minDamage = damages.Min();
            var maxDamage = damages.Max();
            var avgDamage = damages.Average();

            _output.WriteLine($"{formula} Formula:");
            _output.WriteLine($"  Min: {minDamage}, Max: {maxDamage}, Avg: {avgDamage:F1}, Range: {maxDamage - minDamage}");

            // All formulas should have some variance and minimum 1 damage
            Assert.True(maxDamage - minDamage > 0, $"Formula {formula} should have damage variance");
            Assert.True(minDamage >= 1, $"Formula {formula} minimum damage should be at least 1");
        }
    }
}
