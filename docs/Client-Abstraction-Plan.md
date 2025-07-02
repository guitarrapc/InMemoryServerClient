# CliClient抽象化改善計画 - MagicOnion対応

## 現在の課題

### 1. 通信層の直接依存
- `SignalRClient`が`HubConnection`を直接使用
- SignalR固有のAPIに強く依存
- 通信プロトコルの切り替えが困難

### 2. リアルタイム通信の抽象化不足
- SignalRのイベント駆動モデルに特化
- MagicOnionのストリーミングモデルとの互換性なし

## 改善提案

### Phase 1: 通信層の抽象化

#### 1.1 通信プロトコル抽象インターフェース
```csharp
// Shared/Contracts/IClientConnection.cs
public interface IClientConnection : IAsyncDisposable
{
    bool IsConnected { get; }
    event Action<string> OnDisconnected;

    Task<bool> ConnectAsync(string serverUrl, string? groupName = null);
    Task DisconnectAsync();

    // Basic operations
    Task<T> InvokeAsync<T>(string method, params object[] args);
    Task<bool> InvokeAsync(string method, params object[] args);

    // Event subscription
    void Subscribe<T>(string eventName, Action<T> handler);
    void Subscribe<T1, T2>(string eventName, Action<T1, T2> handler);
}
```

#### 1.2 SignalR実装
```csharp
// CliClient/SignalRConnection.cs
internal class SignalRConnection : IClientConnection
{
    private HubConnection? _connection;

    public async Task<T> InvokeAsync<T>(string method, params object[] args)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<T>(method, args);
    }

    public void Subscribe<T>(string eventName, Action<T> handler)
    {
        EnsureConnected();
        _connection!.On<T>(eventName, handler);
    }

    // ... 実装詳細
}
```

#### 1.3 MagicOnion実装（将来）
```csharp
// CliClient/MagicOnionConnection.cs
internal class MagicOnionConnection : IClientConnection
{
    private readonly IInMemoryHub _hub;

    public async Task<T> InvokeAsync<T>(string method, params object[] args)
    {
        // MagicOnionの強く型付けされたインターフェースを使用
        return method switch
        {
            nameof(IInMemoryService.GetAsync) => (T)(object)await _hub.GetAsync((string)args[0]),
            nameof(IInMemoryService.SetAsync) => (T)(object)await _hub.SetAsync((string)args[0], (string)args[1]),
            // ... 他のメソッド
            _ => throw new NotSupportedException($"Method {method} not supported")
        };
    }

    // ストリーミングベースのイベント処理
    public void Subscribe<T>(string eventName, Action<T> handler)
    {
        // MagicOnionのストリーミングを使用してイベントを処理
        // 実装詳細は省略
    }
}
```

### Phase 2: クライアント層の抽象化

#### 2.1 プロトコル非依存クライアント
```csharp
// CliClient/InMemoryClient.cs
public class InMemoryClient
{
    private readonly IClientConnection _connection;
    private readonly ILogger<InMemoryClient> _logger;

    public InMemoryClient(IClientConnection connection, ILogger<InMemoryClient> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string serverUrl, string? groupName = null)
    {
        return await _connection.ConnectAsync(serverUrl, groupName);
    }

    public async Task<string?> GetAsync(string key)
    {
        return await _connection.InvokeAsync<string?>("GetAsync", key);
    }

    // ... 他の操作
}
```

#### 2.2 ファクトリーパターン
```csharp
// CliClient/ClientConnectionFactory.cs
public static class ClientConnectionFactory
{
    public static IClientConnection Create(ConnectionType type, ILoggerFactory loggerFactory)
    {
        return type switch
        {
            ConnectionType.SignalR => new SignalRConnection(loggerFactory.CreateLogger<SignalRConnection>()),
            ConnectionType.MagicOnion => new MagicOnionConnection(loggerFactory.CreateLogger<MagicOnionConnection>()),
            _ => throw new ArgumentException($"Unsupported connection type: {type}")
        };
    }
}

public enum ConnectionType
{
    SignalR,
    MagicOnion
}
```

### Phase 3: 設定とDI統合

#### 3.1 設定ベースの切り替え
```csharp
// CliClient/Program.cs
var app = ConsoleApp.Create()
    .ConfigureServices((services, config) =>
    {
        var connectionType = config.GetValue<ConnectionType>("ConnectionType", ConnectionType.SignalR);

        services.AddSingleton<IClientConnection>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return ClientConnectionFactory.Create(connectionType, loggerFactory);
        });

        services.AddSingleton<InMemoryClient>();
        services.AddSingleton<MultiClientManager>();
    });
```

#### 3.2 設定ファイル
```json
// appsettings.json
{
  "ConnectionType": "SignalR", // or "MagicOnion"
  "ServerUrl": "http://localhost:5000",
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## 実装順序

### Step 1: 基本抽象化
1. `IClientConnection`インターフェースの作成
2. `SignalRConnection`の実装
3. 既存`SignalRClient`の移行

### Step 2: クライアント統合
1. `InMemoryClient`の作成
2. `SignalRCommand`の更新
3. DIコンテナーの設定

### Step 3: MagicOnion対応準備
1. `MagicOnionConnection`の骨組み作成
2. 共有インターフェースの拡張
3. テストフレームワークの準備

## 利点

### 1. プロトコル透過性
- 設定でSignalR ↔ MagicOnionを切り替え可能
- コマンドロジックの変更不要

### 2. 段階的移行
- 既存機能を維持しながら新機能を追加
- リスクの最小化

### 3. テスト性向上
- モック実装での単体テスト
- プロトコル固有のテストの分離

### 4. 将来拡張性
- gRPC以外のプロトコル対応も容易
- WebRTC、WebSocket等への拡張可能

## 注意点

### 1. パフォーマンス
- 抽象化レイヤーのオーバーヘッド
- MagicOnionの型安全性を最大限活用するための工夫が必要

### 2. 機能差異
- SignalRとMagicOnionの機能差の吸収
- リアルタイム通信パターンの違い

### 3. 開発工数
- 初期実装に時間が必要
- 両プロトコルのメンテナンス負荷

## 結論

現在の実装は**MagicOnion対応に向けた抽象化が不十分**ですが、提案した段階的なアプローチにより、既存機能を維持しながら将来のMagicOnion移行を可能にできます。
