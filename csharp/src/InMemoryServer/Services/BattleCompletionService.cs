using Shared.Models;
using BattleLogic.Battle;

namespace InMemoryServer.Services;

/// <summary>
/// Service responsible for handling battle completion and group cleanup
/// </summary>
public class BattleCompletionService
{
    private readonly ILogger<BattleCompletionService> logger;
    private readonly IGroupManager groupManager;
    private readonly CrossProtocolNotificationService notificationService;

    public BattleCompletionService(
        ILogger<BattleCompletionService> logger,
        IGroupManager groupManager,
        CrossProtocolNotificationService notificationService)
    {
        this.logger = logger;
        this.groupManager = groupManager;
        this.notificationService = notificationService;
    }

    /// <summary>
    /// Handle battle completion with unified cleanup across all protocols
    /// </summary>
    /// <param name="group">The group that completed the battle</param>
    /// <param name="battle">The completed battle state</param>
    /// <param name="battleId">Battle ID</param>
    /// <param name="seed">Battle seed</param>
    /// <param name="shouldDissolveGroup">Override for dissolve strategy. If null, uses configuration setting.</param>
    public async Task HandleBattleCompletionAsync(
        GroupInfo group,
        BattleState battle,
        Guid battleId,
        int seed)
    {
        try
        {
            // 1. Send battle completed notification
            await notificationService.NotifyGroupAsync(
                group.GroupId,
                group.ClientIds,
                "BattleCompleted",
                battle.GetStatus());

            logger.LogInformation(
                "Battle {BattleId} (Seed: {Seed}): All replay data sent, battle marked as completed",
                battleId,
                seed);

            // 2. Clear battle data to free memory
            battle.ClearBattleData();

            // 3. Handle group cleanup
            await groupManager.DissolveGroupAsync(
                group.GroupId,
                "Battle completed - group dissolved");

            logger.LogInformation(
                "Battle {BattleId} (Seed: {Seed}): Group {GroupId} dissolved after battle completion",
                battleId,
                seed,
                group.GroupId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error handling battle completion for battle {BattleId} (Seed: {Seed}) in group {GroupId}",
                battleId,
                seed,
                group.GroupId);
            throw;
        }
    }
}
