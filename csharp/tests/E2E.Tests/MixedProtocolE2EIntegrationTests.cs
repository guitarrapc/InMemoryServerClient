using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Shared.Constants;
using Shared.Contracts.Http2Server;
using Shared.Models;

namespace E2E.Tests;

/// <summary>
/// SignalRとMagicOnionの混合接続でのE2Eテスト
/// 異なるプロトコルのクライアントが同じサーバーに接続し、相互運用できることを検証
/// </summary>
public class MixedProtocolE2EIntegrationTests : IDisposable
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
    public async Task SignalRAndMagicOnion_CanConnectSimultaneously()
    {
        // Arrange
        var factory = CreateFactory();

        // SignalR connection
        var signalRConnection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        // MagicOnion connection
        var grpcChannel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            await signalRConnection.StartAsync();
            var magicOnionClient = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(grpcChannel, receiver);

            // Assert
            Assert.Equal(HubConnectionState.Connected, signalRConnection.State);
            Assert.NotNull(magicOnionClient);

            // Cleanup
            await magicOnionClient.DisposeAsync();
        }
        finally
        {
            // Cleanup
            await signalRConnection.DisposeAsync();
            await grpcChannel.ShutdownAsync();
            grpcChannel.Dispose();
        }
    }

    [Fact(Timeout = 15000)]
    public async Task MixedClients_CanJoinSameGroup()
    {
        // Arrange
        var factory = CreateFactory();

        // SignalR connection
        var signalRConnection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        // MagicOnion connection
        var grpcChannel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();
        var memberJoinedReceived = false;
        MemberJoinedData? memberData = null;

        // SignalRでMemberJoinedイベントを受信
        signalRConnection.On<MemberJoinedData>("MemberJoined", (data) =>
        {
            memberJoinedReceived = true;
            memberData = data;
            Console.WriteLine($"SignalR received MemberJoined: Group={data.GroupName}, Count={data.CurrentMemberCount}");
        });

        try
        {
            // Act
            await signalRConnection.StartAsync();
            var magicOnionClient = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(grpcChannel, receiver);

            // SignalRクライアントがグループに参加
            var signalRGroupId = await signalRConnection.InvokeAsync<string>("JoinGroupAsync", nameof(MixedClients_CanJoinSameGroup));
            Console.WriteLine($"SignalR client joined group: {signalRGroupId}");

            await Task.Delay(200); // Allow processing time

            // MagicOnionクライアントが同じグループに参加
            var magicOnionGroupId = await magicOnionClient.JoinGroupAsync(nameof(MixedClients_CanJoinSameGroup));
            Console.WriteLine($"MagicOnion client joined group: {magicOnionGroupId}");

            await Task.Delay(500); // Allow event processing time

            // Assert
            Assert.Equal(signalRGroupId, magicOnionGroupId); // 同じグループに参加
            Assert.True(memberJoinedReceived); // SignalRクライアントがMemberJoinedイベントを受信
            Assert.NotNull(memberData);
            Assert.Equal(nameof(MixedClients_CanJoinSameGroup), memberData.Value.GroupName);
            Assert.Equal(2, memberData.Value.CurrentMemberCount); // 2つのクライアントが参加

            // Cleanup
            await magicOnionClient.DisposeAsync();
        }
        finally
        {
            // Cleanup
            await signalRConnection.DisposeAsync();
            await grpcChannel.ShutdownAsync();
            grpcChannel.Dispose();
        }
    }

    [Fact(Timeout = 15000)]
    public async Task MixedClients_KeyValueOperations_WorkIndependently()
    {
        // Arrange
        var factory = CreateFactory();

        // SignalR connection (グループ操作用)
        var signalRConnection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        // MagicOnion connection (キーバリュー操作用)
        var grpcChannel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();

        try
        {
            // Act
            await signalRConnection.StartAsync();
            var magicOnionClient = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(grpcChannel, receiver);

            // MagicOnionでキーバリュー操作
            await magicOnionClient.SetAsync("mixed-test-key", "test-value");
            var retrievedValue = await magicOnionClient.GetAsync("mixed-test-key");

            // SignalRでグループ操作
            var groupId = await signalRConnection.InvokeAsync<string>("JoinGroupAsync", nameof(MixedClients_KeyValueOperations_WorkIndependently));

            // Assert
            Assert.Equal("test-value", retrievedValue);
            Assert.NotNull(groupId);
            Assert.NotEmpty(groupId);

            // Cleanup
            await magicOnionClient.DisposeAsync();
        }
        finally
        {
            // Cleanup
            await signalRConnection.DisposeAsync();
            await grpcChannel.ShutdownAsync();
            grpcChannel.Dispose();
        }
    }

    [Fact(Timeout = 20000)]
    public async Task ThreeSignalRTwoMagicOnion_AutoStartBattle_WorksCorrectly()
    {
        // Arrange
        var factory = CreateFactory();
        var signalRConnections = new List<HubConnection>();
        var grpcChannels = new List<GrpcChannel>();
        var magicOnionClients = new List<IMagicOnionBattleHub>();
        var magicOnionReceivers = new List<TestReceiver>();
        var groupIds = new List<string>();

        var connectionsReadyCount = 0;
        var battleStartedCount = 0;
        var magicOnionConnectionsReadyCount = 0;
        var magicOnionBattleStartedCount = 0;

        // 3つのSignalR接続を作成
        for (int i = 0; i < 3; i++)
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
                Console.WriteLine($"SignalR ConnectionsReady received by client {current} - BattleId: {data.BattleId}");
                try
                {
                    // Small delay to ensure the server is ready to process the confirmation
                    await Task.Delay(50);
                    // Confirm that this client is ready
                    var confirmed = await connection.InvokeAsync<bool>("ConfirmConnectionReadyAsync");
                    Console.WriteLine($"SignalR Client {current} confirmation result: {confirmed}");
                    if (!confirmed)
                    {
                        Console.WriteLine($"WARNING: SignalR Client {current} failed to confirm connection ready");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: SignalR Client {current} failed to confirm connection ready: {ex.Message}");
                }
            });

            connection.On<BattleStartedData>("BattleStarted", (data) =>
            {
                var current = Interlocked.Increment(ref battleStartedCount);
                Console.WriteLine($"SignalR BattleStarted received by client {current} - BattleId: {data.BattleId}");
            });

            await connection.StartAsync();

            // Join group with SignalR clients first
            var groupId = await connection.InvokeAsync<string>("JoinGroupAsync", nameof(ThreeSignalRTwoMagicOnion_AutoStartBattle_WorksCorrectly));

            signalRConnections.Add(connection);
            groupIds.Add(groupId);
        }

        // 2つのMagicOnion接続を作成
        for (int i = 0; i < 2; i++)
        {
            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                UnsafeUseInsecureChannelCallCredentials = true,
                HttpHandler = factory.Server.CreateHandler()
            });

            var clientIndex = i + 4; // 4番目、5番目のクライアント

            // MagicOnionクライアントのイベントハンドラーを設定
            var receiver = new TestReceiver();
            var client = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel, receiver);

            receiver.OnConnectionsReadyHandler = async (data) =>
            {
                var current = Interlocked.Increment(ref magicOnionConnectionsReadyCount);
                Console.WriteLine($"MagicOnion ConnectionsReady received by client {current} - BattleId: {data.BattleId}");

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
                var current = Interlocked.Increment(ref magicOnionBattleStartedCount);
                Console.WriteLine($"MagicOnion BattleStarted received by client {current} - BattleId: {data.BattleId}");
            };

            // Join group with SignalR clients first
            var groupId = await client.JoinGroupAsync(nameof(ThreeSignalRTwoMagicOnion_AutoStartBattle_WorksCorrectly));
            groupIds.Add(groupId);

            grpcChannels.Add(channel);
            magicOnionClients.Add(client);
            magicOnionReceivers.Add(receiver);
        }

        Console.WriteLine($"All clients joined. Unique group IDs: {groupIds.Distinct().Count()}");

        // Act
        try
        {
            // Wait for battle to auto-start
            Console.WriteLine("Waiting for mixed protocol battle to auto-start...");
            await Task.Delay(3000);

            // Wait for battle events with timeout
            var timeout = TimeSpan.FromSeconds(30);
            var checkInterval = TimeSpan.FromMilliseconds(500);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var totalExpectedConnectionsReady = 5; // 3 SignalR + 2 MagicOnion
            var totalExpectedBattleStarted = 5;    // 3 SignalR + 2 MagicOnion

            while (stopwatch.Elapsed < timeout &&
                   ((connectionsReadyCount + magicOnionConnectionsReadyCount) < totalExpectedConnectionsReady ||
                    (battleStartedCount + magicOnionBattleStartedCount) < totalExpectedBattleStarted))
            {
                await Task.Delay(checkInterval);
                if (stopwatch.Elapsed.TotalSeconds % 2 < 0.5)
                {
                    Console.WriteLine($"Mixed protocol progress - SignalR CR: {connectionsReadyCount}/3, MO CR: {magicOnionConnectionsReadyCount}/2, SignalR BS: {battleStartedCount}/3, MO BS: {magicOnionBattleStartedCount}/2, Elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s");
                }
            }

            Console.WriteLine($"Mixed protocol final state - SignalR CR: {connectionsReadyCount}/3, MO CR: {magicOnionConnectionsReadyCount}/2, SignalR BS: {battleStartedCount}/3, MO BS: {magicOnionBattleStartedCount}/2, Total elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s");

            // Assert
            Assert.True(groupIds.All(id => id == groupIds[0]), "All clients should be in the same group");
            Assert.Equal(3, connectionsReadyCount); // SignalRクライアントがイベントを受信
            Assert.Equal(2, magicOnionConnectionsReadyCount); // MagicOnionクライアントがイベントを受信
            Assert.Equal(3, battleStartedCount);   // SignalRクライアントがイベントを受信
            Assert.Equal(2, magicOnionBattleStartedCount); // MagicOnionクライアントがイベントを受信
        }
        finally
        {
            // Cleanup
            foreach (var connection in signalRConnections)
            {
                await connection.DisposeAsync();
            }
            foreach (var client in magicOnionClients)
            {
                await client.DisposeAsync();
            }
            foreach (var channel in grpcChannels)
            {
                await channel.ShutdownAsync();
                channel.Dispose();
            }
        }
    }

    [Fact(Timeout = 15000)]
    public async Task MixedClients_CrossProtocolNotification_WorksCorrectly()
    {
        // このテストでは、CrossProtocolNotificationServiceが
        // SignalRとMagicOnionクライアント間で正しく通知を配信することを検証

        // Arrange
        var factory = CreateFactory();

        var signalRConnection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{SystemDefines.HubRoute}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var grpcChannel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = factory.Server.CreateHandler()
        });

        var receiver = new TestReceiver();
        var memberJoinedReceived = false;

        signalRConnection.On<MemberJoinedData>("MemberJoined", (data) =>
        {
            memberJoinedReceived = true;
            Console.WriteLine($"Cross-protocol notification received: Group={data.GroupName}, Count={data.CurrentMemberCount}");
        });

        try
        {
            // Act
            await signalRConnection.StartAsync();
            var magicOnionClient = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(grpcChannel, receiver);

            // SignalRクライアントがグループに参加
            var signalRGroupId = await signalRConnection.InvokeAsync<string>("JoinGroupAsync", nameof(MixedClients_CrossProtocolNotification_WorksCorrectly));
            await Task.Delay(200);

            // MagicOnionクライアントが同じグループに参加（これがSignalRクライアントに通知される）
            var magicOnionGroupId = await magicOnionClient.JoinGroupAsync(nameof(MixedClients_CrossProtocolNotification_WorksCorrectly));
            await Task.Delay(500);

            // Assert
            Assert.Equal(signalRGroupId, magicOnionGroupId);
            Assert.True(memberJoinedReceived, "SignalR client should receive cross-protocol notification");

            Console.WriteLine("Cross-protocol notification test completed successfully");

            // Cleanup
            await magicOnionClient.DisposeAsync();
        }
        finally
        {
            // Cleanup
            await signalRConnection.DisposeAsync();
            await grpcChannel.ShutdownAsync();
            grpcChannel.Dispose();
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
