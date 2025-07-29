using Shared.Battle;

namespace CliClient.Services;

/// <summary>
/// Default implementation of battle log message formatting service
/// Provides standardized log message formatting for battle events
/// </summary>
public class BattleLogMessageService : IBattleLogMessageService
{
    // Group-related messages
    public (string message, object?[] args) FormatMemberJoined(string connectionId, string groupName)
        => ("[GROUP] 👤 New member joined! Connection ID: {ConnectionId} in group {GroupName}", [connectionId, groupName]);

    public (string message, object?[] args) FormatMemberLeft(string connectionId, string groupName)
        => ("[GROUP] 👋 Member left! Connection ID: {ConnectionId} from group {GroupName}", [connectionId, groupName]);

    public (string message, object?[] args) FormatGroupMemberCount(int currentCount, int maxMembers)
        => ("[GROUP] 🔢 Total group members: {MemberCount}/{MaxMembers}", [currentCount, maxMembers]);

    public (string message, object?[] args) FormatGroupFull()
        => ("[GROUP] ✅ Group is now full! Battle will start soon...", []);

    public (string message, object?[] args) FormatGroupDissolved(string groupName, string groupId, string reason)
        => ("[GROUP] ❌ Group dissolved! Group: {GroupName} (ID: {GroupId}). Reason: {Reason}", [groupName, groupId, reason]);

    public (string message, object?[] args) FormatGroupExtended(string groupName, string groupId, int extensionCount, int maxExtensions, DateTime newExpiryTime)
        => ("[GROUP] ⏰ Group extended! Group: {GroupName} (ID: {GroupId}). Extension count: {ExtensionCount}/{MaxExtensions}. New expiry time: {NewExpiryTime:yyyy-MM-dd HH:mm:ss}", [groupName, groupId, extensionCount, maxExtensions, newExpiryTime]);

    // Battle lifecycle messages
    public (string message, object?[] args) FormatConnectionsReady(Guid battleId, long seed)
        => ("[BATTLE] ========== Connections Ready! ==========", []);

    public (string message, object?[] args) FormatConnectionsReadyDetails(Guid battleId, long seed)
        => ("[BATTLE] 🔄 Battle ID: {BattleId}\n[BATTLE] 🎲 Seed: {Seed}\n[BATTLE] Group is full! All clients connected.\n[BATTLE] Sending confirmation to server...\n[BATTLE] ========================================", [battleId, seed]);

    public (string message, object?[] args) FormatBattleStarted(Guid battleId, long seed)
        => ("[BATTLE] ========== Battle Started! ==========", []);

    public (string message, object?[] args) FormatBattleStartedDetails(Guid battleId, long seed)
        => ("[BATTLE] 🏆 Battle ID: {BattleId}\n[BATTLE] 🎲 Seed: {Seed}\n[BATTLE] ====================================", [battleId, seed]);

    public (string message, object?[] args) FormatConfirmingConnection()
        => ("[BATTLE] Confirming connection ready status...", []);

    public (string message, object?[] args) FormatConnectionConfirmed(bool result)
        => ("[BATTLE] ✅ Connection ready confirmation sent successfully. Result: {Result}", [result]);

    public (string message, object?[] args) FormatConnectionConfirmationFailed()
        => ("[BATTLE] ❌ Failed to confirm connection ready status", []);

    // Battle replay messages
    public (string message, object?[] args) FormatReplayChunkReceived(int chunkIndex, int totalChunks, int turnCount, Guid battleId, long seed)
        => ("[BATTLE] Received replay chunk {ChunkIndex}/{TotalChunks} with {TurnCount} turns - BattleId: {BattleId}, Seed: {Seed}", [chunkIndex, totalChunks, turnCount, battleId, seed]);

    public (string message, object?[] args) FormatAllChunksReceived(Guid battleId, long seed)
        => ("[BATTLE] All chunks received. Starting replay playback - BattleId: {BattleId}, Seed: {Seed}", [battleId, seed]);

    public (string message, object?[] args) FormatReplayStarting(int turnCount, int fps, Guid battleId, long seed)
        => ("[BATTLE] Playing {TurnCount} turns at {Fps} FPS - BattleId: {BattleId}, Seed: {Seed}", [turnCount, fps, battleId, seed]);

    public (string message, object?[] args) FormatReplayCompleted(Guid battleId, long seed)
        => ("[BATTLE] Battle completed - BattleId: {BattleId}, Seed: {Seed} (replay completed)", [battleId, seed]);

    // Battle status display messages
    public (string message, object?[] args) FormatTurnHeader(int currentTurn, int totalTurns)
        => ("[BATTLE] ===== Turn {CurrentTurn}/{TotalTurns} =====", [currentTurn, totalTurns]);

    public (string message, object?[] args) FormatPlayersAlive(int alivePlayers, int totalPlayers)
        => ("[BATTLE] Players alive: {AlivePlayers}/{TotalPlayers}", [alivePlayers, totalPlayers]);

    public (string message, object?[] args) FormatEnemiesAlive(int aliveEnemies, int totalEnemies)
        => ("[BATTLE] Enemies alive: {AliveEnemies}/{TotalEnemies}", [aliveEnemies, totalEnemies]);

    public (string message, object?[] args) FormatPlayerInfo(string playerName, string jobInfo, int currentHp, int maxHp, string healthBar, int attack, int defense, int speed, string position)
        => ("[BATTLE] {PlayerName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}", [playerName, jobInfo, currentHp, maxHp, healthBar, attack, defense, speed, position]);

    public (string message, object?[] args) FormatEnemyInfo(string enemyName, string jobInfo, int currentHp, int maxHp, string healthBar, int attack, int defense, int speed, string position)
        => ("[BATTLE] {EnemyName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}", [enemyName, jobInfo, currentHp, maxHp, healthBar, attack, defense, speed, position]);

    public (string message, object?[] args) FormatRecentActionsHeader()
        => ("[BATTLE] Recent actions:", []);

    public (string message, object?[] args) FormatActionLog(string log)
        => ("[BATTLE] > {Log}", [log]);

    public (string message, object?[] args) FormatTurnSeparator()
        => ("[BATTLE] ========================================", []);

    // Battle result messages
    public (string message, object?[] args) FormatBattleVictory(int survivingPlayers, int totalPlayers)
        => ("[BATTLE REPLAY] 🎉 Victory! All enemies defeated! 🎉\n[BATTLE REPLAY] Surviving players: {SurvivingPlayers}/{TotalPlayers}", [survivingPlayers, totalPlayers]);

    public (string message, object?[] args) FormatBattleDefeat(int remainingEnemies, int totalEnemies)
        => ("[BATTLE REPLAY] ❌ Defeat! All players defeated! ❌\n[BATTLE REPLAY] Remaining enemies: {RemainingEnemies}/{TotalEnemies}", [remainingEnemies, totalEnemies]);

    public (string message, object?[] args) FormatBattleEndedByTurnLimit()
        => ("[BATTLE REPLAY] ⏰ Battle ended due to turn limit reached!", []);

    public (string message, object?[] args) FormatBattleEndedByElimination()
        => ("[BATTLE REPLAY] ⚔️ Battle ended due to complete elimination!", []);

    public (string message, object?[] args) FormatBattleTotalTurns(int currentTurn, int totalTurns)
        => ("[BATTLE REPLAY] Total turns: {CurrentTurn}/{TotalTurns}", [currentTurn, totalTurns]);

    // Connection management messages
    public (string message, object?[] args) FormatAutoDisconnecting()
        => ("[BATTLE] Auto-disconnecting after battle replay completion", []);

    public (string message, object?[] args) FormatConnecting(string serverUrl)
        => ("Connecting to server: {ServerUrl}", [serverUrl]);

    public (string message, object?[] args) FormatConnected()
        => ("Connected to server", []);

    public (string message, object?[] args) FormatDisconnecting()
        => ("Disconnecting from server", []);

    public (string message, object?[] args) FormatDisconnected()
        => ("Disconnected from server", []);
}
