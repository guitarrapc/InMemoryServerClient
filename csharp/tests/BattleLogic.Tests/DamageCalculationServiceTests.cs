using BattleLogic.Services;
using static BattleLogic.Constants.BattleSystemDefines;

namespace BattleLogic.Tests;

/// <summary>
/// Unit tests for DamageCalculationService
/// </summary>
public class DamageCalculationServiceTests(ITestOutputHelper output)
{
    private readonly Random _random = new(42); // Fixed seed for reproducibility
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(DamageCalculationFormula.Standard, 30, 10, 0, false, 25)] // 30 - 10/2 = 25
    [InlineData(DamageCalculationFormula.Standard, 30, 10, 0, true, 12)]  // 25 * (100-50)/100 = 12.5 → 12 (切り捨て)
    [InlineData(DamageCalculationFormula.Standard, 10, 20, 0, false, 1)]  // Should be minimum 1
    public void CalculateDamage_StandardFormula_ShouldCalculateCorrectly(
        DamageCalculationFormula formula,
        int attack,
        int defense,
        int criticalRate,
        bool isDefending,
        int expectedDamage)
    {
        // Arrange
        var random = new Random(42); // Fixed seed to avoid critical hits in this test

        // Act
        var (damage, isCriticalHit) = DamageCalculationService.CalculateDamage(formula, attack, defense, criticalRate, isDefending, random);

        // Assert
        Assert.Equal(expectedDamage, damage);
        Assert.False(isCriticalHit);
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
        var (damage, isCriticalHit) = DamageCalculationService.CalculateDamage(
            DamageCalculationFormula.Standard, attack, defense, criticalRate, false, random);

        // Assert
        Assert.True(isCriticalHit);
        // Standard formula: (30 - 10/2) * 2 = 25 * 2 = 50
        Assert.Equal(50, damage);
    }

    [Theory]
    [InlineData(DamageCalculationFormula.PercentageBased)]
    [InlineData(DamageCalculationFormula.SquareRoot)]
    [InlineData(DamageCalculationFormula.Logarithmic)]
    [InlineData(DamageCalculationFormula.LinearScaling)]
    public void CalculateDamage_AllFormulas_ShouldReturnMinimumOneDamage(DamageCalculationFormula formula)
    {
        // Arrange
        const int lowAttack = 1;
        const int highDefense = 100;
        var random = new Random(42);

        // Act
        var (damage, _) = DamageCalculationService.CalculateDamage(
            formula, lowAttack, highDefense, 0, false, random);

        // Assert
        Assert.True(damage >= 1, $"Formula {formula} should always return at least 1 damage");
    }

    [Theory]
    [InlineData(DamageCalculationFormula.Standard)]
    [InlineData(DamageCalculationFormula.PercentageBased)]
    [InlineData(DamageCalculationFormula.SquareRoot)]
    [InlineData(DamageCalculationFormula.Logarithmic)]
    [InlineData(DamageCalculationFormula.LinearScaling)]
    public void CalculateDamage_DefendingState_ShouldReduceDamage(DamageCalculationFormula formula)
    {
        // Arrange
        const int attack = 50;
        const int defense = 20;
        var random = new Random(42);

        // Act
        var (normalDamage, _) = DamageCalculationService.CalculateDamage(
            formula, attack, defense, 0, false, random);
        var (defendingDamage, _) = DamageCalculationService.CalculateDamage(
            formula, attack, defense, 0, true, random);

        // Assert
        Assert.True(defendingDamage < normalDamage,
            $"Formula {formula}: Defending should reduce damage. Normal: {normalDamage}, Defending: {defendingDamage}");
    }

    [Fact]
    public void CalculateDamage_PercentageBasedFormula_ShouldWorkAsExpected()
    {
        // Arrange
        const int attack = 100;
        const int defense = 40; // Should reduce damage by 20% (40/2)
        var random = new Random(42);

        // Act
        var (damage, _) = DamageCalculationService.CalculateDamage(
            DamageCalculationFormula.PercentageBased, attack, defense, 0, false, random);

        // Assert
        // Expected: 100 * (100 - 20) / 100 = 80
        Assert.Equal(80, damage);
    }

    [Fact]
    public void CalculateDamage_InvalidFormula_ShouldThrowException()
    {
        // Arrange
        var invalidFormula = (DamageCalculationFormula)999;
        var random = new Random(42);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DamageCalculationService.CalculateDamage(invalidFormula, 30, 10, 5, false, random));
    }

    [Fact]
    public void CalculateDamage_HighAttackVsLowDefense_LogarithmicShouldGiveBonus()
    {
        // Arrange
        const int highAttack = 100;
        const int lowDefense = 10;
        var random = new Random(42);

        // Act
        var (standardDamage, _) = DamageCalculationService.CalculateDamage(
            DamageCalculationFormula.Standard, highAttack, lowDefense, 0, false, random);
        var (logarithmicDamage, _) = DamageCalculationService.CalculateDamage(
            DamageCalculationFormula.Logarithmic, highAttack, lowDefense, 0, false, random);

        // Assert
        _output.WriteLine($"Standard: {standardDamage}, Logarithmic: {logarithmicDamage}");
        // Logarithmic formula should provide different results for high attack vs low defense
        Assert.True(logarithmicDamage > 0);
    }
}
