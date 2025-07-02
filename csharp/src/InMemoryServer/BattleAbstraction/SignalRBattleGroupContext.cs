using Shared.Contracts;
using Shared.Models;

namespace InMemoryServer.BattleAbstraction;

/// <summary>
/// SignalR implementation of IBattleGroupContext
/// </summary>
public class SignalRBattleGroupContext : IBattleGroupContext
{
    private readonly GroupInfo _groupInfo;

    public SignalRBattleGroupContext(GroupInfo groupInfo)
    {
        _groupInfo = groupInfo;
    }

    /// <summary>
    /// Gets the group ID
    /// </summary>
    public string Id => _groupInfo.Id;

    /// <summary>
    /// Gets the group name
    /// </summary>
    public string Name => _groupInfo.Name;

    /// <summary>
    /// Gets the maximum number of clients allowed in the group
    /// </summary>
    public int MaxClients => _groupInfo.MaxConnections;

    /// <summary>
    /// Gets the current connected count
    /// </summary>
    public int ConnectedCount => _groupInfo.ConnectionCount;

    /// <summary>
    /// Gets the client IDs in this group
    /// </summary>
    public IReadOnlyList<string> ClientIds => _groupInfo.ClientIds.AsReadOnly();
}
