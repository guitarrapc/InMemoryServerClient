# GameLift Anywhere 技術解説

## 1. GameLiftAnywhereとは
Amazon GameLift Anywhereは、AWS GameLiftのFleet管理機能をオンプレミスやローカル環境のサーバーにも拡張できるサービスです。これにより、AWS外の任意のサーバーをGameLift Fleetの一部として登録し、クラウドと同様のセッション管理・スケーリング・認証などの機能を利用できます。

### 特徴
- AWS外のサーバー（ローカルPCやオンプレミス）をFleetに追加可能
- GameLiftの制御プレーン（管理API）とサーバーSDK（ランタイム管理）の両方を利用
- クラウド/オンプレ混在のハイブリッド運用が可能

## 2. 典型的な接続フロー

### (A) 制御プレーン（AWS SDK）
1. **Compute登録**: サーバーはFleetId/ComputeName/Locationで自身をFleetに登録（`RegisterCompute`）
2. **認証トークン取得**: サーバーは`GetComputeAuthToken`でFleet/Compute用のAuthTokenとServiceSdkEndpoint(WSS)を取得

### (B) サーバーSDK（WebSocket）
3. **SDK初期化**: サーバーSDKはServiceSdkEndpoint(WSS)にAuthTokenで接続し、GameSession管理の準備
4. **ProcessReady**: サーバーSDKがGameLiftに自身の準備完了を通知
5. **GameSession管理**: GameLiftからの指示でゲームセッションの開始/終了を管理

## 3. アーキテクチャ概要

### 新しい疎結合アーキテクチャ（v2）

#### 設計原則
- **ASP.NET Core標準パターン**: `IHostedService`/`BackgroundService`によるライフサイクル管理
- **条件付きサービス登録**: 必要な時だけGameLift関連サービスを登録
- **完全な疎結合**: 不要なモードでは関連コードが一切実行されない
- **責任分離**: Program.csからGameLift初期化ロジックを完全分離

#### GameLift Anywhereモード
```
Application Startup
├── appsettings.json読み込み
├── Mode: "Anywhere" 検出
├── GameLiftAnywhereHostedService 登録 ← BackgroundService
├── IAmazonGameLift 登録
└── 通常のASP.NET Core起動

GameLiftAnywhereHostedService.ExecuteAsync()
├── 制御プレーン初期化（Compute登録、AuthToken取得）
├── サーバーSDK初期化（WSS接続、ProcessReady）
└── バックグラウンドで実行継続

Application Shutdown
└── GameLiftAnywhereHostedService.StopAsync() ← 自動呼び出し
    └── ProcessEnding通知
```

#### Directモード
```
Application Startup
├── appsettings.json読み込み
├── Mode: "Direct" 検出
├── GameLift関連サービス登録なし ← 完全にスキップ
└── 通常のASP.NET Core起動のみ
```

### 旧アーキテクチャとの比較

| 項目 | 旧実装（密結合） | 新実装（疎結合） |
|------|------------------|------------------|
| 初期化 | Program.csで直接 | HostedServiceで自動 |
| ライフサイクル | 手動管理 | ASP.NET Core標準 |
| Directモード | 不要サービスも登録 | 完全にスキップ |
| テスト容易性 | 難 | 易（HostedService単体テスト可能） |
| 起動オーバーヘッド | 常に存在 | 必要時のみ |

- appsettings.jsonで`GameLift.Mode = Anywhere`を指定
- DIで`GameLiftAnywhereProvider`が選択される
- サーバー起動時に以下を実施：
  1. `RegisterComputeAsync`でFleet/Compute登録（既存ならスキップ）
  2. `GetAuthTokenAsync`で認証トークンとServiceSdkEndpoint取得
  3. （今後実装）ServerSDKでWSS接続・ProcessReady
- クライアントは通常通りWebSocketでサーバーに接続

### クライアント起動時
- appsettings.jsonで`GameLift.Mode = Anywhere`を指定
- FleetId/Locationを指定してサーバー探索（今後拡張）
- 現状はサーバーのWebSocketエンドポイントに直接接続

## 4. 実装状況

### ✅ 完了済み

#### アーキテクチャ改善（v2）
- **疎結合設計**: ASP.NET Core標準の`IHostedService`パターンを採用
- **条件付きサービス登録**: 必要な時だけGameLift関連サービスを登録
- **責任分離**: Program.csからGameLift初期化ロジックを完全分離
- **起動オーバーヘッド削減**: Directモード時は関連コードが一切実行されない

#### フェーズ1A: 基盤実装
- **設定システム**: `GameLiftOptions`クラスによる設定管理 (appsettings.json + 環境変数)
- **HostedService**: `GameLiftAnywhereHostedService`による適切なライフサイクル管理
- **モデル定義**: `ComputeInfo`, `AuthTokenInfo`等の構造体

#### フェーズ1B: GameLift Anywhere実装（サーバー側）
- **制御プレーン（AWS SDK）**: Compute登録・管理、AuthToken取得
- **サーバーSDK（WSS）**: InitSDK, ProcessReady, ActivateGameSession, ProcessEnding
- **認証情報管理**: AWS Profile/SSO優先、フォールバック機構付き
- **エラーハンドリング**: 適切なログ出力と例外処理
- **自動シャットダウン**: ASP.NET Core終了時のGameLift通知

#### フェーズ1C: GameLift Anywhere実装（クライアント側）
- **クライアント抽象化**: `IGameLiftClientProvider`インターフェース
- **Fleet Anywhere対応**: FleetIdベースのサーバー検索
- **Direct Mode維持**: 既存の直接接続機能の保持

### ⏳ 保留中（旧実装の残骸）
- **旧プロバイダーシステム**: `IGameServerProvider`、`GameServerProviderFactory`等は残存
- **クリーンアップ**: 不要なクラスの削除は今後実施

### 📋 今後の実装予定

#### クライアント機能拡張
- [ ] Fleet探索・自動接続機能
- [ ] GameSession参加時の自動処理

#### アプリケーション連携
- [ ] GameSession開始時のバトル開始連携
- [ ] onStartGameSessionコールバックでのアプリ固有処理

#### Phase 2: GameLift FleetIQ対応
- [ ] FleetIQモードのHostedService実装
- [ ] ECS/EC2環境での動作検証

## 5. 設計の利点

### ASP.NET Core標準パターンによる利点
- **自動ライフサイクル管理**: 起動・シャットダウンが自動で適切に処理される
- **例外処理の標準化**: HostedServiceの例外処理により、アプリケーション全体の安定性向上
- **テスト容易性**: HostedServiceは単体でテストしやすい
- **監視・デバッグ**: ASP.NET Coreの標準的な監視ツールで状態確認可能

### 疎結合による利点
- **起動時間短縮**: 不要なサービスが登録されない
- **メモリ使用量削減**: GameLift関連のライブラリが読み込まれない（Directモード時）
- **保守性向上**: 機能ごとの責任が明確に分離されている
- **拡張性**: 新しいGameLiftサービス（FleetIQ等）の追加が容易

## 6. 今後の課題と対応

### 短期的な課題
1. **旧実装のクリーンアップ**: 不要になったプロバイダークラスの削除
2. **統合テスト**: HostedService方式での動作確認
3. **ドキュメント整備**: 新しいアーキテクチャの運用手順

### 中期的な拡張
1. **FleetIQ対応**: 新しいHostedServiceクラスの追加
2. **ゲームセッション連携**: アプリケーション固有のロジックとの統合
3. **監視機能**: GameLiftの状態監視とアラート機能

## 7. 参考
- [AWS公式ドキュメント: GameLift Anywhere](https://docs.aws.amazon.com/ja_jp/gamelift/latest/developerguide/fleets-anywhere.html)
- [AWS SDK for .NET: GameLift API](https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/GameLift/NGameLift.html)

---

**最終更新: 2025-08-20**
