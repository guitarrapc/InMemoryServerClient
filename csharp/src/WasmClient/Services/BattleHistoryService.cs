using WasmClient.Models;

namespace WasmClient.Services;

/// <summary>
/// IndexedDBを使用したバトル履歴管理サービス
/// </summary>
public class BattleHistoryService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<BattleHistoryService> _logger;

    public BattleHistoryService(IJSRuntime jsRuntime, ILogger<BattleHistoryService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// バトル完了時に履歴を保存
    /// </summary>
    public async Task SaveBattleHistoryAsync(BattleHistory battleHistory)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("battleStorage.saveBattle", battleHistory);
            _logger.LogInformation("Battle history {BattleId} saved to IndexedDB", battleHistory.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save battle history {BattleId}", battleHistory.SessionId);
            throw;
        }
    }

    /// <summary>
    /// 指定したバトルIDの完全な履歴を取得
    /// </summary>
    public async Task<BattleHistory?> GetBattleHistoryAsync(string battleId)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<BattleHistory?>("battleStorage.getBattle", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve battle history {BattleId}", battleId);
            return null;
        }
    }

    /// <summary>
    /// バトル履歴の一覧を取得（新しい順、サマリー情報のみ）
    /// </summary>
    public async Task<List<BattleHistorySummary>> GetBattleHistoryListAsync(int limit = 50)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<List<BattleHistorySummary>>("battleStorage.getBattleList", limit);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve battle history list");
            return new List<BattleHistorySummary>();
        }
    }

    /// <summary>
    /// 指定したバトル履歴を削除
    /// </summary>
    public async Task DeleteBattleHistoryAsync(string battleId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("battleStorage.deleteBattle", battleId);
            _logger.LogInformation("Battle history {BattleId} deleted from IndexedDB", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete battle history {BattleId}", battleId);
            throw;
        }
    }

    /// <summary>
    /// IndexedDBを完全にクリアして再初期化
    /// </summary>
    public async Task ClearAllBattleHistoryAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("battleStorage.clearAllBattles");
            _logger.LogInformation("All battle history cleared from IndexedDB");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear IndexedDB");
            throw;
        }
    }

    /// <summary>
    /// 保存されているバトル数とディスク使用量を取得
    /// </summary>
    public async Task<BattleHistoryStats> GetStorageStatsAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<BattleHistoryStats>("battleStorage.getStorageStats");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve storage stats");
            return new BattleHistoryStats();
        }
    }
}
