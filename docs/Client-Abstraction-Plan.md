# CliClient抽象化改善計画 - MagicOnion対応

## 現在の課題

### 1. 業務ロジックとプロトコルの混在
- `SignalRClient`にSignalR固有の詳細が露出
- 業務操作（Get/Set/Join/Battle等）とプロトコル詳細が分離されていない
- コマンド層に通信プロトコルの知識が必要

### 2. 抽象化レベルの問題
- 従来の`InvokeAsync`ベースでは業務ロジックの分離が不十分
- SignalRのイベント機構がクライアント層に漏れている
- MagicOnionの強く型付けされたAPIとの互換性がない

### 3. 拡張性の制約
- 新しいプロトコル追加時にクライアント層の変更が必要
- テストでのモック作成が困難

## 新しい設計方針

### 基本理念: 業務インターフェースとプロトコル実装の完全分離

1. **業務レベルの抽象化**: Get、Set、Join、Battle等の業務メソッドを直接インターフェース化
2. **プロトコル固有実装**: SignalR/MagicOnionの詳細は実装クラス内で完全に隠蔽
3. **段階的移行**: 既存機能を維持しながら抽象化を導入

## 改善提案

### Phase 1: 業務インターフェースの定義

#### 1.1 業務操作インターフェース
```csharp
// Shared/Contracts/IInMemoryServerClient.cs
public interface IInMemoryServerClient : IAsyncDisposable
{
    // Connection management
    bool IsConnected { get; }
    Task<bool> ConnectAsync(string serverUrl, string? groupName = null);
    Task DisconnectAsync();

    // Key-Value operations
    Task<string?> GetAsync(string key);
    Task<bool> SetAsync(string key, string value);
    Task<bool> DeleteAsync(string key);
    Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null);

    // Group operations
    Task<bool> JoinGroupAsync(string groupName);
    Task<bool> BroadcastMessageAsync(string message);
    Task<IReadOnlyList<GroupInfo>> GetGroupsAsync();
    Task<GroupInfo?> GetCurrentGroupAsync();

    // Battle operations
    Task<bool> ConfirmConnectionReadyAsync();
    Task<BattleStatus?> GetBattleStatusAsync();

    // Server status
    Task<ServerStatusInfo> GetServerStatusAsync();

    // Events (業務レベルのイベント)
    event Action<string> OnDisconnected;
    event Action<string, string> OnKeyChanged;
    event Action<string> OnKeyDeleted;
    event Action<string, int> OnMemberJoined;
    event Action<string, string> OnGroupMessage;
    event Action<string> OnConnectionsReady;
    event Action<string> OnBattleStarted;
    event Action<BattleReplayData> OnBattleReplayData;
}
```

#### 1.2 補助モデルクラス
```csharp
// Shared/Models/GroupInfo.cs
public readonly record struct GroupInfo(
    string GroupId,
    string GroupName,
    int MemberCount,
    int MaxMembers,
    TimeSpan RemainingTime
);

// Shared/Models/ServerStatusInfo.cs
public readonly record struct ServerStatusInfo(
    TimeSpan Uptime,
    int TotalConnections,
    int ActiveGroups,
    int ActiveBattles,
    IReadOnlyList<GroupInfo> Groups
);
```

### Phase 2: プロトコル固有実装

#### 2.1 SignalR実装
```csharp
// CliClient/SignalRInMemoryClient.cs
internal class SignalRInMemoryClient : IInMemoryServerClient
{
    private readonly ILogger<SignalRInMemoryClient> _logger;
    private HubConnection? _connection;
    private string _serverUrl = string.Empty;
    private string _currentGroupId = string.Empty;

    // Events
    public event Action<string>? OnDisconnected;
    public event Action<string, string>? OnKeyChanged;
    public event Action<string>? OnKeyDeleted;
    public event Action<string, int>? OnMemberJoined;
    public event Action<string, string>? OnGroupMessage;
    public event Action<string>? OnConnectionsReady;
    public event Action<string>? OnBattleStarted;
    public event Action<BattleReplayData>? OnBattleReplayData;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        if (_connection != null && IsConnected)
        {
            await DisconnectAsync();
        }

        _serverUrl = serverUrl;

        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_serverUrl + SystemDefines.HubRoute)
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();
            await _connection.StartAsync();

            if (!string.IsNullOrEmpty(groupName))
            {
                return await JoinGroupAsync(groupName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server");
            return false;
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<string?>("GetAsync", key);
    }

    public async Task<bool> SetAsync(string key, string value)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<bool>("SetAsync", key, value);
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        var result = await _connection!.InvokeAsync<GroupJoinResult>("JoinGroupAsync", groupName);
        if (result.Success)
        {
            _currentGroupId = result.GroupId;
        }
        return result.Success;
    }

    private void SetupEventHandlers()
    {
        _connection!.On<string, string>("KeyChanged", (key, value) => OnKeyChanged?.Invoke(key, value));
        _connection.On<string>("KeyDeleted", key => OnKeyDeleted?.Invoke(key));
        _connection.On<string, int>("MemberJoined", (connectionId, count) => OnMemberJoined?.Invoke(connectionId, count));
        _connection.On<string, string>("GroupMessage", (connectionId, message) => OnGroupMessage?.Invoke(connectionId, message));
        _connection.On<string>("ConnectionsReady", battleId => OnConnectionsReady?.Invoke(battleId));
        _connection.On<string>("BattleStarted", battleId => OnBattleStarted?.Invoke(battleId));
        _connection.On<BattleReplayData>("BattleReplayData", data => OnBattleReplayData?.Invoke(data));

        _connection.Closed += error =>
        {
            OnDisconnected?.Invoke(error?.Message ?? "Connection closed");
            return Task.CompletedTask;
        };
    }

    // ... 他のメソッド実装
}
```

#### 2.2 MagicOnion実装（将来）
```csharp
// CliClient/MagicOnionInMemoryClient.cs
internal class MagicOnionInMemoryClient : IInMemoryServerClient
{
    private readonly ILogger<MagicOnionInMemoryClient> _logger;
    private GrpcChannel? _channel;
    private IInMemoryService? _service;
    private IInMemoryStreamingHub? _hub;

    public async Task<string?> GetAsync(string key)
    {
        EnsureConnected();
        return await _service!.GetAsync(key);
    }

    public async Task<bool> SetAsync(string key, string value)
    {
        EnsureConnected();
        return await _service!.SetAsync(key, value);
    }

    public async Task<bool> JoinGroupAsync(string groupName)
    {
        EnsureConnected();
        var result = await _hub!.JoinAsync(groupName);
        return result.Success;
    }

    // Events handling through MagicOnion streaming
    private async Task SetupEventStreaming()
    {
        // MagicOnionのストリーミングを使用してイベントを受信
        await foreach (var notification in _hub!.GetNotifications())
        {
            switch (notification.Type)
            {
                case NotificationType.KeyChanged:
                    OnKeyChanged?.Invoke(notification.Key, notification.Value);
                    break;
                case NotificationType.MemberJoined:
                    OnMemberJoined?.Invoke(notification.ConnectionId, notification.Count);
                    break;
                // ... 他のイベント処理
            }
        }
    }

    // ... 他のメソッド実装
}
```

### Phase 3: ファクトリーとDI統合

#### 3.1 クライアントファクトリー
```csharp
// CliClient/InMemoryClientFactory.cs
public static class InMemoryClientFactory
{
    public static IInMemoryServerClient Create(
        ConnectionType connectionType,
        ILoggerFactory loggerFactory)
    {
        return connectionType switch
        {
            ConnectionType.SignalR => new SignalRInMemoryClient(
                loggerFactory.CreateLogger<SignalRInMemoryClient>()),
            ConnectionType.MagicOnion => new MagicOnionInMemoryClient(
                loggerFactory.CreateLogger<MagicOnionInMemoryClient>()),
            _ => throw new ArgumentException($"Unsupported connection type: {connectionType}")
        };
    }
}

public enum ConnectionType
{
    SignalR,
    MagicOnion
}
```

#### 3.2 設定とDI
```csharp
// CliClient/Program.cs
var app = ConsoleApp.Create()
    .ConfigureServices((services, config) =>
    {
        var connectionType = config.GetValue<ConnectionType>("ConnectionType", ConnectionType.SignalR);

        services.AddSingleton<IInMemoryServerClient>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return InMemoryClientFactory.Create(connectionType, loggerFactory);
        });

        services.AddSingleton<MultiClientManager>();
    });
```

#### 3.3 プロトコル非依存コマンド層
```csharp
// CliClient/ConsoleCommand.cs
public class ConsoleCommand
{
    private readonly IInMemoryServerClient _client;
    private readonly MultiClientManager _multiClientManager;
    private readonly ILogger<ConsoleCommand> _logger;

    public ConsoleCommand(
        IInMemoryServerClient client,
        MultiClientManager multiClientManager,
        ILogger<ConsoleCommand> logger)
    {
        _client = client;
        _multiClientManager = multiClientManager;
        _logger = logger;
    }

    [Command("connect")]
    public async Task ConnectAsync(string url, string? group = null)
    {
        var success = await _client.ConnectAsync(url, group);
        _logger.LogInformation(success ? "Connected successfully" : "Failed to connect");
    }

    [Command("get")]
    public async Task GetAsync(string key)
    {
        var value = await _client.GetAsync(key);
        _logger.LogInformation($"Value: {value ?? "(null)"}");
    }

    [Command("set")]
    public async Task SetAsync(string key, string value)
    {
        var success = await _client.SetAsync(key, value);
        _logger.LogInformation(success ? "Set successfully" : "Failed to set");
    }

    // ... 他のコマンド（SignalR/MagicOnionに関係なく同じ実装）
}
```

### Phase 4: マルチクライアント管理とバトル再現機能

#### 4.1 マルチクライアント管理の抽象化
```csharp
// CliClient/MultiClientManager.cs
public class MultiClientManager
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<IInMemoryServerClient> _clients = [];

    public async Task<bool> ConnectMultipleAsync(
        int clientCount,
        string serverUrl,
        string? groupName = null,
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        _clients.Clear();

        for (int i = 0; i < clientCount; i++)
        {
            var client = InMemoryClientFactory.Create(connectionType, _loggerFactory);
            var success = await client.ConnectAsync(serverUrl, groupName);

            if (success)
            {
                _clients.Add(client);
            }
            else
            {
                // クリーンアップ
                await DisconnectAllAsync();
                return false;
            }
        }

        return true;
    }

    public async Task<bool> ReproduceBattleAsync(
        string serverUrl,
        string seed,
        ConnectionType connectionType = ConnectionType.SignalR)
    {
        // バトル再現専用の5クライアント接続
        return await ConnectMultipleAsync(5, serverUrl, $"battle-reproduce-{seed}", connectionType);
    }

    // ... 他のマルチクライアント操作
}
```

#### 4.2 バトル再現コマンドの実装
```csharp
// CliClient/ConsoleCommand.cs (追加)
public class ConsoleCommand
{
    // ... 既存メソッド

    [Command("battle-reproduce")]
    public async Task BattleReproduceAsync(string seed, string url = "http://localhost:5000")
    {
        _logger.LogInformation($"Reproducing battle with seed: {seed}");

        var success = await _multiClientManager.ReproduceBattleAsync(url, seed);
        if (success)
        {
            _logger.LogInformation("Battle reproduction started successfully");
            // バトル完了まで待機
            await _multiClientManager.WaitForBattleCompletionAsync();
        }
        else
        {
            _logger.LogError("Failed to start battle reproduction");
        }
    }

    [Command("batch")]
    public async Task BatchAsync(
        int count = 5,
        string url = "http://localhost:5000",
        string? group = null)
    {
        _logger.LogInformation($"Connecting {count} clients to {url}");

        var success = await _multiClientManager.ConnectMultipleAsync(count, url, group);
        if (success)
        {
            _logger.LogInformation($"Successfully connected {count} clients");
            // バトル開始まで待機
            await _multiClientManager.WaitForBattleStartAsync();
        }
        else
        {
            _logger.LogError("Failed to connect multiple clients");
        }
    }
}
```

## 実装順序

### Step 1: 基本インターフェース定義
1. `IInMemoryServerClient`インターフェースの作成
2. 補助モデルクラス（`GroupInfo`、`ServerStatusInfo`等）の作成
3. `ConnectionType`enum の作成

### Step 2: SignalR実装の移行
1. `SignalRInMemoryClient`クラスの作成
2. 既存`SignalRClient`からロジックを移行
3. イベント処理の抽象化

### Step 3: コマンド層の更新
1. `ConsoleCommand`を`IInMemoryServerClient`に依存するよう変更
2. プロトコル固有コードの除去
3. DIコンテナーの設定更新

### Step 4: マルチクライアント管理
1. `MultiClientManager`の`IInMemoryServerClient`対応
2. バトル再現機能の抽象化
3. 設定ベースの切り替え実装

### Step 5: MagicOnion準備
1. `MagicOnionInMemoryClient`の骨組み作成
2. 共有インターフェースの詳細化
3. ストリーミング処理の設計

### Step 6: テスト・検証
1. モック実装での単体テスト
2. SignalR/MagicOnion切り替えテスト
3. 既存機能の回帰テスト

## 利点

### 1. 完全なプロトコル透過性
- **業務ロジック**: SignalR/MagicOnionの違いを意識する必要なし
- **コマンド層**: プロトコル切り替えでコード変更不要
- **設定切り替え**: 実行時または設定ファイルで簡単に切り替え可能

### 2. 強い型安全性
- **SignalR**: 業務メソッドで型安全性を確保
- **MagicOnion**: 元々の強い型付けを最大限活用
- **共通**: コンパイル時エラーでプロトコル間の差異を検出

### 3. 段階的移行とリスク最小化
- 既存の`SignalRClient`を段階的に移行
- 一つずつメソッドを移行してテスト可能
- 失敗時の迅速なロールバック

### 4. 優れたテスト性
- `IInMemoryServerClient`のモック実装で完全な単体テスト
- プロトコル固有の統合テスト分離
- バトル再現機能のテスト自動化

### 5. 将来拡張性
- gRPC以外のプロトコル対応も容易
- 新しい業務機能追加時の影響範囲限定
- マイクロサービス化への対応

## 注意点と対策

### 1. パフォーマンス考慮
**課題**: 抽象化レイヤーのオーバーヘッド
**対策**:
- インターフェースはコンパイル時に解決される仮想メソッド呼び出し
- 不要な中間オブジェクト生成を避ける設計
- 必要に応じてホットパスの最適化

### 2. プロトコル間の機能差異
**課題**: SignalRとMagicOnionの機能差
**対策**:
- 共通機能のサブセットをベースライン
- プロトコル固有機能は拡張インターフェースで対応
- 機能差異はドキュメント化して明確化

### 3. イベント処理の違い
**課題**: SignalRのイベント vs MagicOnionのストリーミング
**対策**:
- 共通イベントモデルで抽象化
- プロトコル固有の最適化は実装内で処理
- 非同期イベント処理の一貫性確保

### 4. エラー処理の統一
**課題**: プロトコル固有のエラーモデル
**対策**:
- 業務レベルの例外クラス定義
- プロトコル固有エラーの変換処理
- 詳細なエラー情報のログ出力

## 設定例

### appsettings.json
```json
{
  "ConnectionType": "SignalR",
  "ServerUrl": "http://localhost:5000",
  "SignalR": {
    "HubRoute": "/InMemoryHub",
    "AutoReconnect": true,
    "ReconnectDelays": [ 1000, 2000, 5000, 10000 ]
  },
  "MagicOnion": {
    "EnableCompression": true,
    "MaxMessageSize": 4194304,
    "KeepAliveInterval": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "CliClient": "Debug"
    }
  }
}
```

### 環境変数での切り替え
```bash
# SignalR使用
export ConnectionType=SignalR
export ServerUrl=http://localhost:5000

# MagicOnion使用
export ConnectionType=MagicOnion
export ServerUrl=https://production-server:443
```

## 結論

この新しい抽象化方針により、以下を実現できます：

1. **完全なプロトコル独立性**: 業務ロジックレベルでの抽象化により、コマンド層にプロトコルの知識が不要
2. **強い型安全性**: SignalRとMagicOnionの両方で型安全性を確保
3. **段階的移行**: 既存機能を維持しながらリスクを最小化
4. **優れた保守性**: プロトコル固有の詳細が適切に隠蔽され、変更影響が限定的

従来の`InvokeAsync`ベースの低レベル抽象化と異なり、この設計では**業務メソッドを直接インターフェース化**することで、真のプロトコル透過性と型安全性を実現します。
