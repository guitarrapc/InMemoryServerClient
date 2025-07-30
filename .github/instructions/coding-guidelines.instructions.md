---
applyTo: "**"
---

# コーディングガイドライン

## C#実装

C#の基礎的なルールは次の通り

- あなたはC#のプロフェッショナルです。
- C#のコーディング規約とベストプラクティスに従う
- コードの変更を提示する場合、既存のコードの依存関係を十分に調査してから変更を提案、実装計画を立てます。
- すべてのI/O操作にasync/awaitを使用
- 適切な例外処理を実装する
- 適切な場所で依存性注入を使用する
- SOLIDの原則に従う
- 公開APIにはXMLドキュメントコメントを含める
- 変更後にビルドを実行して、コード品質を確保する。`dotnet build`を使用してソリューションをコンパイルする
- サーバーとクライアントの両方のコンポーネントに対するユニットテストを作成する。コードの変更を行ったら、`dotnet test`を使用してテストを実行する
- コードの変更を行ったら、`dotnet format`を使用してコードスタイルを自動的に整形する
- .NET 9以上
- C# 13以上
- TreatWarningsAsErrorsを有効にして、警告をエラーとして扱います。すべての警告を解決することを目指します。
- Top Level Statementsを使用して、エントリポイントを簡潔に保ちます。

## 現代的なC#要件（常に適用）

型安全性とパフォーマンスを最大化するため、以下の要件を常に適用する：

### 1. 型安全性の徹底
- **`dynamic`の使用を禁止**：実行時エラーのリスクがあるため、`dynamic`は使用しない
- **`object`の不適切な使用を避ける**：型安全でない汎用的な`object`の使用を最小限に抑制
  - lockにはC#13で追加された`Lock`型を用いる
- **ジェネリクスを積極活用**：型パラメーターとして`<T>`を使用し、コンパイル時の型チェックを活用

### 2. アロケーション効率の最適化
- **値型（構造体）の活用**：小さなデータは`readonly struct`や`readonly record struct`を使用
- **`in`パラメーター**：大きな構造体を引数で渡す際は`in`修飾子を使用
- **スタック領域の活用**：`stackalloc`、`Span<T>`、`Memory<T>`を適切に使用
- **不必要なボクシングを避ける**：値型から参照型への暗黙的な変換を避ける

### 3. パフォーマンス重視の実装パターン
- **構造体ベースのメッセージ**：ログやデータ転送では`IFormattable`を実装した構造体を使用
- **ジェネリック制約の活用**：`where T : struct, IFormattable`などの制約で型安全性を確保
- **メモリ効率的なコレクション**：初期容量を指定し、適切なコレクション型を選択

### 4. 実装例
```csharp
// ✅ 推奨：型安全な構造体ベースのログメッセージ
public readonly struct PlayerInfo : IFormattable
{
    public string PlayerId { get; }
    public int Health { get; }

    public PlayerInfo(string playerId, int health)
    {
        PlayerId = playerId;
        Health = health;
    }

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Player {PlayerId}: HP {Health}";
}

// ✅ 推奨：ジェネリクスを使用した型安全な拡張メソッド
public static void LogInfo<T>(this ILogger logger, in T data)
    where T : struct, IFormattable
{
    logger.LogInformation("{Message}", data.ToString(null, null));
}

// ❌ 禁止：dynamicの使用
public void ProcessData(dynamic data) // 禁止
{
    // 実行時エラーのリスク
}

// ❌ 禁止：object[]を多用したパラメーター
public void LogMessage(string template, params object[] args) // 避ける
{
    // 型安全性とパフォーマンスの問題
}
```

この要件により、型安全でパフォーマンスに優れたC#コードを実現する。

パフォーマンスの最大化に注意します。

- 短寿命なオブジェクトにはstructをはじめとしてスタック領域を用いることができるか検討します。例えば`readonly ref struct`や`readonly struct`はパフォーマンス向上に寄与します。Mutable Structは意識的に避けます。stackallocを使用して、短寿命な配列をスタック上に割り当てることも検討します。
- 非同期メソッドは`async`/`await`を使用して、I/Oバウンド操作のパフォーマンスを向上させます。生`Task`メソッドを避けて、意識的に`async/await`パターンを使用します。
- 並列アクセスがあるコレクションは、スレッドセーフなコレクションを使用します。例えば、`ConcurrentDictionary<TKey, TValue>`や`ConcurrentBag<T>`などを利用します。

C#ライブラリとコーディングパターンに注意します。

- サーバーはMinimal APIを使用して実装します。
- 設定管理にはIConfiguration/IOptionsパターンを使用し、appsettings.jsonと環境変数の両方をサポート
- CLIパーシングにConsoleAppFrameworkを用います。使い方は[こちら](https://github.com/Cysharp/ConsoleAppFramework)を参照します。
- JWT認証用に`System.IdentityModel.Tokens.Jwt`を用います。
- 通信フレームワークはHTTP/1とHTTP/2それぞれに別のライブラリを使用します。
  - HTTP/1通信用に[SignalR](https://github.com/SignalR/SignalR)を用います。SignalRはWebSocket/SSE/LongPolingの上に構築されたリアルタイム通信フレームワークで、簡単に双方向通信を実現できます。
  - HTTP/2通信用に[MagicOnion](https://github.com/cysharp/MagicOnion)を用います。MagicOnionはgRPCの上に構築されたC#向けのフレームワークで、シンプルなAPI設計と高いパフォーマンスを提供します。
  - それぞれの通信フレームワークの処理は、ディレクトリ構造に分けて実装します。
- ユニットテストに`xunit.v3`(xUnitのバージョン3)、モックに`NSubstirute`を用います。xunit.v3の使い方は[こちら](https://xunit.net/docs/getting-started/v3/whats-new)を参照します。

## Go実装（将来）
- Go 1.21以上
- CLIパーシング用のCobra
- 設定管理にはViperライブラリを使用（YAML/JSON設定ファイルと環境変数の両方をサポート）
- JWT認証用のjwt-go
- WebSocket通信用のgorilla/websocket
- gRPC用の標準ライブラリとprotobuf

# 開発ガイドライン
- クリーンで読みやすく保守しやすいコードを書く
- 適切なコメントを含める
- インメモリ状態のパフォーマンスへの影響を考慮する
- 拡張性を考慮した設計
- 適切なロギングを実装する
- サーバーのスレッドセーフティを考慮する
- **アクセシビリティガイドライン**：
  - アセンブリ内でのみ使用されるサービスクラスは`internal`アクセシビリティを使用し、外部アセンブリから隠蔽する
  - 複雑なビジネスロジックやアルゴリズムを含むクラスは、特定のドメイン内で使用されていてもServicesに配置する
  - APIサーフェスの最小化により適切なカプセル化を実現し、実装詳細を隠蔽する
  - リファクタリング時の影響範囲を制限するため、不要な公開を避ける
- エラー処理のガイドライン：
  - 意味のある例外クラスを定義する
  - 例外メッセージは具体的かつ有用な情報を含める
  - クライアントに返すエラーは適切に抽象化する
  - ログには詳細なエラー情報を記録する

# テスト
- サーバーコマンドハンドラーのユニットテスト
- クライアントコマンドパーシングのユニットテスト
- サーバー-クライアント通信の統合テスト
- 同時接続のロードテスト（オプション）

コードを生成する際は、サーバーとクライアントの両方のコンポーネントが構造化され、最新のC#プラクティスに従い、適切なエラー処理とドキュメントを含むようにしてください。
