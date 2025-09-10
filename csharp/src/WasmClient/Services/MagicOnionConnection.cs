using WasmClient.Models;
using Shared.Models;

namespace WasmClient.Services;

/// <summary>
/// MagicOnion implementation of IBattleConnection for WasmClient
/// TODO: Implement MagicOnion connection logic
/// </summary>
public class MagicOnionConnection : IBattleConnection
{
    public string ConnectionId => throw new NotImplementedException("MagicOnion connection not implemented yet");
    public Shared.Models.ConnectionType Type => Shared.Models.ConnectionType.MagicOnion;
    public ConnectionInfo Info { get; }
    public bool IsConnected => false; // TODO: Implement

    // Events (pragma to suppress unused event warnings for placeholder implementation)
#pragma warning disable CS0067
    public event Action<Shared.Battle.BattleReplayData>? OnBattleReplayReceived;
    public event Action<string>? OnBattleComplete;
    public event Action<Exception>? OnConnectionError;
    public event Action? OnDisconnected;
    public event Action<Shared.Models.ConnectionsReadyData>? OnConnectionsReady;
    public event Action<Shared.Models.BattleStartedData>? OnBattleStarted;
#pragma warning restore CS0067

    public MagicOnionConnection(ConnectionInfo connectionInfo)
    {
        Info = connectionInfo;
    }

    public Task<bool> ConnectAsync()
    {
        throw new NotImplementedException("MagicOnion connection not implemented yet");
    }

    public Task DisconnectAsync()
    {
        return Task.CompletedTask;
    }

    public Task<bool> JoinGroupAsync(string groupName)
    {
        throw new NotImplementedException("MagicOnion connection not implemented yet");
    }

    public Task<Shared.Battle.BattleStatus?> GetBattleStatusAsync()
    {
        throw new NotImplementedException("MagicOnion connection not implemented yet");
    }

    public Task<bool> ConfirmConnectionReadyAsync()
    {
        throw new NotImplementedException("MagicOnion connection not implemented yet");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
