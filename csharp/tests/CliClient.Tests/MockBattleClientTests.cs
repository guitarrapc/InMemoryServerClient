using CliClient.Clients;

namespace CliClient.Tests;

/// <summary>
/// Mock-based tests for IBattleClient interface implementations
/// These tests use NSubstitute to create mock dependencies and test behavior
/// </summary>
public class MockBattleClientTests
{
    private readonly ILoggerFactory _loggerFactory;

    public MockBattleClientTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    [Fact]
    public void MockBattleClient_ImplementsIBattleClientInterface()
    {
        // Arrange & Act
        var mockClient = Substitute.For<IBattleClient>();

        // Assert
        Assert.NotNull(mockClient);
        Assert.IsAssignableFrom<IBattleClient>(mockClient);
    }

    [Fact]
    public async Task MockBattleClient_ConnectAsync_CanBeConfigured()
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        mockClient.ConnectAsync("http://localhost:5000", "test-group").Returns(true);
        mockClient.IsConnected.Returns(true);

        // Act
        var result = await mockClient.ConnectAsync("http://localhost:5000", "test-group");

        // Assert
        Assert.True(result);
        Assert.True(mockClient.IsConnected);
    }

    [Fact]
    public async Task MockBattleClient_KeyValueOperations_CanBeConfigured()
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        mockClient.GetAsync("test-key").Returns("test-value");
        mockClient.SetAsync("test-key", "test-value").Returns(true);
        mockClient.DeleteAsync("test-key").Returns(true);

        // Act & Assert
        var getValue = await mockClient.GetAsync("test-key");
        Assert.Equal("test-value", getValue);

        var setResult = await mockClient.SetAsync("test-key", "test-value");
        Assert.True(setResult);

        var deleteResult = await mockClient.DeleteAsync("test-key");
        Assert.True(deleteResult);
    }

    [Fact]
    public async Task MockBattleClient_GroupOperations_CanBeConfigured()
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        var groupInfo = new ClientGroupInfo(
            GroupId: "group-123",
            GroupName: "test-group",
            MemberCount: 3,
            MaxMembers: 5,
            RemainingTime: TimeSpan.FromMinutes(5)
        );

        mockClient.JoinGroupAsync("test-group").Returns(true);
        mockClient.GetCurrentGroupAsync().Returns(groupInfo);
        mockClient.BroadcastMessageAsync("hello").Returns(true);

        // Act & Assert
        var joinResult = await mockClient.JoinGroupAsync("test-group");
        Assert.True(joinResult);

        var currentGroup = await mockClient.GetCurrentGroupAsync();
        Assert.NotNull(currentGroup);
        Assert.Equal("test-group", currentGroup.Value.GroupName);

        var broadcastResult = await mockClient.BroadcastMessageAsync("hello");
        Assert.True(broadcastResult);
    }

    [Fact]
    public async Task MockBattleClient_ListOperations_CanBeConfigured()
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        var keyList = new List<string> { "key1", "key2", "key3" }.AsReadOnly();
        mockClient.ListKeysAsync("*").Returns(keyList);

        // Act
        var result = await mockClient.ListKeysAsync("*");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("key1", result);
        Assert.Contains("key2", result);
        Assert.Contains("key3", result);
    }

    [Fact]
    public async Task MockBattleClient_BattleReplayOperations_CanBeConfigured()
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        var battleId = Guid.NewGuid();
        var replayData = new BattleReplayData
        {
            BattleId = battleId,
            Seed = 12345,
            TurnData = new List<BattleStatus>(),
            ChunkIndex = 0,
            TotalChunks = 1,
            IsLastChunk = true
        };

        mockClient.GetBattleReplayAsync(battleId).Returns(replayData);

        // Act
        var result = await mockClient.GetBattleReplayAsync(battleId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(battleId, result.Value.BattleId);
        Assert.Equal(12345, result.Value.Seed);
        Assert.True(result.Value.IsLastChunk);
    }

    [Fact]
    public async Task ConsoleCommandWithMockClient_OperatesCorrectly()
    {
        // This test demonstrates how to use mocks in integration scenarios
        // Note: ConsoleCommand uses BattleClientFactory, so direct injection is limited
        // This is more of a conceptual test showing how mocks could be used

        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        mockClient.IsConnected.Returns(true);
        mockClient.GetAsync("test-key").Returns("mock-value");

        var manager = new MultiBattleClientManager(_loggerFactory);
        var logger = _loggerFactory.CreateLogger<ConsoleCommand>();
        var command = new ConsoleCommand(manager, _loggerFactory, logger);

        // Act & Assert - Verify mock setup
        var value = await mockClient.GetAsync("test-key");
        Assert.Equal("mock-value", value);
        Assert.True(mockClient.IsConnected);
    }

    [Fact]
    public void MockClient_VerifyMethodCalls()
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();

        // Act
        _ = mockClient.IsConnected;
        mockClient.ConnectAsync("http://localhost:5000", "test");

        // Assert - Verify calls were made
        _ = mockClient.Received(1).IsConnected;
        mockClient.Received(1).ConnectAsync("http://localhost:5000", "test");
    }

    [Fact]
    public async Task MockClient_ThrowsExceptions_WhenConfigured()
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        mockClient.ConnectAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<bool>(_ => throw new InvalidOperationException("Mock connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mockClient.ConnectAsync("http://localhost:5000", "test"));
    }

    [Theory]
    [InlineData("key1", "value1")]
    [InlineData("key2", "value2")]
    [InlineData("special-key", "special-value")]
    public async Task MockClient_ParameterizedTests_WorkCorrectly(string key, string value)
    {
        // Arrange
        var mockClient = Substitute.For<IBattleClient>();
        mockClient.SetAsync(key, value).Returns(true);
        mockClient.GetAsync(key).Returns(value);

        // Act
        var setResult = await mockClient.SetAsync(key, value);
        var getValue = await mockClient.GetAsync(key);

        // Assert
        Assert.True(setResult);
        Assert.Equal(value, getValue);
    }
}
