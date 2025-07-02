using BattleLogic.Models;

namespace BattleLogic.Battle;

/// <summary>
/// Handles battle initialization logic
/// </summary>
public class BattleInitializer(Random random)
{
    /// <summary>
    /// Initialize players for battle
    /// </summary>
    public List<EntityInfo> InitializePlayers(int playerCount, List<string> battleLogs)
    {
        var players = new List<EntityInfo>(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            var player = CreatePlayer(i);
            players.Add(player);

            battleLogs.Add($"{player.Name} (Job: {player.Job}) - HP: {player.MaxHp}, ATK: {player.Attack}, DEF: {player.Defense}, SPD: {player.Speed}, ACC: {player.Accuracy}%, EVA: {player.Evasion}%");
        }

        return players;
    }

    /// <summary>
    /// Initialize enemies for battle
    /// </summary>
    public List<EntityInfo> InitializeEnemies(List<string> battleLogs)
    {
        int enemyCount = random.Next(BattleBasicDefines.MinEnemyCount, BattleBasicDefines.MaxEnemyCount);
        var enemies = new List<EntityInfo>(enemyCount);

        var enemySizes = new[] { EnemySize.Small, EnemySize.Medium, EnemySize.Large };

        for (int i = 0; i < enemyCount; i++)
        {
            var enemy = CreateEnemy(i, enemySizes);
            enemies.Add(enemy);

            battleLogs.Add($"{enemy.Name} (Job: {enemy.Job}) - HP: {enemy.MaxHp}, ATK: {enemy.Attack}, DEF: {enemy.Defense}, SPD: {enemy.Speed}, ACC: {enemy.Accuracy}%, EVA: {enemy.Evasion}%");
        }

        return enemies;
    }

    /// <summary>
    /// Create a player entity
    /// </summary>
    private EntityInfo CreatePlayer(int playerIndex)
    {
        // Randomly assign a job
        var jobTypes = new[] { JobType.Tank, JobType.Warrior, JobType.Mage, JobType.Archer };
        var assignedJob = jobTypes[random.Next(jobTypes.Length)];
        var jobModifier = BattleBasicDefines.PlayerJobModifiers[assignedJob];

        // Calculate base stats
        var baseMaxHp = random.Next(BattleBasicDefines.PlayerHp.Min, BattleBasicDefines.PlayerHp.Max);
        var baseAttack = random.Next(BattleBasicDefines.PlayerAttackPower.Min, BattleBasicDefines.PlayerAttackPower.Max);
        var baseDefense = random.Next(BattleBasicDefines.PlayerDefencePower.Min, BattleBasicDefines.PlayerDefencePower.Max);
        var baseSpeed = random.Next(BattleBasicDefines.PlayerMoveSpeed.Min, BattleBasicDefines.PlayerMoveSpeed.Max);
        var baseAccuracy = random.Next(BattleBasicDefines.PlayerAccuracy.Min, BattleBasicDefines.PlayerAccuracy.Max);
        var baseEvasion = random.Next(BattleBasicDefines.PlayerEvasion.Min, BattleBasicDefines.PlayerEvasion.Max);

        // Apply job modifiers
        var modifiedMaxHp = Math.Max(1, (int)(baseMaxHp * jobModifier.HpMultiplier) + jobModifier.HpBonus);
        var modifiedAttack = Math.Max(1, (int)(baseAttack * jobModifier.AttackMultiplier) + jobModifier.AttackBonus);
        var modifiedDefense = Math.Max(0, (int)(baseDefense * jobModifier.DefenseMultiplier) + jobModifier.DefenseBonus);
        var modifiedSpeed = Math.Max(1, (int)(baseSpeed * jobModifier.SpeedMultiplier) + jobModifier.SpeedBonus);
        var modifiedAccuracy = Math.Max(0, (int)(baseAccuracy * jobModifier.AccuracyMultiplier) + jobModifier.AccuracyBonus);
        var modifiedEvasion = Math.Max(0, (int)(baseEvasion * jobModifier.EvasionMultiplier) + jobModifier.EvasionBonus);

        return new EntityInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{assignedJob}Player{playerIndex + 1}",
            Type = EntityTypeInfo.Player,
            Job = assignedJob,
            CurrentHp = modifiedMaxHp,
            MaxHp = modifiedMaxHp,
            Attack = modifiedAttack,
            Defense = modifiedDefense,
            Speed = modifiedSpeed,
            Accuracy = modifiedAccuracy,
            Evasion = modifiedEvasion,
            IsDefending = false
        };
    }

    /// <summary>
    /// Create an enemy entity
    /// </summary>
    private EntityInfo CreateEnemy(int enemyIndex, EnemySize[] enemySizes)
    {
        var enemySize = enemySizes[random.Next(enemySizes.Length)];

        // Randomly assign an enemy job
        var jobTypes = new[] { JobType.Bruiser, JobType.Guardian, JobType.Assassin, JobType.Caster };
        var assignedJob = jobTypes[random.Next(jobTypes.Length)];
        var jobModifier = BattleBasicDefines.EnemyJobModifiers[assignedJob];

        // Calculate base stats
        var baseMaxHp = random.Next(BattleBasicDefines.EnemyHpByType[enemySize].Min, BattleBasicDefines.EnemyHpByType[enemySize].Max);
        var baseAttack = random.Next(BattleBasicDefines.EnemyAttackPower[enemySize].Min, BattleBasicDefines.EnemyAttackPower[enemySize].Max);
        var baseDefense = random.Next(BattleBasicDefines.EnemyDefencePower[enemySize].Min, BattleBasicDefines.EnemyDefencePower[enemySize].Max);
        var baseSpeed = random.Next(BattleBasicDefines.EnemyMoveSpeed[enemySize].Min, BattleBasicDefines.EnemyMoveSpeed[enemySize].Max);
        var baseAccuracy = random.Next(BattleBasicDefines.EnemyAccuracy[enemySize].Min, BattleBasicDefines.EnemyAccuracy[enemySize].Max);
        var baseEvasion = random.Next(BattleBasicDefines.EnemyEvasion[enemySize].Min, BattleBasicDefines.EnemyEvasion[enemySize].Max);

        // Apply job modifiers
        var modifiedMaxHp = Math.Max(1, (int)(baseMaxHp * jobModifier.HpMultiplier) + jobModifier.HpBonus);
        var modifiedAttack = Math.Max(1, (int)(baseAttack * jobModifier.AttackMultiplier) + jobModifier.AttackBonus);
        var modifiedDefense = Math.Max(0, (int)(baseDefense * jobModifier.DefenseMultiplier) + jobModifier.DefenseBonus);
        var modifiedSpeed = Math.Max(1, (int)(baseSpeed * jobModifier.SpeedMultiplier) + jobModifier.SpeedBonus);
        var modifiedAccuracy = Math.Max(0, (int)(baseAccuracy * jobModifier.AccuracyMultiplier) + jobModifier.AccuracyBonus);
        var modifiedEvasion = Math.Max(0, (int)(baseEvasion * jobModifier.EvasionMultiplier) + jobModifier.EvasionBonus);

        // Create entity type info based on enemy size
        var entityTypeInfo = enemySize switch
        {
            EnemySize.Small => EntityTypeInfo.SmallEnemy,
            EnemySize.Medium => EntityTypeInfo.MediumEnemy,
            EnemySize.Large => EntityTypeInfo.LargeEnemy,
            _ => throw new ArgumentOutOfRangeException(nameof(enemySize))
        };

        return new EntityInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Enemy{enemyIndex + 1}_{enemySize}",
            Type = entityTypeInfo,
            Job = assignedJob,
            MaxHp = modifiedMaxHp,
            CurrentHp = modifiedMaxHp,
            Attack = modifiedAttack,
            Defense = modifiedDefense,
            Speed = modifiedSpeed,
            Accuracy = modifiedAccuracy,
            Evasion = modifiedEvasion,
            IsDefending = false
        };
    }
}
