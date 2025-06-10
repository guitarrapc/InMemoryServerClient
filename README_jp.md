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

# バトルテスト用に複数セッションを接続
dotnet run -- connect-battle -u http://localhost:5000 -g battle-group -c 5

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
dotnet run -- battle-replay <battle_id>
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
battle-replay <id>     - バトルリプレイデータ表示
exit, quit             - 終了
help                   - ヘルプ表示
```

#### 例：グループセッションワークフロー

典型的なグループセッションワークフローの例を示します：

1. **サーバーを起動する：**
   ```bash
   cd csharp/src/InMemoryServer
   dotnet run
   ```

2. **別々のターミナルで複数のクライアントを起動する：**
   ```bash
   cd csharp/src/CliClient
   dotnet run
   ```

3. **サーバーに接続し、利用可能なグループを確認する：**
   ```
   > connect http://localhost:5000
   Connected to server: http://localhost:5000

   > groups
   Available groups:
     3f7e8d2c-9a6b-4c5d-8e7f-1a2b3c4d5e6f
   ```

4. **既存のグループに参加するか、新しいグループを作成する：**
   ```
   > join my-team
   Joined group: my-team
   ```

5. **現在のグループ情報を確認する：**
   ```
   > mygroup
   Current group: 7b8c9d0e-1f2a-3b4c-5d6e-7f8a9b0c1d2e
   ```

6. **グループ内の全員にメッセージを送信する：**
   ```
   > broadcast バトルの準備はできていますか？
   Message broadcasted: バトルの準備はできていますか？
   ```

7. **他のグループメンバーからのメッセージを受信する：**
   ```
   [GROUP] Message from a4b5c6d7-e8f9-0a1b-2c3d-4e5f6a7b8c9d: 準備OK！
   ```

8. **グループが5人に達すると、バトルが自動的に開始する**

9. **バトル中に現在のバトル状態を確認する：**
   ```
   > battle-status
   [BATTLE] ========== Battle Status ==========
   [BATTLE] Battle ID: 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
   [BATTLE] Turn: 45/231
   [BATTLE] Players alive: 5/5
   ...
   ```

10. **バトル完了後、リプレイを表示する：**
    ```
    > battle-replay 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
    Battle replay for battle 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212:
    ...
    ```

バトル終了後、グループのバトルIDはリセットされ、グループが再び5人に達すると新しいバトルが開始できるようになります。
