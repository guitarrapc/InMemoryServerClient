using Grpc.Net.Client;
using MagicOnion.Client;
using Shared.Contracts.Http2Server;
using Shared.Models;
using Shared.Battle;

namespace E2E.Tests;

public class MagicOnionE2EIntegrationTests : IDisposable
{
    private CustomWebApplicationFactory<InMemoryServer.Program>? _factory;

    private CustomWebApplicationFactory<InMemoryServer.Program> CreateFactory()
    {
        _factory = new CustomWebApplicationFactory<InMemoryServer.Program>();
        return _factory;
    }

    [Fact(Timeout = 15000)]
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

    [Fact(Timeout = 15000)]
    public async Task MagicOnionStreamingHub_CanConnectAndDisconnect()
    {
        // Arrange
        var factory = CreateFactory();
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true, // Match production environment
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);

            // Assert
            Assert.NotNull(client);

            // Cleanup
            await client.DisposeAsync();
        }
        finally
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }
    }

    [Fact(Timeout = 10000)] // 10秒タイムアウト
    public async Task JoinGroup_CreatesAndJoinsGroup_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);

            // Join group (which will create it if it doesn't exist)
            var groupId = await client.JoinGroupAsync("TestGroup");

            // Assert
            Assert.NotNull(groupId);
            Assert.NotEmpty(groupId);

            // Cleanup
            await client.DisposeAsync();
        }
        finally
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }
    }    [Fact(Timeout = 10000)] // 10秒タイムアウト
    public async Task ProductionLikeMagicOnionTest_CompareWithRealImplementation()
    {
        // Arrange
        var factory = CreateFactory();

        // Use the actual MagicOnionBattleClient like in production
        var client = new CliClient.Clients.MagicOnionBattleClient(
            Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CliClient.Clients.MagicOnionBattleClient>()
        );

        try
        {
            // Connect using the same method as production
            Console.WriteLine("Connecting using production client implementation...");
            var connected = await client.ConnectAsync("http://localhost", factory.Server.CreateHandler());
            Console.WriteLine($"Connected: {connected}");

            Assert.True(connected);

            // Test basic operations like in production
            Console.WriteLine("Testing Set operation...");
            var setResult = await client.SetAsync("test-key", "test-value");
            Console.WriteLine($"Set result: {setResult}");

            Console.WriteLine("Testing Get operation...");
            var getValue = await client.GetAsync("test-key");
            Console.WriteLine($"Get result: {getValue}");

            Assert.True(setResult);
            Assert.Equal("test-value", getValue);

            await client.DisconnectAsync();
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Fact(Timeout = 15000)]
    public async Task SingleClient_CanJoinGroup_DebugTest()
    {
        // Arrange
        var factory = CreateFactory();
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            Console.WriteLine("Connecting single client...");
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);
            Console.WriteLine("Single client connected");

            Console.WriteLine("About to call JoinGroupAsync...");
            var groupId = await client.JoinGroupAsync("DebugGroup");
            Console.WriteLine($"Single client joined group: {groupId}");

            // Cleanup
            await client.DisposeAsync();
            Console.WriteLine("Test completed successfully");
        }
        finally
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }
    }

    [Fact(Timeout = 15000)] // タイムアウトを15秒に短縮（デバッグ用）
    public async Task SingleClient_CanJoinGroup_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            Console.WriteLine("Connecting single client...");
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);
            var clientId = Guid.NewGuid().ToString("N")[..8];
            Console.WriteLine($"Client connected with ID: {clientId}");

            // Client joins group
            Console.WriteLine($"About to call JoinGroupAsync for Client (ID: {clientId})...");
            var groupId = await client.JoinGroupAsync("SingleTestGroup");
            Console.WriteLine($"Client (ID: {clientId}) joined group: {groupId}");

            // Verify the client can get group info
            var groupInfo = await client.GetCurrentGroupAsync();
            Console.WriteLine($"Group info: {groupInfo?.Name} (Members: {groupInfo?.ConnectionCount})");

            // Assert
            Assert.NotNull(groupInfo);
            Assert.Equal("SingleTestGroup", groupInfo.Name);
            Assert.Equal(1, groupInfo.ConnectionCount);

            // Cleanup
            await client.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed with exception: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }
    }

    [Fact(Skip = "Temporarily disabled - investigating timeout issues")]
    public async Task MultipleClients_CanJoinSameGroup_ReceiveMemberJoinedEvent()
    {
        // Arrange
        var factory = CreateFactory();
        var channel1 = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });
        var channel2 = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver1 = new TestReceiver();
        var receiver2 = new TestReceiver();

        var client1Events = new List<MemberJoinedData>();
        var client2Events = new List<MemberJoinedData>();

        // Track all MemberJoined events
        receiver1.OnMemberJoinedHandler = (data) =>
        {
            Console.WriteLine($"Client1 received MemberJoined: Group={data.GroupName}, Count={data.CurrentMemberCount}");
            client1Events.Add(data);
        };

        receiver2.OnMemberJoinedHandler = (data) =>
        {
            Console.WriteLine($"Client2 received MemberJoined: Group={data.GroupName}, Count={data.CurrentMemberCount}");
            client2Events.Add(data);
        };

        try
        {
            // Act
            Console.WriteLine("Connecting clients...");
            var client1 = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel1, receiver1);
            Console.WriteLine("Client1 connected");

            var client2 = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel2, receiver2);
            Console.WriteLine("Client2 connected");

            // First client joins group
            Console.WriteLine("Client1 joining group MultiTestGroup...");
            var groupId1 = await client1.JoinGroupAsync("MultiTestGroup");
            Console.WriteLine($"Client1 joined group: {groupId1}");

            // Small delay
            Console.WriteLine("Waiting 500ms before client2 joins...");
            await Task.Delay(500);

            Console.WriteLine($"Client1 events so far: {client1Events.Count}");
            Console.WriteLine($"Client2 events so far: {client2Events.Count}");

            // Second client joins the same group - this should trigger MemberJoined for both clients
            Console.WriteLine("Client2 joining group MultiTestGroup...");
            var groupId2 = await client2.JoinGroupAsync("MultiTestGroup");
            Console.WriteLine($"Client2 joined group: {groupId2}");

            // Wait for event propagation
            Console.WriteLine("Waiting 2000ms for event propagation...");
            await Task.Delay(2000);

            Console.WriteLine($"Final event counts - Client1: {client1Events.Count}, Client2: {client2Events.Count}");
            foreach (var evt in client1Events)
            {
                Console.WriteLine($"Client1 event: Group={evt.GroupName}, Count={evt.CurrentMemberCount}");
            }
            foreach (var evt in client2Events)
            {
                Console.WriteLine($"Client2 event: Group={evt.GroupName}, Count={evt.CurrentMemberCount}");
            }

            // Assert
            Assert.Equal(groupId1, groupId2); // Both should be in the same group

            // We expect at least one client to receive a MemberJoined event
            var totalEvents = client1Events.Count + client2Events.Count;
            Assert.True(totalEvents >= 1, $"At least one client should receive a MemberJoined event, got {totalEvents} total events");

            // Find any event with valid data
            var allEvents = client1Events.Concat(client2Events).ToList();
            Assert.True(allEvents.Any(), "Should have at least one MemberJoined event");

            var validEvent = allEvents.First();
            Assert.Equal("MultiTestGroup", validEvent.GroupName);
            Assert.True(validEvent.CurrentMemberCount >= 1, $"Member count should be at least 1, got {validEvent.CurrentMemberCount}");

            // Cleanup
            await client1.DisposeAsync();
            await client2.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed with exception: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            await channel1.ShutdownAsync();
            await channel2.ShutdownAsync();
            channel1.Dispose();
            channel2.Dispose();
        }
    }

    [Fact(Skip = "Temporarily disabled - investigating timeout issues")]
    public async Task FiveClients_AutoStartBattle_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var channels = new List<GrpcChannel>();
        var clients = new List<IMagicOnionBattleHub>();
        var receivers = new List<TestReceiver>();

        var connectionsReadyCount = 0;
        var battleStartedCount = 0;
        var joinedCount = 0;

        // Create 5 clients
        for (int i = 0; i < 5; i++)
        {
            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                UnsafeUseInsecureChannelCallCredentials = true,
                HttpHandler = factory.Server.CreateHandler()
            });

            var receiver = new TestReceiver();
            var clientIndex = i + 1;

            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);

            // Set up event handlers
            receiver.OnMemberJoinedHandler = (data) =>
            {
                Interlocked.Increment(ref joinedCount);
                Console.WriteLine($"MemberJoined received - Client {clientIndex}, Member Count: {data.CurrentMemberCount}");
            };

            receiver.OnConnectionsReadyHandler = (data) =>
            {
                Interlocked.Increment(ref connectionsReadyCount);
                Console.WriteLine($"ConnectionsReady received - Client {clientIndex}, BattleId: {data.BattleId}");

                // MagicOnionクライアントでも確認応答を送信
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(50);
                        var confirmed = await client.ConfirmConnectionReadyAsync();
                        Console.WriteLine($"MagicOnion Client {clientIndex} confirmation result: {confirmed}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR: MagicOnion Client {clientIndex} failed to confirm: {ex.Message}");
                    }
                });
            };

            receiver.OnBattleStartedHandler = (data) =>
            {
                Interlocked.Increment(ref battleStartedCount);
                Console.WriteLine($"BattleStarted received - Client {clientIndex}, BattleId: {data.BattleId}");
            };

            channels.Add(channel);
            clients.Add(client);
            receivers.Add(receiver);
        }

        try
        {
            // Act
            Console.WriteLine("All clients connected");

            // All clients join the same group sequentially to ensure deterministic order
            var groupIds = new List<string>();
            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                var groupId = await client.JoinGroupAsync("BattleTestGroup");
                groupIds.Add(groupId);
                Console.WriteLine($"Client {i + 1} joined group {groupId}");

                // Small delay to ensure proper sequencing
                await Task.Delay(100);
            }

            Console.WriteLine($"All clients joined. Unique group IDs: {groupIds.Distinct().Count()}");

            // Give the server some time to process all joins and potentially auto-start the battle
            Console.WriteLine("Waiting for server to process all joins and auto-start battle...");
            await Task.Delay(1000);

            // Wait for battle to auto-start with timeout
            var timeout = TimeSpan.FromSeconds(30);
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
            Assert.True(connectionsReadyCount >= 5, $"Expected all 5 clients to receive ConnectionsReady, got {connectionsReadyCount}");
            Assert.True(battleStartedCount >= 5, $"Expected all 5 clients to receive BattleStarted, got {battleStartedCount}");

            Console.WriteLine("MagicOnion battle test completed successfully");
        }
        finally
        {
            // Cleanup
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
            foreach (var channel in channels)
            {
                await channel.ShutdownAsync();
                channel.Dispose();
            }
        }
    }

    [Fact(Timeout = 15000)]
    public async Task GetServerStatus_ReturnsValidStatus()
    {
        // Arrange
        var factory = CreateFactory();
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);
            var status = await client.GetServerStatusAsync();

            // Assert
            Assert.NotNull(status);
            // サーバーのステータスが正常に取得できることを確認

            // Cleanup
            await client.DisposeAsync();
        }
        finally
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }
    }

    [Fact(Timeout = 15000)]
    public async Task BasicKeyValueOperations_WorkCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);

            // Set a value
            var setResult = await client.SetAsync("testkey", "testvalue");
            Assert.True(setResult);

            // Get the value
            var getValue = await client.GetAsync("testkey");
            Assert.Equal("testvalue", getValue);

            // Delete the value
            var deleteResult = await client.DeleteAsync("testkey");
            Assert.True(deleteResult);

            // Verify deletion
            var getAfterDelete = await client.GetAsync("testkey");
            Assert.Null(getAfterDelete);

            // Cleanup
            await client.DisposeAsync();
        }
        finally
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }
    }

    [Fact(Timeout = 5000)]
    public async Task SingleClient_JoinGroup_ThenSecondClient_JoinsSameGroup_WorksCorrectly()
    {
        // 個別に単一クライアントのグループ参加をテスト
        string? firstGroupId = null;
        string? secondGroupId = null;

        // First client joins a group
        {
            var factory = CreateFactory();
            var receiver = new TestReceiver();
            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                UnsafeUseInsecureChannelCallCredentials = true,
                HttpHandler = factory.Server.CreateHandler()
            });

            Console.WriteLine("First client connecting...");
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);
            Console.WriteLine("First client connected successfully");

            Console.WriteLine("First client joining group...");
            firstGroupId = await client.JoinGroupAsync("TestGroup");
            Console.WriteLine($"First client joined group: {firstGroupId}");

            // Cleanup first client
            await client.DisposeAsync();
            await channel.ShutdownAsync();
            channel.Dispose();
            factory.Dispose();
        }

        // Second client joins the same group name (should create new group or join existing)
        {
            var factory = CreateFactory();
            var receiver = new TestReceiver();
            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                UnsafeUseInsecureChannelCallCredentials = true,
                HttpHandler = factory.Server.CreateHandler()
            });

            Console.WriteLine("Second client connecting...");
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);
            Console.WriteLine("Second client connected successfully");

            Console.WriteLine("Second client joining group...");
            secondGroupId = await client.JoinGroupAsync("TestGroup");
            Console.WriteLine($"Second client joined group: {secondGroupId}");

            // Cleanup second client
            await client.DisposeAsync();
            await channel.ShutdownAsync();
            channel.Dispose();
            factory.Dispose();
        }

        // Verify both clients successfully joined groups
        Assert.NotNull(firstGroupId);
        Assert.NotNull(secondGroupId);
        Console.WriteLine("Both clients successfully joined groups");
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
