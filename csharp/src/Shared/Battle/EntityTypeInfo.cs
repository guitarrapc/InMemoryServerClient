namespace Shared.Battle;

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
    public static EntityTypeInfo SmallEnemy => new(EntityType.Enemy, Battle.EnemySize.Small);

    /// <summary>
    /// Creates a medium enemy entity type
    /// </summary>
    public static EntityTypeInfo MediumEnemy => new(EntityType.Enemy, Battle.EnemySize.Medium);

    /// <summary>
    /// Creates a large enemy entity type
    /// </summary>
    public static EntityTypeInfo LargeEnemy => new(EntityType.Enemy, Battle.EnemySize.Large);

    /// <summary>
    /// Returns a string representation of the entity type
    /// </summary>
    public override string ToString() => Type switch
    {
        EntityType.Player => nameof(EntityType.Player),
        EntityType.Enemy when EnemySize.HasValue => $"{EnemySize}{nameof(EntityType.Enemy)}",
        EntityType.Enemy => nameof(EntityType.Enemy),
        _ => "Unknown"
    };
}
