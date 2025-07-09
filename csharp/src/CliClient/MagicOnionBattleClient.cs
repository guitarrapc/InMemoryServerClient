using Microsoft.Extensions.Logging;
using Shared.Battle;
using Shared.Contracts;
using Shared.Models;

namespace CliClient;

/// <summary>
/// MagicOnion implementation of IInMemoryServerClient
/// This is a placeholder implementation for future MagicOnion support
/// </summary>
#pragma warning disable CS0067 // Event is never used - placeholder implementation
internal sealed class MagicOnionBattleClient : IBattleClient
{
    private readonly ILogger<MagicOnionBattleClient> _logger;
    private bool _disposed;
    private bool _isConnected;

    public bool IsConnected => _isConnected;

    public event Action<string>? OnDisconnected;
    public event Action<string, string>? OnKeyChanged;
    public event Action<string>? OnKeyDeleted;
    public event Action<MemberJoinedData>? OnMemberJoined;
    public event Action<MemberLeftData>? OnMemberLeft;
    public event Action<string, string>? OnGroupMessage;
    public event Action<ConnectionsReadyData>? OnConnectionsReady;
    public event Action<BattleStartedData>? OnBattleStarted;
    public event Action<BattleReplayData>? OnBattleReplayData;
    public event Action<GroupDissolvedData>? OnGroupDissolved;
    public event Action<GroupExtendedData>? OnGroupExtended;

    public MagicOnionBattleClient(ILogger<MagicOnionBattleClient> logger)
    {
        _logger = logger;
    }

    public Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task DisconnectAsync()
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<string?> GetAsync(string key)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<bool> SetAsync(string key, string value)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<bool> DeleteAsync(string key)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<IReadOnlyList<string>> ListAsync(string? pattern = null)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task WatchAsync(string key)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<bool> JoinGroupAsync(string groupName)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<bool> BroadcastMessageAsync(string message)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<bool> BroadcastAsync(string message)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<IReadOnlyList<ClientGroupInfo>> GetGroupsAsync()
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<ClientGroupInfo?> GetCurrentGroupAsync()
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<ClientGroupInfo?> GetMyGroupAsync()
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<bool> ConfirmConnectionReadyAsync()
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<BattleStatus?> GetBattleStatusAsync()
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<ServerStatusInfo> GetServerStatusAsync()
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<BattleReplayData?> GetBattleReplayAsync(string battleId)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task PlayBattleReplayAsync(BattleReplayData replayData)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        _logger.LogInformation("MagicOnion implementation not yet available");
        throw new NotImplementedException("MagicOnion implementation will be added in future versions");
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _isConnected = false;

        _logger.LogInformation("MagicOnion client disposed");
        return ValueTask.CompletedTask;
    }
}
#pragma warning restore CS0067
