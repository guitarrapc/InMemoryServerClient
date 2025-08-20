[![Build](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/build.yaml)
[![Release](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/release.yaml/badge.svg)](https://github.com/guitarrapc/InMemoryServerClient/actions/workflows/release.yaml)

[![Docker Pulls](https://img.shields.io/docker/pulls/guitarrapc/inmemoryserverclient.svg?maxAge=3600)](https://hub.docker.com/r/guitarrapc/inmemoryserverclient/)
![Static Badge](https://img.shields.io/badge/ghcr.io-inmemoryserverclient?style=flat&logo=github&logoColor=white&color=2088FF&link=https%3A%2F%2Fgithub.com%2Fguitarrapc%2FInMemoryServerClient%2Fpkgs%2Fcontainer%2Finmemoryserverclient)

# InMemoryServerClient

*[English version](README.md)*

C#で実装されたインメモリステートフルサーバーとCLIクライアントのプロジェクトです。サーバーはメモリ内に状態を保持し、クライアントがこの状態と対話するためのインターフェースを提供します。このシステムはリアルタイム通信、グループ管理、リプレイ機能を備えた自動バトルシステムをサポートしています。

## 機能

### サーバー機能
- **基本的なキーバリューストア操作**
  - GET/SET/DELETE/LIST操作
  - キーの変更監視機能
- **グループ管理**
  - UUIDv4で識別されるグループの作成と管理
  - クライアント指定または自動割り当てのグループ名
  - グループごとの最大接続数制限（最大5セッション）
  - グループの自動有効期限管理（10分）
- **バトルシステム**
  - グループが満員（5セッション）になった時の自動バトル開始
  - 20x20サイズの疑似フィールドでのターン制RPG風バトル
  - 効率的な処理のための事前計算バトルシミュレーション
  - バトル行動：移動、攻撃、防御
  - 勝利条件：すべての敵を倒す
  - バトルリプレイのJSON LINE形式での保存
  - チャンクデータ送信によるメモリ最適化実装

### クライアント機能
- **インタラクティブモード**：リアルタイム応答を備えた対話型コマンドライン
- **バッチモード**：自動化のための単発コマンド実行
- **接続管理**：バトルテスト用の複数セッション接続
- **グループ操作**：グループへの参加、メッセージ送信、グループ状態確認
- **バトル視覚化**：バトル状況表示と5fpsでのバトルリプレイ
- **サーバーステータス**：サーバー統計とリソース使用状況の監視

## アーキテクチャ

### 技術スタック
- **.NET 9**: 最新のC#機能を活用した.NET Runtime
- **SignalR**: 双方向リアルタイム通信
- **Minimal API**: 軽量サーバー実装
- **xUnit + NSubstitute**: 包括的テストフレームワーク
- **ConsoleAppFramework**: クライアントコマンド用の強力なCLIフレームワーク

### 設計方針
- **用途別GUID生成**: バトル再現性とシステム管理の両立
  - エンティティID: 決定論的GUID v4（`BattleSeed.NextEntityId()`）
  - システムID: タイムスタンプベースGUID v7（`BattleSeed.NewTimestampId()`）
- **メモリ効率最適化**: 構造体活用、チャンク送信、適切なクリーンアップ
- **決定論的バトル**: 同一シードでの完全再現可能なバトルシステム

### プロジェクト構造
```
csharp/
├── src/
│   ├── BattleLogic/          # サーバー実装
│   ├── CliClient/            # CLIクライアント
│   ├── InMemoryServer/       # サーバー実装
│   └── Shared/               # 共有ライブラリ
├── tests/
│   ├── BattleLogic.Tests/    # バトルロジックのユニットテスト
│   ├── CliClient.Tests/      # CLIクライアントのユニットテスト
│   ├── E2E.Tests/            # エンドツーエンドテスト
│   └── InMemoryServer.Tests/ # サーバーのユニットテスト
├── Dockerfile                # サーバーコンテナ化
├── Directory.Build.props     # ビルド設定
└── Directory.Packages.props  # パッケージ管理
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
docker run -p 5001:5001 inmemory-server
```

### クライアント使用方法

#### インタラクティブモード
```bash
cd csharp/src/CliClient
dotnet run
```

#### マルチクライアントバトルテスト
単一コマンドで複数クライアントを使用してバトルをテストするには：
```bash
cd csharp/src/CliClient
dotnet run -- connect-battle -u https://localhost:5001 -g test-battle -c 5
```
これにより、自動バトルを開始するために同じグループに5つのクライアント接続が作成されます。

#### 単発コマンド例
```bash
# バトルテスト用に複数セッションを接続
dotnet run -- connect-battle -u https://localhost:5001 -g battle-group -c 1

# バトルテスト用に単一セッションを接続
dotnet run -- connect-battle -u https://localhost:5001 -g battle-group -c 5
```

#### インタラクティブモードコマンド
```
connect [url] [group]                - Connect to server
connect-battle [url] [group] [count] - Connect multiple clients for battle testing
disconnect                           - Disconnect from server
status                               - Show connection status
get <key>                            - Get key
set <key> <value>                    - Set key
delete <key>                         - Delete key
list [pattern]                       - List keys (pattern optional)
watch <key>                          - Watch key changes
join <group_name>                    - Join group
broadcast <message>                  - Send message to group
groups                               - List groups
mygroup                              - Current group info
battle-status                        - Check battle status
battle-replay <id>                   - Show replay data for a battle
battle-complete                      - Signal replay viewing completion
server-status                        - Show server statistics
exit, quit                           - Exit
help                                 - Show help
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
   > connect https://localhost:5001
   Connected to server: https://localhost:5001

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

8. **グループが5人に達すると、バトルが自動的に開始する：**
   ```
   [BATTLE] ========== Connections Ready! ==========
   [BATTLE] Battle ID: 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
   [BATTLE] Group is full! All clients connected.
   [BATTLE] Confirming connection ready status...
   [BATTLE] ========================================
   ```

9. **バトル中、サーバーはすべてのターンを事前計算し、リプレイデータをチャンクでクライアントに送信します：**
   ```
   [BATTLE] Received replay chunk 1/3 with 50 turns
   [BATTLE] Received replay chunk 2/3 with 50 turns
   [BATTLE] Received replay chunk 3/3 with 45 turns
   [BATTLE] All replay chunks received! Starting replay with 145 turns
   ```

10. **バトルは5fps（1秒間に5フレーム）で再生され、ターンごとの更新が表示されます：**
    ```
    [BATTLE] Turn 1: Player1 moved to (10,16)
    [BATTLE] Turn 1: MediumEnemy3 attacked Player2 for 12 damage
    ...
    ```

11. **リプレイが完了したら、サーバーに通知して最終ステータスを確認します：**
    ```
    > battle-complete
    Battle replay viewing completed, notified server.

    > battle-status
    [BATTLE] ========== Battle Status ==========
    [BATTLE] Battle ID: 87a2d6f1-32e4-4f3d-9c03-52b8a9a5e212
    [BATTLE] Result: Victory! All enemies defeated.
    [BATTLE] ======================================
    ```

12. **自動化のために、connect-battleコマンドを使用して複数のクライアントでテストします：**
    ```bash
    dotnet run -- connect-battle -u https://localhost:5001 -g test-battle -c 5
    ```
    これにより、同じグループに5つのクライアント接続が作成され、バトルが開始され、リプレイが自動的に表示されます。

## 技術的詳細

### メモリ最適化

サーバーはメモリ使用量を最適化するためにいくつかの技術を使用しています：

1. **値型**：エンティティ、位置、その他の小さなデータ構造には不変構造体（`readonly struct`と`readonly record struct`）を使用。

2. **事前割り当て**：再サイズを避けるために予想される容量でコレクションを初期化：
   ```csharp
   private readonly List<EntityInfo> _players = new(5); // プレイヤー最大数で事前割り当て
   private readonly List<EntityInfo> _enemies = new(15); // 敵最大数で事前割り当て
   ```

3. **チャンクデータ送信**：大きなペイロードの送信を避けるために、バトルリプレイデータを管理しやすいチャンク（チャンクあたり50ターン）に分割：
   ```csharp
   public required List<BattleStatus> TurnData { get; set; } = new(50);
   ```

4. **メモリクリーンアップ**：データが不要になった後の明示的なメモリ管理：
   ```csharp
   public void ClearBattleData()
   {
       _allTurnData.Clear();
       _players.Clear();
       _enemies.Clear();
       _battleLogs.Clear();
       // ...
   }
   ```

## ドキュメント

### 設計ガイドライン
- [GUID使用ガイドライン](docs/GUID-Usage-Guidelines.md) - 用途別GUID選択の詳細仕様
- [開発者向けインストラクション](.github/copilot-instructions.md) - プロジェクト全体の開発ルール

### API仕様
プロジェクトのAPIと通信プロトコルの詳細は、各コンポーネントのXMLドキュメントコメントを参照してください。

5. **効率的なフィールド表現**：バトルフィールドグリッドに2次元配列と参照型を効率的に使用。

## ライセンス

このプロジェクトはMITライセンスの下でライセンスされています - 詳細はLICENSE.mdファイルを参照してください。
