using CliClient.Services;
using Xunit;

namespace CliClient.Tests;

/// <summary>
/// Test class for BattleLogMessageService
/// </summary>
public class BattleLogMessageServiceTests
{
    private readonly IBattleLogMessageService _service = new BattleLogMessageService();

    [Fact]
    public void FormatMemberJoined_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        const string connectionId = "test-connection-123";
        const string groupName = "test-group";

        // Act
        var (message, args) = _service.FormatMemberJoined(connectionId, groupName);

        // Assert
        Assert.Equal("[GROUP] 👤 New member joined! Connection ID: {ConnectionId} in group {GroupName}", message);
        Assert.Equal(2, args.Length);
        Assert.Equal(connectionId, args[0]);
        Assert.Equal(groupName, args[1]);
    }

    [Fact]
    public void FormatGroupMemberCount_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        const int currentCount = 3;
        const int maxMembers = 5;

        // Act
        var (message, args) = _service.FormatGroupMemberCount(currentCount, maxMembers);

        // Assert
        Assert.Equal("[GROUP] 🔢 Total group members: {MemberCount}/{MaxMembers}", message);
        Assert.Equal(2, args.Length);
        Assert.Equal(currentCount, args[0]);
        Assert.Equal(maxMembers, args[1]);
    }

    [Fact]
    public void FormatGroupFull_ShouldReturnCorrectMessageWithNoArgs()
    {
        // Act
        var (message, args) = _service.FormatGroupFull();

        // Assert
        Assert.Equal("[GROUP] ✅ Group is now full! Battle will start soon...", message);
        Assert.Empty(args);
    }

    [Fact]
    public void FormatConnectionsReady_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        const long seed = 12345L;

        // Act
        var (message, args) = _service.FormatConnectionsReady(battleId, seed);

        // Assert
        Assert.Equal("[BATTLE] ========== Connections Ready! ==========", message);
        Assert.Empty(args);
    }

    [Fact]
    public void FormatConnectionsReadyDetails_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        const long seed = 12345L;

        // Act
        var (message, args) = _service.FormatConnectionsReadyDetails(battleId, seed);

        // Assert
        Assert.Contains("Battle ID: {BattleId}", message);
        Assert.Contains("Seed: {Seed}", message);
        Assert.Equal(2, args.Length);
        Assert.Equal(battleId, args[0]);
        Assert.Equal(seed, args[1]);
    }

    [Fact]
    public void FormatBattleVictory_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        const int survivingPlayers = 4;
        const int totalPlayers = 5;

        // Act
        var (message, args) = _service.FormatBattleVictory(survivingPlayers, totalPlayers);

        // Assert
        Assert.Contains("🎉 Victory! All enemies defeated! 🎉", message);
        Assert.Contains("Surviving players: {SurvivingPlayers}/{TotalPlayers}", message);
        Assert.Equal(2, args.Length);
        Assert.Equal(survivingPlayers, args[0]);
        Assert.Equal(totalPlayers, args[1]);
    }

    [Fact]
    public void FormatTurnHeader_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        const int currentTurn = 42;
        const int totalTurns = 100;

        // Act
        var (message, args) = _service.FormatTurnHeader(currentTurn, totalTurns);

        // Assert
        Assert.Equal("[BATTLE] ===== Turn {CurrentTurn}/{TotalTurns} =====", message);
        Assert.Equal(2, args.Length);
        Assert.Equal(currentTurn, args[0]);
        Assert.Equal(totalTurns, args[1]);
    }

    [Fact]
    public void FormatPlayerInfo_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        const string playerName = "Player1";
        const string jobInfo = " (Warrior)";
        const int currentHp = 80;
        const int maxHp = 100;
        const string healthBar = "████████░░";
        const int attack = 25;
        const int defense = 15;
        const int speed = 2;
        const string position = "(10,5)";

        // Act
        var (message, args) = _service.FormatPlayerInfo(playerName, jobInfo, currentHp, maxHp, healthBar, attack, defense, speed, position);

        // Assert
        Assert.Equal("[BATTLE] {PlayerName}{JobInfo}: HP {CurrentHp}/{MaxHp} {HealthBar} ATK:{Attack} DEF:{Defense} SPD:{Speed} Pos:{Position}", message);
        Assert.Equal(9, args.Length);
        Assert.Equal(playerName, args[0]);
        Assert.Equal(jobInfo, args[1]);
        Assert.Equal(currentHp, args[2]);
        Assert.Equal(maxHp, args[3]);
        Assert.Equal(healthBar, args[4]);
        Assert.Equal(attack, args[5]);
        Assert.Equal(defense, args[6]);
        Assert.Equal(speed, args[7]);
        Assert.Equal(position, args[8]);
    }

    [Fact]
    public void FormatConnecting_ShouldReturnCorrectFormatAndArgs()
    {
        // Arrange
        const string serverUrl = "http://localhost:5000";

        // Act
        var (message, args) = _service.FormatConnecting(serverUrl);

        // Assert
        Assert.Equal("Connecting to server: {ServerUrl}", message);
        Assert.Single(args);
        Assert.Equal(serverUrl, args[0]);
    }
}
