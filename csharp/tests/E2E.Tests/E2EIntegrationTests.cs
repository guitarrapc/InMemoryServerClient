using Microsoft.AspNetCore.SignalR.Client;
using Shared.Constants;
using Shared.Models;
using Xunit;
using System.Threading;

namespace E2E.Tests;

public class E2EIntegrationTests : IDisposable
{
    private CustomWebApplicationFactory<InMemoryServer.Program>? _factory;

    private CustomWebApplicationFactory<InMemoryServer.Program> CreateFactory()
    {
        _factory = new CustomWebApplicationFactory<InMemoryServer.Program>();
        return _factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Arrange
        var factory = CreateFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task SignalRHub_CanConnectAndDisconnect()
    {
        // Arrange
        var factory = CreateFactory();
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        try
        {
            // Act
            await connection.StartAsync();

            // Assert
            Assert.Equal(HubConnectionState.Connected, connection.State);
        }
        finally
        {
            // Cleanup
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task JoinGroup_CreatesAndJoinsGroup_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        try
        {
            // Act
            await connection.StartAsync();

            // Join group (which will create it if it doesn't exist)
            var groupId = await connection.InvokeAsync<string>("JoinGroupAsync", "TestGroup");

            // Assert
            Assert.NotNull(groupId);
            Assert.NotEmpty(groupId);
        }
        finally
        {
            // Cleanup
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task MultipleClients_CanJoinSameGroup()
    {
        // Arrange
        var factory = CreateFactory();
        var connection1 = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var connection1ReceivedMemberJoined = false;
        MemberJoinedData? memberData = null;

        // Connection1 should receive MemberJoined when connection2 joins
        connection1.On<MemberJoinedData>("MemberJoined", (data) =>
        {
            connection1ReceivedMemberJoined = true;
            memberData = data;
        });

        try
        {
            // Act
            await connection1.StartAsync();
            await connection2.StartAsync();

            // First client joins group
            var groupId1 = await connection1.InvokeAsync<string>("JoinGroupAsync", "MultiTestGroup");
            await Task.Delay(100);

            // Second client joins the same group
            var groupId2 = await connection2.InvokeAsync<string>("JoinGroupAsync", "MultiTestGroup");
            await Task.Delay(100);

            // Assert
            Assert.Equal(groupId1, groupId2); // Both should be in the same group
            Assert.True(connection1ReceivedMemberJoined); // Connection1 should receive the MemberJoined event
            Assert.NotNull(memberData);
            Assert.Equal("MultiTestGroup", memberData.Value.GroupName);
            Assert.Equal(2, memberData.Value.CurrentMemberCount); // Should be 2 after second join
        }
        finally
        {
            // Cleanup
            await connection1.DisposeAsync();
            await connection2.DisposeAsync();
        }
    }

    [Fact]
    public async Task FiveClients_AutoStartBattle_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var connections = new List<HubConnection>();
        var connectionsReadyCount = 0;
        var battleStartedCount = 0;
        var joinedCount = 0;

        // Create 5 connections
        for (int i = 0; i < 5; i++)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                })
                .Build();

            connection.On<ConnectionsReadyData>("ConnectionsReady", async (data) =>
            {
                var current = Interlocked.Increment(ref connectionsReadyCount);
                Console.WriteLine($"ConnectionsReady received by client {current} - BattleId: {data.BattleId}");
                try
                {
                    // Small delay to ensure the server is ready to process the confirmation
                    await Task.Delay(50);
                    // Confirm that this client is ready
                    var confirmed = await connection.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
                    Console.WriteLine($"Client {current} confirmation result: {confirmed}");
                    if (!confirmed)
                    {
                        Console.WriteLine($"WARNING: Client {current} failed to confirm connection ready");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Client {current} failed to confirm connection ready: {ex.Message}");
                }
            });

            connection.On<BattleStartedData>("BattleStarted", (data) =>
            {
                var current = Interlocked.Increment(ref battleStartedCount);
                Console.WriteLine($"BattleStarted received by client {current} - BattleId: {data.BattleId}");
            });

            connection.On<MemberJoinedData>("MemberJoined", (data) =>
            {
                var current = Interlocked.Increment(ref joinedCount);
                Console.WriteLine($"MemberJoined received - Member {current}, Group: {data.GroupName}, Count: {data.CurrentMemberCount}");
            });

            connections.Add(connection);
        }

        try
        {
            // Act
            // Start all connections with small delays to avoid overwhelming the server
            foreach (var connection in connections)
            {
                await connection.StartAsync();
                await Task.Delay(50); // Small delay between connection starts
            }
            Console.WriteLine("All connections started");

            // All clients join the same group sequentially to ensure deterministic order
            var groupIds = new List<string>();
            for (int i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                var groupId = await connection.InvokeAsync<string>("JoinGroupAsync", "BattleTestGroup");
                groupIds.Add(groupId);
                Console.WriteLine($"Client {i + 1} joined group {groupId}");

                // Small delay to ensure proper sequencing
                await Task.Delay(100);
            }

            Console.WriteLine($"All clients joined. Unique group IDs: {groupIds.Distinct().Count()}");

            // Give the server some time to process all joins and potentially auto-start the battle
            Console.WriteLine("Waiting for server to process all joins and auto-start battle...");
            await Task.Delay(1000); // Increased wait time for CI environment

            // Wait for battle to auto-start with timeout and periodic checks
            var timeout = TimeSpan.FromSeconds(30); // Further increased timeout for CI environment
            var checkInterval = TimeSpan.FromMilliseconds(500);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout && (connectionsReadyCount < 5 || battleStartedCount < 5))
            {
                await Task.Delay(checkInterval);
                if (stopwatch.Elapsed.TotalSeconds % 2 < 0.5) // Log every 2 seconds
                {
                    Console.WriteLine($"Progress check - ConnectionsReady: {connectionsReadyCount}/5, BattleStarted: {battleStartedCount}/5, Elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s");
                }
            }

            Console.WriteLine($"Final state - ConnectionsReady: {connectionsReadyCount}/5, BattleStarted: {battleStartedCount}/5, JoinedEvents: {joinedCount}/4, Total elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s");

            // Assert
            Assert.True(groupIds.All(id => id == groupIds[0]), "All clients should be in the same group");
            Assert.Equal(5, connectionsReadyCount); // All 5 clients should receive ConnectionsReady
            Assert.Equal(5, battleStartedCount);   // All 5 clients should receive BattleStarted
        }
        finally
        {
            // Cleanup
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Connection_CanStayConnected()
    {
        // Arrange
        var factory = CreateFactory();
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        try
        {
            // Act
            await connection.StartAsync();
            await Task.Delay(200); // Stay connected for a bit

            // Assert
            Assert.Equal(HubConnectionState.Connected, connection.State);
        }
        finally
        {
            // Cleanup
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClientDisconnection_LeavesGroup_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var connection1 = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var memberLeftReceived = false;
        MemberLeftData? memberLeftData = null;

        connection2.On<MemberLeftData>("MemberLeft", (data) =>
        {
            memberLeftReceived = true;
            memberLeftData = data;
        });

        try
        {
            // Act
            await connection1.StartAsync();
            await connection2.StartAsync();

            // Both clients join the same group
            var groupId1 = await connection1.InvokeAsync<string>("JoinGroupAsync", "LeaveTestGroup");
            var groupId2 = await connection2.InvokeAsync<string>("JoinGroupAsync", "LeaveTestGroup");
            await Task.Delay(100);

            Assert.Equal(groupId1, groupId2); // Ensure they're in the same group

            // Disconnect the first client (this should leave the group)
            await connection1.DisposeAsync();
            await Task.Delay(100); // Wait for the leave event to be processed

            // Assert
            Assert.True(memberLeftReceived);
            Assert.NotNull(memberLeftData);
            Assert.Equal("LeaveTestGroup", memberLeftData.Value.GroupName);
            Assert.Equal(1, memberLeftData.Value.CurrentMemberCount); // One member should remain
        }
        finally
        {
            // Cleanup
            if (connection2.State == HubConnectionState.Connected)
            {
                await connection2.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task TwoClients_JoinGroup_ReceivesEvents()
    {
        // This is a simpler test to ensure basic functionality works in CI
        // Arrange
        var factory = CreateFactory();
        var connection1 = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var memberJoinedReceived = false;
        MemberJoinedData? memberData = null;

        connection1.On<MemberJoinedData>("MemberJoined", (data) =>
        {
            Console.WriteLine($"Connection1 received MemberJoined: Group={data.GroupName}, Count={data.CurrentMemberCount}");
            memberJoinedReceived = true;
            memberData = data;
        });

        try
        {
            // Act
            await connection1.StartAsync();
            await connection2.StartAsync();
            Console.WriteLine("Both connections started");

            var groupId1 = await connection1.InvokeAsync<string>("JoinGroupAsync", "SimpleTestGroup");
            Console.WriteLine($"Connection1 joined group: {groupId1}");

            await Task.Delay(200); // Allow processing time

            var groupId2 = await connection2.InvokeAsync<string>("JoinGroupAsync", "SimpleTestGroup");
            Console.WriteLine($"Connection2 joined group: {groupId2}");

            await Task.Delay(500); // Allow event processing time

            // Assert
            Assert.Equal(groupId1, groupId2);
            Assert.True(memberJoinedReceived, "Connection1 should have received MemberJoined event");
            Assert.NotNull(memberData);
            Assert.Equal("SimpleTestGroup", memberData.Value.GroupName);
            Assert.Equal(2, memberData.Value.CurrentMemberCount);

            Console.WriteLine("Simple test completed successfully");
        }
        finally
        {
            // Cleanup
            await connection1.DisposeAsync();
            await connection2.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
