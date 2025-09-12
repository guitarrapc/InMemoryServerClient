namespace WasmClient.Models;

/// <summary>
/// バトル履歴の完全なデータモデル（IndexedDBに保存される）
/// </summary>
public record BattleHistory
{
    public required string SessionId { get; init; } // Session ID (client-generated)
    public required string BattleId { get; init; } // battle ID (Server-generated)
    public required int Seed { get; init; } // Seed (Server-generated)
    public required DateTime CreatedAt { get; init; }
    public required DateTime CompletedAt { get; init; }
    public required string GroupName { get; init; }
    public required string ServerUrl { get; init; }
    public required int TotalTurns { get; init; }
    public List<BattleReplayData> ReplayData { get; init; } = new();
    public BattleResult Result { get; init; } = BattleResult.Default;
    public List<BattleClientHistory> ParticipatingClients { get; init; } = new();
    public long DataSize { get; init; } // Changed from DataSizeBytes
    public TimeSpan BattleDuration => CompletedAt - CreatedAt;

    /// <summary>
    /// ローカル時間でのバトル時間を取得
    /// </summary>
    public TimeSpan LocalBattleDuration => CompletedAt.ToLocalTime() - CreatedAt.ToLocalTime();
}

/// <summary>
/// バトルに参加したクライアントの履歴情報
/// </summary>
public record BattleClientHistory
{
    public string ConnectionId { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public Shared.Models.ConnectionType ConnectionType { get; init; }
    public DateTime ConnectedAt { get; init; }
}

/// <summary>
/// バトル履歴の軽量サマリー（一覧表示用）
/// </summary>
public record BattleHistorySummary
{
    public required string BattleId { get; init; }
    public required string SessionId { get; init; } // Session ID (client-generated)
    public required DateTime CreatedAt { get; init; }
    public required DateTime CompletedAt { get; init; }
    public required string GroupName { get; init; }
    public required string ServerUrl { get; init; }
    public required int TotalTurns { get; init; }
    public required bool IsVictory { get; init; } // Simplified for summary
    public required int PlayersSurvived { get; init; } // Added for UI display
    public required int EnemiesKilled { get; init; } // Added for UI display
    public required string VictoryCondition { get; init; } // Added for UI display
    public required int DataSizeKB { get; init; }
    public required int ClientCount { get; init; }
    public TimeSpan BattleDuration => CompletedAt - CreatedAt;

    /// <summary>
    /// ローカル時間でのバトル時間を取得
    /// </summary>
    public TimeSpan LocalBattleDuration => CompletedAt.ToLocalTime() - CreatedAt.ToLocalTime();

    // Backward compatibility - create a BattleResult from the simple fields
    public BattleResult Result => new()
    {
        IsVictory = IsVictory,
        TotalTurns = TotalTurns,
        PlayersSurvived = PlayersSurvived,
        EnemiesKilled = EnemiesKilled,
        VictoryCondition = VictoryCondition
    };
}

/// <summary>
/// バトル結果データ
/// </summary>
public record BattleResult
{
    public required bool IsVictory { get; init; }
    public required int PlayersSurvived { get; init; }
    public required int EnemiesKilled { get; init; }
    public required int TotalTurns { get; init; }
    public required string VictoryCondition { get; init; }

    /// <summary>
    /// デフォルトの敗北結果
    /// </summary>
    public static BattleResult Default => new()
    {
        IsVictory = false,
        PlayersSurvived = 0,
        EnemiesKilled = 0,
        TotalTurns = 0,
        VictoryCondition = string.Empty
    };
}

/// <summary>
/// IndexedDBストレージ統計情報
/// </summary>
public record BattleHistoryStats
{
    public required int TotalBattles { get; init; }
    public required int TotalVictories { get; init; }
    public required long TotalDataSize { get; init; }
    public DateTime? OldestBattle { get; init; }
    public DateTime? NewestBattle { get; init; }
    public double WinRate => TotalBattles > 0 ? (double)TotalVictories / TotalBattles : 0;
    public double AverageBattleSizeKB => TotalBattles > 0 ? (TotalDataSize / 1024.0) / TotalBattles : 0;

    /// <summary>
    /// デフォルトの空の統計情報
    /// </summary>
    public static BattleHistoryStats Default => new()
    {
        TotalBattles = 0,
        TotalVictories = 0,
        TotalDataSize = 0
    };
}
