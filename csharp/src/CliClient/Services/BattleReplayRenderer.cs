using Microsoft.Extensions.Logging;
using CliClient.Constants;
using CliClient.Extensions;
using CliClient.Models;
using Shared.BattleLogic.Models;

namespace CliClient.Services;

/// <summary>
/// Service for rendering battle replay data and handling replay playback logic
/// This service abstracts the common replay rendering logic from communication-specific clients
/// </summary>
public class BattleReplayRenderer
{
    private readonly ILogger _logger;

    public BattleReplayRenderer(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reconstruct complete replay data from chunks
    /// </summary>
    /// <param name="replayChunks">Dictionary of chunk index to battle status list</param>
    /// <param name="expectedTotalChunks">Expected total number of chunks</param>
    /// <returns>Complete list of battle statuses</returns>
    public List<BattleStatus> ReconstructReplayData(Dictionary<int, List<BattleStatus>> replayChunks, int expectedTotalChunks)
    {
        List<BattleStatus> battleStatuses = [];
        for (int i = 0; i < expectedTotalChunks; i++)
        {
            if (replayChunks.TryGetValue(i, out var chunk))
            {
                battleStatuses.AddRange(chunk);
            }
        }
        return battleStatuses;
    }

    /// <summary>
    /// Play battle replay with visual rendering and timing
    /// </summary>
    /// <param name="battleStatuses">Complete battle replay data</param>
    /// <param name="battleId">Battle ID for logging</param>
    /// <param name="seed">Battle seed for logging</param>
    /// <param name="battleSummary">Optional battle summary for enhanced final results</param>
    public async Task PlayReplayAsync(List<BattleStatus> battleStatuses, Guid battleId, int? seed, BattleReplaySummary? battleSummary = null)
    {
        var seedValue = seed ?? 0;

        _logger.LogBattleInfo(new BattleLogMessages.ReplayStarting(battleStatuses.Count, BattleReplayDefines.ReplayFps, battleId.ToString(), seedValue));
        _logger.LogInformation("[BATTLE REPLAY] ========== Starting Battle Replay ==========");

        // Play battle replay frame by frame
        for (int i = 0; i < battleStatuses.Count; i++)
        {
            var status = battleStatuses[i];
            DisplayBattleStatus(status, i + 1, battleStatuses.Count);
            await Task.Delay(BattleReplayDefines.ReplayFrameTimeMs);
        }

        // Display final results
        await DisplayFinalResultsAsync(battleStatuses, battleId, seedValue, battleSummary);
    }

    /// <summary>
    /// Display final battle results with victory/defeat analysis
    /// </summary>
    private async Task DisplayFinalResultsAsync(List<BattleStatus> battleStatuses, Guid battleId, int seedValue, BattleReplaySummary? battleSummary)
    {
        var finalStatus = battleStatuses.Last();
        var finalAlivePlayers = finalStatus.Players.Count(p => p.IsAlive);
        var finalAliveEnemies = finalStatus.Enemies.Count(e => e.IsAlive);

        _logger.LogInformation("[BATTLE REPLAY] ========== Battle Replay Completed! ==========");

        // Display victory/defeat status
        if (finalAliveEnemies == 0)
        {
            _logger.LogInformation("[BATTLE REPLAY] 🎉 Victory! All enemies defeated! 🎉");
            _logger.LogInformation("[BATTLE REPLAY] Surviving players: {AlivePlayers}/{TotalPlayers}", finalAlivePlayers, finalStatus.Players.Count);

            // Show surviving players stats
            foreach (var player in finalStatus.Players.Where(p => p.IsAlive))
            {
                var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                _logger.LogInformation("[BATTLE REPLAY] {PlayerName}: HP {CurrentHp}/{MaxHp} {HealthBar}",
                    player.Name, player.CurrentHp, player.MaxHp, healthBar);
            }
        }
        else
        {
            _logger.LogInformation("[BATTLE REPLAY] ❌ Defeat! All players defeated! ❌");
            _logger.LogInformation("[BATTLE REPLAY] Remaining enemies: {AliveEnemies}/{TotalEnemies}", finalAliveEnemies, finalStatus.Enemies.Count);

            // Show surviving enemy stats
            foreach (var enemy in finalStatus.Enemies.Where(p => p.IsAlive))
            {
                var healthBar = GenerateHealthBar(enemy.CurrentHp, enemy.MaxHp, 20);
                _logger.LogInformation("[BATTLE REPLAY] {EnemyName}: HP {CurrentHp}/{MaxHp} {HealthBar}",
                    enemy.Name, enemy.CurrentHp, enemy.MaxHp, healthBar);
            }
        }

        // Display battle completion details using summary if available
        DisplayBattleCompletionDetails(finalStatus, battleSummary);

        _logger.LogInformation("[BATTLE REPLAY] Battle completed - BattleId: {BattleId}, Seed: {Seed} (replay completed)",
            battleId, seedValue);
        _logger.LogInformation("[BATTLE REPLAY] ===============================================");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Display battle completion details (turns, ending reason)
    /// </summary>
    private void DisplayBattleCompletionDetails(BattleStatus finalStatus, BattleReplaySummary? battleSummary)
    {
        if (battleSummary.HasValue)
        {
            var summary = battleSummary.Value;
            _logger.LogInformation("[BATTLE REPLAY] Total turns: {FinalTurn} (Battle lasted {FinalTurn} out of max {TotalTurns} turns)",
                summary.FinalTurn, summary.FinalTurn, summary.TotalTurns);

            // Display how the battle ended
            if (summary.IsEndedByTurnLimit)
            {
                _logger.LogInformation("[BATTLE REPLAY] ⏰ Battle ended due to turn limit reached!");
            }
            else
            {
                _logger.LogInformation("[BATTLE REPLAY] ⚔️ Battle ended due to complete elimination!");
            }
        }
        else
        {
            // Fallback to old method if summary is not available
            var displayTotalTurns = finalStatus.FinalTurn ?? finalStatus.TotalTurns;
            _logger.LogInformation("[BATTLE REPLAY] Total turns: {CurrentTurn}/{TotalTurns}", finalStatus.CurrentTurn, displayTotalTurns);

            // Display how the battle ended using the new property
            if (finalStatus.IsEndedByTurnLimit == true)
            {
                _logger.LogInformation("[BATTLE REPLAY] ⏰ Battle ended due to turn limit reached!");
            }
            else if (finalStatus.IsEndedByTurnLimit == false)
            {
                _logger.LogInformation("[BATTLE REPLAY] ⚔️ Battle ended due to complete elimination!");
            }
        }
    }

    /// <summary>
    /// Display battle status for a specific turn with visual rendering
    /// </summary>
    private void DisplayBattleStatus(BattleStatus status, int currentTurn, int totalTurns)
    {
        // Display only every 5th turn, plus the first and last turns
        // Avoid duplicate display when the last turn is also a multiple of 5
        bool isFirstTurn = currentTurn == 1;
        bool isLastTurn = currentTurn == totalTurns;
        bool isIntervalTurn = status.CurrentTurn % BattleReplayDefines.ReplayFps == 0;
        bool shouldDisplay = isFirstTurn || (isLastTurn && !isIntervalTurn) || isIntervalTurn;

        if (shouldDisplay)
        {
            // Display turn information - use FinalTurn if available for more intuitive display
            // During replay, only show current turn to avoid spoilers
            _logger.LogInformation("[BATTLE] ===== Turn {CurrentTurn} =====", status.CurrentTurn);

            // Display visual battle field first for better overview
            RenderBattleField(status);

            // Display players info
            var alivePlayers = status.Players.Count(p => p.IsAlive);
            _logger.LogInformation("[BATTLE] Players alive: {AlivePlayers}/{TotalPlayers}", alivePlayers, status.Players.Count);
            foreach (var player in status.Players)
            {
                var healthBar = GenerateHealthBar(player.CurrentHp, player.MaxHp, 20);
                var jobInfo = player.PlayerJob.HasValue ? $" ({player.PlayerJob})" : "";
                _logger.LogInformation("[BATTLE] {PlayerName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}",
                    player.Name, jobInfo, player.CurrentHp, player.MaxHp, healthBar, player.Attack, player.Defense, player.Speed, player.Position);
            }

            // Display enemies info
            var aliveEnemies = status.Enemies.Count(e => e.IsAlive);
            _logger.LogInformation("[BATTLE] Enemies alive: {AliveEnemies}/{TotalEnemies}", aliveEnemies, status.Enemies.Count);
            foreach (var enemy in status.Enemies.Where(x => x.IsAlive).Take(2)) // Show first 2 enemies to avoid spam
            {
                var healthBar = GenerateHealthBar(enemy.CurrentHp, enemy.MaxHp, 10);
                var jobInfo = enemy.EnemyJob.HasValue ? $" ({enemy.EnemyJob})" : "";
                _logger.LogInformation("[BATTLE] {EnemyName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}",
                    enemy.Name, jobInfo, enemy.CurrentHp, enemy.MaxHp, healthBar, enemy.Attack, enemy.Defense, enemy.Speed, enemy.Position);
            }

            // Display recent logs
            if (status.RecentLogs.Count > 0)
            {
                _logger.LogInformation("[BATTLE] Recent actions:");
                foreach (var log in status.RecentLogs)
                {
                    _logger.LogInformation("[BATTLE] > {Log}", log);
                }
            }

            _logger.LogInformation("[BATTLE] ========================================");
        }
    }

    /// <summary>
    /// Renders a visual representation of the battle field using box-drawing characters
    /// </summary>
    private void RenderBattleField(BattleStatus status)
    {
        // First build the field with entity positions
        var field = BuildBattleField(status);

        // Calculate correct border width (each cell is 2 chars wide + separators)
        // For a 20x20 field with 2 chars per cell and a space between: 20*2 + 19 = 59 chars total width
        int borderWidth = status.FieldSize.X * 2 + (status.FieldSize.X - 1);

        // Draw top border
        _logger.LogInformation("[BATTLE FIELD] ┌{Border}┐", new string('─', borderWidth));

        // Draw field rows
        for (int y = 0; y < status.FieldSize.Y; y++)
        {
            var line = new System.Text.StringBuilder("│");

            for (int x = 0; x < status.FieldSize.X; x++)
            {
                var cellContent = field[y, x];

                if (cellContent == null)
                {
                    // Empty cell
                    line.Append("  ");
                }
                else
                {
                    // Determine if this is a player or enemy
                    bool isPlayer = status.Players.Any(p => p.EntityId == cellContent);

                    if (isPlayer)
                    {
                        // Player: P1, P2, etc.
                        int playerIdx = status.Players.FindIndex(p => p.EntityId == cellContent) + 1;
                        line.Append($"P{playerIdx}");
                    }
                    else
                    {
                        // Enemy: E1, E2, etc.
                        int enemyIdx = status.Enemies.FindIndex(e => e.EntityId == cellContent) + 1;
                        line.Append($"E{enemyIdx}");
                    }
                }

                // Add separator except for the last column
                if (x < status.FieldSize.X - 1)
                {
                    line.Append(' ');
                }
            }

            line.Append('│');
            _logger.LogInformation("[BATTLE FIELD] {Line}", line.ToString());
        }

        // Draw bottom border with the same width as the top border
        _logger.LogInformation("[BATTLE FIELD] └{Border}┘", new string('─', borderWidth));

        // Add a legend for easier identification
        var playerLegend = new System.Text.StringBuilder("Players: ");
        for (int i = 0; i < status.Players.Count; i++)
        {
            var player = status.Players[i];
            if (player.IsAlive)
            {
                playerLegend.Append($"P{i + 1}={player.Name}({player.CurrentHp}/{player.MaxHp}) ");
            }
        }
        _logger.LogInformation("[BATTLE FIELD] {PlayerLegend}", playerLegend.ToString());

        var enemyLegend = new System.Text.StringBuilder("Enemies: ");
        for (int i = 0; i < status.Enemies.Count; i++)
        {
            var enemy = status.Enemies[i];
            if (enemy.IsAlive)
            {
                enemyLegend.Append($"E{i + 1}={enemy.Name}({enemy.CurrentHp}/{enemy.MaxHp}) ");
            }
        }
        _logger.LogInformation("[BATTLE FIELD] {EnemyLegend}", enemyLegend.ToString());
    }

    /// <summary>
    /// Generate a text-based health bar
    /// </summary>
    private static string GenerateHealthBar(int current, int max, int length)
    {
        int filledLength = (int)Math.Round((double)current / max * length);

        // ASCII-compatible characters for better Windows cmd.exe compatibility
        string filled = new string('=', filledLength);
        string empty = new string('-', length - filledLength);

        return $"[{filled}{empty}]";
    }

    /// <summary>
    /// Builds a 2D field array from player and enemy positions
    /// </summary>
    private static Guid?[,] BuildBattleField(BattleStatus status)
    {
        var field = new Guid?[status.FieldSize.Y, status.FieldSize.X];

        // Place players on field
        foreach (var player in status.Players)
        {
            if (player.IsAlive &&
                player.Position.X >= 0 && player.Position.X < status.FieldSize.X &&
                player.Position.Y >= 0 && player.Position.Y < status.FieldSize.Y)
            {
                field[player.Position.Y, player.Position.X] = player.EntityId;
            }
        }

        // Place enemies on field
        foreach (var enemy in status.Enemies)
        {
            if (enemy.IsAlive &&
                enemy.Position.X >= 0 && enemy.Position.X < status.FieldSize.X &&
                enemy.Position.Y >= 0 && enemy.Position.Y < status.FieldSize.Y)
            {
                field[enemy.Position.Y, enemy.Position.X] = enemy.EntityId;
            }
        }

        return field;
    }
}
