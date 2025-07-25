using Shared.Common;
using System.Text.Json.Serialization;

namespace Shared.Battle;

/// <summary>
/// Entity information for client-server communication
/// </summary>
[MessagePackObject(true)]
public readonly record struct EntityInfo
{
    /// <summary>
    /// Entity unique identifier
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Entity name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Entity type information (player or enemy with size)
    /// </summary>
    public required EntityTypeInfo Type { get; init; }

    /// <summary>
    /// Player job type (only set for players)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlayerJob? PlayerJob { get; init; }

    /// <summary>
    /// Enemy job type (only set for enemies)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EnemyJob? EnemyJob { get; init; }

    /// <summary>
    /// Current HP
    /// </summary>
    public required int CurrentHp { get; init; }

    /// <summary>
    /// Maximum HP
    /// </summary>
    public required int MaxHp { get; init; }

    /// <summary>
    /// Attack power
    /// </summary>
    public required int Attack { get; init; }

    /// <summary>
    /// Defense power
    /// </summary>
    public required int Defense { get; init; }

    /// <summary>
    /// Movement speed
    /// </summary>
    public required int Speed { get; init; }

    /// <summary>
    /// Accuracy (hit rate, 0-100)
    /// </summary>
    public required int Accuracy { get; init; }

    /// <summary>
    /// Evasion (dodge rate, 0-100)
    /// </summary>
    public required int Evasion { get; init; }

    /// <summary>
    /// Critical hit rate (0-100)
    /// </summary>
    public required int CriticalRate { get; init; }

    /// <summary>
    /// Position on the battle field
    /// </summary>
    public required Vector2 Position { get; init; }

    /// <summary>
    /// Is defending (damage reduction)
    /// </summary>
    public required bool IsDefending { get; init; }
}
