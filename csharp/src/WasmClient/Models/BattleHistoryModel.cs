using Shared.Models;

namespace WasmClient.Models;

/// <summary>
/// バトル履歴の完全なデータモデル（IndexedDBに保存される）
/// </summary>
public record BattleHistory
{
    public string BattleId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string ServerUrl { get; init; } = string.Empty;
    public int TotalTurns { get; init; }
    public List<BattleReplayData> ReplayData { get; init; } = new();
    public BattleResult Result { get; init; } = new();
    public List<string> ParticipatingClients { get; init; } = new();
    public long DataSize { get; init; } // Changed from DataSizeBytes
    public string BattleSeed { get; init; } = string.Empty;
    public TimeSpan BattleDuration => CompletedAt - CreatedAt;
}

/// <summary>
/// バトル履歴の軽量サマリー（一覧表示用）
/// </summary>
public record BattleHistorySummary
{
    public string BattleId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string ServerUrl { get; init; } = string.Empty;
    public int TotalTurns { get; init; }
    public BattleResult Result { get; init; } = new(); // Changed from IsPlayerVictory
    public int DataSizeKB { get; init; }
    public int ClientCount { get; init; }
    public TimeSpan BattleDuration => CompletedAt - CreatedAt;
}

/// <summary>
/// バトル結果データ
/// </summary>
public record BattleResult
{
    public bool IsVictory { get; init; } // Changed from IsPlayerVictory
    public int PlayersSurvived { get; init; } // Changed from RemainingPlayers
    public int EnemiesKilled { get; init; } // Changed from RemainingEnemies
    public int TotalTurns { get; init; } // Added for consistency
    public string VictoryCondition { get; init; } = string.Empty;
}

/// <summary>
/// IndexedDBストレージ統計情報
/// </summary>
public record BattleHistoryStats
{
    public int TotalBattles { get; init; }
    public int TotalVictories { get; init; } // Added
    public long TotalDataSize { get; init; } // Changed from TotalSizeBytes
    public DateTime? OldestBattle { get; init; }
    public DateTime? NewestBattle { get; init; }
    public double WinRate => TotalBattles > 0 ? (double)TotalVictories / TotalBattles : 0; // Added
    public double AverageBattleSizeKB => TotalBattles > 0 ? (TotalDataSize / 1024.0) / TotalBattles : 0; // Updated
}
