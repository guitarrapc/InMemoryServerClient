using Microsoft.Extensions.Logging;
using Shared.Contracts;
using System.Collections.Concurrent;

namespace BattleLogic.Tests;

/// <summary>
/// バトルシステムのバランス検証用テストクラス
/// </summary>
public class BattleBalanceTests
{
    private readonly ILogger<BattleState> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public BattleBalanceTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = Substitute.For<ILogger<BattleState>>();
    }

    /// <summary>
    /// 単一のバトルを実行し、プレイヤーが勝利したかどうかを返す
    /// </summary>
    /// <returns>プレイヤーが勝利した場合はtrue、敗北した場合はfalse</returns>
    private async Task<bool> RunSingleBattleAsync()
    {
        // テスト用のグループを作成 (プレイヤー数は常に5人)
        var playerCount = 5; // 常に5人のプレイヤー

        var battleId = BattleSeed.NewTimestampId(); // Use GUID v7 for battle ID
        var seed = 12345; // Use fixed seed for testing
        var groupId = BattleSeed.NewTimestampId().ToString(); // Use GUID v7 for group ID
        var group = new GroupInfo
        {
            GroupId = groupId,
            Name = $"test_group_{groupId}",
            ConnectionCount = playerCount,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes),
            ClientIds = Enumerable.Range(0, playerCount)
                                .Select(_ => BattleSeed.NewTimestampId().ToString()) // Use GUID v7 for client IDs
                                .ToList()
        };

        // バトル状態を初期化
        var battleState = new BattleState(battleId, seed, group, _logger, TestHelpers.CreateMemoryReplayWriterFactory(_loggerFactory));

        // バトルを実行
        await battleState.RunBattleAsync();

        // バトルの最終状態を取得して結果を判定
        var finalStatus = battleState.GetStatus();

        // 新しいIsPlayerVictoryプロパティを使用
        bool playerVictory = finalStatus.IsPlayerVictory ?? false;

        // バックアップ検証: 古い方法でも結果を確認
        var allTurnData = battleState.GetAllTurnData();
        var finalState = allTurnData[^1]; // 最後のターンの状態
        bool backupPlayerVictory = finalState.Players.Any(p => p.CurrentHp > 0);

        // 新旧の判定が一致することを確認
        if (playerVictory != backupPlayerVictory)
        {
            _logger.LogError("Battle result mismatch: IsPlayerVictory={PlayerVictory}, backup calculation={BackupPlayerVictory}",
                playerVictory, backupPlayerVictory);
        }

        // メモリ解放
        battleState.ClearBattleData();

        return playerVictory;
    }

    [Fact]
    public async Task BattleBalance_ComprehensiveAnalysisWithWinRateValidation()
    {
        // 設定パラメータ
        const int battlesForAnalysis = 500; // バトル数（効率とカバレッジのバランスを考慮）
        const double minAcceptableWinRate = 0.45; // 最小許容勝率 (45%)
        const double maxAcceptableWinRate = 0.7; // 最大許容勝率 (70%)

        // 分析用のデータ構造 (敵の数別勝率を記録)
        var enemyCountWinRates = new Dictionary<int, List<bool>>();
        for (int i = BattleSystemDefines.EnemyCount.Min; i <= BattleSystemDefines.EnemyCount.Max; i++)
        {
            enemyCountWinRates[i] = new List<bool>();
        }

        // 並列実行のための設定
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        var battleResults = new ConcurrentBag<(int EnemyCount, bool IsVictory)>();

        Console.WriteLine($"Starting comprehensive battle balance analysis with {battlesForAnalysis} battles...");

        // 並列処理でバトルを実行
        await Parallel.ForEachAsync(Enumerable.Range(0, battlesForAnalysis), parallelOptions, async (_, ct) =>
        {
            // プレイヤー数は常に5人
            var playerCount = 5;

            var battleId = BattleSeed.NewTimestampId();
            var seed = 12345; // Use fixed seed for testing
            var groupId = BattleSeed.NewTimestampId().ToString();
            var group = new GroupInfo
            {
                GroupId = groupId,
                Name = $"test_group_{groupId}",
                ConnectionCount = playerCount,
                MaxConnections = SystemDefines.MaxConnectionsPerGroup,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes),
                ClientIds = Enumerable.Range(0, playerCount)
                                    .Select(_ => BattleSeed.NewTimestampId().ToString())
                                    .ToList()
            };

            // バトル状態を初期化
            var battleState = new BattleState(battleId, seed, group, _logger, TestHelpers.CreateMemoryReplayWriterFactory(_loggerFactory));

            // バトルを実行
            await battleState.RunBattleAsync();

            // バトルの最終状態を取得して結果を判定
            var finalStatus = battleState.GetStatus();
            bool playerVictory = finalStatus.IsPlayerVictory ?? false;

            // 敵の数を取得（バトル開始時の敵数）
            var allTurnData = battleState.GetAllTurnData();
            var initialState = allTurnData[0];
            int enemyCount = initialState.Enemies.Count;

            // 結果を記録
            battleResults.Add((enemyCount, playerVictory));

            // メモリ解放
            battleState.ClearBattleData();
        });

        // 結果の集計
        foreach (var (enemyCount, isVictory) in battleResults)
        {
            if (enemyCountWinRates.ContainsKey(enemyCount))
            {
                enemyCountWinRates[enemyCount].Add(isVictory);
            }
        }

        // 全体の勝率を計算
        double overallWinRate = battleResults.Count(r => r.IsVictory) / (double)battleResults.Count;
        int totalVictories = battleResults.Count(r => r.IsVictory);
        int totalBattles = battleResults.Count;

        // 詳細結果の構築
        var detailedResults = new System.Text.StringBuilder();
        detailedResults.AppendLine("=== COMPREHENSIVE BATTLE BALANCE ANALYSIS ===");
        detailedResults.AppendLine($"Total battles: {totalBattles}");
        detailedResults.AppendLine($"Overall win rate (5 players): {overallWinRate:P2} ({totalVictories}/{totalBattles})");
        detailedResults.AppendLine($"Acceptable range: {minAcceptableWinRate:P2} - {maxAcceptableWinRate:P2}");
        detailedResults.AppendLine();
        detailedResults.AppendLine("Win rates by enemy count:");

        bool hasValidData = false;
        foreach (var kvp in enemyCountWinRates.OrderBy(k => k.Key))
        {
            int enemyCount = kvp.Key;
            List<bool> results = kvp.Value;

            if (results.Count > 0)
            {
                hasValidData = true;
                double winRate = results.Count(r => r) / (double)results.Count;
                int victories = results.Count(r => r);
                detailedResults.AppendLine($"  {enemyCount} enemies: {winRate:P2} ({victories}/{results.Count}) - {results.Count} battles");
            }
            else
            {
                detailedResults.AppendLine($"  {enemyCount} enemies: No data");
            }
        }

        // 統計情報の追加
        detailedResults.AppendLine();
        detailedResults.AppendLine("=== BALANCE VALIDATION ===");
        bool isWithinRange = overallWinRate >= minAcceptableWinRate && overallWinRate <= maxAcceptableWinRate;
        detailedResults.AppendLine($"Win rate within acceptable range: {(isWithinRange ? "✓ PASS" : "✗ FAIL")}");

        if (!isWithinRange)
        {
            if (overallWinRate < minAcceptableWinRate)
            {
                detailedResults.AppendLine("  → Battle is too difficult for players");
            }
            else
            {
                detailedResults.AppendLine("  → Battle is too easy for players");
            }
        }

        // ファイルに結果を出力
        var resultsPath = Path.Combine(Directory.GetCurrentDirectory(), "battle_balance_comprehensive_results.txt");
        File.WriteAllText(resultsPath, detailedResults.ToString());

        Console.WriteLine("=====================================");
        Console.WriteLine(detailedResults.ToString());
        Console.WriteLine("=====================================");
        Console.WriteLine($"Comprehensive results saved to: {resultsPath}");

        // アサーション - 勝率が許容範囲内であることを検証
        Assert.True(hasValidData, "Should have valid battle data for analysis");
        Assert.True(totalBattles == battlesForAnalysis, $"Expected {battlesForAnalysis} battles, but got {totalBattles}");
        Assert.True(
            isWithinRange,
            $"Overall win rate ({overallWinRate:P2}) should be between {minAcceptableWinRate:P2} and {maxAcceptableWinRate:P2}. " +
            $"Current rate indicates the battle balance may need adjustment."
        );

        // 詳細分析のための追加検証
        var enemyCountsWithData = enemyCountWinRates.Where(kvp => kvp.Value.Count > 0).ToList();
        Assert.True(enemyCountsWithData.Count > 0, "Should have battle data for at least one enemy count");

        Console.WriteLine($"✓ Battle balance analysis completed successfully. Win rate: {overallWinRate:P2}");
    }

    [Fact]
    public async Task BattleResult_ShouldBeConsistent_BetweenPropertyAndCalculation()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId();
        var seed = 12345; // Use fixed seed for testing
        var group = Substitute.For<IBattleGroupContext>();
        group.ConnectedCount.Returns(5);
        group.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act
        var battleState = new BattleState(battleId, seed, group, _logger, TestHelpers.CreateMemoryReplayWriterFactory(_loggerFactory));
        await battleState.RunBattleAsync();

        var finalStatus = battleState.GetStatus();
        var allTurnData = battleState.GetAllTurnData();
        var finalTurnData = allTurnData[^1];

        // Assert
        Assert.False(finalStatus.IsInProgress); // Battle should be completed
        Assert.NotNull(finalStatus.IsPlayerVictory); // Should have a victory result

        // Calculate victory using the correct method based on how the battle ended
        bool calculatedVictory;
        if (finalStatus.IsEndedByTurnLimit == true)
        {
            // Battle ended by turn limit - use survivor count comparison
            int alivePlayers = finalTurnData.Players.Count(p => p.CurrentHp > 0);
            int aliveEnemies = finalTurnData.Enemies.Count(e => e.CurrentHp > 0);

            if (finalTurnData.Players.All(p => p.CurrentHp <= 0))
            {
                calculatedVictory = false; // All players dead = defeat
            }
            else if (finalTurnData.Enemies.All(e => e.CurrentHp <= 0))
            {
                calculatedVictory = true; // All enemies dead = victory
            }
            else
            {
                calculatedVictory = alivePlayers > aliveEnemies; // More survivors wins
            }
        }
        else
        {
            // Battle ended by elimination - check if any players survived
            calculatedVictory = finalTurnData.Players.Any(p => p.CurrentHp > 0);
        }

        // Verify consistency between new property and correct calculation
        Assert.Equal(calculatedVictory, finalStatus.IsPlayerVictory.Value);

        // Verify battle state consistency
        if (finalStatus.IsPlayerVictory.Value)
        {
            if (finalStatus.IsEndedByTurnLimit == true)
            {
                // Turn limit victory: either all enemies dead OR more players alive
                bool allEnemiesDead = finalTurnData.Enemies.All(e => e.CurrentHp <= 0);
                bool morePlayersAlive = finalTurnData.Players.Count(p => p.CurrentHp > 0) > finalTurnData.Enemies.Count(e => e.CurrentHp > 0);
                Assert.True(allEnemiesDead || morePlayersAlive,
                    "Turn limit victory should have either all enemies defeated or more players alive than enemies");
            }
            else
            {
                // Elimination victory: at least one player alive and all enemies dead
                Assert.True(finalTurnData.Players.Any(p => p.CurrentHp > 0), "At least one player should be alive if players won by elimination");
                Assert.True(finalTurnData.Enemies.All(e => e.CurrentHp <= 0), "All enemies should be defeated if players won by elimination");
            }
        }
        else
        {
            if (finalStatus.IsEndedByTurnLimit == true)
            {
                // Turn limit defeat: either all players dead OR fewer/equal players alive
                bool allPlayersDead = finalTurnData.Players.All(p => p.CurrentHp <= 0);
                bool fewerOrEqualPlayersAlive = finalTurnData.Players.Count(p => p.CurrentHp > 0) <= finalTurnData.Enemies.Count(e => e.CurrentHp > 0);
                Assert.True(allPlayersDead || fewerOrEqualPlayersAlive,
                    "Turn limit defeat should have either all players defeated or fewer/equal players alive than enemies");
            }
            else
            {
                // Elimination defeat: all players dead
                Assert.True(finalTurnData.Players.All(p => p.CurrentHp <= 0), "All players should be defeated if players lost by elimination");
            }
        }

        // Clean up
        battleState.ClearBattleData();
    }
}
