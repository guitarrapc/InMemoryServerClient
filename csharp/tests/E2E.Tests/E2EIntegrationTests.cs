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
                Interlocked.Increment(ref connectionsReadyCount);
                // Confirm that this client is ready
                await connection.InvokeAsync("ConfirmConnectionReadyAsync");
            });

            connection.On<BattleStartedData>("BattleStarted", (data) =>
            {
                Interlocked.Increment(ref battleStartedCount);
            });

            connections.Add(connection);
        }

        try
        {
            // Act
            // Start all connections
            foreach (var connection in connections)
            {
                await connection.StartAsync();
            }

            // All clients join the same group
            var groupIds = new List<string>();
            foreach (var connection in connections)
            {
                var groupId = await connection.InvokeAsync<string>("JoinGroupAsync", "BattleTestGroup");
                groupIds.Add(groupId);
            }

            // Wait for battle to auto-start
            await Task.Delay(5000); // Give enough time for the battle to start

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

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
