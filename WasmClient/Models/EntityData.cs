namespace WasmClient.Models
{
    public class EntityData
    {
        public string Id { get; set; } = string.Empty;
        public EntityType Type { get; set; }
        public Position Position { get; set; } = new Position(0, 0);
        public int Health { get; set; }
        public int MaxHealth { get; set; }

        public EntityData(string id, EntityType type, Position position, int health, int maxHealth)
        {
            Id = id;
            Type = type;
            Position = position;
            Health = health;
            MaxHealth = maxHealth;
        }
    }
}