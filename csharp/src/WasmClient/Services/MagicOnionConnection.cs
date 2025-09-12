using WasmClient.Models;
using MagicOnion.Client;
using Grpc.Net.Client;
using Shared.Contracts.Http2Server;
using System.Diagnostics.CodeAnalysis;
using Grpc.Net.Client.Web;

namespace WasmClient.Services;

/// <summary>
/// MagicOnion implementation of IBattleConnection for WasmClient
/// </summary>
public class MagicOnionConnection : IBattleConnection, IMagicOnionBattleHubReceiver, IAsyncDisposable
{
    private readonly ILogger<MagicOnionConnection> _logger;
    private GrpcChannel? _channel;
    private IMagicOnionBattleHub? _hub;
    private string _connectionId = string.Empty;

    public string ConnectionId => _connectionId;
    public ConnectionType Type => Shared.Models.ConnectionType.MagicOnion;
    public ConnectionInfo Info { get; }
    public bool IsConnected => _hub != null;

    public event Action<BattleReplayData>? OnBattleReplayReceived;
    public event Action<string>? OnBattleComplete;
    public event Action<Exception>? OnConnectionError;
    public event Action? OnDisconnected;
    public event Action<ConnectionsReadyData>? OnConnectionsReady;
    public event Action<BattleStartedData>? OnBattleStarted;

    public static bool Supported => false;

    public MagicOnionConnection(ConnectionInfo connectionInfo, ILogger<MagicOnionConnection> logger)
    {
        Info = connectionInfo;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync()
    {
        if (!Supported)
        {
            throw new NotImplementedException("MagicOnion connection not implemented yet, MagicOnion StreamingHub could not work on WASM by gRPC-Web limitation.");
        }

        try
        {
            _logger.LogInformation("Connecting to MagicOnion server: {ServerUrl}", Info.ServerUrl);

            // Create gRPC channel
            _logger.LogDebug("Creating gRPC channel for {ServerUrl}", Info.ServerUrl);
            _channel = GrpcChannel.ForAddress(Info.ServerUrl, new GrpcChannelOptions
            {
                // Use GrpcWebHandler for WASM compatibility
                HttpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler())),
                UnsafeUseInsecureChannelCallCredentials = Info.ServerUrl.StartsWith("http://"),
            });            // Connect to the streaming hub
            _logger.LogDebug("Connecting to StreamingHub...");
            _hub = await StreamingHubClient.ConnectAsync<IMagicOnionBattleHub, IMagicOnionBattleHubReceiver>(_channel, this);

            _connectionId = GenerateConnectionId();
            _logger.LogInformation("Connected to MagicOnion server with ConnectionId: {ConnectionId}", _connectionId);

            // Join group if specified
            if (!string.IsNullOrEmpty(Info.GroupName))
            {
                var joinResult = await JoinGroupAsync(Info.GroupName);
                if (!joinResult)
                {
                    _logger.LogWarning("Failed to join group: {GroupName}", Info.GroupName);
                    await DisconnectAsync();
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MagicOnion server. Exception Type: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);

            if (ex.InnerException != null)
            {
                _logger.LogError("Inner Exception: {InnerExceptionType} - {InnerMessage}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);
            }

            OnConnectionError?.Invoke(ex);
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

            _connectionId = string.Empty;
            _logger.LogInformation("Disconnected from MagicOnion server");
            OnDisconnected?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MagicOnion disconnection");
        }
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        try
        {
            var groupId = await _hub.JoinGroupAsync(groupName);
            _logger.LogInformation("Joined group: {GroupName} (ID: {GroupId})", groupName, groupId);
            return !string.IsNullOrEmpty(groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join group: {GroupName}", groupName);
            OnConnectionError?.Invoke(ex);
            return false;
        }
    }

    public async Task<Shared.Battle.BattleStatus?> GetBattleStatusAsync()
    {
        EnsureConnected();
        try
        {
            return await _hub.GetBattleStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get battle status");
            OnConnectionError?.Invoke(ex);
            return null;
        }
    }

    public async Task<bool> ConfirmConnectionReadyAsync()
    {
        EnsureConnected();
        try
        {
            return await _hub.ConfirmConnectionReadyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm connection ready");
            OnConnectionError?.Invoke(ex);
            return false;
        }
    }

    [MemberNotNull(nameof(_hub))]
    private void EnsureConnected()
    {
        if (_hub == null)
        {
            throw new InvalidOperationException("Not connected to server. Call ConnectAsync first.");
        }
    }

    private static string GenerateConnectionId()
    {
        return $"wasm-mo-{Guid.NewGuid():N}";
    }

    // IMagicOnionBattleHubReceiver implementation
    void IMagicOnionBattleHubReceiver.OnKeyChanged(string key, string value)
    {
        // Not used in battle scenarios
    }

    void IMagicOnionBattleHubReceiver.OnKeyDeleted(string key)
    {
        // Not used in battle scenarios
    }

    void IMagicOnionBattleHubReceiver.OnGroupMessage(string senderId, string message)
    {
        // Not used in battle scenarios
    }

    void IMagicOnionBattleHubReceiver.OnMemberJoined(MemberJoinedData data)
    {
        _logger.LogInformation("Member joined: {ConnectionId}", data.ConnectionId);
    }

    void IMagicOnionBattleHubReceiver.OnMemberLeft(MemberLeftData data)
    {
        _logger.LogInformation("Member left: {ConnectionId}", data.ConnectionId);
    }

    void IMagicOnionBattleHubReceiver.OnConnectionsReady(ConnectionsReadyData data)
    {
        _logger.LogInformation("Connections ready for battle: {BattleId}", data.BattleId);
        OnConnectionsReady?.Invoke(data);

        // Automatically confirm connection ready
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Confirming connection ready");
                await ConfirmConnectionReadyAsync();
                _logger.LogInformation("Connection confirmed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm connection ready");
            }
        });
    }

    void IMagicOnionBattleHubReceiver.OnBattleStarted(BattleStartedData data)
    {
        _logger.LogInformation("Battle started: {BattleId}", data.BattleId);
        OnBattleStarted?.Invoke(data);
    }

    void IMagicOnionBattleHubReceiver.OnBattleReplayData(BattleReplayData replayData)
    {
        _logger.LogDebug("Received battle replay chunk: {ChunkIndex}", replayData.ChunkIndex);
        OnBattleReplayReceived?.Invoke(replayData);
    }

    void IMagicOnionBattleHubReceiver.OnBattleCompleted(Shared.Battle.BattleStatus battleStatus)
    {
        _logger.LogInformation("Battle completed with status: {Status}", battleStatus);
        OnBattleComplete?.Invoke("Battle completed");
    }

    void IMagicOnionBattleHubReceiver.OnGroupDissolved(GroupDissolvedData data)
    {
        _logger.LogWarning("Group dissolved: {GroupName} - {Reason}", data.GroupName, data.Reason);
    }

    void IMagicOnionBattleHubReceiver.OnGroupExtended(GroupExtendedData data)
    {
        _logger.LogInformation("Group extended: {GroupName}", data.GroupName);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
