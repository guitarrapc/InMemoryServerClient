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

    // Enemy Status
    // Enemies get slightly weaker stats for balance

    /// <summary>
    /// Enemy types and their HP ranges
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyHpByType = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (50, 80) },
        { EnemyType.Medium, new (100, 150) },
        { EnemyType.Large, new (250, 330) }
    };

    /// <summary>
    /// Attack power range for enemy
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyAttackPower = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (10, 15) },
        { EnemyType.Medium, new (15, 22) },
        { EnemyType.Large, new (23, 30) }
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
    /// Defense damage reduction percentage
    /// </summary>
    public const int DefenseDamageReductionPercent = 50;

    /// <summary>
    /// Minimum number of enemies in battle
    /// </summary>
    public const int MinEnemyCount = 15;

    /// <summary>
    /// Maximum number of enemies in battle
    /// </summary>
    public const int MaxEnemyCount = 20;

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
                hpBonus: 80,
                attackBonus: 0,
                defenseBonus: 10,
                speedBonus: -1
            )
        },
        {
            PlayerJob.Warrior,
            new JobStatModifier(
                hpMultiplier: 1.1f,
                attackMultiplier: 1.2f,
                defenseMultiplier: 1.0f,
                speedMultiplier: 1.2f,
                hpBonus: 30,
                attackBonus: 10,
                defenseBonus: 0,
                speedBonus: 1
            )
        },
        {
            PlayerJob.Mage,
            new JobStatModifier(
                hpMultiplier: 0.8f,
                attackMultiplier: 1.4f,
                defenseMultiplier: 0.7f,
                speedMultiplier: 0.9f,
                hpBonus: -50,
                attackBonus: 8,
                defenseBonus: -3,
                speedBonus: 0
            )
        },
        {
            PlayerJob.Archer,
            new JobStatModifier(
                hpMultiplier: 0.9f,
                attackMultiplier: 1.3f,
                defenseMultiplier: 0.8f,
                speedMultiplier: 1.4f,
                hpBonus: -20,
                attackBonus: 3,
                defenseBonus: -2,
                speedBonus: 1
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
                hpBonus: 30,
                attackBonus: 4,
                defenseBonus: 1,
                speedBonus: 0
            )
        },
        {
            EnemyJob.Guardian,
            new JobStatModifier(
                hpMultiplier: 1.4f,
                attackMultiplier: 0.7f,
                defenseMultiplier: 1.6f,
                speedMultiplier: 0.6f,
                hpBonus: 100,
                attackBonus: -2,
                defenseBonus: 10,
                speedBonus: -1
            )
        },
        {
            EnemyJob.Assassin,
            new JobStatModifier(
                hpMultiplier: 0.7f,
                attackMultiplier: 1.2f,
                defenseMultiplier: 0.6f,
                speedMultiplier: 1.5f,
                hpBonus: -30,
                attackBonus: 6,
                defenseBonus: -4,
                speedBonus: 1
            )
        },
        {
            EnemyJob.Caster,
            new JobStatModifier(
                hpMultiplier: 0.8f,
                attackMultiplier: 1.4f,
                defenseMultiplier: 0.7f,
                speedMultiplier: 0.9f,
                hpBonus: -20,
                attackBonus: 9,
                defenseBonus: -3,
                speedBonus: 0
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

    public JobStatModifier(
        float hpMultiplier,
        float attackMultiplier,
        float defenseMultiplier,
        float speedMultiplier,
        int hpBonus,
        int attackBonus,
        int defenseBonus,
        int speedBonus)
    {
        HpMultiplier = hpMultiplier;
        AttackMultiplier = attackMultiplier;
        DefenseMultiplier = defenseMultiplier;
        SpeedMultiplier = speedMultiplier;
        HpBonus = hpBonus;
        AttackBonus = attackBonus;
        DefenseBonus = defenseBonus;
        SpeedBonus = speedBonus;
    }
}
