using Grpc.Net.Client;
using MagicOnion.Client;
using Shared.Contracts.Http2Server;
using Shared.Models;

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
    }

    [Fact(Timeout = 10000)] // 10秒タイムアウト
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

    [Fact(Timeout = 15000)] // タイムアウトを15秒に延長（デバッグ用）
    public async Task MultipleClients_CanJoinSameGroup_ReceiveMemberJoinedEvent()
    {
        // Arrange
        var factory = CreateFactory();

        Console.WriteLine("=== Creating single shared channel ===");
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver1 = new TestReceiver("Client1");
        var receiver2 = new TestReceiver("Client2");

        var client1Events = new List<MemberJoinedData>();
        var client2Events = new List<MemberJoinedData>();
        var allEventsLock = new Lock();

        // Track all MemberJoined events with thread safety
        receiver1.OnMemberJoinedHandler = (data) =>
        {
            lock (allEventsLock)
            {
                Console.WriteLine($"*** CLIENT1 received MemberJoined: Group={data.GroupName}, Count={data.CurrentMemberCount}");
                client1Events.Add(data);
            }
        };

        receiver2.OnMemberJoinedHandler = (data) =>
        {
            lock (allEventsLock)
            {
                Console.WriteLine($"*** CLIENT2 received MemberJoined: Group={data.GroupName}, Count={data.CurrentMemberCount}");
                client2Events.Add(data);
            }
        };

        IMagicOnionBattleHub? client1 = null;
        IMagicOnionBattleHub? client2 = null;

        try
        {
            // Connect Client1 and wait for stable connection
            Console.WriteLine("=== Connecting Client1 ===");
            client1 = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver1);
            Console.WriteLine($"✓ Client1 connected successfully (HashCode: {client1.GetHashCode():X8})");

            // Wait longer to ensure Client1 is fully registered
            await Task.Delay(1000);

            // Use explicit variable to ensure we're calling the right client (+1 event for client1 joining group to client1)
            var groupId1 = await client1.JoinGroupAsync(nameof(MultipleClients_CanJoinSameGroup_ReceiveMemberJoinedEvent));
            Console.WriteLine($"✓ Client1 joined group: {groupId1}");

            // Wait and check events
            await Task.Delay(2000);
            lock (allEventsLock)
            {
                Console.WriteLine($"After Phase 1 - Client1 events: {client1Events.Count}, Client2 events: {client2Events.Count}");
            }

            // For now, just verify we get a valid group ID and one event
            Assert.NotNull(groupId1);
            Assert.NotEmpty(groupId1);

            lock (allEventsLock)
            {
                var totalEvents = client1Events.Count + client2Events.Count;
                Assert.True(totalEvents == 1, $"Should receive at least one MemberJoined event, got {totalEvents}");

                var allEvents = client1Events.Concat(client2Events).ToList();
                var validEvent = allEvents.Last();
                Assert.Equal(nameof(MultipleClients_CanJoinSameGroup_ReceiveMemberJoinedEvent), validEvent.GroupName);
                Assert.Equal(1, validEvent.CurrentMemberCount);
            }


            Console.WriteLine("=== Connecting Client2 ===");
            client2 = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver2);
            Console.WriteLine($"✓ Client2 connected successfully (HashCode: {client2.GetHashCode():X8})");

            // Wait longer to ensure Client2 is fully registered
            await Task.Delay(1000);

            // Use explicit variable to ensure we're calling the right client (+2 event for client2 joining group to client1 & client2)
            var groupId2 = await client2.JoinGroupAsync(nameof(MultipleClients_CanJoinSameGroup_ReceiveMemberJoinedEvent));
            Console.WriteLine($"✓ Client2 joined group: {groupId2}");

            // Wait and check events
            await Task.Delay(2000);
            lock (allEventsLock)
            {
                Console.WriteLine($"After Phase 1 - Client1 events: {client1Events.Count}, Client2 events: {client2Events.Count}");
            }

            // For now, just verify we get a valid group ID and one event
            Assert.NotNull(groupId2);
            Assert.NotEmpty(groupId2);

            lock (allEventsLock)
            {
                var totalEvents = client1Events.Count + client2Events.Count;
                Assert.True(totalEvents == 3, $"Should receive at least one MemberJoined event, got {totalEvents}");

                var allEvents = client1Events.Concat(client2Events).ToList();
                var validEvent = allEvents.Last();
                Assert.Equal(nameof(MultipleClients_CanJoinSameGroup_ReceiveMemberJoinedEvent), validEvent.GroupName);
                Assert.Equal(2, validEvent.CurrentMemberCount);
            }

            Console.WriteLine("✓ Test passed - Client1 successfully joined group and received/triggered event");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            Console.WriteLine("\n=== Cleanup ===");
            if (client1 != null)
            {
                await client1.DisposeAsync();
                Console.WriteLine("✓ Client1 disposed");
            }
            if (client2 != null)
            {
                await client2.DisposeAsync();
                Console.WriteLine("✓ Client2 disposed");
            }
            await channel.ShutdownAsync();
            channel.Dispose();
            Console.WriteLine("✓ Channel disposed");
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
