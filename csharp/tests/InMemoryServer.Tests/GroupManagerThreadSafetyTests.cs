using InMemoryServer.Services;
using InMemoryServer.Models;
using Shared.BattleServer.Constants;

namespace InMemoryServer.Tests;

/// <summary>
/// Thread safety tests for GroupManagerActor
/// </summary>
public class GroupManagerThreadSafetyTests : IDisposable
{
    private readonly ILogger<GroupManagerActor> _logger;
    private readonly ILogger<ConnectionManager> _connectionManagerLogger;
    private readonly ConnectionManager _connectionManager;
    private readonly GroupManagerActor _groupManagerActor;
    private readonly IGroupManager _groupManager;

    public GroupManagerThreadSafetyTests()
    {
        _logger = Substitute.For<ILogger<GroupManagerActor>>();
        _connectionManagerLogger = Substitute.For<ILogger<ConnectionManager>>();
        _connectionManager = new ConnectionManager(_connectionManagerLogger);
        _groupManagerActor = new GroupManagerActor(_logger, _connectionManager);
        _groupManager = new GroupManagerAdapter(_groupManagerActor);
    }

    [Fact]
    public async Task JoinGroupAsync_ConcurrentAccess_ShouldMaintainCorrectConnectionCount()
    {
        // Arrange
        const string groupName = "concurrent_test_group";
        const int numberOfConcurrentClients = 10;

        // Register connections
        var connectionIds = new List<string>();
        for (int i = 0; i < numberOfConcurrentClients; i++)
        {
            var connectionId = $"client_{i}";
            _connectionManager.RegisterConnection(connectionId, ConnectionProtocol.SignalR);
            connectionIds.Add(connectionId);
        }

        // Act - Concurrent join operations
        var tasks = connectionIds.Select(connectionId =>
            Task.Run(async () => await _groupManager.JoinGroupAsync(connectionId, groupName))
        );

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.NotNull(result));

        // All clients should be in groups
        var allGroups = (await _groupManager.GetAllGroupsAsync()).ToList();
        var totalConnectionsInGroups = allGroups.Sum(g => g.ConnectionCount);
        Assert.Equal(numberOfConcurrentClients, totalConnectionsInGroups);

        // No group should exceed max connections
        Assert.All(allGroups, group =>
            Assert.True(group.ConnectionCount <= SystemDefines.MaxConnectionsPerGroup));

        // First group should be full if we have enough clients
        if (numberOfConcurrentClients >= SystemDefines.MaxConnectionsPerGroup)
        {
            // At least one group should be full
            Assert.Contains(allGroups, g => g.ConnectionCount == SystemDefines.MaxConnectionsPerGroup);
        }
    }

    [Fact]
    public async Task JoinGroupAsync_RaceConditionOnGroupFull_ShouldNotExceedMaxConnections()
    {
        // Arrange
        const string groupName = "race_condition_test";
        const int exactMaxClients = SystemDefines.MaxConnectionsPerGroup;

        // Register connections
        var connectionIds = new List<string>();
        for (int i = 0; i < exactMaxClients; i++)
        {
            var connectionId = $"race_client_{i}";
            _connectionManager.RegisterConnection(connectionId, ConnectionProtocol.SignalR);
            connectionIds.Add(connectionId);
        }

        // Act - All clients try to join the same group simultaneously
        var tasks = connectionIds.Select(connectionId =>
            Task.Run(async () => await _groupManager.JoinGroupAsync(connectionId, groupName))
        );

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.NotNull(result));

        // Debug: Print all group information
        var allGroups = (await _groupManager.GetAllGroupsAsync()).ToList();
        Console.WriteLine($"Total groups created: {allGroups.Count}");
        foreach (var group in allGroups)
        {
            Console.WriteLine($"Group: {group.Name} (ID: {group.GroupId}), Connections: {group.ConnectionCount}");
        }

        // Get the target group
        var targetGroup = allGroups.FirstOrDefault(g => g.Name == groupName);
        Assert.NotNull(targetGroup);

        // The group should not exceed max connections
        Assert.True(targetGroup.ConnectionCount <= SystemDefines.MaxConnectionsPerGroup);
        Assert.Equal(SystemDefines.MaxConnectionsPerGroup, targetGroup.ConnectionCount);

        // Verify ClientIds list consistency
        Assert.Equal(targetGroup.ConnectionCount, targetGroup.ClientIds.Count);
        Assert.Equal(targetGroup.ClientIds.Count, targetGroup.ClientIds.Distinct().Count()); // No duplicates
    }

    [Fact]
    public async Task LeaveGroupAsync_ConcurrentAccess_ShouldMaintainCorrectConnectionCount()
    {
        // Arrange
        const string groupName = "leave_test_group";
        const int numberOfClients = 5;

        // Register and join clients
        var connectionIds = new List<string>();
        for (int i = 0; i < numberOfClients; i++)
        {
            var connectionId = $"leave_client_{i}";
            _connectionManager.RegisterConnection(connectionId, ConnectionProtocol.SignalR);
            connectionIds.Add(connectionId);
            await _groupManager.JoinGroupAsync(connectionId, groupName);
        }

        // Verify initial state
        var group = (await _groupManager.GetAllGroupsAsync()).First(g => g.Name == groupName);
        Assert.Equal(numberOfClients, group.ConnectionCount);

        // Act - Concurrent leave operations
        var tasks = connectionIds.Select(connectionId =>
            Task.Run(async () => await _groupManager.LeaveGroupAsync(connectionId))
        );

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.True(result.newCount >= 0));

        // Group should be dissolved or empty
        var remainingGroups = (await _groupManager.GetAllGroupsAsync()).Where(g => g.Name == groupName).ToList();
        if (remainingGroups.Any())
        {
            var remainingGroup = remainingGroups.First();
            Assert.Equal(0, remainingGroup.ConnectionCount);
            Assert.Empty(remainingGroup.ClientIds);
        }
    }

    [Fact]
    public async Task ExtendGroupWaitingTime_ConcurrentAccess_ShouldNotExceedMaxExtensions()
    {
        // Arrange
        const string groupName = "extension_test_group";
        var connectionId = "extension_test_client";
        _connectionManager.RegisterConnection(connectionId, ConnectionProtocol.SignalR);

        var group = await _groupManager.JoinGroupAsync(connectionId, groupName);
        var groupId = group.GroupId;

        // Act - Multiple concurrent extension attempts
        const int numberOfExtensionAttempts = SystemDefines.MaxGroupExtensions + 5; // More than allowed
        var tasks = Enumerable.Range(0, numberOfExtensionAttempts)
            .Select(_ => Task.Run(() => _groupManager.ExtendGroupWaitingTimeAsync(groupId)));

        var results = await Task.WhenAll(tasks);

        // Assert
        var successfulExtensions = results.Count(r => r);
        Assert.True(successfulExtensions <= SystemDefines.MaxGroupExtensions);

        // Verify final group state
        var finalGroup = await _groupManager.GetGroupInfoAsync(groupId);
        Assert.NotNull(finalGroup);
        Assert.True(finalGroup.ExtensionCount <= SystemDefines.MaxGroupExtensions);
    }

    [Fact]
    public async Task MixedOperations_ConcurrentJoinAndLeave_ShouldMaintainConsistency()
    {
        // Arrange
        const string groupName = "mixed_operations_test";
        const int numberOfOperations = 20;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Timeout for test
        var ct = cts.Token;

        var random = new Random(42); // Fixed seed for reproducibility
        var connectionIds = new List<string>();

        // Register all potential connections
        for (int i = 0; i < numberOfOperations; i++)
        {
            var connectionId = $"mixed_client_{i}";
            _connectionManager.RegisterConnection(connectionId, ConnectionProtocol.SignalR);
            connectionIds.Add(connectionId);
        }

        // Act - Mixed concurrent operations
        var tasks = new List<Task>();

        for (int i = 0; i < numberOfOperations; i++)
        {
            var connectionId = connectionIds[i];

            if (random.Next(2) == 0) // 50% chance to join
            {
                tasks.Add(Task.Run(async () => await _groupManager.JoinGroupAsync(connectionId, groupName)));
            }
            else // 50% chance to join then leave
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _groupManager.JoinGroupAsync(connectionId, groupName);
                    // Small yield to increase chance of race conditions
                    await Task.Yield();
                    await _groupManager.LeaveGroupAsync(connectionId);
                }, ct));
            }
        }

        await Task.WhenAll(tasks);

        // Assert - Verify data consistency
        var allGroups = (await _groupManager.GetAllGroupsAsync()).ToList();

        foreach (var group in allGroups)
        {
            // ConnectionCount should match ClientIds.Count
            Assert.Equal(group.ConnectionCount, group.ClientIds.Count);

            // No group should exceed max connections
            Assert.True(group.ConnectionCount <= SystemDefines.MaxConnectionsPerGroup);

            // No duplicate client IDs
            Assert.Equal(group.ClientIds.Count, group.ClientIds.Distinct().Count());

            // All client IDs should be non-empty
            Assert.All(group.ClientIds, clientId => Assert.False(string.IsNullOrEmpty(clientId)));
        }
    }

    public void Dispose()
    {
        if (_groupManager is IDisposable disposable)
            disposable.Dispose();
        _groupManagerActor?.Dispose();
    }
}
