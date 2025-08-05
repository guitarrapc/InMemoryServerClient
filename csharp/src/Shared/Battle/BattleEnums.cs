namespace Shared.Battle;

/// <summary>
/// Basic entity type enum for client-server communication
/// </summary>
public enum EntityType
{
    Player,
    Enemy
}

/// <summary>
/// Enemy size categorization
/// </summary>
public enum EnemySize
{
    Small,
    Medium,
    Large
}

/// <summary>
/// Player job types
/// </summary>
public enum PlayerJob
{
    Tank,
    Warrior,
    Mage,
    Archer,
}

/// <summary>
/// Enemy job types
/// </summary>
public enum EnemyJob
{
    Guardian,   // 防御重視型、HP・防御重視
    Bruiser,    // 近接攻撃型、HP・攻撃重視
    Caster,     // 遠距離攻撃型、攻撃・速度重視
    Assassin,   // 速度・攻撃特化型
}

/// <summary>
/// Battle state management enum
/// </summary>
public enum BattleStateType
{
    Connected = 0,
    Ready,
    ReplayCompleted
}
