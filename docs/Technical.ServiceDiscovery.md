# サービスディスカバリーサーバー技術仕様

## 概要

InMemoryServerClientプロジェクトにおけるサービスディスカバリーサーバーは、バトルサーバーの検索・割り当て・管理を担当する中央制御システムです。GameLift統合とDirect接続の両方を透過的にサポートし、クライアントからはセッション管理の詳細を隠蔽します。

### 重要な設計原則

**責任分離の明確化**:
- **ServiceDiscovery**: サーバー発見とセッション管理のみ
- **BattleServer**: グループ管理とバトル実行のみ
- **セッション ≠ グループ**: セッションは接続先情報、グループは実際のバトルメンバー

**GameLiftスタイル採用の理由**:
- **コスト効率**: 1グループ=1セッションで無駄なセッション作成を防止（5人が個別セッションを作らない）
- **運用簡素化**: セッション管理とバトルグループの1:1対応で管理を簡素化
- **スケーラビリティ**: セッション単位での負荷分散とリソース管理が容易

**用語の定義**:
- **セッション**: GameLiftの管理単位（1つのバトルマッチに対応）
- **バトルグループ**: BattleServer上の実際のバトルメンバー集合（最大5人）
- **BattleServer**: 物理的なサーバープロセス（複数セッションを同時実行可能）

**GameLiftスタイル vs 個別セッション方式の比較**:
```
[ GameLiftスタイル（採用案）]
グループ "battle-001" の場合：
- セッション作成: 1個（session-123）
- 全5人が同一セッション session-123 を取得
- BattleServer上で1つのバトルグループを形成

[ 個別セッション方式（不採用）]
グループ "battle-001" の場合：
- セッション作成: 5個（session-001, session-002, ...）
- 各プレイヤーが異なるセッションを取得
- 後でセッション間の統合が必要（複雑）
```

## アーキテクチャ概要

### システム全体構成

```mermaid
graph TB
    Client[CliClient]
    Discovery[ServiceDiscoveryServer]
    Battle1[BattleServer #1]
    Battle2[BattleServer #2]
    Battle3[BattleServer #3]
    GameLift[GameLift Service]

    Client -->|1. セッション要求| Discovery
    Discovery -->|2. サーバー割り当て| Battle1
    Discovery -->|GameLift統合| GameLift
    GameLift -->|GameSession管理| Battle1
    Client -->|3. 直接接続| Battle1
    Discovery -.->|ヘルスチェック| Battle2
    Discovery -.->|ヘルスチェック| Battle3
```

### 通信フロー

#### **統一フロー（GameLift/Direct共通）**
1. **クライアント → ServiceDiscoveryServer**: セッション作成要求
2. **ServiceDiscoveryServer**: 適切なBattleServerを選択・割り当て
3. **ServiceDiscoveryServer → クライアント**: BattleServer接続情報を返却
4. **クライアント → BattleServer**: 直接接続
5. **クライアント → BattleServer**: JoinGroup実行（**重要**: この時点で実際のバトルメンバーとして登録）
6. **BattleServer**: グループメンバー数が5人に達したらバトル開始

#### **重要な責任分離**
- **Step 1-3**: ServiceDiscoveryの責任範囲（サーバー発見・セッション管理）
- **Step 4-6**: BattleServerの責任範囲（グループ管理・バトル実行）
- **プレイヤー数管理**: BattleServerのグループ参加時にのみ実施（ServiceDiscoveryではカウントしない）

#### **なぜこの分離が重要か**
- **単一責任原則**: ServiceDiscoveryはセッション割り当てのみ、BattleServerは実際のゲームロジックのみ
- **故障隔離**: ServiceDiscovery故障でも既存バトルは継続実行
- **負荷分散**: セッション管理とバトル処理の負荷を分離

#### **接続ライフサイクル管理**

**クライアント⇔ServiceDiscovery接続方式**: **接続切断型（推奨）**
```
1. クライアント → ServiceDiscovery: 接続＆セッション取得
2. ServiceDiscovery → クライアント: BattleServer情報返却
3. クライアント → ServiceDiscovery: 接続切断（セッション情報取得後即座に切断）
4. クライアント → BattleServer: 接続＆JoinGroup実行
```

**セッション終了検出の仕組み**:
```
方式1: BattleServer主導（推奨）
- BattleServer → ServiceDiscovery: バトル終了通知
- ServiceDiscovery: セッション状態をCompletedに変更

方式2: クライアント主導（補助）
- クライアント → ServiceDiscovery: セッション終了通知（TerminateSessionAsync）
- 接続失敗時のクリーンアップで使用

方式3: タイムアウト主導（フェイルセーフ）
- ServiceDiscovery: 30分間未更新のセッションを自動削除
```

**接続切断型を採用する理由**:
- **ServiceDiscovery負荷軽減**: 大量クライアント接続でも負荷を最小化
- **単純性**: クライアント側の接続管理が簡素
- **故障耐性**: ServiceDiscovery故障時でもバトル継続可能

#### **GameLiftモード時の追加フロー**
- ServiceDiscoveryServerがGameLift APIを呼び出してGameSession管理
- BattleServerの登録・ヘルスチェックもGameLift経由

#### **BattleServer → ServiceDiscovery 通知フロー**

**バトル状態変化の通知**:
```csharp
// BattleServerがServiceDiscoveryに状態変化を通知
public interface IServiceDiscoveryNotifier
{
    Task NotifyBattleStartedAsync(string sessionId);
    Task NotifyBattleCompletedAsync(string sessionId, BattleResult result);
    Task NotifySessionTerminatedAsync(string sessionId, TerminationReason reason);
}
```

**具体的な通知タイミング**:
1. **バトル開始時**: グループメンバー5人揃ってバトル開始
   - `NotifyBattleStartedAsync(sessionId)` → セッション状態を`InBattle`に変更
2. **バトル終了時**: 全クライアントのリプレイ視聴完了
   - `NotifyBattleCompletedAsync(sessionId, result)` → セッション状態を`Completed`に変更
3. **異常終了時**: クライアント全断、サーバー故障等
   - `NotifySessionTerminatedAsync(sessionId, reason)` → セッション状態を`Terminated`に変更

**実装済みの仕組み**:
- `BattleServer.ServiceDiscoveryClient`: 定期ハートビート送信とサーバー登録
- `CliClient.ServiceDiscoveryClientProvider.RemovePlayerFromSessionAsync()`: 接続失敗時のクリーンアップ
- `ServiceDiscoveryServer.SessionManager`: セッションタイムアウトによる自動削除

## サーバー仕様

### **ServiceDiscoveryServer**

#### **基本設定**
- **プロジェクト名**: `ServiceDiscoveryServer`
- **場所**: `csharp/src/ServiceDiscoveryServer/`
- **通信プロトコル**: SignalR (HTTP/1) + MagicOnion (HTTP/2)
- **ポート設定**:
  - SignalR: `5010`
  - MagicOnion: `5011`
  - Health Check: `5012`

#### **責任範囲**
- **セッション管理**:
  - グループ名ベースのGameSession作成・検索
  - **重要**: 同一グループに対して常に同一セッションを返却（GameLiftスタイル）
  - セッション状態の追跡（Active/Completed/Terminated）
  - セッション有効期限管理
  - **注意**: プレイヤー数カウントは行わない（BattleServerのグループ管理に委譲）
- **サーバー管理**:
  - 利用可能なBattleServerの登録・管理
  - サーバー負荷分散・割り当て
  - サーバーヘルスチェック・故障検出
- **GameLift統合**:
  - GameLift API経由でのGameSession管理（Anywhereモード）
  - Fleet管理・Compute管理
  - PlayerSession作成の代理実行
- **Direct接続サポート**:
  - インメモリでのグループ管理（セッション情報のみ）
  - BattleServerとの直接通信による状態同期

### **BattleServer（改修）**

#### **基本設定**
- **プロジェクト名**: `BattleServer` (InMemoryServerから改名)
- **場所**: `csharp/src/BattleServer/`
- **通信プロトコル**: SignalR (HTTP/1) + MagicOnion (HTTP/2)
- **ポート設定**: 動的割り当て（5000, 5001, 5002...）

#### **責任範囲**
- **バトル実行**: 既存のバトルロジックの実行
- **グループ管理**:
  - JoinGroup実行時の実際のバトルメンバー登録
  - **重要**: プレイヤー数カウントとバトル開始条件判定
  - グループ状態管理（ConnectionsReady, BattleStarted等）
- **サーバー登録**: ServiceDiscoveryServerへの登録・ヘルスレポート
- **GameLift統合**: GameLift Server SDK統合（Anywhereモード時）
- **Direct接続**: ServiceDiscoveryServerとの直接通信（Directモード時）

#### **なぜBattleServerでグループ管理するのか**
- **リアルタイム性**: 接続断・再接続の即座の反映が必要
- **バトル開始判定**: 実際に接続しているプレイヤーのみでバトル開始
- **故障隔離**: ServiceDiscovery故障時もバトル継続実行

## API仕様

### **ServiceDiscoveryServer API**

#### **SignalR Hub (`/discoveryHub`)**

```csharp
public class ServiceDiscoveryHub : Hub
{
    // セッション管理API
    Task<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request);
    Task<SessionInfo?> GetSessionInfoAsync(string sessionId);
    Task<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync();
    Task<bool> TerminateSessionAsync(string sessionId);

    // サーバー管理API
    Task<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync();
    Task<BattleServerInfo?> GetAssignedServerAsync(string sessionId);

    // BattleServer用API
    Task<bool> RegisterServerAsync(BattleServerRegistration registration);
    Task<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status);
    Task UnregisterServerAsync(string serverId);
}
```

#### **MagicOnion Service**

```csharp
public interface IServiceDiscoveryService : IService<IServiceDiscoveryService>
{
    // セッション管理API
    UnaryResult<SessionCreationResponse> CreateOrFindSessionAsync(SessionCreationRequest request);
    UnaryResult<SessionInfo?> GetSessionInfoAsync(string sessionId);
    UnaryResult<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync();
    UnaryResult<bool> TerminateSessionAsync(string sessionId);

    // サーバー管理API
    UnaryResult<IReadOnlyList<BattleServerInfo>> ListAvailableServersAsync();
    UnaryResult<BattleServerInfo?> GetAssignedServerAsync(string sessionId);

    // BattleServer用API
    UnaryResult<bool> RegisterServerAsync(BattleServerRegistration registration);
    UnaryResult<bool> UpdateServerStatusAsync(string serverId, BattleServerStatus status);
    UnaryResult<bool> UnregisterServerAsync(string serverId);
}
```

## データモデル

### **共有モデル**

```csharp
// セッション作成要求
public class SessionCreationRequest
{
    public string GroupName { get; set; } = string.Empty;
    public int MaxPlayers { get; set; } = 5;
    public SessionMode Mode { get; set; } = SessionMode.Auto; // Auto, GameLift, Direct
    public string? PreferredRegion { get; set; }
}

// セッション作成応答
public class SessionCreationResponse
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public SessionInfo? Session { get; set; }
    public BattleServerConnectionInfo? ConnectionInfo { get; set; }
}

// セッション情報
public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public SessionMode Mode { get; set; }
    public SessionStatus Status { get; set; }
    public string AssignedServerId { get; set; } = string.Empty;
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // GameLiftモード時のみ
    public string? GameSessionId { get; set; }
    public string? FleetId { get; set; }
}

// BattleServer接続情報
public class BattleServerConnectionInfo
{
    public string ServerId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int SignalRPort { get; set; }
    public int MagicOnionPort { get; set; }
    public string SignalRHubPath { get; set; } = "/battlehub";
    public ConnectionType SupportedTypes { get; set; }
}

// BattleServer登録情報
public class BattleServerRegistration
{
    public string ServerId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int SignalRPort { get; set; }
    public int MagicOnionPort { get; set; }
    public int MaxConcurrentSessions { get; set; } = 3;
    public IReadOnlyList<string> SupportedModes { get; set; } = Array.Empty<string>();
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

// BattleServerステータス
public class BattleServerStatus
{
    public string ServerId { get; set; } = string.Empty;
    public ServerHealth Health { get; set; } = ServerHealth.Healthy;
    public int ActiveSessions { get; set; }
    public int MaxSessions { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public DateTime LastHeartbeat { get; set; }
}

public enum SessionMode
{
    Auto,    // サーバーが最適なモードを選択
    GameLift, // GameLift Anywhereを強制使用
    Direct   // Direct接続を強制使用
}

public enum SessionStatus
{
    Creating,
    Active,
    InBattle,
    Completed,
    Terminated,
    Error
}

public enum ServerHealth
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}
```

## 設定仕様

### **ServiceDiscoveryServer設定**

```json
{
  "ServiceDiscovery": {
    "Server": {
      "SignalRPort": 5010,
      "MagicOnionPort": 5011,
      "HealthCheckPort": 5012,
      "AllowedOrigins": ["*"]
    },
    "Session": {
      "DefaultMaxPlayers": 5,
      "SessionTimeoutMinutes": 30,
      "CleanupIntervalMinutes": 5,
      "MaxConcurrentSessions": 100
    },
    "BattleServer": {
      "HeartbeatIntervalSeconds": 30,
      "HealthCheckTimeoutSeconds": 10,
      "UnhealthyThresholdCount": 3,
      "RemoveUnhealthyAfterMinutes": 5
    },
    "GameLift": {
      "Mode": "Auto", // "Disabled", "Auto", "Anywhere"
      "Anywhere": {
        "FleetId": "",
        "CustomLocation": "",
        "Region": "ap-northeast-1",
        "MaxGameSessionsPerFleet": 50
      },
      "AWS": {
        "Profile": "",
        "Region": "ap-northeast-1"
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ServiceDiscoveryServer": "Debug"
    }
  }
}
```

### **BattleServer設定追加**

```json
{
  "BattleServer": {
    "ServiceDiscovery": {
      "SignalREndpoint": "http://localhost:5010",
      "MagicOnionEndpoint": "http://localhost:5011",
      "RegistrationIntervalSeconds": 10,
      "HeartbeatIntervalSeconds": 30
    },
    "Server": {
      "SignalRPort": 0, // 0の場合は自動割り当て
      "MagicOnionPort": 0,
      "MaxConcurrentSessions": 3
    }
  }
}
```

## 実装計画

### **Phase 1: ServiceDiscoveryServer基盤構築**

#### **1.1 プロジェクト構造**

```
csharp/src/ServiceDiscoveryServer/
├── Http1Server/
│   ├── Hubs/
│   │   └── ServiceDiscoveryHub.cs
│   └── Extensions/
│       └── SignalRServiceExtensions.cs
├── Http2Server/
│   ├── Services/
│   │   └── ServiceDiscoveryService.cs
│   └── Extensions/
│       └── MagicOnionServiceExtensions.cs
├── Services/
│   ├── Core/
│   │   ├── ISessionManager.cs
│   │   ├── SessionManager.cs
│   │   ├── IBattleServerRegistry.cs
│   │   ├── BattleServerRegistry.cs
│   │   └── IGameLiftIntegration.cs
│   └── GameLift/
│       └── GameLiftSessionManager.cs
├── Models/
│   ├── Session/
│   │   ├── SessionModels.cs
│   │   └── SessionCreationModels.cs
│   └── Server/
│       ├── BattleServerModels.cs
│       └── ServerRegistrationModels.cs
├── Configuration/
│   └── ServiceDiscoveryOptions.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
├── Program.cs
└── appsettings.json
```

#### **1.2 実装タスク**
1. **基盤インフラ構築**
   - ASP.NET Core + SignalR + MagicOnion設定
   - 設定システム（Options Pattern）
   - ログ設定・構造化ログ

2. **セッション管理サービス**
   - インメモリセッション管理
   - セッション状態遷移ロジック
   - セッションタイムアウト・クリーンアップ

3. **BattleServerレジストリ**
   - サーバー登録・登録解除
   - ヘルスチェック・ハートビート管理
   - 負荷分散アルゴリズム

### **Phase 2: GameLift統合**

#### **2.1 GameLift統合サービス**
```csharp
public class GameLiftSessionManager : IGameLiftIntegration
{
    Task<SessionCreationResponse> CreateGameLiftSessionAsync(SessionCreationRequest request);
    Task<SessionInfo?> GetGameLiftSessionAsync(string sessionId);
    Task<bool> TerminateGameLiftSessionAsync(string sessionId);
    Task<IReadOnlyList<SessionInfo>> ListGameLiftSessionsAsync();
}
```

#### **2.2 実装タスク**
1. **GameLift API統合**
   - CreateGameSession API呼び出し
   - DescribeGameSessions API呼び出し
   - CreatePlayerSession API代理実行

2. **Fleet管理**
   - 利用可能Fleet検索
   - Fleet容量監視
   - 自動スケーリング対応

### **Phase 3: BattleServer改修**

#### **3.1 ServiceDiscovery統合**
```csharp
public class ServiceDiscoveryClient : IHostedService
{
    Task RegisterAsync();
    Task SendHeartbeatAsync();
    Task UpdateStatusAsync(BattleServerStatus status);
    Task UnregisterAsync();
}
```

#### **3.2 実装タスク**
1. **サーバー登録機能**
   - 起動時の自動登録
   - 設定情報の送信
   - サーバーID管理

2. **ヘルスレポート**
   - 定期ハートビート送信
   - リソース使用率レポート
   - セッション状態レポート

### **Phase 4: Client統合**

#### **4.1 Client改修**
```csharp
public class ServiceDiscoveryClientProvider : IBattleClientProvider
{
    Task<SessionCreationResponse> CreateSessionAsync(string groupName);
    Task<BattleServerConnectionInfo> GetServerConnectionAsync(string sessionId);
    Task<IHubConnection> ConnectToBattleServerAsync(BattleServerConnectionInfo connectionInfo);
}
```

#### **4.2 実装タスク**
1. **ServiceDiscovery通信**
   - セッション作成要求
   - サーバー情報取得
   - 接続先解決

2. **BattleServer接続**
   - 動的接続先変更
   - 接続エラーハンドリング
   - 再接続ロジック

## 負荷分散・可用性

### **サーバー選択アルゴリズム**

#### **1. ラウンドロビン（デフォルト）**
```csharp
public BattleServerInfo? SelectServer(IReadOnlyList<BattleServerInfo> availableServers)
{
    return availableServers
        .Where(s => s.Health == ServerHealth.Healthy)
        .OrderBy(s => s.ActiveSessions)
        .FirstOrDefault();
}
```

#### **2. 負荷ベース選択**
```csharp
public BattleServerInfo? SelectServerByLoad(IReadOnlyList<BattleServerInfo> availableServers)
{
    return availableServers
        .Where(s => s.Health == ServerHealth.Healthy)
        .OrderBy(s => s.LoadScore) // CPU + Memory + Sessions
        .FirstOrDefault();
}
```

### **高可用性設計**

#### **1. ServiceDiscoveryServer冗長化**
- 複数インスタンスでの実行
- Redis/Hazelcastによる状態共有
- ロードバランサーによる負荷分散

#### **2. BattleServer故障対応**
- ヘルスチェック故障検出
- 自動的なサーバー除外
- セッション移行（将来実装）

## セキュリティ考慮事項

### **1. 認証・認可**
- JWT認証によるクライアント認証
- BattleServer登録時の認証
- API呼び出し時の権限チェック

### **2. 通信セキュリティ**
- HTTPS/WSS通信の強制
- CORS設定による制限
- Rate limiting実装

### **3. GameLift統合セキュリティ**
- AWS IAMロールによる認証
- 最小権限の原則適用
- アクセスキー管理の統一

## 監視・運用

### **1. メトリクス**
- セッション作成・完了数
- BattleServer登録・除外数
- レスポンス時間
- エラー発生率

### **2. ログ**
- 構造化ログによる出力
- セッションライフサイクル追跡
- BattleServer状態変更追跡
- GameLift API呼び出し追跡

### **3. ヘルスチェック**
- `/health` エンドポイント
- 依存サービス接続チェック
- リソース使用率チェック

## 移行戦略

### **1. 段階的移行**
1. ServiceDiscoveryServer導入
2. BattleServer改修・ServiceDiscovery統合
3. Client改修・新フロー対応
4. 既存Direct接続の段階的移行

### **2. 後方互換性**
- 既存のDirect接続API維持
- 設定による移行制御
- 段階的な機能切り替え

### **3. テスト戦略**
- ServiceDiscoveryServerの単体テスト
- BattleServerとの統合テスト
- Client-ServiceDiscovery-BattleServerのE2Eテスト
- GameLift統合テスト

この仕様により、スケーラブルで可用性が高く、GameLiftとDirect接続を透過的に扱えるサービスディスカバリーシステムを構築します。
