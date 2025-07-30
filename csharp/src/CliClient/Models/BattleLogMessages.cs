namespace CliClient.Models;

/// <summary>
/// Type-safe battle log message structures.
/// All structures implement IFormattable to provide consistent string formatting.
/// </summary>
public static class BattleLogMessages
{
    /// <summary>
    /// Log message for member joined event
    /// </summary>
    public readonly struct MemberJoined : IFormattable
    {
        public string ConnectionId { get; }
        public string GroupName { get; }

        public MemberJoined(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Member joined: {ConnectionId} → Group: {GroupName}";
    }

    /// <summary>
    /// Log message for group member count
    /// </summary>
    public readonly struct GroupMemberCount : IFormattable
    {
        public int CurrentCount { get; }
        public int MaxMembers { get; }

        public GroupMemberCount(int currentCount, int maxMembers)
        {
            CurrentCount = currentCount;
            MaxMembers = maxMembers;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Group members: {CurrentCount}/{MaxMembers}";
    }

    /// <summary>
    /// Log message for group full event
    /// </summary>
    public readonly struct GroupFull : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "Group is now full! Battle can start.";
    }

    /// <summary>
    /// Log message for member left event
    /// </summary>
    public readonly struct MemberLeft : IFormattable
    {
        public string ConnectionId { get; }
        public string GroupName { get; }

        public MemberLeft(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Member left: {ConnectionId} ← Group: {GroupName}";
    }

    /// <summary>
    /// Log message for connections ready event
    /// </summary>
    public readonly struct ConnectionsReady : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "All connections are ready for battle!";
    }

    /// <summary>
    /// Log message for connections ready details
    /// </summary>
    public readonly struct ConnectionsReadyDetails : IFormattable
    {
        public string BattleId { get; }
        public long Seed { get; }

        public ConnectionsReadyDetails(string battleId, long seed)
        {
            BattleId = battleId;
            Seed = seed;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Battle ID: {BattleId}, Seed: {Seed}";
    }

    /// <summary>
    /// Log message for confirming connection
    /// </summary>
    public readonly struct ConfirmingConnection : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "Confirming connection ready...";
    }

    /// <summary>
    /// Log message for connection confirmed
    /// </summary>
    public readonly struct ConnectionConfirmed : IFormattable
    {
        public bool Result { get; }

        public ConnectionConfirmed(bool result)
        {
            Result = result;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Connection confirmed: {Result}";
    }

    /// <summary>
    /// Log message for connection confirmation failed
    /// </summary>
    public readonly struct ConnectionConfirmationFailed : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "Failed to confirm connection readiness";
    }

    /// <summary>
    /// Log message for battle victory
    /// </summary>
    public readonly struct BattleVictory : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "🎉 Victory! All enemies defeated!";
    }

    /// <summary>
    /// Log message for battle defeat
    /// </summary>
    public readonly struct BattleDefeat : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "💀 Defeat! All players have fallen!";
    }

    /// <summary>
    /// Log message for connection ready header
    /// </summary>
    public readonly struct ConnectionReadyHeader : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"========== Connection Ready! ==========";
    }

    /// <summary>
    /// Log message for turn header
    /// </summary>
    public readonly struct TurnHeader : IFormattable
    {
        public int TurnNumber { get; }

        public TurnHeader(int turnNumber)
        {
            TurnNumber = turnNumber;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"========== Turn {TurnNumber} ==========";
    }

    /// <summary>
    /// Log message for player info
    /// </summary>
    public readonly struct PlayerInfo : IFormattable
    {
        public string PlayerId { get; }
        public string Job { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Accuracy { get; }
        public int Evasion { get; }
        public int PosX { get; }
        public int PosY { get; }

        public PlayerInfo(string playerId, string job, int hp, int maxHp, int attack, int defense,
            int accuracy, int evasion, int posX, int posY)
        {
            PlayerId = playerId;
            Job = job;
            Hp = hp;
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            Accuracy = accuracy;
            Evasion = evasion;
            PosX = posX;
            PosY = posY;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Player {PlayerId} ({Job}): HP {Hp}/{MaxHp}, ATK {Attack}, DEF {Defense}, " +
            $"ACC {Accuracy}%, EVA {Evasion}%, Pos ({PosX},{PosY})";
    }

    /// <summary>
    /// Log message for connecting
    /// </summary>
    public readonly struct Connecting : IFormattable
    {
        public string Url { get; }

        public Connecting(string url)
        {
            Url = url;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Connecting to {Url}...";
    }

    /// <summary>
    /// Log message for connection success
    /// </summary>
    public readonly struct ConnectionSuccess : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "✅ Connected successfully!";
    }

    /// <summary>
    /// Log message for replay chunk received
    /// </summary>
    public readonly struct ReplayChunkReceived : IFormattable
    {
        public int ChunkIndex { get; }
        public int TotalChunks { get; }
        public int TurnCount { get; }
        public long Seed { get; }

        public ReplayChunkReceived(int chunkIndex, int totalChunks, int turnCount, long seed)
        {
            ChunkIndex = chunkIndex;
            TotalChunks = totalChunks;
            TurnCount = turnCount;
            Seed = seed;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Replay chunk {ChunkIndex + 1}/{TotalChunks} received (Turns: {TurnCount}, Seed: {Seed})";
    }

    /// <summary>
    /// Log message for replay playback
    /// </summary>
    public readonly struct ReplayPlayback : IFormattable
    {
        public int TotalTurns { get; }

        public ReplayPlayback(int totalTurns)
        {
            TotalTurns = totalTurns;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Starting replay playback ({TotalTurns} turns at 5 fps)...";
    }

    /// <summary>
    /// Log message for batch connection
    /// </summary>
    public readonly struct BatchConnection : IFormattable
    {
        public int Count { get; }
        public string GroupName { get; }

        public BatchConnection(int count, string groupName)
        {
            Count = count;
            GroupName = groupName;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Starting batch connection with {Count} clients to group '{GroupName}'";
    }

    /// <summary>
    /// Log message for connection attempt
    /// </summary>
    public readonly struct ConnectionAttempt : IFormattable
    {
        public int Index { get; }
        public string ClientId { get; }

        public ConnectionAttempt(int index, string clientId)
        {
            Index = index;
            ClientId = clientId;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Client {Index + 1}: {ClientId}";
    }

    /// <summary>
    /// Log message for battle complete notification
    /// </summary>
    public readonly struct BattleCompleteNotification : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "Sending battle complete notification to server...";
    }

    /// <summary>
    /// Log message for battle started
    /// </summary>
    public readonly struct BattleStarted : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "========== Battle Started! ==========";
    }

    /// <summary>
    /// Log message for battle started details
    /// </summary>
    public readonly struct BattleStartedDetails : IFormattable
    {
        public string BattleId { get; }
        public long Seed { get; }

        public BattleStartedDetails(string battleId, long seed)
        {
            BattleId = battleId;
            Seed = seed;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"🏆 Battle ID: {BattleId}, Seed: {Seed}";
    }

    /// <summary>
    /// Log message for group dissolved
    /// </summary>
    public readonly struct GroupDissolved : IFormattable
    {
        public string GroupName { get; }
        public string GroupId { get; }
        public string Reason { get; }

        public GroupDissolved(string groupName, string groupId, string reason)
        {
            GroupName = groupName;
            GroupId = groupId;
            Reason = reason;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Group '{GroupName}' ({GroupId}) dissolved: {Reason}";
    }

    /// <summary>
    /// Log message for group extended
    /// </summary>
    public readonly struct GroupExtended : IFormattable
    {
        public string GroupName { get; }
        public string GroupId { get; }
        public int ExtensionCount { get; }
        public int MaxExtensions { get; }
        public DateTime NewExpiryTime { get; }

        public GroupExtended(string groupName, string groupId, int extensionCount, int maxExtensions, DateTime newExpiryTime)
        {
            GroupName = groupName;
            GroupId = groupId;
            ExtensionCount = extensionCount;
            MaxExtensions = maxExtensions;
            NewExpiryTime = newExpiryTime;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Group '{GroupName}' ({GroupId}) extended {ExtensionCount}/{MaxExtensions}, expires: {NewExpiryTime:HH:mm:ss}";
    }

    /// <summary>
    /// Log message for all chunks received
    /// </summary>
    public readonly struct AllChunksReceived : IFormattable
    {
        public string BattleId { get; }
        public long Seed { get; }

        public AllChunksReceived(string battleId, long seed)
        {
            BattleId = battleId;
            Seed = seed;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"All replay chunks received for battle {BattleId} (Seed: {Seed})";
    }

    /// <summary>
    /// Log message for replay starting
    /// </summary>
    public readonly struct ReplayStarting : IFormattable
    {
        public int TurnCount { get; }
        public int Fps { get; }
        public string BattleId { get; }
        public long Seed { get; }

        public ReplayStarting(int turnCount, int fps, string battleId, long seed)
        {
            TurnCount = turnCount;
            Fps = fps;
            BattleId = battleId;
            Seed = seed;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"Starting replay: {TurnCount} turns at {Fps}fps (Battle: {BattleId}, Seed: {Seed})";
    }

    /// <summary>
    /// Log message for auto disconnecting
    /// </summary>
    public readonly struct AutoDisconnecting : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            "Auto-disconnecting from server...";
    }
}
