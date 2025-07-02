namespace Shared;

/// <summary>
/// Basic entity type enum for client-server communication
/// </summary>
public enum EntityType
{
    Player,
    Enemy
}

/// <summary>
/// Enemy size categorization
/// </summary>
public enum EnemySize
{
    Small,
    Medium,
    Large
}

/// <summary>
/// Complete entity type information combining type and enemy size
/// </summary>
public readonly record struct EntityTypeInfo(EntityType Type, EnemySize? EnemySize = null)
{
    /// <summary>
    /// Gets whether this entity is a player
    /// </summary>
    public bool IsPlayer => Type == EntityType.Player;

    /// <summary>
    /// Gets whether this entity is an enemy
    /// </summary>
    public bool IsEnemy => Type == EntityType.Enemy;

    /// <summary>
    /// Creates a player entity type
    /// </summary>
    public static EntityTypeInfo Player => new(EntityType.Player);

    /// <summary>
    /// Creates a small enemy entity type
    /// </summary>
    public static EntityTypeInfo SmallEnemy => new(EntityType.Enemy, Shared.EnemySize.Small);

    /// <summary>
    /// Creates a medium enemy entity type
    /// </summary>
    public static EntityTypeInfo MediumEnemy => new(EntityType.Enemy, Shared.EnemySize.Medium);

    /// <summary>
    /// Creates a large enemy entity type
    /// </summary>
    public static EntityTypeInfo LargeEnemy => new(EntityType.Enemy, Shared.EnemySize.Large);

    /// <summary>
    /// Returns a string representation of the entity type
    /// </summary>
    public override string ToString() => Type switch
    {
        EntityType.Player => "Player",
        EntityType.Enemy when EnemySize.HasValue => $"{EnemySize}Enemy",
        EntityType.Enemy => "Enemy",
        _ => "Unknown"
    };
}

/// <summary>
/// Unified job type enum for client-server communication
/// </summary>
public enum JobType
{
    // Player jobs
    Tank,
    Warrior,
    Mage,
    Archer,

    // Enemy jobs
    Bruiser,
    Guardian,
    Assassin,
    Caster
}

/// <summary>
/// Legacy PlayerJob enum for backward compatibility
/// </summary>
public enum PlayerJob
{
    Tank,
    Warrior,
    Mage,
    Archer
}

/// <summary>
/// Legacy EnemyJob enum for backward compatibility
/// </summary>
public enum EnemyJob
{
    Bruiser,    // 近接攻撃型、HP・攻撃重視
    Guardian,   // 防御重視型、HP・防御重視
    Assassin,   // 速度・攻撃特化型
    Caster      // 遠距離攻撃型、攻撃・速度重視
}

/// <summary>
/// Battle state management enum
/// </summary>
public enum BattleStateType
{
    Connected = 0,
    Ready,
    ReplayCompleted
}
