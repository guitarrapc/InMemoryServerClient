using BattleLogic.Models;

namespace BattleLogic.Interfaces;

/// <summary>
/// Interface for handling battle combat logic
/// </summary>
public interface IBattleCombat
{
    /// <summary>
    /// Execute attack between attacker and defender
    /// </summary>
    /// <param name="attacker">Attacking entity</param>
    /// <param name="defender">Defending entity</param>
    /// <param name="players">List of all players</param>
    /// <param name="enemies">List of all enemies</param>
    /// <param name="battleLogs">Battle logs to append combat information</param>
    void ExecuteAttack(EntityInfo attacker, EntityInfo defender, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs);

    /// <summary>
    /// Execute defend action for entity
    /// </summary>
    /// <param name="entity">Entity to defend</param>
    /// <param name="players">List of all players</param>
    /// <param name="enemies">List of all enemies</param>
    /// <param name="battleLogs">Battle logs to append defense information</param>
    void ExecuteDefend(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs);
}
