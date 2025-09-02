using Microsoft.Extensions.Logging;

namespace BattleLogic.Tests;

/// <summary>
/// Tests for comparing win rates across different damage calculation formulas
/// </summary>
public class BattleFormulaWinRateTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

    [Fact]
    public async Task BattleFormula_WinRateComparison_ShouldProvideInsights()
    {
        // Arrange
        const int battleCount = 50; // Number of battles to simulate for each formula
        const int baseSeed = 12345;
        var formulas = Enum.GetValues<DamageCalculationFormula>();
        var results = new Dictionary<DamageCalculationFormula, WinRateResult>();

        // Act - Test each formula with multiple battles
        foreach (var formula in formulas)
        {
            var wins = 0;
            var totalBattles = 0;
            var battleDurations = new List<int>();

            for (int i = 0; i < battleCount; i++)
            {
                var battleId = BattleSeed.NewTimestampId();
                var seed = baseSeed + i; // Vary seed for each battle
                var mockGroup = CreateMockGroup();
                var logger = _loggerFactory.CreateLogger<BattleState>();

                var battle = TestHelpers.CreateBattleState(battleId, seed, mockGroup, logger, _loggerFactory, formula);

                await battle.RunBattleAsync();
                var status = battle.GetStatus();

                totalBattles++;
                if (status.IsPlayerVictory == true)
                {
                    wins++;
                }

                battleDurations.Add(status.CurrentTurn);
            }

            results[formula] = new WinRateResult
            {
                Formula = formula,
                WinRate = (double)wins / totalBattles,
                TotalBattles = totalBattles,
                Wins = wins,
                AverageDuration = battleDurations.Average(),
                MinDuration = battleDurations.Min(),
                MaxDuration = battleDurations.Max()
            };
        }

        // Assert and Output results
        _output.WriteLine("=== Battle Formula Win Rate Comparison ===");
        _output.WriteLine($"Battles per formula: {battleCount}");
        _output.WriteLine($"Base seed: {baseSeed}");
        _output.WriteLine("");

        foreach (var kvp in results.OrderByDescending(x => x.Value.WinRate))
        {
            var result = kvp.Value;
            _output.WriteLine($"Formula: {result.Formula}");
            _output.WriteLine($"  Win Rate: {result.WinRate:P1} ({result.Wins}/{result.TotalBattles})");
            _output.WriteLine($"  Average Duration: {result.AverageDuration:F1} turns");
            _output.WriteLine($"  Duration Range: {result.MinDuration}-{result.MaxDuration} turns");
            _output.WriteLine("");
        }

        // Basic validation - all formulas should have reasonable win rates
        foreach (var result in results.Values)
        {
            Assert.True(result.WinRate >= 0.0 && result.WinRate <= 1.0,
                $"Formula {result.Formula} should have win rate between 0% and 100%");
            Assert.True(result.AverageDuration > 0,
                $"Formula {result.Formula} should have positive average duration");
        }

        // Log summary statistics
        var avgWinRate = results.Values.Average(r => r.WinRate);
        var winRateStdDev = Math.Sqrt(results.Values.Average(r => Math.Pow(r.WinRate - avgWinRate, 2)));

        _output.WriteLine($"Summary Statistics:");
        _output.WriteLine($"  Average Win Rate: {avgWinRate:P1}");
        _output.WriteLine($"  Win Rate Std Dev: {winRateStdDev:P1}");
        _output.WriteLine($"  Formula Count: {results.Count}");
    }

    [Theory]
    [InlineData(DamageCalculationFormula.Standard)]
    [InlineData(DamageCalculationFormula.PercentageBased)]
    [InlineData(DamageCalculationFormula.SquareRoot)]
    [InlineData(DamageCalculationFormula.Logarithmic)]
    [InlineData(DamageCalculationFormula.LinearScaling)]
    [InlineData(DamageCalculationFormula.DragonQuest)]
    public async Task BattleFormula_IndividualFormulaTest_ShouldCompleteSuccessfully(DamageCalculationFormula formula)
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId();
        const int seed = 42;
        var mockGroup = CreateMockGroup();
        var logger = _loggerFactory.CreateLogger<BattleState>();

        // Act
        var battle = TestHelpers.CreateBattleState(battleId, seed, mockGroup, logger, _loggerFactory, formula);
        await battle.RunBattleAsync();
        var status = battle.GetStatus();

        // Assert
        Assert.True(status.CurrentTurn > 0, $"Formula {formula} should complete at least one turn");
        Assert.NotNull(status.IsPlayerVictory);
        Assert.True(status.CurrentTurn <= BattleSystemDefines.BattleTurns.Max,
            $"Formula {formula} should not exceed maximum turns");

        _output.WriteLine($"Formula {formula}: {status.CurrentTurn} turns, " +
                         $"Result: {(status.IsPlayerVictory == true ? "Victory" : "Defeat")}");
    }

    [Fact]
    public async Task BattleFormula_SameFormulaConsistency_ShouldProduceSameResults()
    {
        // Arrange
        const DamageCalculationFormula formula = DamageCalculationFormula.Standard;
        var battleId = BattleSeed.NewTimestampId();
        const int seed = 99999;
        var mockGroup = CreateMockGroup();
        var logger = _loggerFactory.CreateLogger<BattleState>();

        // Act - Run same battle twice with same parameters
        var battle1 = TestHelpers.CreateBattleState(battleId, seed, mockGroup, logger, _loggerFactory, formula);
        var battle2 = TestHelpers.CreateBattleState(battleId, seed, mockGroup, logger, _loggerFactory, formula);

        await battle1.RunBattleAsync();
        await battle2.RunBattleAsync();

        var status1 = battle1.GetStatus();
        var status2 = battle2.GetStatus();

        // Assert
        Assert.Equal(status1.CurrentTurn, status2.CurrentTurn);
        Assert.Equal(status1.IsPlayerVictory, status2.IsPlayerVictory);
        Assert.Equal(status1.Players.Count, status2.Players.Count);
        Assert.Equal(status1.Enemies.Count, status2.Enemies.Count);

        _output.WriteLine($"Consistency test passed: {status1.CurrentTurn} turns, " +
                         $"Result: {(status1.IsPlayerVictory == true ? "Victory" : "Defeat")}");
    }

    private static IBattleGroupContext CreateMockGroup()
    {
        var mockGroup = Substitute.For<IBattleGroupContext>();
        mockGroup.GroupId.Returns(BattleSeed.NewTimestampId().ToString());
        mockGroup.Name.Returns("test_group");
        mockGroup.ConnectedCount.Returns(5);
        mockGroup.MaxClients.Returns(SystemDefines.MaxConnectionsPerGroup);
        mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });
        return mockGroup;
    }

    private record WinRateResult
    {
        public required DamageCalculationFormula Formula { get; init; }
        public required double WinRate { get; init; }
        public required int TotalBattles { get; init; }
        public required int Wins { get; init; }
        public required double AverageDuration { get; init; }
        public required int MinDuration { get; init; }
        public required int MaxDuration { get; init; }
    }
}
