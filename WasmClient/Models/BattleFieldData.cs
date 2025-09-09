namespace WasmClient.Models
{
    public record BattleFieldData
    {
        public int Turn { get; init; }
        public List<EntityData> Entities { get; init; } = new();
    }
}