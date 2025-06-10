namespace Shared;

/// <summary>
/// Constants used in InMemoryServer
/// </summary>
public static class Constants
{
    /// <summary>
    /// SignalR hub route
    /// </summary>
    public const string HubRoute = "/inmemoryhub";

    /// <summary>
    /// Default server port
    /// </summary>
    public const int DefaultServerPort = 5000;

    /// <summary>
    /// Maximum connections per group
    /// </summary>
    public const int MaxConnectionsPerGroup = 5;

    /// <summary>
    /// Group expiration time in minutes
    /// </summary>
    public const int GroupExpirationMinutes = 10;

    /// <summary>
    /// Battle field width
    /// </summary>
    public const int BattleFieldWidth = 20;

    /// <summary>
    /// Battle field height
    /// </summary>
    public const int BattleFieldHeight = 20;

    /// <summary>
    /// Player HP
    /// </summary>
    public const int PlayerHp = 200;

    /// <summary>
    /// Enemy types and their HP
    /// </summary>
    public static readonly Dictionary<string, int> EnemyHpByType = new Dictionary<string, int>
    {
        { "Small", 100 },
        { "Medium", 200 },
        { "Large", 300 }
    };

    /// <summary>
    /// Minimum attack power
    /// </summary>
    public const int MinAttackPower = 10;

    /// <summary>
    /// Maximum attack power
    /// </summary>
    public const int MaxAttackPower = 30;

    /// <summary>
    /// Minimum defense power
    /// </summary>
    public const int MinDefensePower = 5;

    /// <summary>
    /// Maximum defense power
    /// </summary>
    public const int MaxDefensePower = 15;

    /// <summary>
    /// Minimum movement speed
    /// </summary>
    public const int MinMovementSpeed = 1;

    /// <summary>
    /// Maximum movement speed
    /// </summary>
    public const int MaxMovementSpeed = 3;

    /// <summary>
    /// Defense damage reduction percentage
    /// </summary>
    public const int DefenseDamageReductionPercent = 50;

    /// <summary>
    /// Minimum number of enemies in battle
    /// </summary>
    public const int MinEnemyCount = 10;

    /// <summary>
    /// Maximum number of enemies in battle
    /// </summary>
    public const int MaxEnemyCount = 15;

    /// <summary>
    /// Minimum battle turns
    /// </summary>
    public const int MinBattleTurns = 100;

    /// <summary>
    /// Maximum battle turns
    /// </summary>
    public const int MaxBattleTurns = 300;

    /// <summary>
    /// Battle replay frames per second
    /// </summary>
    public const int BattleReplayFps = 30;

    /// <summary>
    /// Battle replay directory
    /// </summary>
    public const string BattleReplayDirectory = "./battle_replay/";
}
