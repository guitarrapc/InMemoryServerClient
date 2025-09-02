using InMemoryServer.Models;
using InMemoryServer.Services;
using Shared.BattleServer.Constants;

namespace InMemoryServer.Tests;

/// <summary>
/// Tests for cross-protocol functionality
/// </summary>
public class CrossProtocolTests : IDisposable
{
    private readonly ILogger<GroupManagerActor> _groupLogger;
    private readonly ILogger<ConnectionManager> _connectionLogger;
    private readonly ILogger<CrossProtocolNotificationService> _notificationLogger;
    private readonly ConnectionManager _connectionManager;
    private readonly GroupManagerActor _groupManagerActor;
    private readonly IGroupManager _groupManager;

    public CrossProtocolTests()
    {
        _groupLogger = Substitute.For<ILogger<GroupManagerActor>>();
        _connectionLogger = Substitute.For<ILogger<ConnectionManager>>();
        _notificationLogger = Substitute.For<ILogger<CrossProtocolNotificationService>>();
        _connectionManager = new ConnectionManager(_connectionLogger);
        _groupManagerActor = new GroupManagerActor(_groupLogger, _connectionManager);
        _groupManager = new GroupManagerAdapter(_groupManagerActor);
    }

    [Fact]
    public async Task MixedProtocolGroup_ShouldReachMaxCapacity_AndStartBattle()
    {
        // Arrange - Register connections from different protocols
        const string groupName = "cross_protocol_test";

        // SignalR connections
        var signalRConnection1 = _connectionManager.RegisterConnection("signalr-1", ConnectionProtocol.SignalR);
        var signalRConnection2 = _connectionManager.RegisterConnection("signalr-2", ConnectionProtocol.SignalR);
        var signalRConnection3 = _connectionManager.RegisterConnection("signalr-3", ConnectionProtocol.SignalR);

        // MagicOnion connections
        var magicOnionConnection1 = _connectionManager.RegisterConnection("magiconion-1", ConnectionProtocol.MagicOnion);
        var magicOnionConnection2 = _connectionManager.RegisterConnection("magiconion-2", ConnectionProtocol.MagicOnion);

        // Act - Join the same group from different protocols
        var group1 = await _groupManager.JoinGroupAsync(signalRConnection1, groupName);
        var group2 = await _groupManager.JoinGroupAsync(magicOnionConnection1, groupName);
        var group3 = await _groupManager.JoinGroupAsync(signalRConnection2, groupName);
        var group4 = await _groupManager.JoinGroupAsync(magicOnionConnection2, groupName);
        var group5 = await _groupManager.JoinGroupAsync(signalRConnection3, groupName);

        // Assert - All should be in the same group
        Assert.Equal(group1.GroupId, group2.GroupId);
        Assert.Equal(group1.GroupId, group3.GroupId);
        Assert.Equal(group1.GroupId, group4.GroupId);
        Assert.Equal(group1.GroupId, group5.GroupId);

        // Group should be at max capacity
        Assert.Equal(SystemDefines.MaxConnectionsPerGroup, group5.ConnectionCount);

        // Group should contain all connections
        Assert.Contains(signalRConnection1, group5.ClientIds);
        Assert.Contains(signalRConnection2, group5.ClientIds);
        Assert.Contains(signalRConnection3, group5.ClientIds);
        Assert.Contains(magicOnionConnection1, group5.ClientIds);
        Assert.Contains(magicOnionConnection2, group5.ClientIds);
    }

    [Fact]
    public void ConnectionManager_ShouldTrackProtocolTypes_Correctly()
    {
        // Arrange & Act
        var signalRConnectionId = _connectionManager.RegisterConnection("signalr-test", ConnectionProtocol.SignalR);
        var magicOnionConnectionId = _connectionManager.RegisterConnection("magiconion-test", ConnectionProtocol.MagicOnion);

        // Assert
        var signalRInfo = _connectionManager.GetConnectionInfo(signalRConnectionId);
        var magicOnionInfo = _connectionManager.GetConnectionInfo(magicOnionConnectionId);

        Assert.NotNull(signalRInfo);
        Assert.NotNull(magicOnionInfo);
        Assert.Equal(ConnectionProtocol.SignalR, signalRInfo.Protocol);
        Assert.Equal(ConnectionProtocol.MagicOnion, magicOnionInfo.Protocol);
        Assert.Equal("signalr-test", signalRInfo.OriginalConnectionId);
        Assert.Equal("magiconion-test", magicOnionInfo.OriginalConnectionId);
    }

    [Fact]
    public void ConnectionManager_ShouldFilterByProtocol_Correctly()
    {
        // Arrange
        var signalRConnections = new[]
        {
            _connectionManager.RegisterConnection("signalr-1", ConnectionProtocol.SignalR),
            _connectionManager.RegisterConnection("signalr-2", ConnectionProtocol.SignalR)
        };

        var magicOnionConnections = new[]
        {
            _connectionManager.RegisterConnection("magiconion-1", ConnectionProtocol.MagicOnion),
            _connectionManager.RegisterConnection("magiconion-2", ConnectionProtocol.MagicOnion)
        };

        // Act
        var signalRFiltered = _connectionManager.GetConnectionsByProtocol(ConnectionProtocol.SignalR).ToList();
        var magicOnionFiltered = _connectionManager.GetConnectionsByProtocol(ConnectionProtocol.MagicOnion).ToList();

        // Assert
        Assert.Equal(2, signalRFiltered.Count);
        Assert.Equal(2, magicOnionFiltered.Count);
        Assert.All(signalRFiltered, conn => Assert.Equal(ConnectionProtocol.SignalR, conn.Protocol));
        Assert.All(magicOnionFiltered, conn => Assert.Equal(ConnectionProtocol.MagicOnion, conn.Protocol));
    }

    [Fact]
    public async Task MixedProtocolGroup_LeaveGroup_ShouldUpdateCountCorrectly()
    {
        // Arrange
        const string groupName = "leave_test_group";
        var signalRConnection = _connectionManager.RegisterConnection("signalr-leave", ConnectionProtocol.SignalR);
        var magicOnionConnection = _connectionManager.RegisterConnection("magiconion-leave", ConnectionProtocol.MagicOnion);

        // Act
        var group1 = await _groupManager.JoinGroupAsync(signalRConnection, groupName);
        var group2 = await _groupManager.JoinGroupAsync(magicOnionConnection, groupName);

        Assert.Equal(2, group2.ConnectionCount);

        // Leave one connection
        var (leftGroup, newCount) = await _groupManager.LeaveGroupAsync(signalRConnection);

        // Assert
        Assert.NotNull(leftGroup);
        Assert.Equal(1, newCount);
        Assert.Equal(group1.GroupId, leftGroup.GroupId);

        // Check final group state
        var finalGroup = await _groupManager.GetGroupInfoAsync(group1.GroupId);
        Assert.NotNull(finalGroup);
        Assert.Equal(1, finalGroup.ConnectionCount);
        Assert.Contains(magicOnionConnection, finalGroup.ClientIds);
        Assert.DoesNotContain(signalRConnection, finalGroup.ClientIds);
    }

    [Fact]
    public void ConnectionManager_UnregisterConnection_ShouldCleanUpCorrectly()
    {
        // Arrange
        var connectionId = "test-unregister";
        var registeredConnectionId = _connectionManager.RegisterConnection(connectionId, ConnectionProtocol.SignalR);

        // Verify it's registered (connection ID should be the same as input)
        Assert.Equal(connectionId, registeredConnectionId);
        Assert.NotNull(_connectionManager.GetConnectionInfo(connectionId));

        // Act
        var removedSuccessfully = _connectionManager.UnregisterConnection(connectionId);

        // Assert
        Assert.True(removedSuccessfully);
        Assert.Null(_connectionManager.GetConnectionInfo(connectionId));
    }

    public void Dispose()
    {
        if (_groupManager is IDisposable disposable)
            disposable.Dispose();
        _groupManagerActor?.Dispose();
    }
}
