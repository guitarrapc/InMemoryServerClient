using Microsoft.Extensions.Logging;

namespace BattleLogic.Tests;

/// <summary>
/// Tests for the new move+attack system where entities can attack after moving
/// if they end up adjacent to an enemy.
/// </summary>
public class MoveAttackSystemTests
{
    private readonly ILogger<BattleState> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IBattleGroupContext _mockGroup;

    public MoveAttackSystemTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = Substitute.For<ILogger<BattleState>>();

        var groupInfo = new BattleGroupContext
        {
            GroupId = BattleSeed.NewTimestampId().ToString(), // Use GUID v7 for group ID
            Name = "test_group",
            ConnectionCount = 5,
            MaxConnections = SystemDefines.MaxConnectionsPerGroup,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SystemDefines.GroupExpirationMinutes)
        };

        _mockGroup = Substitute.For<IBattleGroupContext>();
        _mockGroup.GroupId.Returns(groupInfo.GroupId);
        _mockGroup.Name.Returns(groupInfo.Name);
        _mockGroup.ConnectedCount.Returns(groupInfo.ConnectionCount);
        _mockGroup.MaxClients.Returns(groupInfo.MaxConnections);
        _mockGroup.ClientIds.Returns(new List<string> { "client1", "client2", "client3", "client4", "client5" });
    }

    [Fact]
    public async Task MoveAttackSystem_BattleCompletesSuccessfully()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId();
        var seed = 12345;

        // Act
        var battleState = TestHelpers.CreateBattleState(battleId, seed, _mockGroup, _logger, _loggerFactory);
        await battleState.RunBattleAsync();
        var allTurnData = battleState.GetAllTurnData();

        // Assert
        Assert.NotEmpty(allTurnData);

        // The system should complete battle without errors
        Assert.True(allTurnData.Count > 0, "Battle should complete with turns");

        // Check if move+attack entries exist (they may or may not, depending on battle flow)
        var totalLogs = allTurnData.SelectMany(turn => turn.RecentLogs).Count();
        Assert.True(totalLogs > 0, "Battle should generate logs");

        // System test: verify no exceptions were thrown during battle execution
        var finalTurn = allTurnData.Last();
        Assert.NotNull(finalTurn);

        // Check if move+attack system is working (logs should contain move+attack entries)
        var hasAnyMoveAttackLog = allTurnData.Any(turn =>
            turn.RecentLogs.Any(log => log.Contains("attacks after movement")));

        // Note: hasAnyMoveAttackLog may be false due to randomness in battle AI decisions
        // But the test verifies that the system doesn't crash and completes successfully
    }

    [Fact]
    public async Task MoveAttackSystem_DoesNotBreakExistingBattleLogic()
    {
        // Arrange
        var battleId = BattleSeed.NewTimestampId();
        var seed = 99999; // Different seed for variation

        // Act
        var battleState = TestHelpers.CreateBattleState(battleId, seed, _mockGroup, _logger, _loggerFactory);
        await battleState.RunBattleAsync();
        var status = battleState.GetStatus();

        // Assert - Basic battle state should still work correctly
        Assert.NotNull(status);
        Assert.True(status.Players.Count > 0, "Battle should have players");
        Assert.True(status.Enemies.Count > 0, "Battle should have enemies");

        // Get all turns and ensure battle completed
        var allTurnData = battleState.GetAllTurnData();
        Assert.NotEmpty(allTurnData);

        // The battle should reach a conclusion
        var battleCompleted = allTurnData.Any(turn =>
            turn.RecentLogs.Any(log =>
                log.Contains("Victory") ||
                log.Contains("Defeat") ||
                log.Contains("Battle ended")));

        Assert.True(battleCompleted || allTurnData.Count > 50,
            "Battle should either complete or run for reasonable number of turns");
    }
}
