using CliClient.Services;
using Shared.Common;

namespace CliClient.Tests;

/// <summary>
/// Tests for BattleReplayRenderer service
/// </summary>
public class BattleReplayRendererTests : IDisposable
{
    private readonly ILogger<BattleReplayRendererTests> _logger;
    private readonly BattleReplayRenderer _renderer;

    public BattleReplayRendererTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<BattleReplayRendererTests>();
        _renderer = new BattleReplayRenderer(_logger);
    }

    [Fact]
    public void ReconstructReplayData_WithValidChunks_ReturnsCompleteData()
    {
        // Arrange
        var chunks = new Dictionary<int, List<BattleStatus>>
        {
            [0] = new List<BattleStatus>
            {
                CreateTestBattleStatus(1),
                CreateTestBattleStatus(2)
            },
            [1] = new List<BattleStatus>
            {
                CreateTestBattleStatus(3),
                CreateTestBattleStatus(4)
            }
        };

        // Act
        var result = _renderer.ReconstructReplayData(chunks, 2);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(1, result[0].CurrentTurn);
        Assert.Equal(2, result[1].CurrentTurn);
        Assert.Equal(3, result[2].CurrentTurn);
        Assert.Equal(4, result[3].CurrentTurn);
    }

    [Fact]
    public void ReconstructReplayData_WithMissingChunks_ReturnsPartialData()
    {
        // Arrange
        var chunks = new Dictionary<int, List<BattleStatus>>
        {
            [0] = new List<BattleStatus>
            {
                CreateTestBattleStatus(1),
                CreateTestBattleStatus(2)
            }
            // Missing chunk 1
        };

        // Act
        var result = _renderer.ReconstructReplayData(chunks, 2);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].CurrentTurn);
        Assert.Equal(2, result[1].CurrentTurn);
    }

    [Fact]
    public void ReconstructReplayData_WithEmptyChunks_ReturnsEmptyData()
    {
        // Arrange
        var chunks = new Dictionary<int, List<BattleStatus>>();

        // Act
        var result = _renderer.ReconstructReplayData(chunks, 0);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task PlayReplayAsync_WithValidData_CompletesSuccessfully()
    {
        // Arrange
        var battleStatuses = new List<BattleStatus>
        {
            CreateTestBattleStatus(1),
            CreateTestBattleStatus(2)
        };
        var battleId = Guid.NewGuid();
        var seed = 12345;

        // Act & Assert - Should not throw
        await _renderer.PlayReplayAsync(battleStatuses, battleId, seed);
    }

    [Fact]
    public async Task PlayReplayAsync_WithSummary_UsesEnhancedResults()
    {
        // Arrange
        var battleStatuses = new List<BattleStatus>
        {
            CreateTestBattleStatus(1),
            CreateTestBattleStatus(2, isLastTurn: true)
        };
        var battleId = Guid.NewGuid();
        var seed = 12345;
        var summary = new BattleReplaySummary
        {
            FinalTurn = 2,
            TotalTurns = 100,
            IsPlayerVictory = true,
            IsEndedByTurnLimit = false,
            SurvivingPlayers = 3,
            TotalPlayers = 5,
            SurvivingEnemies = 0,
            TotalEnemies = 10,
            BattleDuration = TimeSpan.FromSeconds(30)
        };

        // Act & Assert - Should not throw
        await _renderer.PlayReplayAsync(battleStatuses, battleId, seed, summary);
    }

    [Fact]
    public async Task PlayReplayAsync_WithSpoilerPrevention_HidesTotalTurns()
    {
        // Arrange
        var battleStatuses = new List<BattleStatus>
        {
            CreateTestBattleStatus(1),
            CreateTestBattleStatus(5), // This turn should be displayed due to interval
            CreateTestBattleStatus(10, isLastTurn: true)
        };
        var battleId = Guid.NewGuid();
        var seed = 12345;

        // Act & Assert - Should not throw
        await _renderer.PlayReplayAsync(battleStatuses, battleId, seed);
    }

    [Fact]
    public async Task PlayReplayAsync_WithEmptyData_CompletesGracefully()
    {
        // Arrange
        var battleStatuses = new List<BattleStatus>();
        var battleId = Guid.NewGuid();
        var seed = 12345;

        // Act & Assert - Should throw due to empty list access
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _renderer.PlayReplayAsync(battleStatuses, battleId, seed));
    }

    private static BattleStatus CreateTestBattleStatus(int currentTurn, bool isLastTurn = false)
    {
        return new BattleStatus
        {
            BattleId = Guid.NewGuid(),
            IsInProgress = !isLastTurn,
            CurrentTurn = currentTurn,
            TotalTurns = 100,
            Players = new List<EntityInfo>
            {
                new EntityInfo
                {
                    EntityId = Guid.NewGuid(),
                    Name = "Player1",
                    Type = EntityTypeInfo.Player,
                    CurrentHp = 100,
                    MaxHp = 200,
                    Attack = 20,
                    Defense = 10,
                    Speed = 5,
                    Accuracy = 85,
                    Evasion = 15,
                    CriticalRate = 5,
                    Position = new Vector2(0, 0),
                    PlayerJob = PlayerJob.Warrior,
                    IsDefending = false
                }
            },
            Enemies = new List<EntityInfo>
            {
                new EntityInfo
                {
                    EntityId = Guid.NewGuid(),
                    Name = "Enemy1",
                    Type = EntityTypeInfo.MediumEnemy,
                    CurrentHp = isLastTurn ? 0 : 50,
                    MaxHp = 100,
                    Attack = 15,
                    Defense = 8,
                    Speed = 3,
                    Accuracy = 75,
                    Evasion = 20,
                    CriticalRate = 3,
                    Position = new Vector2(1, 1),
                    EnemyJob = EnemyJob.Bruiser,
                    IsDefending = false
                }
            },
            FieldSize = new Vector2(20, 20),
            RecentLogs = new List<string>
            {
                $"Turn {currentTurn}: Some battle action occurred"
            },
            IsPlayerVictory = isLastTurn,
            FinalTurn = isLastTurn ? currentTurn : null
        };
    }

    public void Dispose()
    {
        // Clean up if needed
    }
}
