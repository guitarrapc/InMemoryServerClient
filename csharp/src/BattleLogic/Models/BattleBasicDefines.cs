namespace BattleLogic.Models;

public static class BattleBasicDefines
{
    /// <summary>
    /// Battle replay directory
    /// </summary>
    public const string BattleReplayDirectory = "./battle_replay/";

    /// <summary>
    /// Battle field width
    /// </summary>
    public const int BattleFieldWidth = 20;

    /// <summary>
    /// Battle field height
    /// </summary>
    public const int BattleFieldHeight = 20;

    // Player Status
    // Players get slightly better stats than enemies for balance

    /// <summary>
    /// Player HP range
    /// </summary>
    public static readonly StatusRange PlayerHp = new(300, 430);

    /// <summary>
    /// Attack power range for player
    /// </summary>
    public static readonly StatusRange PlayerAttackPower = new(25, 34);

    /// <summary>
    /// Defense power range for player
    /// </summary>
    public static readonly StatusRange PlayerDefencePower = new(10, 22);

    /// <summary>
    /// Movement speed range for player
    /// </summary>
    public static readonly StatusRange PlayerMoveSpeed = new(2, 4);

    /// <summary>
    /// Accuracy range for player (75-90%)
    /// </summary>
    public static readonly StatusRange PlayerAccuracy = new(75, 90);

    /// <summary>
    /// Evasion range for player (15-30%)
    /// </summary>
    public static readonly StatusRange PlayerEvasion = new(15, 30);

    // Enemy Status
    // Enemies get slightly weaker stats for balance

    /// <summary>
    /// Enemy types and their HP ranges
    /// </summary>
    public static readonly Dictionary<EntityType, StatusRange> EnemyHpByType = new Dictionary<EntityType, StatusRange>
    {
        { EntityType.SmallEnemy, new (55, 85) },
        { EntityType.MediumEnemy, new (110, 160) },
        { EntityType.LargeEnemy, new (265, 350) }
    };

    /// <summary>
    /// Attack power range for enemy
    /// </summary>
    public static readonly Dictionary<EntityType, StatusRange> EnemyAttackPower = new Dictionary<EntityType, StatusRange>
    {
        { EntityType.SmallEnemy, new (11, 16) },
        { EntityType.MediumEnemy, new (16, 24) },
        { EntityType.LargeEnemy, new (24, 32) }
    };

    /// <summary>
    /// Defense power range for enemy
    /// </summary>
    public static readonly Dictionary<EntityType, StatusRange> EnemyDefencePower = new Dictionary<EntityType, StatusRange>
    {
        { EntityType.SmallEnemy, new (5, 10) },
        { EntityType.MediumEnemy, new (8, 13) },
        { EntityType.LargeEnemy, new (15, 20) }
    };

    /// <summary>
    /// Movement speed range for enemy
    /// </summary>
    public static readonly Dictionary<EntityType, StatusRange> EnemyMoveSpeed = new Dictionary<EntityType, StatusRange>
    {
        { EntityType.SmallEnemy, new (1, 1) },
        { EntityType.MediumEnemy, new (1, 2) },
        { EntityType.LargeEnemy, new (1, 3) }
    };

    /// <summary>
    /// Accuracy range for enemy
    /// </summary>
    public static readonly Dictionary<EntityType, StatusRange> EnemyAccuracy = new Dictionary<EntityType, StatusRange>
    {
        { EntityType.SmallEnemy, new (65, 80) },
        { EntityType.MediumEnemy, new (70, 85) },
        { EntityType.LargeEnemy, new (75, 90) }
    };

    /// <summary>
    /// Evasion range for enemy (small enemies have higher evasion)
    /// </summary>
    public static readonly Dictionary<EntityType, StatusRange> EnemyEvasion = new Dictionary<EntityType, StatusRange>
    {
        { EntityType.SmallEnemy, new (25, 40) },   // 高回避
        { EntityType.MediumEnemy, new (15, 30) },  // 中回避
        { EntityType.LargeEnemy, new (5, 20) }     // 低回避
    };

    /// <summary>
    /// Defense damage reduction percentage
    /// </summary>
    public const int DefenseDamageReductionPercent = 50;

    /// <summary>
    /// Minimum number of enemies in a battle
    /// </summary>
    public const int MinEnemyCount = 14;

    /// <summary>
    /// Maximum number of enemies in a battle
    /// </summary>
    public const int MaxEnemyCount = 19;

    /// <summary>
    /// Minimum battle turns
    /// </summary>
    public const int MinBattleTurns = 100;

    /// <summary>
    /// Maximum battle turns
    /// </summary>
    public const int MaxBattleTurns = 300;

    /// <summary>
    /// Battle replay frames per second
    /// </summary>
    public const int BattleReplayFps = 30;

    // Random Flavor Ranges
    // Small random variations to add flavor to combat calculations

    /// <summary>
    /// Random variation range for attack power (±8% of base value, minimum ±1)
    /// </summary>
    public const float AttackFlavorPercent = 0.08f;

    /// <summary>
    /// Random variation range for defense power (±8% of base value, minimum ±1)
    /// </summary>
    public const float DefenseFlavorPercent = 0.08f;

    /// <summary>
    /// Random variation range for accuracy (±2% absolute, e.g., 85% becomes 83-87%)
    /// </summary>
    public const int AccuracyFlavorRange = 2;

    /// <summary>
    /// Random variation range for evasion (±2% absolute, e.g., 20% becomes 18-22%)
    /// </summary>
    public const int EvasionFlavorRange = 2;

    /// <summary>
    /// Minimum absolute variation for attack and defense (ensures at least ±1 variation)
    /// </summary>
    public const int MinAbsoluteFlavor = 1;

    /// <summary>
    /// Player job stat modifiers
    /// </summary>
    public static readonly Dictionary<JobType, JobStatModifier> PlayerJobModifiers = new Dictionary<JobType, JobStatModifier>
    {
        {
            JobType.Tank,
            new JobStatModifier
            {
                HpMultiplier = 1.3f,
                AttackMultiplier = 0.8f,
                DefenseMultiplier = 1.5f,
                SpeedMultiplier = 0.7f,
                AccuracyMultiplier = 0.9f,  // タンクは命中率がやや低い
                EvasionMultiplier = 0.6f,   // タンクは低回避率（重装備）
                HpBonus = 80,
                AttackBonus = 0,
                DefenseBonus = 10,
                SpeedBonus = -1,
                AccuracyBonus = -5,
                EvasionBonus = -10
            }
        },
        {
            JobType.Warrior,
            new JobStatModifier
            {
                HpMultiplier = 1.1f,
                AttackMultiplier = 1.2f,
                DefenseMultiplier = 1.0f,
                SpeedMultiplier = 1.2f,
                AccuracyMultiplier = 1.0f,  // ウォリアーは標準的な命中率
                EvasionMultiplier = 1.0f,   // ウォリアーは標準的な回避率
                HpBonus = 30,
                AttackBonus = 10,
                DefenseBonus = 0,
                SpeedBonus = 1,
                AccuracyBonus = 0,
                EvasionBonus = 0
            }
        },
        {
            JobType.Mage,
            new JobStatModifier
            {
                HpMultiplier = 0.8f,
                AttackMultiplier = 1.4f,
                DefenseMultiplier = 0.7f,
                SpeedMultiplier = 0.9f,
                AccuracyMultiplier = 1.2f,  // メイジは高い命中率（魔法の精密性）
                EvasionMultiplier = 0.8f,   // メイジは低い回避率（運動性が低い）
                HpBonus = -50,
                AttackBonus = 8,
                DefenseBonus = -3,
                SpeedBonus = 0,
                AccuracyBonus = 10,
                EvasionBonus = -5
            }
        },
        {
            JobType.Archer,
            new JobStatModifier
            {
                HpMultiplier = 0.9f,
                AttackMultiplier = 1.3f,
                DefenseMultiplier = 0.8f,
                SpeedMultiplier = 1.4f,
                AccuracyMultiplier = 1.3f,  // アーチャーは最高の命中率（弓術の精度）
                EvasionMultiplier = 1.5f,   // アーチャーは最高の回避率（機動性重視）
                HpBonus = -20,
                AttackBonus = 3,
                DefenseBonus = -2,
                SpeedBonus = 1,
                AccuracyBonus = 15,
                EvasionBonus = 10
            }
        }
    };

    /// <summary>
    /// Enemy job stat modifiers
    /// </summary>
    public static readonly Dictionary<JobType, JobStatModifier> EnemyJobModifiers = new Dictionary<JobType, JobStatModifier>
    {
        {
            JobType.Bruiser,
            new JobStatModifier
            {
                HpMultiplier = 1.2f,
                AttackMultiplier = 1.1f,
                DefenseMultiplier = 1.0f,
                SpeedMultiplier = 1.0f,
                AccuracyMultiplier = 1.0f,  // ブルーザーは標準的な命中率
                EvasionMultiplier = 1.0f,    // ブルーザーは標準的な回避率
                HpBonus = 30,
                AttackBonus = 4,
                DefenseBonus = 1,
                SpeedBonus = 0,
                AccuracyBonus = 0,
                EvasionBonus = 0
            }
        },
        {
            JobType.Guardian,
            new JobStatModifier
            {
                HpMultiplier = 1.4f,
                AttackMultiplier = 0.7f,
                DefenseMultiplier = 1.6f,
                SpeedMultiplier = 0.6f,
                AccuracyMultiplier = 0.85f,  // ガーディアンは低い命中率（重装備のため）
                EvasionMultiplier = 0.6f,    // ガーディアンは最低回避率（重装備）
                HpBonus = 100,
                AttackBonus = -2,
                DefenseBonus = 10,
                SpeedBonus = -1,
                AccuracyBonus = -8,
                EvasionBonus = -10
            }
        },
        {
            JobType.Assassin,
            new JobStatModifier
            {
                HpMultiplier = 0.7f,
                AttackMultiplier = 1.2f,
                DefenseMultiplier = 0.6f,
                SpeedMultiplier = 1.5f,
                AccuracyMultiplier = 1.15f,  // アサシンは高い命中率（精密攻撃）
                EvasionMultiplier = 1.4f,    // アサシンは高い回避率（機動性）
                HpBonus = -30,
                AttackBonus = 6,
                DefenseBonus = -4,
                SpeedBonus = 1,
                AccuracyBonus = 8,
                EvasionBonus = 12
            }
        },
        {
            JobType.Caster,
            new JobStatModifier
            {
                HpMultiplier = 0.8f,
                AttackMultiplier = 1.4f,
                DefenseMultiplier = 0.7f,
                SpeedMultiplier = 0.9f,
                AccuracyMultiplier = 1.1f,   // キャスターは高い命中率（魔法の精度）
                EvasionMultiplier = 0.9f,    // キャスターは少し低い回避率（運動性低め）
                HpBonus = -20,
                AttackBonus = 9,
                DefenseBonus = -3,
                SpeedBonus = 0,
                AccuracyBonus = 5,
                EvasionBonus = -2
            }
        }
    };
}
