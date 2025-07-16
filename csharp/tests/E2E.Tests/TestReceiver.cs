using Shared.Contracts.Http2Server;
using Shared.Models;
using Shared.Battle;

namespace E2E.Tests;

/// <summary>
/// Test implementation of IInMemoryHubReceiver for E2E testing
/// </summary>
public class TestReceiver : IMagicOnionBattleHubReceiver
{
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

    public void OnMemberJoined(MemberJoinedData data) => OnMemberJoinedHandler?.Invoke(data);
    public void OnMemberLeft(MemberLeftData data) => OnMemberLeftHandler?.Invoke(data);
    public void OnGroupMessage(string senderId, string message) => OnGroupMessageHandler?.Invoke(senderId, message);
    public void OnGroupDissolved(GroupDissolvedData data) => OnGroupDissolvedHandler?.Invoke(data);
    public void OnConnectionsReady(ConnectionsReadyData data) => OnConnectionsReadyHandler?.Invoke(data);
    public void OnBattleStarted(BattleStartedData data) => OnBattleStartedHandler?.Invoke(data);
    public void OnBattleReplayData(BattleReplayData data) => OnBattleReplayDataHandler?.Invoke(data);
    public void OnBattleCompleted(BattleStatus status) => OnBattleCompletedHandler?.Invoke(status);
    public void OnKeyChanged(string key, string value) => OnKeyChangedHandler?.Invoke(key, value);
    public void OnKeyDeleted(string key) => OnKeyDeletedHandler?.Invoke(key);
    public void OnGroupExtended(GroupExtendedData data) => OnGroupExtendedHandler?.Invoke(data);
}
