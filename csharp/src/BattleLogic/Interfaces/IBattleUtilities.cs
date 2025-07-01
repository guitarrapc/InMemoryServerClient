using BattleLogic.Models;

namespace BattleLogic.Interfaces;

/// <summary>
/// Interface for battle utilities and helper functions
/// </summary>
public interface IBattleUtilities
{
    /// <summary>
    /// Update entity position in the appropriate list
    /// </summary>
    /// <param name="entity">Entity to update</param>
    /// <param name="newPosition">New position</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    void UpdateEntityPosition(EntityInfo entity, Vector2 newPosition, List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Update entity HP in the appropriate list
    /// </summary>
    /// <param name="entity">Entity to update</param>
    /// <param name="newHp">New HP value</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    void UpdateEntityHp(EntityInfo entity, int newHp, List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Update entity defending status
    /// </summary>
    /// <param name="entity">Entity to update</param>
    /// <param name="isDefending">Defending status</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    void UpdateEntityDefending(EntityInfo entity, bool isDefending, List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Find an adjacent target for attack
    /// </summary>
    /// <param name="entity">Entity looking for target</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    /// <param name="battleField">Battle field instance</param>
    /// <returns>Adjacent target entity, or null if none found</returns>
    EntityInfo? FindAdjacentTarget(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, IBattleField battleField);

    /// <summary>
    /// Find the nearest target
    /// </summary>
    /// <param name="entity">Entity looking for target</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    /// <returns>Nearest target entity, or null if none found</returns>
    EntityInfo? FindNearestTarget(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Find the target with lowest HP
    /// </summary>
    /// <param name="entity">Entity looking for target</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    /// <returns>Lowest HP target entity, or null if none found</returns>
    EntityInfo? FindLowestHpTarget(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Check if there are enemies within the specified distance threshold
    /// </summary>
    /// <param name="entity">Entity to check from</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    /// <param name="distanceThreshold">Maximum distance to consider as nearby</param>
    /// <returns>True if enemies are nearby</returns>
    bool AreEnemiesNearby(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, int distanceThreshold);

    /// <summary>
    /// Check if the entity can surround an enemy
    /// </summary>
    /// <param name="entity">Entity attempting to surround</param>
    /// <param name="target">Target entity to surround</param>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    /// <returns>True if surrounding is possible</returns>
    bool CanSurroundEnemy(EntityInfo entity, EntityInfo target, List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Calculate Manhattan distance between two points
    /// </summary>
    /// <param name="a">First point</param>
    /// <param name="b">Second point</param>
    /// <returns>Manhattan distance</returns>
    int CalculateManhattanDistance(Vector2 a, Vector2 b);

    /// <summary>
    /// Check if battle is over
    /// </summary>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    /// <returns>Tuple indicating if battle is over and if players won</returns>
    (bool isOver, bool isPlayerVictory) CheckBattleOver(List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Reset defending status for all entities
    /// </summary>
    /// <param name="players">List of players</param>
    /// <param name="enemies">List of enemies</param>
    void ResetDefendingStatus(List<EntityInfo> players, List<EntityInfo> enemies);
}
