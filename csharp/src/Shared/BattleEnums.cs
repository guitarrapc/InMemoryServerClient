namespace Shared;

/// <summary>
/// Unified entity type enum for client-server communication
/// </summary>
public enum EntityType
{
    Player,
    SmallEnemy,
    MediumEnemy,
    LargeEnemy
}

/// <summary>
/// Unified job type enum for client-server communication
/// </summary>
public enum JobType
{
    // Player jobs
    Tank,
    Warrior,
    Mage,
    Archer,

    // Enemy jobs
    Bruiser,
    Guardian,
    Assassin,
    Caster
}

/// <summary>
/// Legacy PlayerJob enum for backward compatibility
/// </summary>
public enum PlayerJob
{
    Tank,
    Warrior,
    Mage,
    Archer
}

/// <summary>
/// Legacy EnemyJob enum for backward compatibility
/// </summary>
public enum EnemyJob
{
    Bruiser,    // 近接攻撃型、HP・攻撃重視
    Guardian,   // 防御重視型、HP・防御重視
    Assassin,   // 速度・攻撃特化型
    Caster      // 遠距離攻撃型、攻撃・速度重視
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
