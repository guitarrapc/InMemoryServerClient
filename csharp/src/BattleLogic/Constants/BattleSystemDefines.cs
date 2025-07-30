using BattleLogic.Models;

namespace BattleLogic.Constants;

public static class BattleSystemDefines
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
    public static readonly StatusRange PlayerHp = new(320, 450);

    /// <summary>
    /// Attack power range for player
    /// </summary>
    public static readonly StatusRange PlayerAttackPower = new(28, 38);

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
    public static readonly StatusRange PlayerAccuracy = new(87, 100);

    /// <summary>
    /// Evasion range for player (15-30%)
    /// </summary>
    public static readonly StatusRange PlayerEvasion = new(15, 30);

    /// <summary>
    /// Critical hit rate range for player (base 1%)
    /// </summary>
    public static readonly StatusRange PlayerCriticalRate = new(1, 1);

    // Enemy Status
    // Enemies get slightly weaker stats for balance

    /// <summary>
    /// Enemy types and their HP ranges
    /// </summary>
    public static readonly Dictionary<EnemySize, StatusRange> EnemyHpByType = new Dictionary<EnemySize, StatusRange>
    {
        { EnemySize.Small, new (65, 85) },
        { EnemySize.Medium, new (110, 160) },
        { EnemySize.Large, new (265, 350) }
    };

    /// <summary>
    /// Attack power range for enemy
    /// </summary>
    public static readonly Dictionary<EnemySize, StatusRange> EnemyAttackPower = new Dictionary<EnemySize, StatusRange>
    {
        { EnemySize.Small, new (11, 16) },
        { EnemySize.Medium, new (16, 24) },
        { EnemySize.Large, new (24, 32) }
    };

    /// <summary>
    /// Defense power range for enemy
    /// </summary>
    public static readonly Dictionary<EnemySize, StatusRange> EnemyDefencePower = new Dictionary<EnemySize, StatusRange>
    {
        { EnemySize.Small, new (5, 10) },
        { EnemySize.Medium, new (8, 13) },
        { EnemySize.Large, new (15, 20) }
    };

    /// <summary>
    /// Movement speed range for enemy
    /// </summary>
    public static readonly Dictionary<EnemySize, StatusRange> EnemyMoveSpeed = new Dictionary<EnemySize, StatusRange>
    {
        { EnemySize.Small, new (1, 1) },
        { EnemySize.Medium, new (1, 2) },
        { EnemySize.Large, new (2, 3) }
    };

    /// <summary>
    /// Accuracy range for enemy
    /// </summary>
    public static readonly Dictionary<EnemySize, StatusRange> EnemyAccuracy = new Dictionary<EnemySize, StatusRange>
    {
        { EnemySize.Small, new (70, 85) },
        { EnemySize.Medium, new (75, 90) },
        { EnemySize.Large, new (85, 95) }
    };

    /// <summary>
    /// Evasion range for enemy (small enemies have higher evasion)
    /// </summary>
    public static readonly Dictionary<EnemySize, StatusRange> EnemyEvasion = new Dictionary<EnemySize, StatusRange>
    {
        { EnemySize.Small, new (20, 30) },   // 高回避
        { EnemySize.Medium, new (13, 20) },  // 中回避
        { EnemySize.Large, new (10, 20) },   // 低回避
    };

    /// <summary>
    /// Critical hit rate for enemy (large enemies have higher critical rate)
    /// </summary>
    public static readonly Dictionary<EnemySize, StatusRange> EnemyCriticalRate = new Dictionary<EnemySize, StatusRange>
    {
        { EnemySize.Small, new (1, 1) },   // 低クリティカル攻撃率
        { EnemySize.Medium, new (3, 5) },  // 中クリティカル攻撃率
        { EnemySize.Large, new (8, 8) },   // 高クリティカル攻撃率
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
    public const int MinBattleTurns = 300;

    /// <summary>
    /// Maximum battle turns
    /// </summary>
    public const int MaxBattleTurns = 500;

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
    public static readonly Dictionary<PlayerJob, JobStatModifier> PlayerJobModifiers = new Dictionary<PlayerJob, JobStatModifier>
    {
        {
            PlayerJob.Tank,
            new JobStatModifier
            {
                HpMultiplier = 1.5f,
                AttackMultiplier = 0.8f,
                DefenseMultiplier = 1.5f,
                SpeedMultiplier = 0.7f,
                AccuracyMultiplier = 0.9f,  // タンクは命中率がやや低い
                EvasionMultiplier = 0.6f,   // タンクは低回避率（重装備）
                CriticalRateMultiplier = 3.0f, // 戦士系のクリティカル攻撃率は中程度（3%）
                HpBonus = 80,
                AttackBonus = 0,
                DefenseBonus = 10,
                SpeedBonus = -1,
                AccuracyBonus = -5,
                EvasionBonus = -10,
                CriticalRateBonus = 2
            }
        },
        {
            PlayerJob.Warrior,
            new JobStatModifier
            {
                HpMultiplier = 1.2f,
                AttackMultiplier = 1.2f,
                DefenseMultiplier = 1.1f,
                SpeedMultiplier = 1.2f,
                AccuracyMultiplier = 1.0f,  // ウォリアーは標準的な命中率
                EvasionMultiplier = 1.0f,   // ウォリアーは標準的な回避率
                CriticalRateMultiplier = 3.0f, // 戦士系のクリティカル攻撃率は中程度（3%）
                HpBonus = 50,
                AttackBonus = 10,
                DefenseBonus = 0,
                SpeedBonus = 1,
                AccuracyBonus = 5,
                EvasionBonus = 0,
                CriticalRateBonus = 2
            }
        },
        {
            PlayerJob.Mage,
            new JobStatModifier
            {
                HpMultiplier = 0.8f,
                AttackMultiplier = 1.4f,
                DefenseMultiplier = 0.7f,
                SpeedMultiplier = 0.9f,
                AccuracyMultiplier = 1.2f,  // メイジは高い命中率（魔法の精密性）
                EvasionMultiplier = 0.8f,   // メイジは低い回避率（運動性が低い）
                CriticalRateMultiplier = 1.0f, // 魔法のクリティカル攻撃率は低く1%
                HpBonus = -30,
                AttackBonus = 8,
                DefenseBonus = -5,
                SpeedBonus = 0,
                AccuracyBonus = 10,
                EvasionBonus = -15,
                CriticalRateBonus = 0
            }
        },
        {
            PlayerJob.Archer,
            new JobStatModifier
            {
                HpMultiplier = 0.9f,
                AttackMultiplier = 1.3f,
                DefenseMultiplier = 0.8f,
                SpeedMultiplier = 1.4f,
                AccuracyMultiplier = 1.3f,  // アーチャーは最高の命中率（弓術の精度）
                EvasionMultiplier = 1.5f,   // アーチャーは最高の回避率（機動性重視）
                CriticalRateMultiplier = 15.0f, // シーフ系のクリティカル攻撃率は高く15%
                HpBonus = -10,
                AttackBonus = 3,
                DefenseBonus = -2,
                SpeedBonus = 1,
                AccuracyBonus = 15,
                EvasionBonus = 10,
                CriticalRateBonus = 9
            }
        }
    };

    /// <summary>
    /// Enemy job stat modifiers
    /// </summary>
    public static readonly Dictionary<EnemyJob, JobStatModifier> EnemyJobModifiers = new Dictionary<EnemyJob, JobStatModifier>
    {
        {
            EnemyJob.Bruiser,
            new JobStatModifier
            {
                HpMultiplier = 1.2f,
                AttackMultiplier = 1.2f,
                DefenseMultiplier = 1.0f,
                SpeedMultiplier = 1.0f,
                AccuracyMultiplier = 1.0f,  // ブルーザーは標準的な命中率
                EvasionMultiplier = 1.0f,    // ブルーザーは標準的な回避率
                CriticalRateMultiplier = 3.0f, // 戦士系のクリティカル攻撃率は中程度（3%）
                HpBonus = 30,
                AttackBonus = 6,
                DefenseBonus = 1,
                SpeedBonus = 0,
                AccuracyBonus = 0,
                EvasionBonus = 0,
                CriticalRateBonus = 2
            }
        },
        {
            EnemyJob.Guardian,
            new JobStatModifier
            {
                HpMultiplier = 1.4f,
                AttackMultiplier = 0.7f,
                DefenseMultiplier = 1.6f,
                SpeedMultiplier = 0.6f,
                AccuracyMultiplier = 0.85f,  // ガーディアンは低い命中率（重装備のため）
                EvasionMultiplier = 0.6f,    // ガーディアンは最低回避率（重装備）
                CriticalRateMultiplier = 3.0f, // 戦士系のクリティカル攻撃率は中程度（3%）
                HpBonus = 100,
                AttackBonus = -2,
                DefenseBonus = 20,
                SpeedBonus = -1,
                AccuracyBonus = -8,
                EvasionBonus = -10,
                CriticalRateBonus = 2
            }
        },
        {
            EnemyJob.Assassin,
            new JobStatModifier
            {
                HpMultiplier = 0.7f,
                AttackMultiplier = 1.2f,
                DefenseMultiplier = 0.6f,
                SpeedMultiplier = 1.2f,
                AccuracyMultiplier = 1.15f,  // アサシンは高い命中率（精密攻撃）
                EvasionMultiplier = 1.4f,    // アサシンは高い回避率（機動性）
                CriticalRateMultiplier = 10.0f, // シーフ系のクリティカル攻撃率は高く10%
                HpBonus = -30,
                AttackBonus = 6,
                DefenseBonus = -4,
                SpeedBonus = 1,
                AccuracyBonus = 8,
                EvasionBonus = 12,
                CriticalRateBonus = 9
            }
        },
        {
            EnemyJob.Caster,
            new JobStatModifier
            {
                HpMultiplier = 0.8f,
                AttackMultiplier = 1.4f,
                DefenseMultiplier = 0.7f,
                SpeedMultiplier = 0.9f,
                AccuracyMultiplier = 1.1f,   // キャスターは高い命中率（魔法の精度）
                EvasionMultiplier = 0.9f,    // キャスターは少し低い回避率（運動性低め）
                CriticalRateMultiplier = 1.0f, // 魔法のクリティカル攻撃率は低く1%
                HpBonus = -20,
                AttackBonus = 9,
                DefenseBonus = -3,
                SpeedBonus = 0,
                AccuracyBonus = 5,
                EvasionBonus = -2,
                CriticalRateBonus = 0
            }
        }
    };

    /// <summary>
    /// Current damage calculation formula setting
    /// </summary>
    public static DamageCalculationFormula CurrentDamageFormula { get; set; } = DamageCalculationFormula.Standard;
}
