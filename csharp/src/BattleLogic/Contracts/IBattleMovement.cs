namespace BattleLogic.Contracts;

/// <summary>
/// Interface for handling entity movement logic in battles
/// </summary>
public interface IBattleMovement
{
    /// <summary>
    /// Move entity towards target or in random direction
    /// </summary>
    /// <param name="entity">Entity to move</param>
    /// <param name="targetEntity">Target entity to move towards (if any)</param>
    /// <param name="players">List of all players</param>
    /// <param name="enemies">List of all enemies</param>
    /// <param name="battleLogs">Battle logs to append movement information</param>
    /// <returns>True if entity successfully moved, false otherwise</returns>
    bool MoveEntity(EntityInfo entity, EntityInfo? targetEntity, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs);
}
