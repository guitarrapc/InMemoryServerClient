using WasmClient.Models;

namespace WasmClient.Services;

/// <summary>
/// Battle session management service for WasmClient
/// </summary>
public class BattleSessionManager
{
    private readonly Dictionary<string, BattleSessionModel> _battles = new();
    private readonly IConnectionFactory _connectionFactory;
    private readonly SettingsService _settings;
    private readonly ILogger<BattleSessionManager> _logger;

    public BattleSessionManager(IConnectionFactory connectionFactory, SettingsService settings, ILogger<BattleSessionManager> logger)
    {
        _connectionFactory = connectionFactory;
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

        _battles[battle.Id] = battle;
        _logger.LogInformation("Created battle {BattleId} with group {GroupName}", battle.Id, groupName);
        return battle;
    }

    public BattleSessionModel? GetBattle(string battleId) => _battles.TryGetValue(battleId, out var battle) ? battle : null;

    public async Task RemoveBattleAsync(string battleId)
    {
        if (_battles.TryGetValue(battleId, out var battle))
        {
            await battle.DisposeAsync();
            _battles.Remove(battleId);
            _logger.LogInformation("Removed battle {BattleId}", battleId);
        }
    }

    public IReadOnlyList<BattleSessionModel> ActiveBattles => _battles.Values.ToList();
}
