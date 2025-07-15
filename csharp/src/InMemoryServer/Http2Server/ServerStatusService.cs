using MagicOnion;
using MagicOnion.Server;
using Shared.Contracts.MagicOnion;
using Shared.Models;
using Shared.Battle;
using InMemoryServer.Services;

namespace InMemoryServer.Http2Server;

/// <summary>
/// MagicOnion implementation of server status operations
/// </summary>
public class ServerStatusService(
    ILogger<ServerStatusService> logger,
    InMemoryState state,
    GroupManager groupManager) : ServiceBase<IServerStatusService>, IServerStatusService
{
    /// <summary>
    /// Get server status
    /// </summary>
    public async UnaryResult<ServerStatus> GetServerStatusAsync()
    {
        logger.LogInformation("Client requested server status");

        var status = new ServerStatus
        {
            Uptime = DateTime.UtcNow - state.StartTime,
            TotalConnections = state.ConnectionCount,
            GroupCount = groupManager.GetAllGroups().Count(),
            ActiveBattleCount = state.BattleStates.Count
        };

        // Get group summaries
        foreach (var group in groupManager.GetAllGroups())
        {
            status.Groups.Add(new GroupSummary
            {
                GroupId = group.GroupId,
                Name = group.Name,
                ConnectionCount = group.ConnectionCount,
                BattleId = group.BattleId
            });
        }

        // Get battle summaries
        foreach (var battleEntry in state.BattleStates)
        {
            var battleState = battleEntry.Value;
            var battleStatus = battleState.GetStatus();

            status.ActiveBattles.Add(new BattleSummary
            {
                BattleId = battleEntry.Key,
                GroupId = battleState.GroupId,
                CurrentTurn = battleStatus.CurrentTurn,
                PlayerCount = battleStatus.Players.Count,
                EnemyCount = battleStatus.Enemies.Count,
                StartedAt = battleState.StartTime
            });
        }

        return status;
    }
}
