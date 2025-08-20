# CliClient.Tests - テスト構造とガイドライン

## 🎯 解決された問題

以前のテストは外部でサーバーを起動しているかどうかによって結果が変わってしまい、テストの信頼性に問題がありました。この新しい構造により、**外部の状態に依存しない安定したテスト実行**が可能になりました。

## テストカテゴリ

### 1. ユニットテスト (`[Fact]`)
- 外部依存なし
- モック・スタブ使用
- 高速実行
- 常に実行される

### 2. 統合テスト (`[IntegrationTest]`)
- モック・スタブ使用の統合テスト
- サーバー不要
- 中程度の実行速度

### 3. 内蔵サーバーテスト (`[EmbeddedServerTest]`)
- `TestServerManager`を使用した内蔵サーバー
- 実際のHTTP通信
- 環境依存なし
- **推奨アプローチ** ✅

### 4. 外部サーバーテスト (`[ExternalServerRequiredTest]`)
- 外部で起動されたサーバーが必要
- 環境依存
- **非推奨** (レガシー・デバッグ用途のみ) ⚠️

## 実行方法

### 全テスト実行
```bash
dotnet test
```

### カテゴリ別実行

#### 内蔵サーバーテストのみ実行
```bash
dotnet test --filter "FullyQualifiedName~WithEmbeddedServer"
```

#### 外部サーバー不要テストのみ実行
```bash
dotnet test --filter "FullyQualifiedName~WithoutServer"
```

#### 内蔵サーバーテストをスキップ
```bash
set SKIP_EMBEDDED_SERVER_TESTS=true
dotnet test
```

#### 外部サーバーテストをスキップ (デフォルト)
```bash
set SKIP_EXTERNAL_SERVER_TESTS=true
dotnet test
```

## テスト設計原則

### ✅ 推奨パターン

1. **内蔵サーバーテストの使用**
```csharp
[EmbeddedServerTest]
public async Task TestName_WithEmbeddedServer()
{
    using var serverManager = new TestServerManager();
    serverManager.StartServer();

    // サーバーの健全性確認
    var isHealthy = await serverManager.IsServerAvailableAsync();
    Assert.True(isHealthy);

    // テストロジック
    var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);
    var result = await client.ConnectAsync(serverManager.ServerUrl);

    // 結果の確認（接続失敗も想定内の場合あり）
    Console.WriteLine($"Connection result: {result}");
}
```

2. **サーバー不要テストの分離**
```csharp
[Fact]
public async Task TestName_WithoutServer()
{
    var client = BattleClientFactory.Create(ConnectionType.SignalR, _loggerFactory);
    var result = await client.ConnectAsync("http://localhost:9999"); // 未使用ポート

    Assert.False(result); // 接続失敗を期待
}
```

### ❌ 避けるべきパターン

1. **外部サーバー依存テスト**
```csharp
// ❌ 外部環境に依存
[Fact]
public async Task TestName_RequiresExternalServer()
{
    var result = await client.ConnectAsync("https://localhost:5001");
    Assert.True(result); // 外部サーバーの状態次第で失敗
}
```

## 並列実行制御

内蔵サーバーテストは`[Collection("EmbeddedServerTests")]`により順次実行されます：

```csharp
[Collection("EmbeddedServerTests")]
public class ClientIntegrationTests : IDisposable
{
    // 内蔵サーバーテストは順次実行される
}
```

## トラブルシューティング

### ポート競合エラー
- 内蔵サーバーは動的ポート (`http://127.0.0.1:0`) を使用
- 並列実行は無効化済み

### テスト実行が遅い
- 環境変数で不要なテストカテゴリをスキップ
- 内蔵サーバーテストのみ実行を検討

### 外部サーバーテストの実行
```bash
# 外部サーバーを起動
dotnet run --project csharp/src/InMemoryServer

# 外部サーバーテストを有効化
set SKIP_EXTERNAL_SERVER_TESTS=false
dotnet test --filter "ExternalServerRequiredTest"
```

## 実装詳細

### TestServerManager
- `TestWebApplicationFactory<InMemoryServer.Program>`を使用
- 動的ポート割り当てでポート競合を回避
- ヘルスチェック機能付き
- 自動的なリソース管理（IDisposable）

### テストの並列実行制御
- 内蔵サーバーテストは順次実行（`DisableParallelization = true`）
- ユニットテストは並列実行可能
- 外部サーバーテストは個別に制御可能

## 今後の方針

1. **新しいテストは内蔵サーバーテストを使用** ✅
2. **外部サーバーテストは段階的に内蔵サーバーテストに移行** 🔄
3. **バトル機能の完全なE2Eテストを内蔵サーバーで実装** 🎯

## 成果

✅ **外部状態に依存しない安定したテスト実行**
✅ **開発者の環境に関係なく一貫したテスト結果**
✅ **CIパイプラインでの確実なテスト実行**
✅ **テストの信頼性向上**
