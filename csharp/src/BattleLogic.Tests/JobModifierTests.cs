using Microsoft.Extensions.Logging;
using Shared.Battle;

namespace BattleLogic.Tests;

/// <summary>
/// Job modifier specific tests for battle entities
/// </summary>
public class JobModifierTests
{
    private readonly ILogger<BattleState> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public JobModifierTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<BattleState>();
    }

    /// <summary>
    /// Helper method to validate stat ranges (without flavor variations since flavors are applied per-turn, not at creation)
    /// </summary>
    private static void ValidateStat(int actualValue, int expectedMin, int expectedMax, string statName, string jobName)
    {
        Assert.True(actualValue >= expectedMin && actualValue <= expectedMax,
            $"{jobName} {statName} {actualValue} should be in range [{expectedMin}-{expectedMax}]");
    }

    /// <summary>
    /// Test Tank job modifier application
    /// </summary>
    [Fact]
    public void PlayerJob_Tank_ShouldApplyCorrectModifiers()
    {
        // Arrange
        var group = new GroupInfo
        {
            Id = BattleSeed.NewTimestampId().ToString(), // Use GUID v7 for group ID
            Name = "tank_test_group",
            ConnectionCount = 5, // Full group to ensure Tank might be selected
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act - Run multiple times to get different job assignments
        var tankPlayerFound = false;
        for (int attempt = 0; attempt < 200 && !tankPlayerFound; attempt++)
        {
            var battleState = TestHelpers.CreateBattleState(group, _logger, _loggerFactory);
            var status = battleState.GetStatus();

            var tankPlayer = status.Players.FirstOrDefault(p => p.PlayerJob == PlayerJob.Tank);
            if (!tankPlayer.Equals(default))
            {
                tankPlayerFound = true;
                var player = tankPlayer;
                var tankModifier = BattleSystemDefines.PlayerJobModifiers[PlayerJob.Tank];

                // Calculate expected ranges for Tank
                var baseHpMin = BattleSystemDefines.PlayerHp.Min;
                var baseHpMax = BattleSystemDefines.PlayerHp.Max;
                var expectedHpMin = Math.Max(1, (int)(baseHpMin * tankModifier.HpMultiplier) + tankModifier.HpBonus);
                var expectedHpMax = (int)(baseHpMax * tankModifier.HpMultiplier) + tankModifier.HpBonus;

                var baseAttackMin = BattleSystemDefines.PlayerAttackPower.Min;
                var baseAttackMax = BattleSystemDefines.PlayerAttackPower.Max;
                var expectedAttackMin = Math.Max(1, (int)(baseAttackMin * tankModifier.AttackMultiplier) + tankModifier.AttackBonus);
                var expectedAttackMax = (int)(baseAttackMax * tankModifier.AttackMultiplier) + tankModifier.AttackBonus;

                var baseDefenseMin = BattleSystemDefines.PlayerDefencePower.Min;
                var baseDefenseMax = BattleSystemDefines.PlayerDefencePower.Max;
                var expectedDefenseMin = Math.Max(0, (int)(baseDefenseMin * tankModifier.DefenseMultiplier) + tankModifier.DefenseBonus);
                var expectedDefenseMax = (int)(baseDefenseMax * tankModifier.DefenseMultiplier) + tankModifier.DefenseBonus;

                var baseSpeedMin = BattleSystemDefines.PlayerMoveSpeed.Min;
                var baseSpeedMax = BattleSystemDefines.PlayerMoveSpeed.Max;
                var expectedSpeedMin = Math.Max(1, (int)(baseSpeedMin * tankModifier.SpeedMultiplier) + tankModifier.SpeedBonus);
                var expectedSpeedMax = (int)(baseSpeedMax * tankModifier.SpeedMultiplier) + tankModifier.SpeedBonus;

                var baseAccuracyMin = BattleSystemDefines.PlayerAccuracy.Min;
                var baseAccuracyMax = BattleSystemDefines.PlayerAccuracy.Max;
                var expectedAccuracyMin = Math.Max(0, (int)(baseAccuracyMin * tankModifier.AccuracyMultiplier) + tankModifier.AccuracyBonus);
                var expectedAccuracyMax = (int)(baseAccuracyMax * tankModifier.AccuracyMultiplier) + tankModifier.AccuracyBonus;

                // Assert Tank-specific modifiers
                Assert.Equal(PlayerJob.Tank, player.PlayerJob);
                Assert.True(player.MaxHp >= expectedHpMin && player.MaxHp <= expectedHpMax,
                    $"Tank HP {player.MaxHp} should be in range [{expectedHpMin}-{expectedHpMax}]");

                ValidateStat(player.Attack, expectedAttackMin, expectedAttackMax, "Attack", "Tank");
                ValidateStat(player.Defense, expectedDefenseMin, expectedDefenseMax, "Defense", "Tank");

                Assert.True(player.Speed >= expectedSpeedMin && player.Speed <= expectedSpeedMax,
                    $"Tank Speed {player.Speed} should be in range [{expectedSpeedMin}-{expectedSpeedMax}]");

                Assert.True(player.Accuracy >= expectedAccuracyMin && player.Accuracy <= expectedAccuracyMax,
                    $"Tank Accuracy {player.Accuracy} should be in range [{expectedAccuracyMin}-{expectedAccuracyMax}]");
            }
        }

        Assert.True(tankPlayerFound, "Tank player should be found within 200 attempts");
    }

    /// <summary>
    /// Test Warrior job modifier application
    /// </summary>
    [Fact]
    public void PlayerJob_Warrior_ShouldApplyCorrectModifiers()
    {
        // Similar pattern to Tank test but for Warrior
        var group = new GroupInfo
        {
            Id = BattleSeed.NewTimestampId().ToString(),
            Name = "warrior_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var warriorPlayerFound = false;
        for (int attempt = 0; attempt < 200 && !warriorPlayerFound; attempt++)
        {
            var battleState = TestHelpers.CreateBattleState(group, _logger, _loggerFactory);
            var status = battleState.GetStatus();

            var warriorPlayer = status.Players.FirstOrDefault(p => p.PlayerJob == PlayerJob.Warrior);
            if (!warriorPlayer.Equals(default))
            {
                warriorPlayerFound = true;
                var player = warriorPlayer;
                var warriorModifier = BattleSystemDefines.PlayerJobModifiers[PlayerJob.Warrior];

                // Calculate expected ranges for Warrior
                var expectedHpMin = Math.Max(1, (int)(BattleSystemDefines.PlayerHp.Min * warriorModifier.HpMultiplier) + warriorModifier.HpBonus);
                var expectedHpMax = (int)(BattleSystemDefines.PlayerHp.Max * warriorModifier.HpMultiplier) + warriorModifier.HpBonus;

                var expectedAttackMin = Math.Max(1, (int)(BattleSystemDefines.PlayerAttackPower.Min * warriorModifier.AttackMultiplier) + warriorModifier.AttackBonus);
                var expectedAttackMax = (int)(BattleSystemDefines.PlayerAttackPower.Max * warriorModifier.AttackMultiplier) + warriorModifier.AttackBonus;

                var expectedAccuracyMin = Math.Max(0, (int)(BattleSystemDefines.PlayerAccuracy.Min * warriorModifier.AccuracyMultiplier) + warriorModifier.AccuracyBonus);
                var expectedAccuracyMax = (int)(BattleSystemDefines.PlayerAccuracy.Max * warriorModifier.AccuracyMultiplier) + warriorModifier.AccuracyBonus;

                Assert.Equal(PlayerJob.Warrior, player.PlayerJob);
                Assert.True(player.MaxHp >= expectedHpMin && player.MaxHp <= expectedHpMax,
                    $"Warrior HP {player.MaxHp} should be in range [{expectedHpMin}-{expectedHpMax}]");

                ValidateStat(player.Attack, expectedAttackMin, expectedAttackMax, "Attack", "Warrior");

                Assert.True(player.Accuracy >= expectedAccuracyMin && player.Accuracy <= expectedAccuracyMax,
                    $"Warrior Accuracy {player.Accuracy} should be in range [{expectedAccuracyMin}-{expectedAccuracyMax}]");
            }
        }

        Assert.True(warriorPlayerFound, "Warrior player should be found within 200 attempts");
    }

    /// <summary>
    /// Test Mage job modifier application
    /// </summary>
    [Fact]
    public void PlayerJob_Mage_ShouldApplyCorrectModifiers()
    {
        var group = new GroupInfo
        {
            Id = BattleSeed.NewTimestampId().ToString(),
            Name = "mage_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var magePlayerFound = false;
        for (int attempt = 0; attempt < 200 && !magePlayerFound; attempt++)
        {
            var battleState = TestHelpers.CreateBattleState(group, _logger, _loggerFactory);
            var status = battleState.GetStatus();

            var magePlayer = status.Players.FirstOrDefault(p => p.PlayerJob == PlayerJob.Mage);
            if (!magePlayer.Equals(default))
            {
                magePlayerFound = true;
                var player = magePlayer;
                var mageModifier = BattleSystemDefines.PlayerJobModifiers[PlayerJob.Mage];

                Assert.Equal(PlayerJob.Mage, player.PlayerJob);
                // Mage should have lower HP due to 0.8x multiplier and -50 bonus
                // Base HP 200-500, so (200*0.8)-50=110 to (500*0.8)-50=350 ??N 110-350 range
                Assert.True(player.MaxHp <= 350, $"Mage should have lower HP, got {player.MaxHp}");
                // But very high attack due to 1.4x multiplier + 8 bonus
                // Base attack 10-30, so (10*1.4)+8=22 to (30*1.4)+8=50 ??N 22-50 range
                Assert.True(player.Attack >= 22, $"Mage should have very high attack power, got {player.Attack}");
                // And lower defense due to 0.7x multiplier and -3 bonus
                // Base defense 10-22, so (10*0.7)-3=4 to (22*0.7)-3=12.4 ??N 4-12 range
                Assert.True(player.Defense <= 12, $"Mage should have lower defense, got {player.Defense}");
            }
        }

        Assert.True(magePlayerFound, "Mage player should be found within 200 attempts");
    }

    /// <summary>
    /// Test Archer job modifier application
    /// </summary>
    [Fact]
    public void PlayerJob_Archer_ShouldApplyCorrectModifiers()
    {
        var group = new GroupInfo
        {
            Id = BattleSeed.NewTimestampId().ToString(),
            Name = "archer_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var archerPlayerFound = false;
        for (int attempt = 0; attempt < 200 && !archerPlayerFound; attempt++)
        {
            var battleState = TestHelpers.CreateBattleState(group, _logger, _loggerFactory);
            var status = battleState.GetStatus();

            var archerPlayer = status.Players.FirstOrDefault(p => p.PlayerJob == PlayerJob.Archer);
            if (!archerPlayer.Equals(default))
            {
                archerPlayerFound = true;
                var player = archerPlayer;
                var archerModifier = BattleSystemDefines.PlayerJobModifiers[PlayerJob.Archer];

                Assert.Equal(PlayerJob.Archer, player.PlayerJob);
                // Archer should have high speed due to 1.4x multiplier + 1 bonus
                // Base speed 2-4, so (2*1.4)+1=3.8 to (4*1.4)+1=6.6 ??N 3-6 range
                Assert.True(player.Speed >= 3, $"Archer should have high speed, got {player.Speed}");
                // Good attack due to 1.3x multiplier + 3 bonus
                // Base attack 25-34, so (25*1.3)+3=35.5 to (34*1.3)+3=47.2 ??N 35-47 range
                Assert.True(player.Attack >= 35, $"Archer should have good attack power, got {player.Attack}");
            }
        }

        Assert.True(archerPlayerFound, "Archer player should be found within 200 attempts");
    }

    /// <summary>
    /// Test enemy job modifiers
    /// </summary>
    [Fact]
    public void EnemyJobs_ShouldApplyCorrectModifiers()
    {
        var group = new GroupInfo
        {
            Id = BattleSeed.NewTimestampId().ToString(),
            Name = "enemy_job_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var battleState = TestHelpers.CreateBattleState(group, _logger, _loggerFactory);
        var status = battleState.GetStatus();

        // Check that enemies have valid jobs assigned
        var enemyJobs = status.Enemies.Where(e => e.EnemyJob.HasValue).Select(e => e.EnemyJob!.Value).Distinct().ToList();
        Assert.True(enemyJobs.Count > 0, "Enemies should have jobs assigned");

        foreach (var enemy in status.Enemies)
        {
            Assert.True(enemy.EnemyJob.HasValue, "Enemy should have a job assigned");
            Assert.True(Enum.IsDefined(typeof(EnemyJob), enemy.EnemyJob.Value), $"Enemy job {enemy.EnemyJob} should be valid");

            // Verify job-specific characteristics
            switch (enemy.EnemyJob.Value)
            {
                case EnemyJob.Guardian:
                    // Guardian should have high HP and defense but low speed
                    var guardianModifier = BattleSystemDefines.EnemyJobModifiers[EnemyJob.Guardian];
                    Assert.True(enemy.MaxHp > enemy.Attack, "Guardian should prioritize HP over attack");
                    break;

                case EnemyJob.Assassin:
                    // Assassin should have high speed and attack but lower HP
                    Assert.True(enemy.Speed >= 2, "Assassin should have decent speed");
                    break;

                case EnemyJob.Caster:
                    // Caster should have high attack but lower defense
                    Assert.True(enemy.Attack > 15, "Caster should have good attack");
                    break;

                case EnemyJob.Bruiser:
                    // Bruiser should be well-balanced
                    Assert.True(enemy.MaxHp > 50, "Bruiser should have decent HP");
                    break;
            }
        }
    }

    /// <summary>
    /// Test Evasion rate ranges for all player jobs
    /// </summary>
    [Fact]
    public void PlayerJobs_EvasionRates_ShouldBeWithinExpectedRanges()
    {
        // Arrange
        var group = new GroupInfo
        {
            Id = BattleSeed.NewTimestampId().ToString(),
            Name = "evasion_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var jobsFound = new HashSet<PlayerJob>();
        var jobEvasionData = new Dictionary<PlayerJob, List<int>>();

        // Act - Run multiple times to collect evasion data for different jobs
        for (int attempt = 0; attempt < 500 && jobsFound.Count < 4; attempt++)
        {
            var battleState = TestHelpers.CreateBattleState(group, _logger, _loggerFactory);
            var status = battleState.GetStatus();

            foreach (var player in status.Players)
            {
                if (player.PlayerJob.HasValue && !jobsFound.Contains(player.PlayerJob.Value))
                {
                    jobsFound.Add(player.PlayerJob.Value);
                    if (!jobEvasionData.ContainsKey(player.PlayerJob.Value))
                    {
                        jobEvasionData[player.PlayerJob.Value] = new List<int>();
                    }
                }

                if (player.PlayerJob.HasValue)
                {
                    if (jobEvasionData.ContainsKey(player.PlayerJob.Value))
                    {
                        jobEvasionData[player.PlayerJob.Value].Add(player.Evasion);
                    }
                }
            }
        }

        // Assert - Check evasion ranges for each job
        foreach (var job in jobsFound)
        {
            var modifier = BattleSystemDefines.PlayerJobModifiers[job];
            var baseEvasionMin = BattleSystemDefines.PlayerEvasion.Min;
            var baseEvasionMax = BattleSystemDefines.PlayerEvasion.Max;
            var expectedEvasionMin = Math.Max(0, (int)(baseEvasionMin * modifier.EvasionMultiplier) + modifier.EvasionBonus);
            var expectedEvasionMax = (int)(baseEvasionMax * modifier.EvasionMultiplier) + modifier.EvasionBonus;

            // Apply flavor variation for evasion
            var expectedEvasionMinWithFlavor = Math.Max(0, expectedEvasionMin - BattleSystemDefines.EvasionFlavorRange);
            var expectedEvasionMaxWithFlavor = Math.Min(100, expectedEvasionMax + BattleSystemDefines.EvasionFlavorRange);

            if (jobEvasionData.ContainsKey(job) && jobEvasionData[job].Count > 0)
            {
                var actualMin = jobEvasionData[job].Min();
                var actualMax = jobEvasionData[job].Max();

                Assert.True(actualMin >= expectedEvasionMinWithFlavor,
                    $"{job} Evasion minimum {actualMin} should be >= {expectedEvasionMinWithFlavor} (includes flavor variation)");
                Assert.True(actualMax <= expectedEvasionMaxWithFlavor,
                    $"{job} Evasion maximum {actualMax} should be <= {expectedEvasionMaxWithFlavor} (includes flavor variation)");

                // Job-specific evasion expectations (adjusted for flavor)
                switch (job)
                {
                    case PlayerJob.Archer:
                        // Archer should have the highest evasion among all jobs (adjusted for flavor)
                        Assert.True(actualMin >= Math.Max(0, 20 - BattleSystemDefines.EvasionFlavorRange),
                            $"Archer should have high evasion, but minimum was {actualMin} (considering flavor variation)");
                        break;
                    case PlayerJob.Tank:
                        // Tank should have the lowest evasion among all jobs (adjusted for flavor)
                        Assert.True(actualMax <= 15 + BattleSystemDefines.EvasionFlavorRange,
                            $"Tank should have low evasion, but maximum was {actualMax} (considering flavor variation)");
                        break;
                    case PlayerJob.Mage:
                        // Mage should have lower evasion than Warrior (adjusted for flavor)
                        Assert.True(actualMax <= 25 + BattleSystemDefines.EvasionFlavorRange,
                            $"Mage should have lower evasion, but maximum was {actualMax} (considering flavor variation)");
                        break;
                    case PlayerJob.Warrior:
                        // Warrior should have standard evasion (adjusted for flavor)
                        var warriorMinWithFlavor = Math.Max(0, 10 - BattleSystemDefines.EvasionFlavorRange);
                        var warriorMaxWithFlavor = 35 + BattleSystemDefines.EvasionFlavorRange;
                        Assert.True(actualMin >= warriorMinWithFlavor && actualMax <= warriorMaxWithFlavor,
                            $"Warrior should have standard evasion range, but got {actualMin}-{actualMax} (expected {warriorMinWithFlavor}-{warriorMaxWithFlavor} with flavor)");
                        break;
                }
            }
        }

        Assert.True(jobsFound.Count >= 3, "At least 3 different jobs should be found within 500 attempts");
    }
}
