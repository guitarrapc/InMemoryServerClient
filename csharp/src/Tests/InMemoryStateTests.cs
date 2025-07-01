using InMemoryServer;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;

namespace Tests;

/// <summary>
/// Tests for InMemoryState
/// </summary>
public class InMemoryStateTests
{
    [Fact]
    public void KeyValueStore_ShouldBeEmpty_Initially()
    {
        // Arrange & Act
        var state = new InMemoryState();

        // Assert
        Assert.Empty(state.KeyValueStore);
        Assert.Empty(state.KeyWatchers);
        Assert.Empty(state.BattleStates);
    }

    [Fact]
    public void KeyValueStore_ShouldStore_KeyValuePairs()
    {
        // Arrange
        var state = new InMemoryState();
        const string key = "test_key";
        const string value = "test_value";

        // Act
        state.KeyValueStore[key] = value;

        // Assert
        Assert.True(state.KeyValueStore.ContainsKey(key));
        Assert.Equal(value, state.KeyValueStore[key]);
    }
}

/// <summary>
/// Tests for GroupManager
/// </summary>
public class GroupManagerTests
{
    private readonly ILogger<GroupManager> _logger;
    private readonly GroupManager _groupManager;

    public GroupManagerTests()
    {
        _logger = Substitute.For<ILogger<GroupManager>>();
        _groupManager = new GroupManager(_logger);
    }

    [Fact]
    public async Task JoinGroupAsync_ShouldCreateNewGroup_WhenNoGroupsExist()
    {
        // Arrange
        const string connectionId = "test_connection";
        const string groupName = "test_group";

        // Act
        var result = await _groupManager.JoinGroupAsync(connectionId, groupName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(groupName, result.Name);
        Assert.Equal(1, result.ConnectionCount);
        Assert.Equal(SystemDefines.MaxConnectionsPerGroup, result.MaxConnections);
    }

    [Fact]
    public async Task JoinGroupAsync_ShouldJoinExistingGroup_WhenGroupHasSpace()
    {
        // Arrange
        const string connectionId1 = "test_connection_1";
        const string connectionId2 = "test_connection_2";
        const string groupName = "test_group";

        // Act
        var group1 = await _groupManager.JoinGroupAsync(connectionId1, groupName);
        var group2 = await _groupManager.JoinGroupAsync(connectionId2, groupName);

        // Assert
        Assert.Equal(group1.Id, group2.Id);
        Assert.Equal(2, group2.ConnectionCount);
    }

    [Fact]
    public async Task LeaveGroupAsync_ShouldReduceConnectionCount()
    {
        // Arrange
        const string connectionId = "test_connection";
        const string groupName = "test_group";

        // Act
        var group = await _groupManager.JoinGroupAsync(connectionId, groupName);
        await _groupManager.LeaveGroupAsync(connectionId);

        // Assert
        var groupInfo = _groupManager.GetGroupInfo(group.Id);
        Assert.Null(groupInfo); // Group should be removed when empty
    }

    [Fact]
    public void GetGroupIdForConnection_ShouldReturnNull_WhenConnectionNotInGroup()
    {
        // Arrange
        const string connectionId = "test_connection";

        // Act
        var result = _groupManager.GetGroupIdForConnection(connectionId);

        // Assert
        Assert.Null(result);
    }
}

/// <summary>
/// Tests for BattleState
/// </summary>
public class BattleStateTests
{
    private readonly ILogger<BattleState> _logger;

    public BattleStateTests()
    {
        _logger = Substitute.For<ILogger<BattleState>>();
    }
    [Fact]
    public void BattleState_ShouldInitialize_WithProvidedGroup()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test_group",
            ConnectionCount = 3,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(battleId, status.BattleId);
        Assert.Equal(group.ConnectionCount, status.Players.Count);
        Assert.True(status.Enemies.Count >= BattleBasicDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleBasicDefines.MaxEnemyCount);
        Assert.Equal(BattleBasicDefines.BattleFieldWidth, status.FieldWidth);
        Assert.Equal(BattleBasicDefines.BattleFieldHeight, status.FieldHeight);
    }

    [Fact]
    public void BattleStatus_ShouldHaveValidPlayers()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test_group",
            ConnectionCount = 2,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.Equal(2, status.Players.Count);

        foreach (var player in status.Players)
        {
            Assert.Equal("Player", player.Type);
            // Players have job modifiers applied, so we need to check wider ranges
            Assert.True(player.MaxHp >= 200 && player.MaxHp <= 700, $"Player HP {player.MaxHp} is outside expected range");
            Assert.Equal(player.MaxHp, player.CurrentHp); // Should start at full health
            Assert.True(player.Attack >= 15 && player.Attack <= 60, $"Player Attack {player.Attack} is outside expected range");
            Assert.True(player.Defense >= 0 && player.Defense <= 50, $"Player Defense {player.Defense} is outside expected range");
            Assert.True(player.Speed >= 1 && player.Speed <= 8, $"Player Speed {player.Speed} is outside expected range");
        }
    }

    [Fact]
    public void BattleStatus_ShouldHaveValidEnemies()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test_group",
            ConnectionCount = 1,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        var status = battleState.GetStatus();

        // Assert
        Assert.True(status.Enemies.Count >= BattleBasicDefines.MinEnemyCount);
        Assert.True(status.Enemies.Count <= BattleBasicDefines.MaxEnemyCount);

        foreach (var enemy in status.Enemies)
        {
            var enemyType = Enum.Parse<EnemyType>(enemy.Type);
            Assert.True(BattleBasicDefines.EnemyHpByType.ContainsKey(enemyType));

            // Enemies also have job modifiers applied, so we check wider ranges
            var baseHpRange = BattleBasicDefines.EnemyHpByType[enemyType];
            var baseAttackRange = BattleBasicDefines.EnemyAttackPower[enemyType];
            var baseDefenseRange = BattleBasicDefines.EnemyDefencePower[enemyType];
            var baseSpeedRange = BattleBasicDefines.EnemyMoveSpeed[enemyType];

            // Job modifiers can multiply values by up to 1.4x and add up to +100, or reduce by 0.6x and subtract up to -30
            var expectedMinHp = Math.Max(1, (int)(baseHpRange.Min * 0.6f - 30));
            var expectedMaxHp = (int)(baseHpRange.Max * 1.4f + 100);
            var expectedMinAttack = Math.Max(1, (int)(baseAttackRange.Min * 0.6f - 5));
            var expectedMaxAttack = (int)(baseAttackRange.Max * 1.4f + 10);
            var expectedMinDefense = Math.Max(0, (int)(baseDefenseRange.Min * 0.6f - 5));
            var expectedMaxDefense = (int)(baseDefenseRange.Max * 1.6f + 10);
            var expectedMinSpeed = Math.Max(1, (int)(baseSpeedRange.Min * 0.6f - 1));
            var expectedMaxSpeed = (int)(baseSpeedRange.Max * 1.5f + 1);

            Assert.True(enemy.MaxHp >= expectedMinHp && enemy.MaxHp <= expectedMaxHp,
                $"Enemy HP {enemy.MaxHp} is outside expected range [{expectedMinHp}-{expectedMaxHp}]");
            Assert.Equal(enemy.MaxHp, enemy.CurrentHp); // Should start at full health
            Assert.True(enemy.Attack >= expectedMinAttack && enemy.Attack <= expectedMaxAttack,
                $"Enemy Attack {enemy.Attack} is outside expected range [{expectedMinAttack}-{expectedMaxAttack}]");
            Assert.True(enemy.Defense >= expectedMinDefense && enemy.Defense <= expectedMaxDefense,
                $"Enemy Defense {enemy.Defense} is outside expected range [{expectedMinDefense}-{expectedMaxDefense}]");
            Assert.True(enemy.Speed >= expectedMinSpeed && enemy.Speed <= expectedMaxSpeed,
                $"Enemy Speed {enemy.Speed} is outside expected range [{expectedMinSpeed}-{expectedMaxSpeed}]");
        }
    }
}

/// <summary>
/// Job modifier specific tests for battle entities
/// </summary>
public class JobModifierTests
{
    private readonly ILogger<BattleState> _logger;

    public JobModifierTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<BattleState>();
    }

    /// <summary>
    /// Test Tank job modifier application
    /// </summary>
    [Fact]
    public void PlayerJob_Tank_ShouldApplyCorrectModifiers()
    {
        // Arrange
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
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
            var battleState = new BattleState(battleId + attempt, group, _logger);
            var status = battleState.GetStatus();

            var tankPlayer = status.Players.FirstOrDefault(p => p.Job == PlayerJob.Tank);
            if (!tankPlayer.Equals(default(EntityInfo)))
            {
                tankPlayerFound = true;
                var player = tankPlayer;
                var tankModifier = BattleBasicDefines.PlayerJobModifiers[PlayerJob.Tank];

                // Calculate expected ranges for Tank
                var baseHpMin = BattleBasicDefines.PlayerHp.Min;
                var baseHpMax = BattleBasicDefines.PlayerHp.Max;
                var expectedHpMin = Math.Max(1, (int)(baseHpMin * tankModifier.HpMultiplier) + tankModifier.HpBonus);
                var expectedHpMax = (int)(baseHpMax * tankModifier.HpMultiplier) + tankModifier.HpBonus;

                var baseAttackMin = BattleBasicDefines.PlayerAttackPower.Min;
                var baseAttackMax = BattleBasicDefines.PlayerAttackPower.Max;
                var expectedAttackMin = Math.Max(1, (int)(baseAttackMin * tankModifier.AttackMultiplier) + tankModifier.AttackBonus);
                var expectedAttackMax = (int)(baseAttackMax * tankModifier.AttackMultiplier) + tankModifier.AttackBonus;

                var baseDefenseMin = BattleBasicDefines.PlayerDefencePower.Min;
                var baseDefenseMax = BattleBasicDefines.PlayerDefencePower.Max;
                var expectedDefenseMin = Math.Max(0, (int)(baseDefenseMin * tankModifier.DefenseMultiplier) + tankModifier.DefenseBonus);
                var expectedDefenseMax = (int)(baseDefenseMax * tankModifier.DefenseMultiplier) + tankModifier.DefenseBonus;

                var baseSpeedMin = BattleBasicDefines.PlayerMoveSpeed.Min;
                var baseSpeedMax = BattleBasicDefines.PlayerMoveSpeed.Max;
                var expectedSpeedMin = Math.Max(1, (int)(baseSpeedMin * tankModifier.SpeedMultiplier) + tankModifier.SpeedBonus);
                var expectedSpeedMax = (int)(baseSpeedMax * tankModifier.SpeedMultiplier) + tankModifier.SpeedBonus;

                // Assert Tank-specific modifiers
                Assert.Equal(PlayerJob.Tank, player.Job);
                Assert.True(player.MaxHp >= expectedHpMin && player.MaxHp <= expectedHpMax,
                    $"Tank HP {player.MaxHp} should be in range [{expectedHpMin}-{expectedHpMax}]");
                Assert.True(player.Attack >= expectedAttackMin && player.Attack <= expectedAttackMax,
                    $"Tank Attack {player.Attack} should be in range [{expectedAttackMin}-{expectedAttackMax}]");
                Assert.True(player.Defense >= expectedDefenseMin && player.Defense <= expectedDefenseMax,
                    $"Tank Defense {player.Defense} should be in range [{expectedDefenseMin}-{expectedDefenseMax}]");
                Assert.True(player.Speed >= expectedSpeedMin && player.Speed <= expectedSpeedMax,
                    $"Tank Speed {player.Speed} should be in range [{expectedSpeedMin}-{expectedSpeedMax}]");
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
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "warrior_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var warriorPlayerFound = false;
        for (int attempt = 0; attempt < 200 && !warriorPlayerFound; attempt++)
        {
            var battleState = new BattleState(battleId + attempt, group, _logger);
            var status = battleState.GetStatus();

            var warriorPlayer = status.Players.FirstOrDefault(p => p.Job == PlayerJob.Warrior);
            if (!warriorPlayer.Equals(default(EntityInfo)))
            {
                warriorPlayerFound = true;
                var player = warriorPlayer;
                var warriorModifier = BattleBasicDefines.PlayerJobModifiers[PlayerJob.Warrior];

                // Calculate expected ranges for Warrior
                var expectedHpMin = Math.Max(1, (int)(BattleBasicDefines.PlayerHp.Min * warriorModifier.HpMultiplier) + warriorModifier.HpBonus);
                var expectedHpMax = (int)(BattleBasicDefines.PlayerHp.Max * warriorModifier.HpMultiplier) + warriorModifier.HpBonus;

                Assert.Equal(PlayerJob.Warrior, player.Job);
                Assert.True(player.MaxHp >= expectedHpMin && player.MaxHp <= expectedHpMax,
                    $"Warrior HP {player.MaxHp} should be in range [{expectedHpMin}-{expectedHpMax}]");
                // High attack due to 1.2x multiplier + 10 bonus
                // Base attack 25-34, so (25*1.2)+10=40 to (34*1.2)+10=50.8 → 40-50 range
                Assert.True(player.Attack >= 40, $"Warrior should have high attack power, got {player.Attack}");
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
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "mage_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var magePlayerFound = false;
        for (int attempt = 0; attempt < 200 && !magePlayerFound; attempt++)
        {
            var battleState = new BattleState(battleId + attempt, group, _logger);
            var status = battleState.GetStatus();

            var magePlayer = status.Players.FirstOrDefault(p => p.Job == PlayerJob.Mage);
            if (!magePlayer.Equals(default(EntityInfo)))
            {
                magePlayerFound = true;
                var player = magePlayer;
                var mageModifier = BattleBasicDefines.PlayerJobModifiers[PlayerJob.Mage];

                Assert.Equal(PlayerJob.Mage, player.Job);
                // Mage should have lower HP due to 0.8x multiplier and -50 bonus
                // Base HP 200-500, so (200*0.8)-50=110 to (500*0.8)-50=350 → 110-350 range
                Assert.True(player.MaxHp <= 350, $"Mage should have lower HP, got {player.MaxHp}");
                // But very high attack due to 1.4x multiplier + 8 bonus
                // Base attack 10-30, so (10*1.4)+8=22 to (30*1.4)+8=50 → 22-50 range
                Assert.True(player.Attack >= 22, $"Mage should have very high attack power, got {player.Attack}");
                // And lower defense due to 0.7x multiplier and -3 bonus
                // Base defense 10-22, so (10*0.7)-3=4 to (22*0.7)-3=12.4 → 4-12 range
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
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "archer_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var archerPlayerFound = false;
        for (int attempt = 0; attempt < 200 && !archerPlayerFound; attempt++)
        {
            var battleState = new BattleState(battleId + attempt, group, _logger);
            var status = battleState.GetStatus();

            var archerPlayer = status.Players.FirstOrDefault(p => p.Job == PlayerJob.Archer);
            if (!archerPlayer.Equals(default(EntityInfo)))
            {
                archerPlayerFound = true;
                var player = archerPlayer;
                var archerModifier = BattleBasicDefines.PlayerJobModifiers[PlayerJob.Archer];

                Assert.Equal(PlayerJob.Archer, player.Job);
                // Archer should have high speed due to 1.4x multiplier + 1 bonus
                // Base speed 2-4, so (2*1.4)+1=3.8 to (4*1.4)+1=6.6 → 3-6 range
                Assert.True(player.Speed >= 3, $"Archer should have high speed, got {player.Speed}");
                // Good attack due to 1.3x multiplier + 3 bonus
                // Base attack 25-34, so (25*1.3)+3=35.5 to (34*1.3)+3=47.2 → 35-47 range
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
        var battleId = Guid.NewGuid().ToString();
        var group = new GroupInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "enemy_job_test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        var battleState = new BattleState(battleId, group, _logger);
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
                    var guardianModifier = BattleBasicDefines.EnemyJobModifiers[EnemyJob.Guardian];
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
}
