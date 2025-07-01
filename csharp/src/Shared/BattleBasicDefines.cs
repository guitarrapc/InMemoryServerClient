namespace Shared;

public static class BattleBasicDefines
{
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
    public static readonly StatusRange PlayerHp = new (300, 430);

    /// <summary>
    /// Attack power range for player
    /// </summary>
    public static readonly StatusRange PlayerAttackPower = new (25, 34);

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
    public static readonly Dictionary<EnemyType, StatusRange> EnemyHpByType = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (55, 85) },
        { EnemyType.Medium, new (110, 160) },
        { EnemyType.Large, new (265, 350) }
    };

    /// <summary>
    /// Attack power range for enemy
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyAttackPower = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (11, 16) },
        { EnemyType.Medium, new (16, 24) },
        { EnemyType.Large, new (24, 32) }
    };

    /// <summary>
    /// Defense power range for enemy
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyDefencePower = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (5, 10) },
        { EnemyType.Medium, new (8, 13) },
        { EnemyType.Large, new (15, 20) }
    };

    /// <summary>
    /// Movement speed range for enemy
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyMoveSpeed = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (1, 1) },
        { EnemyType.Medium, new (1, 2) },
        { EnemyType.Large, new (1, 3) }
    };

    /// <summary>
    /// Accuracy range for enemy
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyAccuracy = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (65, 80) },
        { EnemyType.Medium, new (70, 85) },
        { EnemyType.Large, new (75, 90) }
    };

    /// <summary>
    /// Evasion range for enemy (small enemies have higher evasion)
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyEvasion = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (25, 40) },   // 高回避
        { EnemyType.Medium, new (15, 30) },  // 中回避
        { EnemyType.Large, new (5, 20) }     // 低回避
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

    // Flavor Helper Methods
    // These methods apply random variations to combat parameters

    /// <summary>
    /// Apply random flavor variation to attack power
    /// </summary>
    /// <param name="baseAttack">Base attack value</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Attack value with random flavor applied</returns>
    public static int ApplyAttackFlavor(int baseAttack, Random random)
    {
        var variation = Math.Max(MinAbsoluteFlavor, (int)(baseAttack * AttackFlavorPercent));
        var flavorRange = variation * 2 + 1; // -variation to +variation
        var flavor = random.Next(flavorRange) - variation;
        return Math.Max(1, baseAttack + flavor); // Ensure minimum 1 attack
    }

    /// <summary>
    /// Apply random flavor variation to defense power
    /// </summary>
    /// <param name="baseDefense">Base defense value</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Defense value with random flavor applied</returns>
    public static int ApplyDefenseFlavor(int baseDefense, Random random)
    {
        var variation = Math.Max(MinAbsoluteFlavor, (int)(baseDefense * DefenseFlavorPercent));
        var flavorRange = variation * 2 + 1; // -variation to +variation
        var flavor = random.Next(flavorRange) - variation;
        return Math.Max(0, baseDefense + flavor); // Defense can be 0
    }

    /// <summary>
    /// Apply random flavor variation to accuracy
    /// </summary>
    /// <param name="baseAccuracy">Base accuracy percentage</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Accuracy percentage with random flavor applied (minimum 0, no maximum limit)</returns>
    public static int ApplyAccuracyFlavor(int baseAccuracy, Random random)
    {
        var flavorRange = AccuracyFlavorRange * 2 + 1; // -range to +range
        var flavor = random.Next(flavorRange) - AccuracyFlavorRange;
        return Math.Max(0, baseAccuracy + flavor); // Keep minimum 0, but allow exceeding 100%
    }

    /// <summary>
    /// Apply random flavor variation to evasion
    /// </summary>
    /// <param name="baseEvasion">Base evasion percentage</param>
    /// <param name="random">Random number generator</param>
    /// <returns>Evasion percentage with random flavor applied (minimum 0, no maximum limit)</returns>
    public static int ApplyEvasionFlavor(int baseEvasion, Random random)
    {
        var flavorRange = EvasionFlavorRange * 2 + 1; // -range to +range
        var flavor = random.Next(flavorRange) - EvasionFlavorRange;
        return Math.Max(0, baseEvasion + flavor); // Keep minimum 0, but allow exceeding 100%
    }

    /// <summary>
    /// Player job stat modifiers
    /// </summary>
    public static readonly Dictionary<PlayerJob, JobStatModifier> PlayerJobModifiers = new Dictionary<PlayerJob, JobStatModifier>
    {
        {
            PlayerJob.Tank,
            new JobStatModifier(
                hpMultiplier: 1.3f,
                attackMultiplier: 0.8f,
                defenseMultiplier: 1.5f,
                speedMultiplier: 0.7f,
                accuracyMultiplier: 0.9f,  // タンクは命中率がやや低い
                evasionMultiplier: 0.6f,   // タンクは低回避率（重装備）
                hpBonus: 80,
                attackBonus: 0,
                defenseBonus: 10,
                speedBonus: -1,
                accuracyBonus: -5,
                evasionBonus: -10
            )
        },
        {
            PlayerJob.Warrior,
            new JobStatModifier(
                hpMultiplier: 1.1f,
                attackMultiplier: 1.2f,
                defenseMultiplier: 1.0f,
                speedMultiplier: 1.2f,
                accuracyMultiplier: 1.0f,  // ウォリアーは標準的な命中率
                evasionMultiplier: 1.0f,   // ウォリアーは標準的な回避率
                hpBonus: 30,
                attackBonus: 10,
                defenseBonus: 0,
                speedBonus: 1,
                accuracyBonus: 0,
                evasionBonus: 0
            )
        },
        {
            PlayerJob.Mage,
            new JobStatModifier(
                hpMultiplier: 0.8f,
                attackMultiplier: 1.4f,
                defenseMultiplier: 0.7f,
                speedMultiplier: 0.9f,
                accuracyMultiplier: 1.2f,  // メイジは高い命中率（魔法の精密性）
                evasionMultiplier: 0.8f,   // メイジは低い回避率（運動性が低い）
                hpBonus: -50,
                attackBonus: 8,
                defenseBonus: -3,
                speedBonus: 0,
                accuracyBonus: 10,
                evasionBonus: -5
            )
        },
        {
            PlayerJob.Archer,
            new JobStatModifier(
                hpMultiplier: 0.9f,
                attackMultiplier: 1.3f,
                defenseMultiplier: 0.8f,
                speedMultiplier: 1.4f,
                accuracyMultiplier: 1.3f,  // アーチャーは最高の命中率（弓術の精度）
                evasionMultiplier: 1.5f,   // アーチャーは最高の回避率（機動性重視）
                hpBonus: -20,
                attackBonus: 3,
                defenseBonus: -2,
                speedBonus: 1,
                accuracyBonus: 15,
                evasionBonus: 10
            )
        }
    };

    /// <summary>
    /// Enemy job stat modifiers
    /// </summary>
    public static readonly Dictionary<EnemyJob, JobStatModifier> EnemyJobModifiers = new Dictionary<EnemyJob, JobStatModifier>
    {
        {
            EnemyJob.Bruiser,
            new JobStatModifier(
                hpMultiplier: 1.2f,
                attackMultiplier: 1.1f,
                defenseMultiplier: 1.0f,
                speedMultiplier: 1.0f,
                accuracyMultiplier: 1.0f,  // ブルーザーは標準的な命中率
                evasionMultiplier: 1.0f,    // ブルーザーは標準的な回避率
                hpBonus: 30,
                attackBonus: 4,
                defenseBonus: 1,
                speedBonus: 0,
                accuracyBonus: 0,
                evasionBonus: 0
            )
        },
        {
            EnemyJob.Guardian,
            new JobStatModifier(
                hpMultiplier: 1.4f,
                attackMultiplier: 0.7f,
                defenseMultiplier: 1.6f,
                speedMultiplier: 0.6f,
                accuracyMultiplier: 0.85f,  // ガーディアンは低い命中率（重装備のため）
                evasionMultiplier: 0.6f,    // ガーディアンは最低回避率（重装備）
                hpBonus: 100,
                attackBonus: -2,
                defenseBonus: 10,
                speedBonus: -1,
                accuracyBonus: -8,
                evasionBonus: -10
            )
        },
        {
            EnemyJob.Assassin,
            new JobStatModifier(
                hpMultiplier: 0.7f,
                attackMultiplier: 1.2f,
                defenseMultiplier: 0.6f,
                speedMultiplier: 1.5f,
                accuracyMultiplier: 1.15f,  // アサシンは高い命中率（精密攻撃）
                evasionMultiplier: 1.4f,    // アサシンは高い回避率（機動性）
                hpBonus: -30,
                attackBonus: 6,
                defenseBonus: -4,
                speedBonus: 1,
                accuracyBonus: 8,
                evasionBonus: 12
            )
        },
        {
            EnemyJob.Caster,
            new JobStatModifier(
                hpMultiplier: 0.8f,
                attackMultiplier: 1.4f,
                defenseMultiplier: 0.7f,
                speedMultiplier: 0.9f,
                accuracyMultiplier: 1.1f,   // キャスターは高い命中率（魔法の精度）
                evasionMultiplier: 0.9f,    // キャスターは少し低い回避率（運動性低め）
                hpBonus: -20,
                attackBonus: 9,
                defenseBonus: -3,
                speedBonus: 0,
                accuracyBonus: 5,
                evasionBonus: -2
            )
        }
    };
}

public readonly record struct StatusRange
{
    public int Min { get; init; }
    public int Max { get; init; }
    public StatusRange(int min, int max)
    {
        Min = min;
        Max = max;
    }
}

public enum EnemyType
{
    Small,
    Medium,
    Large
}

public enum PlayerJob
{
    Tank,
    Warrior,
    Mage,
    Archer
}

public enum EnemyJob
{
    Bruiser,    // 近接攻撃型、HP・攻撃重視
    Guardian,   // 防御重視型、HP・防御重視
    Assassin,   // 速度・攻撃特化型
    Caster      // 遠距離攻撃型、攻撃・速度重視
}

/// <summary>
/// Job stat modifier
/// </summary>
public readonly record struct JobStatModifier
{
    /// <summary>
    /// HP multiplier
    /// </summary>
    public float HpMultiplier { get; init; }

    /// <summary>
    /// Attack multiplier
    /// </summary>
    public float AttackMultiplier { get; init; }

    /// <summary>
    /// Defense multiplier
    /// </summary>
    public float DefenseMultiplier { get; init; }

    /// <summary>
    /// Speed multiplier
    /// </summary>
    public float SpeedMultiplier { get; init; }

    /// <summary>
    /// Accuracy multiplier
    /// </summary>
    public float AccuracyMultiplier { get; init; }

    /// <summary>
    /// Evasion multiplier
    /// </summary>
    public float EvasionMultiplier { get; init; }

    /// <summary>
    /// HP bonus (flat addition)
    /// </summary>
    public int HpBonus { get; init; }

    /// <summary>
    /// Attack bonus (flat addition)
    /// </summary>
    public int AttackBonus { get; init; }

    /// <summary>
    /// Defense bonus (flat addition)
    /// </summary>
    public int DefenseBonus { get; init; }

    /// <summary>
    /// Speed bonus (flat addition)
    /// </summary>
    public int SpeedBonus { get; init; }

    /// <summary>
    /// Accuracy bonus (flat addition, 0-100)
    /// </summary>
    public int AccuracyBonus { get; init; }

    /// <summary>
    /// Evasion bonus (flat addition, 0-100)
    /// </summary>
    public int EvasionBonus { get; init; }

    public JobStatModifier(
        float hpMultiplier,
        float attackMultiplier,
        float defenseMultiplier,
        float speedMultiplier,
        float accuracyMultiplier,
        float evasionMultiplier,
        int hpBonus,
        int attackBonus,
        int defenseBonus,
        int speedBonus,
        int accuracyBonus,
        int evasionBonus)
    {
        HpMultiplier = hpMultiplier;
        AttackMultiplier = attackMultiplier;
        DefenseMultiplier = defenseMultiplier;
        SpeedMultiplier = speedMultiplier;
        AccuracyMultiplier = accuracyMultiplier;
        EvasionMultiplier = evasionMultiplier;
        HpBonus = hpBonus;
        AttackBonus = attackBonus;
        DefenseBonus = defenseBonus;
        SpeedBonus = speedBonus;
        AccuracyBonus = accuracyBonus;
        EvasionBonus = evasionBonus;
    }
}
