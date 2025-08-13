---
applyTo: "**"
---

# GameLift統合仕様と実装計画

## 概要

InMemoryServerClientプロジェクトにAmazon GameLiftの統合を段階的に実装する。GameLiftの利用はオプショナルとし、既存の直接接続機能と共存できる設計とする。

## 対応するGameLiftサービス

### 1. GameLift Fleet Anywhere（フェーズ1）
- **用途**: ローカル開発環境やオンプレミス環境での利用
- **特徴**:
  - 既存のサーバーインフラをGameLiftに登録して利用
  - ローカル開発時のテストに適している
  - AWS外のリソースでもGameLiftの機能を利用可能
- **実装優先度**: 高（最初に実装）

### 2. GameLift FleetIQ（フェーズ2）
- **用途**: ECS、EC2、Kubernetesなどのコンテナ環境での利用
- **特徴**:
  - スポットインスタンスの活用によるコスト最適化
  - 自動スケーリングとヘルスモニタリング
  - 高可用性とコスト効率性の両立
- **実装優先度**: 中（フェーズ1完了後）

## アーキテクチャ設計

### 動作モード

システムは以下の3つの動作モードをサポートする：

1. **Direct Mode（既存）**: 直接接続による通信
2. **GameLift Anywhere Mode**: GameLift Fleet Anywhereを使用
3. **GameLift FleetIQ Mode**: GameLift FleetIQを使用

### 設計原則

#### 1. オプショナル統合
- **設計意図**: GameLiftの利用は任意とし、既存機能に影響を与えない
- **実装方針**:
  - 設定ファイルによる動作モード切り替え
  - GameLift未使用時はSDKを読み込まない（遅延読み込み）
  - 既存のテストコードは影響を受けない

#### 2. 抽象化による実装
- **設計意図**: 複数のGameLiftサービスと直接接続を統一インターフェースで扱う
- **実装方針**:
  - `IGameServerProvider`インターフェースによる抽象化
  - Factory Patternによる実装切り替え
  - 依存性注入による疎結合設計

#### 3. 設定駆動
- **設計意図**: 実行時に動作モードを変更可能にする
- **実装方針**:
  - appsettings.jsonによる設定管理
  - 環境変数による設定オーバーライド
  - コマンドライン引数による設定オーバーライド

## 設定仕様

### サーバー設定（appsettings.json）

```json
{
  "GameLift": {
    "Mode": "Direct", // "Direct" | "Anywhere" | "FleetIQ"
    "Anywhere": {
      "FleetId": "",
      "ComputeName": "",
      "CustomLocation": "",
      "WebSocketUrl": "wss://localhost:5001/battlehub"
    },
    "FleetIQ": {
      "GameServerGroupName": "",
      "GameServerId": "",
      "InstanceId": ""
    },
    "AWS": {
      "Region": "us-west-2",
      "Profile": "", // AWS CLI Profile名（推奨）
      "SsoSessionName": "", // AWS Identity Center SSO Session名（推奨）
      "AccessKeyId": "", // 非推奨：開発・テスト時のみ
      "SecretAccessKey": "", // 非推奨：開発・テスト時のみ
      "SessionToken": "" // STSトークン使用時
    }
  },
  "Server": {
    "Port": 5001,
    "AllowedOrigins": ["*"]
  }
}
```

### クライアント設定（appsettings.json）

```json
{
  "GameLift": {
    "Mode": "Direct", // "Direct" | "Anywhere" | "FleetIQ"
    "Anywhere": {
      "FleetId": "",
      "CustomLocation": ""
    },
    "FleetIQ": {
      "GameServerGroupName": ""
    },
    "AWS": {
      "Region": "us-west-2",
      "Profile": "",
      "SsoSessionName": "",
      "AccessKeyId": "",
      "SecretAccessKey": "",
      "SessionToken": ""
    }
  },
  "Client": {
    "DefaultServerUrl": "wss://localhost:5001/battlehub",
    "ConnectionTimeout": 30000
  }
}
```

### 環境変数による設定オーバーライド

```bash
# AWS認証情報（Profile/SSO推奨）
export AWS_PROFILE=my-gamelift-profile
export AWS_SSO_SESSION_NAME=my-sso-session

# 動作モード
export GAMELIFT__MODE=Anywhere

# AWS認証情報（非推奨：開発・テスト時のみ）
export GAMELIFT__AWS__REGION=us-west-2
export GAMELIFT__AWS__ACCESSKEYID=your-access-key
export GAMELIFT__AWS__SECRETACCESSKEY=your-secret-key

# GameLift Anywhere
export GAMELIFT__ANYWHERE__FLEETID=fleet-12345
export GAMELIFT__ANYWHERE__COMPUTENAME=local-compute-01

# GameLift FleetIQ
export GAMELIFT__FLEETIQ__GAMESERVERGROUPNAME=my-game-server-group
```

## 実装計画

### フェーズ1: GameLift Fleet Anywhere対応

#### 1.1 GameLift Anywhere通信アーキテクチャ

GameLift Anywhereでは2つの独立した通信経路を実装する：

##### 1.1.1 制御プレーン通信（AWS SDK for GameLift）
**用途**: Compute管理とAuthToken取得
**通信方式**: HTTPS REST API

- **Compute探索・登録フロー**:
  1. `ListCompute` APIでCompute存在確認
  2. 見つからない場合は `RegisterCompute` APIで新規登録
  3. `GetComputeAuthToken` APIで認可トークン取得
  4. 取得した `ServiceSdkEndpoint`（WSS URL）と `AuthToken` をサーバーSDKに渡す

- **実装考慮事項**:
  - AWS認証情報（IAM）による API認証
  - リトライ・エラーハンドリング機構
  - Compute情報の永続化（再起動時の高速化）

##### 1.1.2 サーバーSDKランタイム通信（WebSocket）
**用途**: GameSessionライフサイクル管理
**通信方式**: WSS（WebSocket Secure）

- **初期化フロー**:
  1. `ServerParameters`（HostID, FleetID, AuthToken, ProcessID, WebSocketURL）構築
  2. `InitSDK` でWebSocket接続確立
  3. `ProcessReady` でコールバック登録（HealthCheck, OnStartGameSession等）

- **ライフサイクル管理**:
  - セッション開始: `ActivateGameSession`
  - セッション終了: `ProcessEnding`
  - ヘルスチェック: 定期的な生存確認

#### 1.2 インターフェース設計

```csharp
// 抽象化インターフェース
public interface IGameServerProvider
{
    // 制御プレーン操作
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    Task<ComputeInfo> RegisterComputeAsync(CancellationToken cancellationToken = default);
    Task<AuthTokenInfo> GetAuthTokenAsync(CancellationToken cancellationToken = default);

    // サーバーSDK操作
    Task<bool> InitServerSdkAsync(AuthTokenInfo authToken, CancellationToken cancellationToken = default);
    Task<bool> ProcessReadyAsync(ProcessParameters parameters, CancellationToken cancellationToken = default);
    Task ActivateGameSessionAsync(string gameSessionId, CancellationToken cancellationToken = default);
    Task ProcessEndingAsync(CancellationToken cancellationToken = default);

    // 共通操作
    Task<string> GetConnectionEndpointAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

// GameLift Anywhere実装
public class GameLiftAnywhereProvider : IGameServerProvider
{
    private readonly IGameLiftClient _controlPlaneClient; // AWS SDK
    private readonly IGameLiftServerSdk _serverSdk; // Server SDK v5
    // 制御プレーンとサーバーSDKの両方を管理
}

// 直接接続実装
public class DirectConnectionProvider : IGameServerProvider
{
    // 既存の直接接続ロジック
}
```

#### 1.2 設定管理

```csharp
public class GameLiftOptions
{
    public GameLiftMode Mode { get; set; } = GameLiftMode.Direct;
    public GameLiftAnywhereOptions Anywhere { get; set; } = new();
    public GameLiftFleetIQOptions FleetIQ { get; set; } = new();
    public AWSOptions AWS { get; set; } = new();
}

public class GameLiftAnywhereOptions
{
    public string FleetId { get; set; } = string.Empty;
    public string ComputeName { get; set; } = string.Empty;
    public string CustomLocation { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;

    // 制御プレーン設定
    public string HostId { get; set; } = Environment.MachineName;
    public string ProcessId { get; set; } = Environment.ProcessId.ToString();
    public TimeSpan AuthTokenRefreshInterval { get; set; } = TimeSpan.FromHours(1);

    // サーバーSDK設定
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConcurrentGameSessions { get; set; } = 1;
}

public enum GameLiftMode
{
    Direct,
    Anywhere,
    FleetIQ
}
```

#### 1.3 依存性注入設定

```csharp
// Program.cs
public static void ConfigureGameLiftServices(this IServiceCollection services, GameLiftOptions options)
{
    services.Configure<GameLiftOptions>(config =>
    {
        config.Mode = options.Mode;
        config.Anywhere = options.Anywhere;
        // ... 設定のコピー
    });

    services.AddSingleton<IGameServerProviderFactory, GameServerProviderFactory>();

    // プロバイダーの登録
    services.AddTransient<DirectConnectionProvider>();
    services.AddTransient<GameLiftAnywhereProvider>();
    // services.AddTransient<GameLiftFleetIQProvider>(); // フェーズ2で追加
}
```

#### 1.4 実装タスク

1. **設定システム構築**
   - GameLiftOptionsクラスの実装
   - IConfigurationからの設定読み込み
   - 環境変数・コマンドライン引数サポート

2. **抽象化レイヤー実装**
   - IGameServerProviderインターフェース定義
   - DirectConnectionProvider（既存ロジックのラップ）
   - GameServerProviderFactoryの実装

3. **GameLift Anywhere実装**
   - AWS SDK for .NET参照追加
   - GameLift Server SDK v5の統合
   - GameLiftAnywhereProviderの実装

4. **サーバー統合**
   - 起動時のプロバイダー初期化
   - SignalRハブでのGameLift統合
   - ヘルスチェック機能の統合

5. **クライアント統合**
   - GameLiftクライアント機能の実装
   - 接続先解決ロジック
   - FleetId指定によるサーバー検索

#### 1.5 テスト戦略

```csharp
// ユニットテスト
[Fact]
public async Task GameLiftAnywhereProvider_ShouldInitializeCorrectly()
{
    // モックAWS SDKを使用したテスト
}

// 統合テスト
[Fact]
public async Task ServerClient_ShouldWorkWithGameLiftAnywhere()
{
    // ローカルGameLift Anywhereを使用した統合テスト
}

// 設定テスト
[Theory]
[InlineData("Direct")]
[InlineData("Anywhere")]
public void Configuration_ShouldSupportAllModes(string mode)
{
    // 設定の正しい読み込みテスト
}
```

### フェーズ2: GameLift FleetIQ対応

#### 2.1 追加実装

```csharp
public class GameLiftFleetIQProvider : IGameServerProvider
{
    // GameLift FleetIQ APIを使用した実装
    // ECS、EC2インスタンスとの統合
}
```

#### 2.2 コンテナ対応

- Docker環境でのGameLift FleetIQ統合
- ECSタスク定義の提供
- Kubernetesマニフェストの提供

## セキュリティ考慮事項

### 1. AWS認証情報管理
- **設計意図**: 認証情報の安全な管理
- **実装方針**:
  - **推奨**: AWS CLI ProfileまたはAWS Identity Center（SSO）の使用
  - **Profile設定例**: `aws configure --profile my-gamelift-profile`
  - **SSO設定例**: `aws configure sso --session-name my-sso-session`
  - **非推奨**: 環境変数やappsettings.jsonでのAccessKey直接指定
  - **開発・テスト時のみ**: 一時的なAccessKey使用を許可
  - IAMロールの使用を優先（EC2、ECS、Lambda等での実行時）

### 2. 通信セキュリティ
- **設計意図**: GameLiftとの通信セキュリティ確保
- **実装方針**:
  - TLS 1.3による暗号化通信
  - AWS Signature V4による認証
  - セッションベースの一時認証情報

### 3. アクセス制御
- **設計意図**: 最小権限の原則に従ったアクセス制御
- **実装方針**:
  - GameLift専用IAMロールの作成
  - 必要最小限の権限のみ付与
  - リソースベースの権限制御

## 必要なパッケージ

### サーバー（InMemoryServer）
```xml
<PackageReference Include="AWS.GameLift.Server.Sdk" Version="5.1.0" />
<PackageReference Include="AWSSDK.GameLift" Version="3.7.300" />
<PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.7.300" />
```

### クライアント（CliClient）
```xml
<PackageReference Include="AWSSDK.GameLift" Version="3.7.300" />
<PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.7.300" />
```

## リスクと対策

### 1. AWS SDK依存関係
- **リスク**: 大きなSDKによるアプリケーションサイズ増加
- **対策**: 条件付きコンパイルと遅延読み込みによる最適化

### 2. 設定複雑性
- **リスク**: 多様な設定オプションによる運用複雑化
- **対策**: デフォルト値の適切な設定と設定検証の実装

### 3. テストの複雑性
- **リスク**: AWS依存テストの実行環境課題
- **対策**: モックとローカルエミュレーターの活用

## 実装順序

1. **フェーズ1A**: 基盤実装（設定・抽象化・Direct Mode対応）
2. **フェーズ1B**: GameLift Anywhere実装（サーバー側）
3. **フェーズ1C**: GameLift Anywhere実装（クライアント側）
4. **フェーズ1D**: 統合テストとドキュメント整備
5. **フェーズ2A**: GameLift FleetIQ設計
6. **フェーズ2B**: GameLift FleetIQ実装
7. **フェーズ2C**: コンテナ環境対応

## 成功指標

### フェーズ1完了時
- [ ] 設定ファイルによる動作モード切り替えが動作する
- [ ] GameLift Anywhereでのサーバー登録が成功する
- [ ] クライアントがGameLift経由でサーバーに接続できる
- [ ] 既存の直接接続機能が影響を受けない
- [ ] 全てのテストが通過する

### フェーズ2完了時
- [ ] GameLift FleetIQでのサーバー管理が動作する
- [ ] コンテナ環境でのデプロイが成功する
- [ ] 自動スケーリングが適切に機能する
- [ ] コスト最適化が確認できる

この仕様書に基づいて段階的にGameLift統合を実装し、各フェーズ完了時に動作確認とテストを実施する。
