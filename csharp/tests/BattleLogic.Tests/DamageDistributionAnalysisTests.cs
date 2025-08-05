using BattleLogic.Services;
using BattleLogic.Constants;
using Shared.Battle;

namespace BattleLogic.Tests;

/// <summary>
/// Tests for analyzing damage distribution by formula and job combinations
/// </summary>
public class DamageDistributionAnalysisTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void DamageDistribution_ByFormulaAndJob_ShouldProvideDetailedInsights()
    {
        // Arrange
        var formulas = Enum.GetValues<DamageCalculationFormula>();
        var playerJobs = Enum.GetValues<PlayerJob>();
        var enemyJobs = Enum.GetValues<EnemyJob>();
        const int iterations = 1000;
        const int fixedSeed = 42;

        _output.WriteLine("=== Damage Distribution Analysis by Formula and Job ===");
        _output.WriteLine($"Test iterations: {iterations}");
        _output.WriteLine("");

        // Act & Assert - Test each formula
        foreach (var formula in formulas)
        {
            _output.WriteLine($"■ Formula: {formula}");
            _output.WriteLine("");

            // Test Player Job combinations
            _output.WriteLine("  ▼ Player Jobs vs Enemy (Standard Medium Enemy Stats):");
            foreach (var playerJob in playerJobs)
            {
                var playerStats = GeneratePlayerStatsForJob(playerJob, fixedSeed);
                var enemyStats = GenerateStandardEnemyStats(fixedSeed + 1000); // Different seed for enemy

                var result = TestDamageDistribution(formula, playerStats, enemyStats, iterations, fixedSeed + 2000);

                _output.WriteLine($"    {playerJob,-8}: Avg={result.AverageDamage:F1}, Min={result.MinDamage}, Max={result.MaxDamage}, Crit={result.CriticalHitRate:P1}, StdDev={result.StandardDeviation:F1}");
            }

            _output.WriteLine("");

            // Test Enemy Job combinations
            _output.WriteLine("  ▼ Enemy Jobs vs Player (Standard Player Stats):");
            foreach (var enemyJob in enemyJobs)
            {
                var playerStats = GenerateStandardPlayerStats(fixedSeed + 3000);
                var enemyStats = GenerateEnemyStatsForJob(enemyJob, fixedSeed + 4000);

                var result = TestDamageDistribution(formula, enemyStats, playerStats, iterations, fixedSeed + 5000);

                _output.WriteLine($"    {enemyJob,-8}: Avg={result.AverageDamage:F1}, Min={result.MinDamage}, Max={result.MaxDamage}, Crit={result.CriticalHitRate:P1}, StdDev={result.StandardDeviation:F1}");
            }

            _output.WriteLine("");

            // Cross-job damage analysis (Player vs Enemy jobs)
            _output.WriteLine("  ▼ Cross-Job Damage Matrix (Player → Enemy):");
            _output.WriteLine("    Player\\Enemy Guardian Bruiser  Caster  Assassin");
            foreach (var playerJob in playerJobs)
            {
                var playerStats = GeneratePlayerStatsForJob(playerJob, fixedSeed + 6000);
                var line = $"    {playerJob,-12}";

                foreach (var enemyJob in enemyJobs)
                {
                    var enemyStats = GenerateEnemyStatsForJob(enemyJob, fixedSeed + 7000);
                    var result = TestDamageDistribution(formula, playerStats, enemyStats, iterations / 10, fixedSeed + 8000); // Reduced iterations for matrix
                    line += $" {result.AverageDamage,7:F1}";
                }
                _output.WriteLine(line);
            }

            _output.WriteLine("");

            // Cross-job damage analysis (Enemy vs Player jobs)
            _output.WriteLine("  ▼ Cross-Job Damage Matrix (Enemy → Player):");
            _output.WriteLine("    Enemy\\Player    Tank  Warrior   Mage  Archer");
            foreach (var enemyJob in enemyJobs)
            {
                var enemyStats = GenerateEnemyStatsForJob(enemyJob, fixedSeed + 9000);
                var line = $"    {enemyJob,-12}";

                foreach (var playerJob in playerJobs)
                {
                    var playerStats = GeneratePlayerStatsForJob(playerJob, fixedSeed + 10000);
                    var result = TestDamageDistribution(formula, enemyStats, playerStats, iterations / 10, fixedSeed + 11000); // Enemy attacking player
                    line += $" {result.AverageDamage,7:F1}";
                }
                _output.WriteLine(line);
            }

            _output.WriteLine("");
            _output.WriteLine("".PadRight(80, '='));
            _output.WriteLine("");
        }
    }

    [Fact]
    public void DamageDistribution_ComparisonSummary_ShouldShowKeyInsights()
    {
        // Arrange
        var formulas = Enum.GetValues<DamageCalculationFormula>();
        var testCases = new[]
        {
            new { PlayerJob = PlayerJob.Tank, EnemyJob = EnemyJob.Guardian, Name = "Tank vs Guardian (Defense vs Defense)" },
            new { PlayerJob = PlayerJob.Archer, EnemyJob = EnemyJob.Assassin, Name = "Archer vs Assassin (Speed vs Speed)" },
            new { PlayerJob = PlayerJob.Warrior, EnemyJob = EnemyJob.Bruiser, Name = "Warrior vs Bruiser (Balanced)" },
            new { PlayerJob = PlayerJob.Mage, EnemyJob = EnemyJob.Caster, Name = "Mage vs Caster (Magic vs Magic)" }
        };
        const int iterations = 2000;
        const int fixedSeed = 42;

        _output.WriteLine("=== Damage Distribution Comparison Summary ===");
        _output.WriteLine($"Test iterations: {iterations}");
        _output.WriteLine("");

        foreach (var testCase in testCases)
        {
            _output.WriteLine($"■ Scenario: {testCase.Name}");
            _output.WriteLine($"   Player: {testCase.PlayerJob} vs Enemy: {testCase.EnemyJob}");
            _output.WriteLine("");

            var playerStats = GeneratePlayerStatsForJob(testCase.PlayerJob, fixedSeed + 100);
            var enemyStats = GenerateEnemyStatsForJob(testCase.EnemyJob, fixedSeed + 200);

            _output.WriteLine($"   Player Stats: ATK={playerStats.attack}, DEF={playerStats.defense}, ACC={playerStats.accuracy}%, EVA={playerStats.evasion}%, CRIT={playerStats.criticalRate}%");
            _output.WriteLine($"   Enemy Stats:  ATK={enemyStats.attack}, DEF={enemyStats.defense}, ACC={enemyStats.accuracy}%, EVA={enemyStats.evasion}%, CRIT={enemyStats.criticalRate}%");
            _output.WriteLine("");

            _output.WriteLine("   Formula      | Avg Dmg | Min | Max | StdDev | Crit% | Consistency");
            _output.WriteLine("   -------------|---------|-----|-----|--------|-------|------------");

            var results = new List<(DamageCalculationFormula formula, DamageDistributionResult result)>();

            foreach (var formula in formulas)
            {
                var result = TestDamageDistribution(formula, playerStats, enemyStats, iterations, fixedSeed + 300);
                results.Add((formula, result));

                // Calculate consistency score (lower standard deviation relative to average = more consistent)
                var consistencyScore = result.StandardDeviation / result.AverageDamage;
                var consistencyLevel = consistencyScore switch
                {
                    < 0.2 => "Very High",
                    < 0.4 => "High     ",
                    < 0.6 => "Medium   ",
                    < 0.8 => "Low      ",
                    _ => "Very Low "
                };

                _output.WriteLine($"   {formula,-12} | {result.AverageDamage,7:F1} | {result.MinDamage,3} | {result.MaxDamage,3} | {result.StandardDeviation,6:F1} | {result.CriticalHitRate,4:P0} | {consistencyLevel}");
            }

            // Analysis insights
            _output.WriteLine("");
            var highestDamage = results.OrderByDescending(r => r.result.AverageDamage).First();
            var mostConsistent = results.OrderBy(r => r.result.StandardDeviation / r.result.AverageDamage).First();
            var widestRange = results.OrderByDescending(r => r.result.MaxDamage - r.result.MinDamage).First();

            _output.WriteLine($"   📊 Insights:");
            _output.WriteLine($"      • Highest Damage:   {highestDamage.formula} ({highestDamage.result.AverageDamage:F1} avg)");
            _output.WriteLine($"      • Most Consistent:  {mostConsistent.formula} (σ/μ = {mostConsistent.result.StandardDeviation / mostConsistent.result.AverageDamage:F3})");
            _output.WriteLine($"      • Widest Range:     {widestRange.formula} ({widestRange.result.MinDamage}-{widestRange.result.MaxDamage})");
            _output.WriteLine("");
            _output.WriteLine("".PadRight(80, '='));
            _output.WriteLine("");
        }

        // Overall formula ranking
        _output.WriteLine("■ Overall Formula Performance Summary");
        _output.WriteLine("");

        var allResults = new Dictionary<DamageCalculationFormula, List<double>>();
        foreach (var formula in formulas)
        {
            allResults[formula] = new List<double>();
        }

        foreach (var testCase in testCases)
        {
            var playerStats = GeneratePlayerStatsForJob(testCase.PlayerJob, fixedSeed + 400);
            var enemyStats = GenerateEnemyStatsForJob(testCase.EnemyJob, fixedSeed + 500);

            foreach (var formula in formulas)
            {
                var result = TestDamageDistribution(formula, playerStats, enemyStats, iterations / 4, fixedSeed + 600);
                allResults[formula].Add(result.AverageDamage);
            }
        }

        _output.WriteLine("   Formula      | Avg Across All | Consistency | Recommendation");
        _output.WriteLine("   -------------|----------------|-------------|----------------");

        foreach (var formula in formulas.OrderByDescending(f => allResults[f].Average()))
        {
            var avgDamage = allResults[formula].Average();
            var variance = allResults[formula].Select(d => Math.Pow(d - avgDamage, 2)).Average();
            var consistency = Math.Sqrt(variance) / avgDamage;

            var recommendation = (avgDamage, consistency) switch
            {
                (> 50, < 0.3) => "🟢 Balanced & Stable",
                (> 80, _) => "🔴 Too High Damage",
                (< 20, _) => "🔴 Too Low Damage",
                (_, > 0.5) => "🟡 Inconsistent",
                _ => "🟡 Needs Tuning"
            };

            _output.WriteLine($"   {formula,-12} | {avgDamage,14:F1} | {consistency,11:F3} | {recommendation}");
        }
    }

    /// <summary>
    /// Generate player stats for specific job with job modifiers applied
    /// </summary>
    private (int attack, int defense, int accuracy, int evasion, int criticalRate) GeneratePlayerStatsForJob(PlayerJob job, int seed)
    {
        var random = new Random(seed);
        var jobModifier = BattleSystemDefines.PlayerJobModifiers[job];

        // Generate base stats similar to BattleInitializer
        var baseAttack = random.Next(BattleSystemDefines.PlayerAttackPower.Min, BattleSystemDefines.PlayerAttackPower.Max);
        var baseDefense = random.Next(BattleSystemDefines.PlayerDefencePower.Min, BattleSystemDefines.PlayerDefencePower.Max);
        var baseAccuracy = random.Next(BattleSystemDefines.PlayerAccuracy.Min, BattleSystemDefines.PlayerAccuracy.Max);
        var baseEvasion = random.Next(BattleSystemDefines.PlayerEvasion.Min, BattleSystemDefines.PlayerEvasion.Max);
        var baseCriticalRate = random.Next(BattleSystemDefines.PlayerCriticalRate.Min, BattleSystemDefines.PlayerCriticalRate.Max);

        // Apply job modifiers
        var modifiedAttack = Math.Max(1, (int)(baseAttack * jobModifier.AttackMultiplier) + jobModifier.AttackBonus);
        var modifiedDefense = Math.Max(0, (int)(baseDefense * jobModifier.DefenseMultiplier) + jobModifier.DefenseBonus);
        var modifiedAccuracy = Math.Max(0, (int)(baseAccuracy * jobModifier.AccuracyMultiplier) + jobModifier.AccuracyBonus);
        var modifiedEvasion = Math.Max(0, (int)(baseEvasion * jobModifier.EvasionMultiplier) + jobModifier.EvasionBonus);
        var modifiedCriticalRate = Math.Max(0, (int)(baseCriticalRate * jobModifier.CriticalRateMultiplier) + jobModifier.CriticalRateBonus);

        return (modifiedAttack, modifiedDefense, modifiedAccuracy, modifiedEvasion, modifiedCriticalRate);
    }

    /// <summary>
    /// Generate enemy stats for specific job with job modifiers applied
    /// </summary>
    private (int attack, int defense, int accuracy, int evasion, int criticalRate) GenerateEnemyStatsForJob(EnemyJob job, int seed)
    {
        var random = new Random(seed);
        var jobModifier = BattleSystemDefines.EnemyJobModifiers[job];
        var enemySize = EnemySize.Medium; // Use medium size as standard

        // Generate base stats similar to BattleInitializer
        var baseAttack = random.Next(BattleSystemDefines.EnemyAttackPower[enemySize].Min, BattleSystemDefines.EnemyAttackPower[enemySize].Max);
        var baseDefense = random.Next(BattleSystemDefines.EnemyDefencePower[enemySize].Min, BattleSystemDefines.EnemyDefencePower[enemySize].Max);
        var baseAccuracy = random.Next(BattleSystemDefines.EnemyAccuracy[enemySize].Min, BattleSystemDefines.EnemyAccuracy[enemySize].Max);
        var baseEvasion = random.Next(BattleSystemDefines.EnemyEvasion[enemySize].Min, BattleSystemDefines.EnemyEvasion[enemySize].Max);
        var baseCriticalRate = random.Next(BattleSystemDefines.EnemyCriticalRate[enemySize].Min, BattleSystemDefines.EnemyCriticalRate[enemySize].Max);

        // Apply job modifiers
        var modifiedAttack = Math.Max(1, (int)(baseAttack * jobModifier.AttackMultiplier) + jobModifier.AttackBonus);
        var modifiedDefense = Math.Max(0, (int)(baseDefense * jobModifier.DefenseMultiplier) + jobModifier.DefenseBonus);
        var modifiedAccuracy = Math.Max(0, (int)(baseAccuracy * jobModifier.AccuracyMultiplier) + jobModifier.AccuracyBonus);
        var modifiedEvasion = Math.Max(0, (int)(baseEvasion * jobModifier.EvasionMultiplier) + jobModifier.EvasionBonus);
        var modifiedCriticalRate = Math.Max(0, (int)(baseCriticalRate * jobModifier.CriticalRateMultiplier) + jobModifier.CriticalRateBonus);

        return (modifiedAttack, modifiedDefense, modifiedAccuracy, modifiedEvasion, modifiedCriticalRate);
    }

    /// <summary>
    /// Generate standard player stats (average across all jobs)
    /// </summary>
    private (int attack, int defense, int accuracy, int evasion, int criticalRate) GenerateStandardPlayerStats(int seed)
    {
        var random = new Random(seed);
        return (
            random.Next(BattleSystemDefines.PlayerAttackPower.Min, BattleSystemDefines.PlayerAttackPower.Max),
            random.Next(BattleSystemDefines.PlayerDefencePower.Min, BattleSystemDefines.PlayerDefencePower.Max),
            random.Next(BattleSystemDefines.PlayerAccuracy.Min, BattleSystemDefines.PlayerAccuracy.Max),
            random.Next(BattleSystemDefines.PlayerEvasion.Min, BattleSystemDefines.PlayerEvasion.Max),
            random.Next(BattleSystemDefines.PlayerCriticalRate.Min, BattleSystemDefines.PlayerCriticalRate.Max)
        );
    }

    /// <summary>
    /// Generate standard enemy stats (medium size, average across all jobs)
    /// </summary>
    private (int attack, int defense, int accuracy, int evasion, int criticalRate) GenerateStandardEnemyStats(int seed)
    {
        var random = new Random(seed);
        var enemySize = EnemySize.Medium;
        return (
            random.Next(BattleSystemDefines.EnemyAttackPower[enemySize].Min, BattleSystemDefines.EnemyAttackPower[enemySize].Max),
            random.Next(BattleSystemDefines.EnemyDefencePower[enemySize].Min, BattleSystemDefines.EnemyDefencePower[enemySize].Max),
            random.Next(BattleSystemDefines.EnemyAccuracy[enemySize].Min, BattleSystemDefines.EnemyAccuracy[enemySize].Max),
            random.Next(BattleSystemDefines.EnemyEvasion[enemySize].Min, BattleSystemDefines.EnemyEvasion[enemySize].Max),
            random.Next(BattleSystemDefines.EnemyCriticalRate[enemySize].Min, BattleSystemDefines.EnemyCriticalRate[enemySize].Max)
        );
    }

    /// <summary>
    /// Test damage distribution with detailed statistics
    /// </summary>
    private DamageDistributionResult TestDamageDistribution(
        DamageCalculationFormula formula,
        (int attack, int defense, int accuracy, int evasion, int criticalRate) attackerStats,
        (int attack, int defense, int accuracy, int evasion, int criticalRate) defenderStats,
        int iterations,
        int seed)
    {
        var random = new Random(seed);
        var damages = new List<int>();
        var criticalHits = 0;

        for (int i = 0; i < iterations; i++)
        {
            var (damage, isCritical) = DamageCalculationService.CalculateDamage(
                formula, attackerStats.attack, defenderStats.defense, attackerStats.criticalRate, false, random);

            damages.Add(damage);
            if (isCritical) criticalHits++;
        }

        var average = damages.Average();
        var variance = damages.Select(d => Math.Pow(d - average, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        return new DamageDistributionResult
        {
            Formula = formula,
            AverageDamage = average,
            MinDamage = damages.Min(),
            MaxDamage = damages.Max(),
            StandardDeviation = standardDeviation,
            CriticalHitRate = (double)criticalHits / iterations
        };
    }

    private record DamageDistributionResult
    {
        public required DamageCalculationFormula Formula { get; init; }
        public required double AverageDamage { get; init; }
        public required int MinDamage { get; init; }
        public required int MaxDamage { get; init; }
        public required double StandardDeviation { get; init; }
        public required double CriticalHitRate { get; init; }
    }
}
