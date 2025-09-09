namespace WasmClient.Models;

public class Battle
{
    public string Id { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public BattleStatus Status { get; set; } = BattleStatus.Waiting;
    public List<BattleClient> Clients { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int ClientCount => Clients.Count;
    public bool IsFull => Clients.Count >= 5;
}

public class BattleClient
{
    public string ConnectionId { get; set; } = string.Empty;
    public ConnectionType Type { get; set; }
    public string? PlayerId { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.Now;
    public BattleFieldData? CurrentField { get; set; }
}

public enum BattleStatus
{
    Waiting,
    InProgress,
    Completed
}

public record BattleFieldData
{
    public int Turn { get; init; }
    public List<EntityData> Entities { get; init; } = new();
}

public record EntityData
{
    public string Id { get; init; } = string.Empty;
    public EntityType Type { get; init; }
    public Position Position { get; init; } = new(0, 0);
    public int Health { get; init; }
    public int MaxHealth { get; init; }
}

public enum EntityType
{
    Player,
    Small,
    Medium,
    Large
}

public record Position(int X, int Y);
