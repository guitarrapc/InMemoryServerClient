using MagicOnion;
using Shared.Battle;

namespace Shared.Contracts.MagicOnion;

/// <summary>
/// MagicOnion service interface for battle operations
/// </summary>
public interface IBattleService : IService<IBattleService>
{
    /// <summary>
    /// Get battle status
    /// </summary>
    /// <returns>Current battle status</returns>
    UnaryResult<BattleStatus?> GetBattleStatusAsync();

    /// <summary>
    /// Execute battle action (currently not implemented for automated battles)
    /// </summary>
    /// <param name="actionType">Type of action to execute</param>
    /// <returns>True if action was accepted</returns>
    UnaryResult<bool> BattleActionAsync(string actionType);

    /// <summary>
    /// Get battle replay data
    /// </summary>
    /// <param name="battleId">Battle ID to get replay for</param>
    /// <returns>Battle replay data as string, null if not found</returns>
    UnaryResult<string?> GetBattleReplayAsync(Guid battleId);

    /// <summary>
    /// Confirm that client has received ConnectionsReady notification
    /// </summary>
    /// <returns>True if confirmation was successful</returns>
    UnaryResult<bool> ConfirmConnectionReadyAsync();

    /// <summary>
    /// Reproduce a battle with specific battle ID and seed
    /// </summary>
    /// <param name="battleId">Battle ID for reproduction</param>
    /// <param name="seedValue">Seed value for reproduction</param>
    /// <param name="groupName">Group name for the reproduction session</param>
    /// <returns>True if reproduction was started successfully</returns>
    UnaryResult<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName);
}
