using WasmClient.Models;

namespace WasmClient.Services;

public class BattleSessionManager
{
    private readonly Dictionary<string, Battle> _battles = new();
    private readonly SettingsService _settings;

    public BattleSessionManager(SettingsService settings)
    {
        _settings = settings;
    }

    public Battle CreateBattle(string groupName, string? serverUrl = null)
    {
        var battle = new Battle
        {
            Id = Guid.NewGuid().ToString(),
            GroupName = groupName,
            ServerUrl = serverUrl ?? _settings.SignalRUrl,
            Status = BattleStatus.Waiting,
            CreatedAt = DateTime.Now
        };

        _battles[battle.Id] = battle;
        return battle;
    }

    public Battle? GetBattle(string battleId) =>
        _battles.TryGetValue(battleId, out var battle) ? battle : null;

    public void RemoveBattle(string battleId)
    {
        _battles.Remove(battleId);
    }

    public IReadOnlyList<Battle> ActiveBattles => _battles.Values.ToList();

    // モックデータでテスト用のバトルを追加
    public void AddMockBattles()
    {
        var mockBattle1 = new Battle
        {
            Id = Guid.NewGuid().ToString(),
            GroupName = "test-group-1",
            ServerUrl = "http://localhost:5000",
            Status = BattleStatus.Waiting,
            CreatedAt = DateTime.Now.AddMinutes(-5)
        };

        var mockBattle2 = new Battle
        {
            Id = Guid.NewGuid().ToString(),
            GroupName = "battle-group-2",
            ServerUrl = "http://localhost:5000",
            Status = BattleStatus.InProgress,
            CreatedAt = DateTime.Now.AddMinutes(-10)
        };

        // モッククライアントを追加
        mockBattle1.Clients.Add(new BattleClient
        {
            ConnectionId = "client-001",
            Type = ConnectionType.SignalR,
            PlayerId = "player-001",
            ConnectedAt = DateTime.Now.AddMinutes(-4)
        });

        mockBattle2.Clients.AddRange([
            new BattleClient { ConnectionId = "client-101", Type = ConnectionType.SignalR },
            new BattleClient { ConnectionId = "client-102", Type = ConnectionType.MagicOnion },
            new BattleClient { ConnectionId = "client-103", Type = ConnectionType.SignalR }
        ]);

        _battles[mockBattle1.Id] = mockBattle1;
        _battles[mockBattle2.Id] = mockBattle2;
    }
}
