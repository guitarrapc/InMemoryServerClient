namespace BattleLogic.Contracts;

/// <summary>
/// Interface for AI decision making in battle
/// </summary>
public interface IBattleAI
{
    /// <summary>
    /// Decide what action an entity should take
    /// </summary>
    /// <param name="entity">The entity making the decision</param>
    /// <param name="players">List of all players</param>
    /// <param name="enemies">List of all enemies</param>
    /// <param name="battleField">The battle field instance</param>
    /// <returns>Tuple of action type and target entity</returns>
    (string action, EntityInfo? target) DecideAction(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, IBattleField battleField);
}
