using System.Text.Json.Serialization;

namespace WasmClient.Models;

/// <summary>
/// バトル履歴の完全なデータモデル（IndexedDBに保存される）
/// </summary>
public record BattleHistory
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; } // Session ID (client-generated)

    [JsonPropertyName("battleId")]
    public required string BattleId { get; init; } // battle ID (Server-generated)

    [JsonPropertyName("seed")]
    public required int Seed { get; init; } // Seed (Server-generated)

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("completedAt")]
    public required DateTime CompletedAt { get; init; }

    [JsonPropertyName("groupName")]
    public required string GroupName { get; init; }

    [JsonPropertyName("serverUrl")]
    public required string ServerUrl { get; init; }

    [JsonPropertyName("totalTurns")]
    public required int TotalTurns { get; init; }

    [JsonPropertyName("replayData")]
    public List<BattleReplayData> ReplayData { get; init; } = new();

    [JsonPropertyName("result")]
    public BattleResult Result { get; init; } = BattleResult.Default;

    [JsonPropertyName("participatingClients")]
    public List<BattleClientHistory> ParticipatingClients { get; init; } = new();

    [JsonPropertyName("dataSize")]
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
    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; init; } = string.Empty;

    [JsonPropertyName("playerId")]
    public string PlayerId { get; init; } = string.Empty;

    [JsonPropertyName("connectionType")]
    public Shared.Models.ConnectionType ConnectionType { get; init; }

    [JsonPropertyName("connectedAt")]
    public DateTime ConnectedAt { get; init; }
}

/// <summary>
/// バトル履歴の軽量サマリー（一覧表示用）
/// </summary>
public record BattleHistorySummary
{
    [JsonPropertyName("battleId")]
    public required string BattleId { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; } // Session ID (client-generated)

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("completedAt")]
    public required DateTime CompletedAt { get; init; }

    [JsonPropertyName("groupName")]
    public required string GroupName { get; init; }

    [JsonPropertyName("serverUrl")]
    public required string ServerUrl { get; init; }

    [JsonPropertyName("totalTurns")]
    public required int TotalTurns { get; init; }

    [JsonPropertyName("isVictory")]
    public required bool IsVictory { get; init; } // Simplified for summary

    [JsonPropertyName("playersSurvived")]
    public required int PlayersSurvived { get; init; } // Added for UI display

    [JsonPropertyName("enemiesKilled")]
    public required int EnemiesKilled { get; init; } // Added for UI display

    [JsonPropertyName("victoryCondition")]
    public required string VictoryCondition { get; init; } // Added for UI display

    [JsonPropertyName("dataSizeKB")]
    public required int DataSizeKB { get; init; }

    [JsonPropertyName("clientCount")]
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
    [JsonPropertyName("isVictory")]
    public required bool IsVictory { get; init; }

    [JsonPropertyName("playersSurvived")]
    public required int PlayersSurvived { get; init; }

    [JsonPropertyName("enemiesKilled")]
    public required int EnemiesKilled { get; init; }

    [JsonPropertyName("totalTurns")]
    public required int TotalTurns { get; init; }

    [JsonPropertyName("victoryCondition")]
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
    [JsonPropertyName("totalBattles")]
    public required int TotalBattles { get; init; }

    [JsonPropertyName("totalVictories")]
    public required int TotalVictories { get; init; }

    [JsonPropertyName("totalDataSize")]
    public required long TotalDataSize { get; init; }

    [JsonPropertyName("oldestBattle")]
    public DateTime? OldestBattle { get; init; }

    [JsonPropertyName("newestBattle")]
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
