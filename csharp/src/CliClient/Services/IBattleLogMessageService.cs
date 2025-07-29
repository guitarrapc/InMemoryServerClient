using Shared.Battle;

namespace CliClient.Services;

/// <summary>
/// Battle log message formatting service interface
/// Provides standardized log message formatting for battle events
/// </summary>
public interface IBattleLogMessageService
{
    // Group-related messages
    (string message, object?[] args) FormatMemberJoined(string connectionId, string groupName);
    (string message, object?[] args) FormatMemberLeft(string connectionId, string groupName);
    (string message, object?[] args) FormatGroupMemberCount(int currentCount, int maxMembers);
    (string message, object?[] args) FormatGroupFull();
    (string message, object?[] args) FormatGroupDissolved(string groupName, string groupId, string reason);
    (string message, object?[] args) FormatGroupExtended(string groupName, string groupId, int extensionCount, int maxExtensions, DateTime newExpiryTime);

    // Battle lifecycle messages
    (string message, object?[] args) FormatConnectionsReady(Guid battleId, long seed);
    (string message, object?[] args) FormatConnectionsReadyDetails(Guid battleId, long seed);
    (string message, object?[] args) FormatBattleStarted(Guid battleId, long seed);
    (string message, object?[] args) FormatBattleStartedDetails(Guid battleId, long seed);
    (string message, object?[] args) FormatConfirmingConnection();
    (string message, object?[] args) FormatConnectionConfirmed(bool result);
    (string message, object?[] args) FormatConnectionConfirmationFailed();

    // Battle replay messages
    (string message, object?[] args) FormatReplayChunkReceived(int chunkIndex, int totalChunks, int turnCount, Guid battleId, long seed);
    (string message, object?[] args) FormatAllChunksReceived(Guid battleId, long seed);
    (string message, object?[] args) FormatReplayStarting(int turnCount, int fps, Guid battleId, long seed);
    (string message, object?[] args) FormatReplayCompleted(Guid battleId, long seed);

    // Battle status display messages
    (string message, object?[] args) FormatTurnHeader(int currentTurn, int totalTurns);
    (string message, object?[] args) FormatPlayersAlive(int alivePlayers, int totalPlayers);
    (string message, object?[] args) FormatEnemiesAlive(int aliveEnemies, int totalEnemies);
    (string message, object?[] args) FormatPlayerInfo(string playerName, string jobInfo, int currentHp, int maxHp, string healthBar, int attack, int defense, int speed, string position);
    (string message, object?[] args) FormatEnemyInfo(string enemyName, string jobInfo, int currentHp, int maxHp, string healthBar, int attack, int defense, int speed, string position);
    (string message, object?[] args) FormatRecentActionsHeader();
    (string message, object?[] args) FormatActionLog(string log);
    (string message, object?[] args) FormatTurnSeparator();

    // Battle result messages
    (string message, object?[] args) FormatBattleVictory(int survivingPlayers, int totalPlayers);
    (string message, object?[] args) FormatBattleDefeat(int remainingEnemies, int totalEnemies);
    (string message, object?[] args) FormatBattleEndedByTurnLimit();
    (string message, object?[] args) FormatBattleEndedByElimination();
    (string message, object?[] args) FormatBattleTotalTurns(int currentTurn, int totalTurns);

    // Connection management messages
    (string message, object?[] args) FormatAutoDisconnecting();
    (string message, object?[] args) FormatConnecting(string serverUrl);
    (string message, object?[] args) FormatConnected();
    (string message, object?[] args) FormatDisconnecting();
    (string message, object?[] args) FormatDisconnected();
}
