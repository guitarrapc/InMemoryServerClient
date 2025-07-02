using BattleLogic.Constans;
using BattleLogic.Models;

namespace BattleLogic.Services;

/// <summary>
/// Handles battle initialization logic
/// </summary>
internal class BattleInitializer(BattleSeed battleSeed)
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

            battleLogs.Add($"{player.Name} (Job: {player.PlayerJob}) - HP: {player.MaxHp}, ATK: {player.Attack}, DEF: {player.Defense}, SPD: {player.Speed}, ACC: {player.Accuracy}%, EVA: {player.Evasion}%");
        }

        return players;
    }

    /// <summary>
    /// Initialize enemies for battle
    /// </summary>
    public List<EntityInfo> InitializeEnemies(List<string> battleLogs)
    {
        int enemyCount = battleSeed.Random.Next(BattleSystemDefines.MinEnemyCount, BattleSystemDefines.MaxEnemyCount);
        var enemies = new List<EntityInfo>(enemyCount);

        var enemySizes = new[] { EnemySize.Small, EnemySize.Medium, EnemySize.Large };

        for (int i = 0; i < enemyCount; i++)
        {
            var enemy = CreateEnemy(i, enemySizes);
            enemies.Add(enemy);

            battleLogs.Add($"{enemy.Name} (Job: {enemy.EnemyJob}) - HP: {enemy.MaxHp}, ATK: {enemy.Attack}, DEF: {enemy.Defense}, SPD: {enemy.Speed}, ACC: {enemy.Accuracy}%, EVA: {enemy.Evasion}%");
        }

        return enemies;
    }

    /// <summary>
    /// Create a player entity
    /// </summary>
    private EntityInfo CreatePlayer(int playerIndex)
    {
        // Randomly assign a player job
        var playerJobs = Enum.GetValues<PlayerJob>();
        var assignedJob = playerJobs[battleSeed.Random.Next(playerJobs.Length)];
        var jobModifier = BattleSystemDefines.PlayerJobModifiers[assignedJob];

        // Calculate base stats
        var baseMaxHp = battleSeed.Random.Next(BattleSystemDefines.PlayerHp.Min, BattleSystemDefines.PlayerHp.Max);
        var baseAttack = battleSeed.Random.Next(BattleSystemDefines.PlayerAttackPower.Min, BattleSystemDefines.PlayerAttackPower.Max);
        var baseDefense = battleSeed.Random.Next(BattleSystemDefines.PlayerDefencePower.Min, BattleSystemDefines.PlayerDefencePower.Max);
        var baseSpeed = battleSeed.Random.Next(BattleSystemDefines.PlayerMoveSpeed.Min, BattleSystemDefines.PlayerMoveSpeed.Max);
        var baseAccuracy = battleSeed.Random.Next(BattleSystemDefines.PlayerAccuracy.Min, BattleSystemDefines.PlayerAccuracy.Max);
        var baseEvasion = battleSeed.Random.Next(BattleSystemDefines.PlayerEvasion.Min, BattleSystemDefines.PlayerEvasion.Max);

        // Apply job modifiers
        var modifiedMaxHp = Math.Max(1, (int)(baseMaxHp * jobModifier.HpMultiplier) + jobModifier.HpBonus);
        var modifiedAttack = Math.Max(1, (int)(baseAttack * jobModifier.AttackMultiplier) + jobModifier.AttackBonus);
        var modifiedDefense = Math.Max(0, (int)(baseDefense * jobModifier.DefenseMultiplier) + jobModifier.DefenseBonus);
        var modifiedSpeed = Math.Max(1, (int)(baseSpeed * jobModifier.SpeedMultiplier) + jobModifier.SpeedBonus);
        var modifiedAccuracy = Math.Max(0, (int)(baseAccuracy * jobModifier.AccuracyMultiplier) + jobModifier.AccuracyBonus);
        var modifiedEvasion = Math.Max(0, (int)(baseEvasion * jobModifier.EvasionMultiplier) + jobModifier.EvasionBonus);

        return new EntityInfo
        {
            Id = battleSeed.NextGuid().ToString(), // Use deterministic GUID for players
            Name = $"{assignedJob}Player{playerIndex + 1}",
            Type = EntityTypeInfo.Player,
            PlayerJob = assignedJob,
            EnemyJob = null,
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
        var enemySize = enemySizes[battleSeed.Random.Next(enemySizes.Length)];

        // Randomly assign an enemy job
        var enemyJobs = Enum.GetValues<EnemyJob>();
        var assignedJob = enemyJobs[battleSeed.Random.Next(enemyJobs.Length)];
        var jobModifier = BattleSystemDefines.EnemyJobModifiers[assignedJob];

        // Calculate base stats
        var baseMaxHp = battleSeed.Random.Next(BattleSystemDefines.EnemyHpByType[enemySize].Min, BattleSystemDefines.EnemyHpByType[enemySize].Max);
        var baseAttack = battleSeed.Random.Next(BattleSystemDefines.EnemyAttackPower[enemySize].Min, BattleSystemDefines.EnemyAttackPower[enemySize].Max);
        var baseDefense = battleSeed.Random.Next(BattleSystemDefines.EnemyDefencePower[enemySize].Min, BattleSystemDefines.EnemyDefencePower[enemySize].Max);
        var baseSpeed = battleSeed.Random.Next(BattleSystemDefines.EnemyMoveSpeed[enemySize].Min, BattleSystemDefines.EnemyMoveSpeed[enemySize].Max);
        var baseAccuracy = battleSeed.Random.Next(BattleSystemDefines.EnemyAccuracy[enemySize].Min, BattleSystemDefines.EnemyAccuracy[enemySize].Max);
        var baseEvasion = battleSeed.Random.Next(BattleSystemDefines.EnemyEvasion[enemySize].Min, BattleSystemDefines.EnemyEvasion[enemySize].Max);

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
            Id = battleSeed.NextGuid().ToString(), // Use deterministic GUID for enemies
            Name = $"Enemy{enemyIndex + 1}_{enemySize}",
            Type = entityTypeInfo,
            PlayerJob = null,
            EnemyJob = assignedJob,
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
