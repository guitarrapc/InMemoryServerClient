using Shared.BattleServer.Models;
using Shared.BattleServer.Contracts.Http2Server;
using Shared.BattleLogic.Models;

namespace E2E.Tests;

/// <summary>
/// Test implementation of IInMemoryHubReceiver for E2E testing
/// </summary>
public class TestReceiver : IMagicOnionBattleHubReceiver
{
    private readonly string _clientIdentifier;

    public TestReceiver(string clientIdentifier = "Unknown")
    {
        _clientIdentifier = clientIdentifier;
    }

    public Action<MemberJoinedData>? OnMemberJoinedHandler { get; set; }
    public Action<MemberLeftData>? OnMemberLeftHandler { get; set; }
    public Action<string, string>? OnGroupMessageHandler { get; set; }
    public Action<GroupDissolvedData>? OnGroupDissolvedHandler { get; set; }
    public Action<ConnectionsReadyData>? OnConnectionsReadyHandler { get; set; }
    public Action<BattleStartedData>? OnBattleStartedHandler { get; set; }
    public Action<BattleReplayData>? OnBattleReplayDataHandler { get; set; }
    public Action<BattleStatus>? OnBattleCompletedHandler { get; set; }
    public Action<string, string>? OnKeyChangedHandler { get; set; }
    public Action<string>? OnKeyDeletedHandler { get; set; }
    public Action<GroupExtendedData>? OnGroupExtendedHandler { get; set; }

    public void OnMemberJoined(MemberJoinedData data)
    {
        Console.WriteLine($"[TestReceiver-{_clientIdentifier}] OnMemberJoined called with GroupName: {data.GroupName}, MemberCount: {data.CurrentMemberCount}");
        OnMemberJoinedHandler?.Invoke(data);
    }

    public void OnMemberLeft(MemberLeftData data)
    {
        Console.WriteLine($"[TestReceiver] OnMemberLeft called");
        OnMemberLeftHandler?.Invoke(data);
    }

    public void OnGroupMessage(string senderId, string message)
    {
        Console.WriteLine($"[TestReceiver] OnGroupMessage called");
        OnGroupMessageHandler?.Invoke(senderId, message);
    }

    public void OnGroupDissolved(GroupDissolvedData data)
    {
        Console.WriteLine($"[TestReceiver] OnGroupDissolved called");
        OnGroupDissolvedHandler?.Invoke(data);
    }

    public void OnConnectionsReady(ConnectionsReadyData data)
    {
        Console.WriteLine($"[TestReceiver] OnConnectionsReady called");
        OnConnectionsReadyHandler?.Invoke(data);
    }

    public void OnBattleStarted(BattleStartedData data)
    {
        Console.WriteLine($"[TestReceiver] OnBattleStarted called");
        OnBattleStartedHandler?.Invoke(data);
    }

    public void OnBattleReplayData(BattleReplayData data)
    {
        Console.WriteLine($"[TestReceiver] OnBattleReplayData called");
        OnBattleReplayDataHandler?.Invoke(data);
    }

    public void OnBattleCompleted(BattleStatus status)
    {
        Console.WriteLine($"[TestReceiver] OnBattleCompleted called");
        OnBattleCompletedHandler?.Invoke(status);
    }

    public void OnKeyChanged(string key, string value)
    {
        Console.WriteLine($"[TestReceiver] OnKeyChanged called");
        OnKeyChangedHandler?.Invoke(key, value);
    }

    public void OnKeyDeleted(string key)
    {
        Console.WriteLine($"[TestReceiver] OnKeyDeleted called");
        OnKeyDeletedHandler?.Invoke(key);
    }

    public void OnGroupExtended(GroupExtendedData data)
    {
        Console.WriteLine($"[TestReceiver] OnGroupExtended called");
        OnGroupExtendedHandler?.Invoke(data);
    }
}

/// <summary>
/// Extension methods for TestReceiver to provide convenient wait functionality for testing
/// </summary>
public static class TestReceiverExtensions
{
    public static async Task<MemberJoinedData> WaitForMemberJoined(this TestReceiver receiver, int timeoutMs = 5000)
    {
        Console.WriteLine($"[WaitForMemberJoined] Starting wait with {timeoutMs}ms timeout");
        var tcs = new TaskCompletionSource<MemberJoinedData>();
        var cancellationToken = new CancellationTokenSource(timeoutMs);

        receiver.OnMemberJoinedHandler = (data) =>
        {
            Console.WriteLine($"[WaitForMemberJoined] Handler called with GroupName: {data.GroupName}, MemberCount: {data.CurrentMemberCount}");
            if (!tcs.Task.IsCompleted)
            {
                Console.WriteLine($"[WaitForMemberJoined] Setting result");
                tcs.SetResult(data);
            }
            else
            {
                Console.WriteLine($"[WaitForMemberJoined] Task already completed, ignoring");
            }
        };

        cancellationToken.Token.Register(() =>
        {
            Console.WriteLine($"[WaitForMemberJoined] Timeout reached after {timeoutMs}ms");
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetException(new TimeoutException($"WaitForMemberJoined timed out after {timeoutMs}ms"));
            }
        });

        return await tcs.Task;
    }

    public static async Task<MemberLeftData> WaitForMemberLeft(this TestReceiver receiver, int timeoutMs = 5000)
    {
        var tcs = new TaskCompletionSource<MemberLeftData>();
        var cancellationToken = new CancellationTokenSource(timeoutMs);

        receiver.OnMemberLeftHandler = (data) =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetResult(data);
            }
        };

        cancellationToken.Token.Register(() =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetException(new TimeoutException($"WaitForMemberLeft timed out after {timeoutMs}ms"));
            }
        });

        return await tcs.Task;
    }

    public static async Task<ConnectionsReadyData> WaitForConnectionsReady(this TestReceiver receiver, int timeoutMs = 5000)
    {
        var tcs = new TaskCompletionSource<ConnectionsReadyData>();
        var cancellationToken = new CancellationTokenSource(timeoutMs);

        receiver.OnConnectionsReadyHandler = (data) =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetResult(data);
            }
        };

        cancellationToken.Token.Register(() =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetException(new TimeoutException($"WaitForConnectionsReady timed out after {timeoutMs}ms"));
            }
        });

        return await tcs.Task;
    }

    public static async Task<BattleStartedData> WaitForBattleStarted(this TestReceiver receiver, int timeoutMs = 5000)
    {
        var tcs = new TaskCompletionSource<BattleStartedData>();
        var cancellationToken = new CancellationTokenSource(timeoutMs);

        receiver.OnBattleStartedHandler = (data) =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetResult(data);
            }
        };

        cancellationToken.Token.Register(() =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetException(new TimeoutException($"WaitForBattleStarted timed out after {timeoutMs}ms"));
            }
        });

        return await tcs.Task;
    }
}
