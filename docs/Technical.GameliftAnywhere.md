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
3. **SDK初期化**: サーバーSDKは地域ベースのGameLift WebSocketエンドポイント（例：`wss://{region}.api.amazongamelift.com`）にAuthTokenで接続し、GameSession管理の準備
4. **ProcessReady**: サーバーSDKがGameLiftに自身の準備完了を通知
5. **GameSession管理**: GameLiftからの指示でゲームセッションの開始/終了を管理

**重要な注意事項**:
- WebSocketURLはAWSリージョンに基づいて決定される（例：us-west-2の場合 `wss://us-west-2.api.amazongamelift.com`）
- `GetComputeAuthToken`のレスポンスにはWebSocket URLは含まれないため、リージョン設定から動的に構築する

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

### クライアント起動時
- appsettings.jsonで`GameLift.Mode = Anywhere`を指定
- FleetId/Locationを指定してサーバー探索（今後拡張）
- 現状はサーバーのWebSocketエンドポイントに直接接続

## 4. 実装状況

### ✅ 完了済み（最新アーキテクチャ v3）

#### 🚀 WebSocketエンドポイント修正（動作確認済み）
- **正しいエンドポイント形式**: `wss://{region}.api.amazongamelift.com`形式のWebSocketURLを動的構築
- **動作確認完了**: GameLift Anywhereサーバー登録・InitSDK・ProcessReady処理が正常動作
- **地域対応**: AWS地域設定に基づく適切なエンドポイント選択機能

#### モジュラー設計による関心事の分離
- **フォルダ構成**: Server/GameLift、Shared/GameLift、Client/GameLiftによる明確な責任分離
- **疎結合設計**: ASP.NET Core標準の`IHostedService`パターンを採用
- **条件付きサービス登録**: Anywhereモード時のみGameLift関連サービスを登録
- **起動オーバーヘッド削減**: Directモード時は関連コードが一切実行されない

#### フェーズ1A: 基盤実装
- **設定システム**: `GameLiftOptions`クラスによる設定管理 (appsettings.json + 環境変数)
- **HostedService**: `GameLiftAnywhereHostedService`による適切なライフサイクル管理
- **モデル定義**: `ComputeInfo`, `AuthTokenInfo`等の構造体

#### フェーズ1B: GameLift Anywhere実装（サーバー側）✅ 完了
- **制御プレーン（AWS SDK）**: Compute登録・管理、AuthToken取得
- **サーバーSDK（WSS）**: InitSDK, ProcessReady, ActivateGameSession, ProcessEnding
- **WebSocketエンドポイント**: 地域ベース動的構築（`wss://{region}.api.amazongamelift.com`）
- **動作確認**: GameLift Anywhereでの接続・認証・SDK初期化が正常動作
- **認証情報管理**: AWS Profile/SSO優先、フォールバック機構付き
- **エラーハンドリング**: 適切なログ出力と例外処理
- **自動シャットダウン**: ASP.NET Core終了時のGameLift通知

#### フェーズ1C: GameLift Anywhere実装（クライアント側）
- **クライアント抽象化**: `IGameLiftClientProvider`インターフェース
- **Fleet Anywhere対応**: FleetIdベースのサーバー検索
- **Direct Mode維持**: 既存の直接接続機能の保持

### 📋 今後の実装予定

#### 次の優先実装項目

**1. アプリケーション連携（優先度: 高）**
- [ ] GameSession開始時のバトル開始連携
- [ ] `onStartGameSession`コールバックでのアプリ固有処理
- [ ] GameSessionとInMemoryServerのバトル機能の統合

**2. 統合テスト（優先度: 高）**
- [ ] GameLift Anywhereモードでのサーバー起動テスト
- [ ] 実際のFleet環境でのE2Eテスト
- [ ] バトル開始からGameSession終了までの完全フローテスト

#### クライアント機能拡張
- [ ] Fleet探索・自動接続機能の拡充
- [ ] GameSession参加時の自動処理

#### Phase 2: GameLift FleetIQ対応（優先度: 中）
- [ ] FleetIQモードのHostedService実装
- [ ] ECS/EC2環境での動作検証

## 5. 現在の状況と次のステップ

### 🎉 現在達成できていること
1. **GameLift Anywhere基盤の動作**: サーバー登録・認証・SDK初期化が完全に動作
2. **適切なアーキテクチャ**: ASP.NET Core標準パターンによる保守性の高い実装
3. **設定の柔軟性**: appsettings.jsonと環境変数による設定管理

### 🎯 次に実装すべき機能（推奨順序）

**ステップ1: GameSessionとバトルシステムの統合**
- `onStartGameSession`コールバックでバトル開始をトリガー
- バトル完了時に`GameLiftServerAPI.ProcessEnding()`を呼び出し
- GameSessionIdとバトルIdの連携

**ステップ2: クライアント側GameLift対応**
- GameLiftクライアントSDKでGameSession参加
- FleetIdベースのサーバー検索機能

**ステップ3: 統合テストとドキュメント**
- 実際のFleet環境でのテスト
- 運用手順書の整備

## 6. 設計の利点

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

## 7. 今後の課題と対応

### 短期的な課題
1. ~~旧実装のクリーンアップ: 不要になったプロバイダークラスの削除~~ ✅ 完了
2. ~~統合テスト: HostedService方式での動作確認~~ ✅ GameLift Anywhere動作確認完了
3. ~~ドキュメント整備: 新しいアーキテクチャの運用手順~~ ✅ 完了

### 中期的な拡張
1. **GameSessionとアプリ統合**: バトルシステムとGameSessionの連携
2. **FleetIQ対応**: 新しいHostedServiceクラスの追加
3. **監視機能**: GameLiftの状態監視とアラート機能

## 8. 参考
- [AWS公式ドキュメント: GameLift Anywhere](https://docs.aws.amazon.com/ja_jp/gamelift/latest/developerguide/fleets-anywhere.html)
- [AWS SDK for .NET: GameLift API](https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/GameLift/NGameLift.html)
- [AWS公式ドキュメント: C# server SDK 5.x for Amazon GameLift Servers -- Actions](https://docs.aws.amazon.com/gameliftservers/latest/developerguide/integration-server-sdk5-csharp-actions.html)
- [AWS公式ドキュメント: Game client/server interactions with Amazon GameLift Servers](https://docs.aws.amazon.com/gameliftservers/latest/developerguide/gamelift-sdk-interactions.html)
- [AWS公式ドキュメント: Amazon GameLift Servers AMI バージョン](https://docs.aws.amazon.com/ja_jp/gameliftservers/latest/developerguide/reference-ec2-ami-version-history.html)
- [aws-samples/amazon-gamelift-anywhere-sample - GitHub](https://github.com/aws-samples/amazon-gamelift-anywhere-sample)
- [amazon-gamelift/amazon-gamelift-servers-csharp-server-sdk - GitHub](https://github.com/amazon-gamelift/amazon-gamelift-servers-csharp-server-sdk)

---

**最終更新: 2025-08-20**
