using BattleLogic.Models;

namespace BattleLogic.Interfaces;

/// <summary>
/// Interface for battle initialization logic
/// </summary>
public interface IBattleInitializer
{
    /// <summary>
    /// Initialize players for battle
    /// </summary>
    /// <param name="playerCount">Number of players to create</param>
    /// <param name="battleLogs">List to append initialization logs</param>
    /// <returns>List of initialized player entities</returns>
    List<EntityInfo> InitializePlayers(int playerCount, List<string> battleLogs);

    /// <summary>
    /// Initialize enemies for battle
    /// </summary>
    /// <param name="battleLogs">List to append initialization logs</param>
    /// <returns>List of initialized enemy entities</returns>
    List<EntityInfo> InitializeEnemies(List<string> battleLogs);
}
