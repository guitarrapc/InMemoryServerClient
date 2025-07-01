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
    public static readonly StatusRange PlayerHp = new (330, 400);

    /// <summary>
    /// Attack power range for player
    /// </summary>
    public static readonly StatusRange PlayerAttackPower = new (15, 26);

    /// <summary>
    /// Defense power range for player
    /// </summary>
    public static readonly StatusRange PlayerDefencePower = new(10, 22);

    /// <summary>
    /// Movement speed range for player
    /// </summary>
    public static readonly StatusRange PlayerMoveSpeed = new(1, 4);

    // Enemy Status
    // Enemies get slightly weaker stats for balance

    /// <summary>
    /// Enemy types and their HP ranges
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyHpByType = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (50, 80) },
        { EnemyType.Medium, new (100, 150) },
        { EnemyType.Large, new (200, 300) }
    };

    /// <summary>
    /// Attack power range for enemy
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyAttackPower = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (10, 15) },
        { EnemyType.Medium, new (15, 22) },
        { EnemyType.Large, new (20, 30) }
    };

    /// <summary>
    /// Defense power range for enemy
    /// </summary>
    public static readonly Dictionary<EnemyType, StatusRange> EnemyDefencePower = new Dictionary<EnemyType, StatusRange>
    {
        { EnemyType.Small, new (5, 10) },
        { EnemyType.Medium, new (8, 13) },
        { EnemyType.Large, new (12, 20) }
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

