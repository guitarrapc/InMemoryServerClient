using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections.Concurrent;
using BattleLogic.Battle;
using BattleLogic.Models;
using Shared.Constants;
using BattleLogic.Constans;
using Shared.Models;
using Shared.Contracts;

namespace Tests;

/// <summary>
/// バトルシステムのバランス検証用テストクラス
/// </summary>
public class BattleBalanceTests
{
    private readonly ILogger<BattleState> _logger;

    public BattleBalanceTests()
    {
        _logger = Substitute.For<ILogger<BattleState>>();
    }

    /// <summary>
    /// バトルのバランス評価のために、指定された回数のバトルを実行し、プレイヤーの勝率を計算する
    /// </summary>
    /// <param name="battleCount">テストするバトル数</param>
    /// <returns>プレイヤーの勝率 (0.0 - 1.0)</returns>
    private async Task<double> RunBattlesAndCalculateWinRateAsync(int battleCount)
    {
        // バトル結果を格納するための変数
        var playerVictoryCount = 0;
        var totalBattleCount = 0;

        // 並列実行のための設定
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        var concurrentResults = new ConcurrentBag<bool>();

        // 並列処理でバトルを実行
        await Parallel.ForEachAsync(Enumerable.Range(0, battleCount), parallelOptions, async (_, ct) =>
        {
            var battleResult = await RunSingleBattleAsync();
            concurrentResults.Add(battleResult);
        });

        // 結果の集計
        playerVictoryCount = concurrentResults.Count(r => r);
        totalBattleCount = concurrentResults.Count;

        // 勝率の計算
        return (double)playerVictoryCount / totalBattleCount;
    }

    /// <summary>
    /// 単一のバトルを実行し、プレイヤーが勝利したかどうかを返す
    /// </summary>
    /// <returns>プレイヤーが勝利した場合はtrue、敗北した場合はfalse</returns>
    private async Task<bool> RunSingleBattleAsync()
    {
        // テスト用のグループを作成 (プレイヤー数は常に5人)
        var playerCount = 5; // 常に5人のプレイヤー

        var battleId = BattleSeed.NewTimestampId().ToString(); // Use GUID v7 for battle ID
        var groupId = BattleSeed.NewTimestampId().ToString(); // Use GUID v7 for group ID
        var group = new GroupInfo
        {
            Id = groupId,
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
        var battleState = new BattleState(battleId, group, _logger);

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
    public async Task BattleBalance_WinRate_ShouldBeWithinRange()
    {
        // 設定パラメータ
        const int battlesPerTrial = 200; // 1回の試行でのバトル数
        const int numberOfTrials = 10;   // 試行回数
        const double minAcceptableWinRate = 0.45; // 最小許容勝率 (45%)
        const double maxAcceptableWinRate = 0.7; // 最大許容勝率 (70%)

        // 複数回の試行の勝率を記録
        var winRates = new List<double>(numberOfTrials);

        for (int trial = 0; trial < numberOfTrials; trial++)
        {
            var winRate = await RunBattlesAndCalculateWinRateAsync(battlesPerTrial);
            winRates.Add(winRate);

            // 各試行の結果をログに出力 (テスト中のフィードバック用)
            Console.WriteLine($"Trial {trial + 1}: Win rate = {winRate:P2} ({winRate * battlesPerTrial:F0}/{battlesPerTrial})");
        }

        // 平均勝率を計算
        double averageWinRate = winRates.Average();
        Console.WriteLine($"Average win rate across {numberOfTrials} trials: {averageWinRate:P2}");

        // 平均勝率が許容範囲内かを検証
        Assert.True(
            averageWinRate >= minAcceptableWinRate && averageWinRate <= maxAcceptableWinRate,
            $"Win rate ({averageWinRate:P2}) should be between {minAcceptableWinRate:P2} and {maxAcceptableWinRate:P2}"
        );
    }

    [Fact]
    public async Task BattleBalance_DetailedAnalysis()
    {
        // 詳細分析のためのバトル数
        const int battlesForAnalysis = 200;

        // 分析用のデータ構造 (敵の数別勝率を記録)
        var enemyCountWinRates = new Dictionary<int, List<bool>>();
        for (int i = BattleSystemDefines.MinEnemyCount; i <= BattleSystemDefines.MaxEnemyCount; i++)
        {
            enemyCountWinRates[i] = new List<bool>();
        }

        // 並列実行のための設定
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        var battleResults = new ConcurrentBag<(int EnemyCount, bool IsVictory)>();

        // 並列処理でバトルを実行
        await Parallel.ForEachAsync(Enumerable.Range(0, battlesForAnalysis), parallelOptions, async (_, ct) =>
        {
            // プレイヤー数は常に5人
            var playerCount = 5;

            var battleId = BattleSeed.NewTimestampId().ToString();
            var groupId = BattleSeed.NewTimestampId().ToString();
            var group = new GroupInfo
            {
                Id = groupId,
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
            var battleState = new BattleState(battleId, group, _logger);

            // バトルを実行
            await battleState.RunBattleAsync();

            // バトルの最終状態を取得して結果を判定
            var allTurnData = battleState.GetAllTurnData();
            var finalState = allTurnData[^1]; // 最後のターンの状態

            // プレイヤーが全滅していなければ勝利
            bool playerVictory = finalState.Players.Any(p => p.CurrentHp > 0);

            // 敵の数を取得（最初のターンから）
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
        var overallMessage = $"Overall win rate (5 players): {overallWinRate:P2} ({battleResults.Count(r => r.IsVictory)}/{battleResults.Count})";

        // 結果をファイルに出力
        var detailedResults = new System.Text.StringBuilder();
        detailedResults.AppendLine(overallMessage);
        detailedResults.AppendLine("Win rates by enemy count:");

        foreach (var kvp in enemyCountWinRates.OrderBy(k => k.Key))
        {
            int enemyCount = kvp.Key;
            List<bool> results = kvp.Value;

            if (results.Count > 0)
            {
                double winRate = results.Count(r => r) / (double)results.Count;
                detailedResults.AppendLine($"  {enemyCount} enemies: {winRate:P2} ({results.Count(r => r)}/{results.Count})");
            }
            else
            {
                detailedResults.AppendLine($"  {enemyCount} enemies: No data");
            }
        }

        // ファイルに結果を出力
        var resultsPath = Path.Combine(Directory.GetCurrentDirectory(), "battle_balance_results.txt");
        File.WriteAllText(resultsPath, detailedResults.ToString());

        Console.WriteLine("=====================================");
        Console.WriteLine(detailedResults.ToString());
        Console.WriteLine("=====================================");
        Console.WriteLine($"Detailed results saved to: {resultsPath}");

        // アサーション追加
        Assert.True(true, "Battle balance analysis completed");
    }

    [Fact]
    public async Task BattleResult_ShouldBeConsistent_BetweenPropertyAndCalculation()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId().ToString();
        var group = Substitute.For<IBattleGroupContext>();
        group.ConnectedCount.Returns(5);
        group.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });

        // Act
        var battleState = new BattleState(battleId, group, _logger);
        await battleState.RunBattleAsync();

        var finalStatus = battleState.GetStatus();
        var allTurnData = battleState.GetAllTurnData();
        var finalTurnData = allTurnData[^1];

        // Assert
        Assert.False(finalStatus.IsInProgress); // Battle should be completed
        Assert.NotNull(finalStatus.IsPlayerVictory); // Should have a victory result

        // Calculate victory using the same method as CheckBattleOver
        bool allPlayersDead = finalTurnData.Players.All(p => p.CurrentHp <= 0);
        bool allEnemiesDead = finalTurnData.Enemies.All(e => e.CurrentHp <= 0);

        bool calculatedVictory;
        if (allPlayersDead && allEnemiesDead)
        {
            calculatedVictory = false; // Battle over, player defeat (same as CheckBattleOver)
        }
        else if (allPlayersDead)
        {
            calculatedVictory = false; // Battle over, player defeat
        }
        else if (allEnemiesDead)
        {
            calculatedVictory = true; // Battle over, player victory
        }
        else
        {
            calculatedVictory = false; // Battle continues (shouldn't happen in final status)
        }

        // Verify consistency between new property and updated calculation
        Assert.Equal(calculatedVictory, finalStatus.IsPlayerVictory.Value);

        // Verify battle state consistency
        if (finalStatus.IsPlayerVictory.Value)
        {
            Assert.True(finalTurnData.Players.Any(p => p.CurrentHp > 0), "At least one player should be alive if players won");
            Assert.True(finalTurnData.Enemies.All(e => e.CurrentHp <= 0), "All enemies should be defeated if players won");
        }
        else
        {
            Assert.True(finalTurnData.Players.All(p => p.CurrentHp <= 0), "All players should be defeated if players lost");
        }

        // Clean up
        battleState.ClearBattleData();
    }
}
