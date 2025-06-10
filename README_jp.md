# InMemoryServerClient

*[English version](README.md)*

C#で実装されたインメモリステートフルサーバーとCLIクライアントのプロジェクトです。サーバーはメモリ内に状態を保持し、クライアントがこの状態と対話するためのインターフェースを提供します。

## 機能

### サーバー機能
- **基本的なキーバリューストア操作**
  - GET/SET/DELETE/LIST操作
  - キーの変更監視機能
- **グループ管理**
  - UUIDv4で識別されるグループの作成と管理
  - グループごとの最大接続数制限（最大5セッション）
  - グループの自動有効期限管理（10分）
- **バトルシステム**
  - グループが満員（5セッション）になった時の自動バトル開始
  - 20x20サイズの疑似フィールドでのターン制RPG風バトル
  - 完全オートバトルシステム
  - バトルリプレイのJSON LINE形式での保存

### クライアント機能
- **インタラクティブモード**：対話型コマンドライン
- **バッチモード**：単発コマンド実行
- **グループ操作**：グループへの参加、メッセージ送信
- **バトル監視**：リアルタイムバトル状況表示

## アーキテクチャ

### 技術スタック
- **.NET 9**: 最新の.NET Runtime
- **SignalR**: リアルタイム通信
- **xUnit + NSubstitute**: テストフレームワーク
- **ConsoleAppFramework**: CLIフレームワーク

### プロジェクト構造
```
csharp/
├── src/
│   ├── InMemoryServer/     # サーバー実装
│   ├── CliClient/          # CLIクライアント
│   ├── Shared/             # 共有ライブラリ
│   └── Tests/              # テストプロジェクト
├── Dockerfile              # サーバーコンテナ化
└── Directory.Build.props   # ビルド設定
```

## 始め方

### 前提条件
- .NET 9 SDK
- Docker（コンテナ実行の場合）

### ビルド
```bash
cd csharp
dotnet build
```

### テスト実行
```bash
cd csharp
dotnet test
```

### サーバー起動

#### ローカル実行
```bash
cd csharp/src/InMemoryServer
dotnet run
```

#### Docker実行
```bash
cd csharp
docker build -t inmemory-server .
docker run -p 5000:5000 inmemory-server
```

### クライアント使用方法

#### インタラクティブモード
```bash
cd csharp/src/CliClient
dotnet run
```

#### 単発コマンド例
```bash
# サーバーに接続
dotnet run -- connect -u http://localhost:5000

# キーバリュー操作
dotnet run -- set mykey "Hello World"
dotnet run -- get mykey
dotnet run -- delete mykey
dotnet run -- list "*"

# グループ操作
dotnet run -- join mygroup
dotnet run -- broadcast "Hello everyone!"
dotnet run -- groups
dotnet run -- my-group

# バトル機能
dotnet run -- battle-status
```

#### インタラクティブモードコマンド
```
connect [url] [group]  - サーバーに接続
disconnect             - サーバーから切断
status                 - 接続状態表示
get <key>              - キー取得
set <key> <value>      - キー設定
delete <key>           - キー削除
list [pattern]         - キー一覧（パターン指定可能）
watch <key>            - キー変更監視
join <group_name>      - グループ参加
broadcast <message>    - グループ内メッセージ送信
groups                 - グループ一覧
mygroup                - 現在のグループ情報
battle-status          - バトル状況確認
exit, quit             - 終了
help                   - ヘルプ表示
```

## バトルシステム

### バトル開始条件
- グループに5人のクライアントが接続した時に自動開始

### バトルの特徴
- **フィールド**: 20x20のグリッド
- **エンティティ**: プレイヤー（HP200固定）と敵（小型:HP100、中型:HP200、大型:HP300）
- **ステータス**: 攻撃力(10-30)、防御力(5-15)、移動速度(1-3)はランダム生成
- **行動**: 移動、攻撃、防御の3種類
- **ターン制**: 移動速度順で行動
- **期間**: 100-300ターンで完了

### バトルリプレイ
- `./battle_replay/` ディレクトリにJSON LINE形式で保存
- 各ターンの状態が記録される

## 設定

### サーバー設定
- **ポート**: 5000 (デフォルト)
- **SignalRエンドポイント**: `/inmemoryhub`
- **ヘルスチェック**: `/health`

### 環境変数対応
- `ASPNETCORE_URLS`: サーバーURL設定
- `Logging__LogLevel__Default`: ログレベル設定

## 開発情報

### コーディング規約
- TreatWarningsAsErrors が有効
- Nullable Reference Types が有効
- すべての公開APIにXMLドキュメントコメント

### テストカバレッジ
- InMemoryState: 基本操作テスト
- GroupManager: グループ管理テスト
- BattleState: バトルロジックテスト

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 貢献

1. フォークしてください
2. フィーチャーブランチを作成してください (`git checkout -b feature/amazing-feature`)
3. 変更をコミットしてください (`git commit -m 'Add some amazing feature'`)
4. ブランチにプッシュしてください (`git push origin feature/amazing-feature`)
5. プルリクエストを開いてください

## 今後の拡張予定

- JWT認証の実装
- gRPC（MagicOnion）サポート
- Go言語での実装
- Webベースのクライアント
- より複雑なバトルシステム
