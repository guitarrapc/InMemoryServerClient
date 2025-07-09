using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Constants;
using Shared.Contracts;
using Shared.Models;
using Xunit;

namespace E2ETests;

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
    public async Task CreateGroup_AndJoinGroup_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var groupCreated = false;
        var joinedGroup = false;
        GroupInfo? createdGroupInfo = null;
        
        connection.On<GroupInfo>("GroupCreated", (groupInfo) =>
        {
            groupCreated = true;
            createdGroupInfo = groupInfo;
        });

        connection.On<GroupInfo>("JoinedGroup", (groupInfo) =>
        {
            joinedGroup = true;
        });

        try
        {
            // Act
            await connection.StartAsync();
            
            // Create group
            await connection.InvokeAsync("CreateGroup", "TestGroup");
            
            // Wait for group creation
            await Task.Delay(100);
            
            // Join group
            if (createdGroupInfo != null)
            {
                await connection.InvokeAsync("JoinGroup", createdGroupInfo.GroupId);
            }
            
            // Wait for join
            await Task.Delay(100);

            // Assert
            Assert.True(groupCreated);
            Assert.True(joinedGroup);
            Assert.NotNull(createdGroupInfo);
            Assert.Equal("TestGroup", createdGroupInfo.Name);
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

        GroupInfo? createdGroupInfo = null;
        var client1Joined = false;
        var client2Joined = false;

        connection1.On<GroupInfo>("GroupCreated", (groupInfo) =>
        {
            createdGroupInfo = groupInfo;
        });

        connection1.On<GroupInfo>("JoinedGroup", (groupInfo) =>
        {
            client1Joined = true;
        });

        connection2.On<GroupInfo>("JoinedGroup", (groupInfo) =>
        {
            client2Joined = true;
        });

        try
        {
            // Act
            await connection1.StartAsync();
            await connection2.StartAsync();

            // Create group with first client
            await connection1.InvokeAsync("CreateGroup", "MultiTestGroup");
            await Task.Delay(100);

            // Both clients join the group
            if (createdGroupInfo != null)
            {
                await connection1.InvokeAsync("JoinGroup", createdGroupInfo.GroupId);
                await connection2.InvokeAsync("JoinGroup", createdGroupInfo.GroupId);
            }

            await Task.Delay(100);

            // Assert
            Assert.NotNull(createdGroupInfo);
            Assert.True(client1Joined);
            Assert.True(client2Joined);
        }
        finally
        {
            // Cleanup
            await connection1.DisposeAsync();
            await connection2.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartBattle_WithMultipleClients_WorksCorrectly()
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

        GroupInfo? createdGroupInfo = null;
        var battleStarted1 = false;
        var battleStarted2 = false;

        connection1.On<GroupInfo>("GroupCreated", (groupInfo) =>
        {
            createdGroupInfo = groupInfo;
        });

        connection1.On("BattleStarted", () =>
        {
            battleStarted1 = true;
        });

        connection2.On("BattleStarted", () =>
        {
            battleStarted2 = true;
        });

        try
        {
            // Act
            await connection1.StartAsync();
            await connection2.StartAsync();

            // Create and join group
            await connection1.InvokeAsync("CreateGroup", "BattleTestGroup");
            await Task.Delay(100);

            if (createdGroupInfo != null)
            {
                await connection1.InvokeAsync("JoinGroup", createdGroupInfo.GroupId);
                await connection2.InvokeAsync("JoinGroup", createdGroupInfo.GroupId);
                await Task.Delay(100);

                // Start battle
                await connection1.InvokeAsync("StartBattle", createdGroupInfo.GroupId);
                await Task.Delay(200);
            }

            // Assert
            Assert.NotNull(createdGroupInfo);
            Assert.True(battleStarted1);
            Assert.True(battleStarted2);
        }
        finally
        {
            // Cleanup
            await connection1.DisposeAsync();
            await connection2.DisposeAsync();
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
    public async Task LeaveGroup_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        GroupInfo? createdGroupInfo = null;
        var groupLeft = false;

        connection.On<GroupInfo>("GroupCreated", (groupInfo) =>
        {
            createdGroupInfo = groupInfo;
        });

        connection.On("LeftGroup", () =>
        {
            groupLeft = true;
        });

        try
        {
            // Act
            await connection.StartAsync();
            
            // Create and join group
            await connection.InvokeAsync("CreateGroup", "LeaveTestGroup");
            await Task.Delay(100);

            if (createdGroupInfo != null)
            {
                await connection.InvokeAsync("JoinGroup", createdGroupInfo.GroupId);
                await Task.Delay(100);

                // Leave group
                await connection.InvokeAsync("LeaveGroup", createdGroupInfo.GroupId);
                await Task.Delay(100);
            }

            // Assert
            Assert.NotNull(createdGroupInfo);
            Assert.True(groupLeft);
        }
        finally
        {
            // Cleanup
            await connection.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}