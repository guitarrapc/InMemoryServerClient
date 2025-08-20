# GameLift Anywhere 技術解説

## 1. GameL## 実装状況

### ✅ 完了済み

#### フェーズ1A: 基盤実装
- **設定システム**: `GameLiftOptions`クラスによる設定管理 (appsettings.json + 環境変数)
- **抽象化レイヤー**: `IGameServerProvider`インターフェースと`GameServerProviderFactory`
- **モデル定義**: `ComputeInfo`, `AuthTokenInfo`, `ProcessParameters`等の構造体
- **Direct Mode対応**: `DirectConnectionProvider`による既存機能の保持

#### フェーズ1B: GameLift Anywhere実装（サーバー側）
- **制御プレーン（AWS SDK）**: Compute登録・管理、AuthToken取得
- **サーバーSDK（WSS）**: InitSDK, ProcessReady, ActivateGameSession, ProcessEnding
- **依存性注入**: 設定ベースのプロバイダー選択とライフサイクル管理
- **認証情報管理**: AWS Profile/SSO優先、フォールバック機構付き
- **エラーハンドリング**: 適切なログ出力と例外処理
- **シャットダウン処理**: アプリケーション終了時のGameLift通知

#### フェーズ1C: GameLift Anywhere実装（クライアント側）
- **クライアント抽象化**: `IGameLiftClientProvider`インターフェース
- **Fleet Anywhere対応**: FleetIdベースのサーバー検索
- **Direct Mode維持**: 既存の直接接続機能の保持

#### 実装品質改善
- **DI最適化**: 条件付きサービス登録（Directモード時はGameLiftクライアント未登録）
- **AWS認証優先順序**: Profile > SSO > 明示的認証情報 > デフォルトチェーン
- **型安全性**: null許容型の適切な処理とエラー防止
- **ライフサイクル管理**: Singletonプロバイダーによる適切なリソース管理hereとは
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

## 3. 本アプリの接続フロー

### サーバー起動時
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

## 4. 実装状態
- [x] 制御プレーン（AWS SDK）によるCompute登録・AuthToken取得（`GameLiftAnywhereProvider`）
- [x] サーバーSDK（WSS）によるGameLiftとのランタイム連携（InitSDK/ProcessReady/ActivateGameSession/ProcessEnding）
- [x] 設定ファイル・DIによるモード切替
- [x] クライアント/サーバー両方でAnywhereモード対応
- [ ] クライアント側のFleet探索・自動接続（今後拡張）
- [ ] GameSession開始時のアプリ固有ロジック連携（onStartGameSessionコールバックの拡張）
- [ ] GameLift Anywhere/FleetIQの統合テスト・運用ドキュメント

## 5. 次にやること（TODO）
- クライアント側のFleet探索・自動接続機能の実装
- GameSession開始時のアプリロジック連携（onStartGameSessionでバトル開始等）
- GameLift Anywhere/FleetIQの統合テスト・運用手順の整備
- エラー/障害時のリカバリ・監視設計

## 6. 参考
- [AWS公式ドキュメント: GameLift Anywhere](https://docs.aws.amazon.com/ja_jp/gamelift/latest/developerguide/fleets-anywhere.html)
- [AWS SDK for .NET: GameLift API](https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/GameLift/NGameLift.html)

---

**最終更新: 2025-08-20**
