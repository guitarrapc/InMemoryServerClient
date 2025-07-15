using MagicOnion;
using MagicOnion.Server;
using Shared.Contracts.MagicOnion;
using Shared.Battle;
using Shared.Models;
using BattleLogic.Constans;
using BattleLogic.Models;
using InMemoryServer.Services;

namespace InMemoryServer.Http2Server;

/// <summary>
/// MagicOnion implementation of battle operations
/// Note: Most battle operations require streaming hub for full functionality.
/// </summary>
public class BattleService(
    ILogger<BattleService> logger) : ServiceBase<IBattleService>, IBattleService
{
    /// <summary>
    /// Get battle status (limited functionality in unary service)
    /// </summary>
    public async UnaryResult<BattleStatus?> GetBattleStatusAsync()
    {
        logger.LogWarning("GetBattleStatusAsync called on unary service. Use streaming hub for connection-aware operations.");

        // Return a generic battle status since we can't determine the client's group
        return new BattleStatus
        {
            IsInProgress = false,
            FieldWidth = BattleSystemDefines.BattleFieldWidth,
            FieldHeight = BattleSystemDefines.BattleFieldHeight
        };
    }

    /// <summary>
    /// Execute battle action (currently not implemented for automated battles)
    /// </summary>
    public async UnaryResult<bool> BattleActionAsync(string actionType)
    {
        logger.LogInformation("Battle action {ActionType} requested, but battles are currently automated", actionType);
        return false;
    }

    /// <summary>
    /// Get battle replay data
    /// </summary>
    public async UnaryResult<string?> GetBattleReplayAsync(Guid battleId)
    {
        logger.LogInformation("Battle replay requested for battle: {BattleId}", battleId);

        var replayPath = Path.Combine(BattleSystemDefines.BattleReplayDirectory, $"{battleId}.jsonl");
        if (File.Exists(replayPath))
        {
            // Ensure directory exists for battle replays
            Directory.CreateDirectory(BattleSystemDefines.BattleReplayDirectory);

            try
            {
                // Use memory-efficient file reading
                return await File.ReadAllTextAsync(replayPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading battle replay file: {ReplayPath}", replayPath);
                return null;
            }
        }
        else
        {
            logger.LogWarning("Battle replay file not found: {ReplayPath}", replayPath);
            return null;
        }
    }

    /// <summary>
    /// Confirm connection ready (not available in unary service)
    /// </summary>
    public async UnaryResult<bool> ConfirmConnectionReadyAsync()
    {
        logger.LogWarning("ConfirmConnectionReadyAsync called on unary service. Use streaming hub for connection-aware operations.");
        return false;
    }

    /// <summary>
    /// Reproduce battle (not available in unary service)
    /// </summary>
    public async UnaryResult<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        logger.LogWarning("ReproduceBattleAsync called on unary service. Use streaming hub for full battle functionality.");
        return false;
    }
}
