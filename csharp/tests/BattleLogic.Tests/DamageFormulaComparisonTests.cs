using BattleLogic.Services;

namespace BattleLogic.Tests;

/// <summary>
/// Tests for comparing different damage calculation formulas
/// </summary>
public class DamageFormulaComparisonTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void CompareAllDamageFormulas_ShouldProvideBasicCalculationResults()
    {
        // Arrange
        var formulas = Enum.GetValues<DamageCalculationFormula>();
        var results = new Dictionary<DamageCalculationFormula, FormulaTestResult>();

        // Test parameters
        const int testAttack = 50;
        const int testDefense = 20;
        const int criticalRate = 10;
        const int iterations = 100;

        // Act - Test each formula with basic calculations
        foreach (var formula in formulas)
        {
            var result = TestFormulaBasicCalculation(formula, testAttack, testDefense, criticalRate, iterations);
            results[formula] = result;
        }

        // Assert and Output results
        _output.WriteLine("=== Damage Formula Comparison Results ===");
        _output.WriteLine($"Attack: {testAttack}, Defense: {testDefense}, Critical Rate: {criticalRate}%");
        _output.WriteLine($"Test iterations: {iterations}");
        _output.WriteLine("");

        foreach (var kvp in results.OrderByDescending(x => x.Value.AverageDamage))
        {
            var formula = kvp.Key;
            var result = kvp.Value;

            _output.WriteLine($"Formula: {formula}");
            _output.WriteLine($"  Average Damage: {result.AverageDamage:F1}");
            _output.WriteLine($"  Critical Hit Rate: {result.CriticalHitRate:P1}");
            _output.WriteLine($"  Min/Max Damage: {result.MinDamage}/{result.MaxDamage}");
            _output.WriteLine("");
        }

        // Ensure all formulas produce reasonable results
        foreach (var result in results.Values)
        {
            Assert.True(result.AverageDamage > 0, "All formulas should produce damage");
            Assert.True(result.MinDamage >= 1, "All formulas should have minimum damage of 1");
        }
    }

    [Theory]
    [InlineData(DamageCalculationFormula.Standard)]
    [InlineData(DamageCalculationFormula.PercentageBased)]
    [InlineData(DamageCalculationFormula.SquareRoot)]
    [InlineData(DamageCalculationFormula.Logarithmic)]
    [InlineData(DamageCalculationFormula.LinearScaling)]
    [InlineData(DamageCalculationFormula.DragonQuest)]
    public void TestIndividualFormula_ShouldProduceValidResults(DamageCalculationFormula formula)
    {
        // Arrange & Act
        var result = TestFormulaBasicCalculation(formula, 30, 15, 5, 10);

        // Assert
        Assert.True(result.AverageDamage > 0);
        Assert.True(result.MinDamage >= 1);
        Assert.True(result.CriticalHitRate >= 0 && result.CriticalHitRate <= 1);

        _output.WriteLine($"Formula {formula}: {result.AverageDamage:F1} avg damage, {result.CriticalHitRate:P1} crit rate");
    }

    private FormulaTestResult TestFormulaBasicCalculation(DamageCalculationFormula formula, int attack, int defense, int criticalRate, int iterations)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var damages = new List<int>();
        var criticalHits = 0;

        for (int i = 0; i < iterations; i++)
        {
            var (damage, isCritical) = DamageCalculationService.CalculateDamage(
                formula, attack, defense, criticalRate, false, random);

            damages.Add(damage);
            if (isCritical) criticalHits++;
        }

        return new FormulaTestResult
        {
            Formula = formula,
            AverageDamage = damages.Average(),
            MinDamage = damages.Min(),
            MaxDamage = damages.Max(),
            CriticalHitRate = (double)criticalHits / iterations
        };
    }

    private record FormulaTestResult
    {
        public required DamageCalculationFormula Formula { get; init; }
        public required double AverageDamage { get; init; }
        public required int MinDamage { get; init; }
        public required int MaxDamage { get; init; }
        public required double CriticalHitRate { get; init; }
    }
}
