using Shared;

namespace InMemoryServer.Battle;

/// <summary>
/// Handles battle initialization logic
/// </summary>
public class BattleInitializer
{
    private readonly Random _random;
    private readonly ILogger _logger;

    public BattleInitializer(Random random, ILogger logger)
    {
        _random = random;
        _logger = logger;
    }

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
        int enemyCount = _random.Next(BattleBasicDefines.MinEnemyCount, BattleBasicDefines.MaxEnemyCount);
        var enemies = new List<EntityInfo>(enemyCount);

        string[] enemyTypes = Enum.GetNames<EnemyType>();

        for (int i = 0; i < enemyCount; i++)
        {
            var enemy = CreateEnemy(i, enemyTypes);
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
        // Randomly assign a job
        var jobTypes = Enum.GetValues<PlayerJob>();
        var assignedJob = jobTypes[_random.Next(jobTypes.Length)];
        var jobModifier = BattleBasicDefines.PlayerJobModifiers[assignedJob];

        // Calculate base stats
        var baseMaxHp = _random.Next(BattleBasicDefines.PlayerHp.Min, BattleBasicDefines.PlayerHp.Max);
        var baseAttack = _random.Next(BattleBasicDefines.PlayerAttackPower.Min, BattleBasicDefines.PlayerAttackPower.Max);
        var baseDefense = _random.Next(BattleBasicDefines.PlayerDefencePower.Min, BattleBasicDefines.PlayerDefencePower.Max);
        var baseSpeed = _random.Next(BattleBasicDefines.PlayerMoveSpeed.Min, BattleBasicDefines.PlayerMoveSpeed.Max);
        var baseAccuracy = _random.Next(BattleBasicDefines.PlayerAccuracy.Min, BattleBasicDefines.PlayerAccuracy.Max);
        var baseEvasion = _random.Next(BattleBasicDefines.PlayerEvasion.Min, BattleBasicDefines.PlayerEvasion.Max);

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
            Type = "Player",
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
    private EntityInfo CreateEnemy(int enemyIndex, string[] enemyTypes)
    {
        var enemyType = Enum.Parse<EnemyType>(enemyTypes[_random.Next(enemyTypes.Length)]);

        // Randomly assign an enemy job
        var jobTypes = Enum.GetValues<EnemyJob>();
        var assignedEnemyJob = jobTypes[_random.Next(jobTypes.Length)];
        var jobModifier = BattleBasicDefines.EnemyJobModifiers[assignedEnemyJob];

        // Calculate base stats
        var baseMaxHp = _random.Next(BattleBasicDefines.EnemyHpByType[enemyType].Min, BattleBasicDefines.EnemyHpByType[enemyType].Max);
        var baseAttack = _random.Next(BattleBasicDefines.EnemyAttackPower[enemyType].Min, BattleBasicDefines.EnemyAttackPower[enemyType].Max);
        var baseDefense = _random.Next(BattleBasicDefines.EnemyDefencePower[enemyType].Min, BattleBasicDefines.EnemyDefencePower[enemyType].Max);
        var baseSpeed = _random.Next(BattleBasicDefines.EnemyMoveSpeed[enemyType].Min, BattleBasicDefines.EnemyMoveSpeed[enemyType].Max);
        var baseAccuracy = _random.Next(BattleBasicDefines.EnemyAccuracy[enemyType].Min, BattleBasicDefines.EnemyAccuracy[enemyType].Max);
        var baseEvasion = _random.Next(BattleBasicDefines.EnemyEvasion[enemyType].Min, BattleBasicDefines.EnemyEvasion[enemyType].Max);

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
            Name = $"{assignedEnemyJob}{enemyType}Enemy{enemyIndex + 1}",
            Type = enemyType.ToString(),
            EnemyJob = assignedEnemyJob,
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
}
