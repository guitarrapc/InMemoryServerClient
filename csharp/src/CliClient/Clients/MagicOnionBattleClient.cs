using MagicOnion.Client;
using Microsoft.Extensions.Logging;
using Shared.Battle;
using Shared.Constants;
using Shared.Contracts;
using Shared.Contracts.Http2Server;
using Shared.Models;
using Grpc.Net.Client;
using System.Diagnostics.CodeAnalysis;
using CliClient.Extensions;
using CliClient.Models;
using CliClient.Services;

namespace CliClient.Clients;

/// <summary>
/// MagicOnion implementation of IInMemoryServerClient
/// </summary>
internal class MagicOnionBattleClient : IBattleClient, IMagicOnionBattleHubReceiver, IAsyncDisposable
{
    private readonly ILogger<MagicOnionBattleClient> _logger;
    private readonly BattleReplayRenderer _replayRenderer;
    private IMagicOnionBattleHub? _hub;
    private GrpcChannel? _channel;
    private string _serverUrl = string.Empty;
    private string _currentGroupId = string.Empty;

    // Battle replay data storage
    private readonly Dictionary<int, List<BattleStatus>> _replayChunks = [];
    private int _expectedTotalChunks = 0;
    private BattleReplaySummary? _battleSummary = null;

    // This is used to track if the battle has completed and to notify the client when it is done
    private readonly TaskCompletionSource<bool> _battleCompletionSource = new();

    public TaskCompletionSource<bool> BattleCompletionSource => _battleCompletionSource;

    // Events
#pragma warning disable CS0067 // The event is never used
    public event Action<string>? OnDisconnected;
#pragma warning restore CS0067
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
        _replayRenderer = new BattleReplayRenderer(_logger);
    }

    public bool IsConnected => _hub != null;

    public async Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        if (IsConnected)
        {
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation("Connecting to MagicOnion server: {ServerUrl}", serverUrl);

            // Create gRPC channel
            _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
            {
                UnsafeUseInsecureChannelCallCredentials = serverUrl.StartsWith("http://"),
            });

            // Connect to the streaming hub
            _hub = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(_channel, this);

            _logger.LogInformation("Connected to MagicOnion server");

            if (!string.IsNullOrEmpty(groupName))
            {
                await JoinGroupAsync(groupName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MagicOnion server");
            await DisconnectAsync();
            return false;
        }
    }

    public async Task<bool> ConnectAsync(string serverUrl, HttpMessageHandler httpHandler, string? groupName = null)
    {
        if (_hub != null && IsConnected)
        {
            _logger.LogInformation("Already connected to server, disconnecting first");
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _logger.LogInformation("Connecting to MagicOnion test server: {ServerUrl}", serverUrl);

            // Create gRPC channel with test server handler
            _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
            {
                HttpHandler = httpHandler,
                UnsafeUseInsecureChannelCallCredentials = serverUrl.StartsWith("http://"),
            });

            // Connect to the streaming hub
            _hub = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(_channel, this);

            _logger.LogInformation("Connected to MagicOnion test server");

            if (!string.IsNullOrEmpty(groupName))
            {
                await JoinGroupAsync(groupName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MagicOnion test server");
            await DisconnectAsync();
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_hub != null)
            {
                _logger.LogInformation("Disconnecting from MagicOnion server");
                await _hub.DisposeAsync();
                _hub = null;
            }

            if (_channel != null)
            {
                _channel.Dispose();
                _channel = null;
            }

            _currentGroupId = string.Empty;
            _logger.LogInformation("Disconnected from MagicOnion server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MagicOnion disconnection");
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        EnsureConnected();
        return await _hub.GetAsync(key);
    }

    public async Task<bool> SetAsync(string key, string value)
    {
        EnsureConnected();
        return await _hub.SetAsync(key, value);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        EnsureConnected();
        return await _hub.DeleteAsync(key);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null)
    {
        return await ListAsync(pattern);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string? pattern = null)
    {
        EnsureConnected();
        var result = await _hub.ListKeysAsync(pattern);
        return result.ToList();
    }

    public async Task WatchAsync(string key)
    {
        EnsureConnected();
        _logger.LogInformation("Watching key: {Key}", key);
        await _hub.WatchAsync(key);
    }

    public async Task<bool> BroadcastAsync(string message)
    {
        return await BroadcastMessageAsync(message);
    }

    public async Task<ClientGroupInfo?> GetMyGroupAsync()
    {
        return await GetCurrentGroupAsync();
    }

    public async Task<BattleReplayData?> GetBattleReplayAsync(Guid battleId)
    {
        EnsureConnected();
        return await _hub.GetBattleReplayAsync(battleId);
    }

    public async Task PlayBattleReplayAsync(BattleReplayData replayData)
    {
        // This is handled automatically when OnBattleReplayData event is triggered
        _logger.LogInformation("Battle replay play requested for {TurnCount} turns", replayData.TurnData.Count);
        await Task.CompletedTask;
    }

    // IBattleClient specific methods
    public async Task<bool> BroadcastMessageAsync(string message)
    {
        EnsureConnected();
        return await _hub.BroadcastAsync(message);
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        _currentGroupId = await _hub.JoinGroupAsync(groupName);
        _logger.LogInformation("Joined group: {GroupName} (ID: {GroupId})", groupName, _currentGroupId);
        return !string.IsNullOrEmpty(_currentGroupId);
    }

    public async Task<IReadOnlyList<ClientGroupInfo>> GetGroupsAsync()
    {
        EnsureConnected();
        var groups = await _hub.GetGroupsAsync();

        // Convert GroupInfo to ClientGroupInfo
        return groups.Select(g => new ClientGroupInfo(
            g.GroupId,
            g.Name,
            g.ConnectionCount,
            g.MaxConnections,
            g.CreatedAt.Add(TimeSpan.FromMinutes(10)) - DateTime.UtcNow // Approximate remaining time
        )).ToList();
    }

    public async Task<ClientGroupInfo?> GetCurrentGroupAsync()
    {
        EnsureConnected();
        var groupInfo = await _hub.GetCurrentGroupAsync();
        if (groupInfo == null) return null;

        // Convert GroupInfo to ClientGroupInfo
        return new ClientGroupInfo(
            groupInfo.GroupId,
            groupInfo.Name,
            groupInfo.ConnectionCount,
            groupInfo.MaxConnections,
            groupInfo.CreatedAt.Add(TimeSpan.FromMinutes(10)) - DateTime.UtcNow // Approximate remaining time
        );
    }

    public async Task<BattleStatus?> GetBattleStatusAsync()
    {
        EnsureConnected();
        return await _hub.GetBattleStatusAsync();
    }

    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        return await _hub.ConfirmConnectionReadyAsync();
    }

    public async Task<bool> ReproduceBattleAsync(Guid battleId, int seedValue, string groupName)
    {
        EnsureConnected();
        _logger.LogInformation("Requesting battle reproduction - BattleId: {BattleId}, Seed: {Seed}, GroupName: {GroupName}", battleId, seedValue, groupName);

        var result = await _hub.ReproduceBattleAsync(battleId, seedValue, groupName);
        return result;
    }

    public async Task<ServerStatusInfo> GetServerStatusAsync()
    {
        EnsureConnected();
        var serverStatus = await _hub.GetServerStatusAsync();

        // Convert ServerStatus to ServerStatusInfo
        var groups = serverStatus.Groups.Select(g => new ClientGroupInfo(
            g.GroupId,
            g.Name,
            g.ConnectionCount,
            SystemDefines.MaxConnectionsPerGroup,
            TimeSpan.Zero // TODO: Calculate remaining time
        )).ToList() ?? [];

        return new ServerStatusInfo(
            serverStatus.Uptime,
            serverStatus.TotalConnections,
            serverStatus.GroupCount,
            serverStatus.ActiveBattleCount,
            groups
        );
    }

    [MemberNotNull(nameof(_hub))]
    private void EnsureConnected()
    {
        if (_hub == null)
        {
            throw new InvalidOperationException("Not connected to server. Call ConnectAsync first.");
        }
    }

    // IInMemoryHubReceiver implementation
    void IMagicOnionBattleHubReceiver.OnKeyChanged(string key, string value)
    {
        OnKeyChanged?.Invoke(key, value);
    }

    void IMagicOnionBattleHubReceiver.OnKeyDeleted(string key)
    {
        OnKeyDeleted?.Invoke(key);
    }

    void IMagicOnionBattleHubReceiver.OnGroupMessage(string senderId, string message)
    {
        OnGroupMessage?.Invoke(senderId, message);
    }

    void IMagicOnionBattleHubReceiver.OnMemberJoined(MemberJoinedData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.MemberJoined(data.ConnectionId, data.GroupName));
        _logger.LogBattleInfo(new BattleLogMessages.GroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
        if (data.CurrentMemberCount == data.MaxMembers)
        {
            _logger.LogBattleInfo(new BattleLogMessages.GroupFull());
        }
        OnMemberJoined?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnMemberLeft(MemberLeftData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.MemberLeft(data.ConnectionId, data.GroupName));
        _logger.LogBattleInfo(new BattleLogMessages.GroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
        OnMemberLeft?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnConnectionsReady(ConnectionsReadyData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.ConnectionReadyHeader());
        _logger.LogBattleInfo(new BattleLogMessages.ConnectionsReady());
        _logger.LogBattleInfo(new BattleLogMessages.ConnectionsReadyDetails(data.BattleId.ToString(), data.Seed));

        OnConnectionsReady?.Invoke(data);

        // Automatically confirm connection ready
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogBattleInfo(new BattleLogMessages.ConfirmingConnection());
                var result = await ConfirmConnectionReadyAsync();
                _logger.LogBattleInfo(new BattleLogMessages.ConnectionConfirmed(result));
            }
            catch (Exception ex)
            {
                _logger.LogBattleError(new BattleLogMessages.ConnectionConfirmationFailed());
                _logger.LogError(ex, "Exception details");
            }
        });
    }

    void IMagicOnionBattleHubReceiver.OnBattleStarted(BattleStartedData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.BattleStarted());
        _logger.LogBattleInfo(new BattleLogMessages.BattleStartedDetails(data.BattleId.ToString(), data.Seed));
        OnBattleStarted?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnBattleReplayData(BattleReplayData replayData)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogBattleInfo(new BattleLogMessages.ReplayChunkReceived(replayData.ChunkIndex, replayData.TotalChunks, replayData.TurnData.Count, (long)replayData.Seed));

                // Store the chunk
                _replayChunks[replayData.ChunkIndex] = replayData.TurnData;
                _expectedTotalChunks = replayData.TotalChunks;

                // Store battle summary if this is the last chunk
                if (replayData.IsLastChunk && replayData.Summary.HasValue)
                {
                    _battleSummary = replayData.Summary.Value;
                }

                OnBattleReplayData?.Invoke(replayData);

                // Check if we have all chunks
                if (_replayChunks.Count == _expectedTotalChunks)
                {
                    await PlayBattleReplayAsync(replayData.BattleId, replayData.Seed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing battle replay data");
            }
        });
    }

    void IMagicOnionBattleHubReceiver.OnBattleCompleted(BattleStatus battleStatus)
    {
        _logger.LogInformation("[BATTLE] Battle completed!");
        // Battle completion is handled in the replay playback
    }

    void IMagicOnionBattleHubReceiver.OnGroupDissolved(GroupDissolvedData data)
    {
        _logger.LogBattleWarning(new BattleLogMessages.GroupDissolved(data.GroupName, data.GroupId, data.Reason));
        OnGroupDissolved?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnGroupExtended(GroupExtendedData data)
    {
        _logger.LogBattleInfo(new BattleLogMessages.GroupExtended(data.GroupName, data.GroupId, data.ExtensionCount, data.MaxExtensions, data.NewExpiryTime));
        OnGroupExtended?.Invoke(data);
    }

    private async Task PlayBattleReplayAsync(Guid battleId, int? seed)
    {
        var seedValue = seed ?? 0;
        _logger.LogBattleInfo(new BattleLogMessages.AllChunksReceived(battleId.ToString(), seedValue));

        // Reconstruct complete replay data using the service
        var battleStatuses = _replayRenderer.ReconstructReplayData(_replayChunks, _expectedTotalChunks);

        // Play the replay using the service (disable showing total turns to avoid spoilers)
        await _replayRenderer.PlayReplayAsync(battleStatuses, battleId, seed, _battleSummary);

        // Clean up
        _replayChunks.Clear();

        // Signal that the battle is complete
        _battleCompletionSource.TrySetResult(true);

        // Auto-disconnect after replay completion
        _logger.LogBattleInfo(new BattleLogMessages.AutoDisconnecting());
        await DisconnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}
