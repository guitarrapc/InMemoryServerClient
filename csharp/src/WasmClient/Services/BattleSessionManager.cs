using WasmClient.Models;
using BattleStatus = WasmClient.Models.BattleStatus;

namespace WasmClient.Services;

/// <summary>
/// Battle session management service for WasmClient
/// </summary>
public class BattleSessionManager
{
    private readonly Dictionary<string, BattleSessionModel> _battles = new();
    private readonly IConnectionFactory _connectionFactory;
    private readonly BattleHistoryService _battleHistory;
    private readonly SettingsService _settings;
    private readonly ILogger<BattleSessionManager> _logger;

    public BattleSessionManager(
        IConnectionFactory connectionFactory,
        BattleHistoryService battleHistory,
        SettingsService settings,
        ILogger<BattleSessionManager> logger)
    {
        _connectionFactory = connectionFactory;
        _battleHistory = battleHistory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<BattleSessionModel> CreateBattleAsync(string groupName, string? serverUrl = null)
    {
        var battle = new BattleSessionModel(
            _connectionFactory,
            _logger)
        {
            Id = Guid.NewGuid().ToString(),
            GroupName = groupName,
            ServerUrl = serverUrl ?? _settings.SignalRUrl,
            Status = BattleStatus.Waiting,
            CreatedAt = DateTime.Now
        };

        // バトル完了イベントを購読
        battle.OnBattleCompleted += OnBattleCompleted;

        _battles[battle.Id] = battle;
        _logger.LogInformation("Created battle {BattleId} with group {GroupName}", battle.Id, groupName);
        return battle;
    }

    public BattleSessionModel? GetBattle(string battleId) => _battles.TryGetValue(battleId, out var battle) ? battle : null;

    public async Task RemoveBattleAsync(string battleId)
    {
        if (_battles.TryGetValue(battleId, out var battle))
        {
            battle.OnBattleCompleted -= OnBattleCompleted;
            await battle.DisposeAsync();
            _battles.Remove(battleId);
            _logger.LogInformation("Removed battle {BattleId}", battleId);
        }
    }

    public IReadOnlyList<BattleSessionModel> ActiveBattles => _battles.Values.ToList();

    /// <summary>
    /// IndexedDBから過去のバトル履歴一覧を取得
    /// </summary>
    public async Task<List<BattleHistorySummary>> GetBattleHistoryAsync(int limit = 50)
    {
        return await _battleHistory.GetBattleHistoryListAsync(limit);
    }

    /// <summary>
    /// 指定したバトル履歴を読み込み専用バトルとして復元
    /// </summary>
    public async Task<BattleSessionModel?> LoadBattleFromHistoryAsync(string battleId)
    {
        var history = await _battleHistory.GetBattleHistoryAsync(battleId);
        if (history == null)
        {
            _logger.LogWarning("Battle history {BattleId} not found", battleId);
            return null;
        }

        var battle = new BattleSessionModel(_connectionFactory, _logger)
        {
            Id = history.BattleId,
            GroupName = history.GroupName,
            ServerUrl = history.ServerUrl,
            Status = BattleStatus.Completed,
            CreatedAt = history.CreatedAt,
            IsHistoricalBattle = true,
            BattleHistory = history
        };

        _battles[battle.Id] = battle;
        _logger.LogInformation("Loaded historical battle {BattleId}", battleId);
        return battle;
    }

    /// <summary>
    /// バトル履歴を削除
    /// </summary>
    public async Task DeleteBattleHistoryAsync(string battleId)
    {
        await _battleHistory.DeleteBattleHistoryAsync(battleId);
        _logger.LogInformation("Deleted battle history {BattleId}", battleId);
    }

    /// <summary>
    /// バトル完了時にIndexedDBに保存
    /// </summary>
    private async void OnBattleCompleted(BattleSessionModel battle, BattleResult result)
    {
        try
        {
            var history = new BattleHistory
            {
                BattleId = battle.Id,
                CreatedAt = battle.CreatedAt,
                CompletedAt = DateTime.UtcNow,
                GroupName = battle.GroupName,
                ServerUrl = battle.ServerUrl,
                TotalTurns = battle.TotalTurns,
                ReplayData = battle.ReplayData.ToList(),
                Result = result,
                ParticipatingClients = battle.Clients.Select(c => new BattleClientHistory
                {
                    ConnectionId = c.ConnectionId,
                    PlayerId = c.PlayerId ?? "Unknown",
                    ConnectionType = c.Type,
                    ConnectedAt = c.ConnectedAt
                }).ToList()
            };

            await _battleHistory.SaveBattleHistoryAsync(history);
            _logger.LogInformation("Battle history saved for {BattleId}", battle.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save battle history for {BattleId}", battle.Id);
        }
    }

    /// <summary>
    /// 履歴からバトルセッションを復元（履歴表示用）
    /// </summary>
    public async Task<BattleSessionModel?> LoadHistoricalBattleAsync(BattleHistory battleHistory)
    {
        try
        {
            _logger.LogInformation("Loading historical battle {BattleId}", battleHistory.BattleId);

            // Create historical battle session with proper client information
            var historicalSession = BattleSessionModel.CreateHistorical(battleHistory);

            _logger.LogInformation("Historical battle {BattleId} loaded with {ClientCount} clients",
                battleHistory.BattleId, historicalSession.Clients.Count);

            return historicalSession;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load historical battle {BattleId}", battleHistory.BattleId);
            return null;
        }
    }
}
