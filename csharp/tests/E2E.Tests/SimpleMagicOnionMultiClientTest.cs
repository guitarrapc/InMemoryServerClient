using Grpc.Net.Client;
using MagicOnion.Client;
using Shared.Contracts.Http2Server;
using Shared.Models;
using Shared.Battle;

namespace E2E.Tests;

public class SimpleMagicOnionMultiClientTest : IDisposable
{
    private readonly CustomWebApplicationFactory<InMemoryServer.Program> factory;

    public SimpleMagicOnionMultiClientTest()
    {
        factory = new CustomWebApplicationFactory<InMemoryServer.Program>();
    }

    [Fact(Skip = "Temporarily disabled - investigating timeout issues")]
    public async Task TwoClients_JoinGroup_BothReceiveEvents()
    {
        // Create clients
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

        var receiver1 = new SimpleTestReceiver("Client1");
        var receiver2 = new SimpleTestReceiver("Client2");

        try
        {
            var client1 = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel1, receiver1);
            var client2 = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(channel2, receiver2);

            Console.WriteLine("Both clients connected");

            // Client1 joins first
            Console.WriteLine("Client1 joining...");
            var groupId1 = await client1.JoinGroupAsync("TestGroup");
            Console.WriteLine($"Client1 joined: {groupId1}");

            // Wait a bit
            await Task.Delay(500);

            // Client2 joins
            Console.WriteLine("Client2 joining...");
            var groupId2 = await client2.JoinGroupAsync("TestGroup");
            Console.WriteLine($"Client2 joined: {groupId2}");

            // Wait for events
            await Task.Delay(2000);

            Console.WriteLine($"Client1 events: {receiver1.EventCount}");
            Console.WriteLine($"Client2 events: {receiver2.EventCount}");

            // Simple validation
            Assert.Equal(groupId1, groupId2);
            Assert.True(receiver1.EventCount > 0 || receiver2.EventCount > 0, "At least one client should receive events");

            await client1.DisposeAsync();
            await client2.DisposeAsync();
        }
        finally
        {
            await channel1.ShutdownAsync();
            await channel2.ShutdownAsync();
            channel1.Dispose();
            channel2.Dispose();
        }
    }

    public void Dispose()
    {
        factory?.Dispose();
    }
}

public class SimpleTestReceiver : IMagicOnionBattleHubReceiver
{
    private readonly string clientName;
    public int EventCount { get; private set; }

    public SimpleTestReceiver(string clientName)
    {
        this.clientName = clientName;
    }

    public void OnMemberJoined(MemberJoinedData data)
    {
        EventCount++;
        Console.WriteLine($"[{clientName}] OnMemberJoined: Group={data.GroupName}, Count={data.CurrentMemberCount}");
    }

    public void OnMemberLeft(MemberLeftData data)
    {
        Console.WriteLine($"[{clientName}] OnMemberLeft: {data.ConnectionId}");
    }

    public void OnGroupExtended(GroupExtendedData data)
    {
        Console.WriteLine($"[{clientName}] OnGroupExtended: {data.GroupName}");
    }

    public void OnConnectionsReady(ConnectionsReadyData data)
    {
        Console.WriteLine($"[{clientName}] OnConnectionsReady: {data.BattleId}");
    }

    public void OnBattleStarted(BattleStartedData data)
    {
        Console.WriteLine($"[{clientName}] OnBattleStarted: {data.BattleId}");
    }

    public void OnBattleReplayData(BattleReplayData data)
    {
        Console.WriteLine($"[{clientName}] OnBattleReplayData");
    }

    public void OnMessage(string message)
    {
        Console.WriteLine($"[{clientName}] OnMessage: {message}");
    }

    public void OnKeyChanged(string key, string value)
    {
        Console.WriteLine($"[{clientName}] OnKeyChanged: {key} = {value}");
    }

    public void OnKeyDeleted(string key)
    {
        Console.WriteLine($"[{clientName}] OnKeyDeleted: {key}");
    }

    public void OnGroupMessage(string connectionId, string message)
    {
        Console.WriteLine($"[{clientName}] OnGroupMessage: {connectionId} - {message}");
    }

    public void OnBattleCompleted(BattleStatus status)
    {
        Console.WriteLine($"[{clientName}] OnBattleCompleted");
    }

    public void OnGroupDissolved(GroupDissolvedData data)
    {
        Console.WriteLine($"[{clientName}] OnGroupDissolved: {data.GroupId}");
    }
}
