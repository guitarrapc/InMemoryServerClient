# WasmClient Technical Specification

## Overview

WasmClientは、InMemoryServerとの接続にSignalR/MagicOnionを使用するWebAssemblyベースのバトルクライアントです。ブラウザ上で複数の接続プロトコルを統一的に扱い、リアルタイムバトルの可視化とバトル履歴の永続化を提供します。

CliClientと比較して、以下の利点があります：

- **Visual Representation**: リアルタイムでバトルの進行状況を視覚的に確認
- **Multiple Battles**: 複数のバトルセッションを同時実行・監視
- **Interactive Controls**: マウスホバーでの詳細表示、リプレイの一時停止・再生制御
- **No Installation Required**: ブラウザ上で動作するため配布・実行が容易
- **Persistent Battle History**: IndexedDBによるブラウザリロード後もアクセス可能なバトル履歴
- **Battle History Management**: 過去のバトルの詳細確認、削除、統計情報表示機能
- **Historical Battle Replay**: 保存されたバトル履歴の完全なリプレイ再生機能
- **Advanced Replay Controls**: 再生速度調整、フレーム単位のシーク、一時停止・再開機能

### Design Goals

- **Protocol Unification**: SignalRとMagicOnionの統一インターフェースによる透明な接続管理
- **Multi-Battle Support**: 複数バトルの同時実行と管理
- **Battle History**: ブラウザリロード後も利用可能な永続的なバトル履歴
- **Real-time Visualization**: リアルタイムバトル進行の可視化とリプレイ機能

## Architecture

### Component Structure

```
WasmClient/
├── Pages/
│   ├── Home.razor                  # バトル一覧・履歴管理画面
│   ├── BattleDetail.razor          # バトル詳細・フィールド表示
│   └── Options.razor              # 設定・データ管理画面
├── Components/
│   ├── BattleField.razor          # 個別バトルフィールド可視化
│   └── BattleHistoryCard.razor    # バトル履歴カード表示
├── Services/
│   ├── BattleSessionManager.cs    # バトルセッション統合管理
│   ├── BattleHistoryService.cs    # IndexedDBバトル履歴管理
│   ├── IConnectionFactory.cs      # 接続ファクトリーインターフェース
│   ├── ConnectionFactory.cs       # 接続ファクトリー実装
│   ├── IBattleConnection.cs       # 統一接続インターフェース
│   ├── SignalRConnection.cs       # SignalR接続実装
│   └── MagicOnionConnection.cs    # MagicOnion接続実装
├── Models/
│   ├── BattleSessionModel.cs      # バトルセッション・クライアントモデル
│   ├── BattleHistoryModel.cs      # バトル履歴データモデル
│   └── ConnectionInfo.cs          # 接続情報モデル
└── wwwroot/
    └── js/
        └── battleStorage.js       # IndexedDB操作JavaScript
```

## Core Design Principles

### 1. Protocol Abstraction Layer

**Background**: SignalRとMagicOnionは異なるAPIを持つため、統一的な接続管理が必要

**Design Intent**: `IBattleConnection`インターフェースにより、接続プロトコルに依存しないバトル管理を実現

**Constraints**:
- 各プロトコルの特性を抽象化しつつ、パフォーマンスを損なわない設計
- 接続エラーやタイムアウトの統一的なハンドリング

```csharp
// 統一インターフェースの例
public interface IBattleConnection : IAsyncDisposable
{
    string ConnectionId { get; }
    ConnectionType Type { get; }
    bool IsConnected { get; }

    event Action<BattleReplayData>? OnBattleReplayReceived;
    event Action<ConnectionsReadyData>? OnConnectionsReady;
    event Action<BattleStartedData>? OnBattleStarted;
    event Action<string>? OnBattleComplete;
}
```

### 2. Unified Battle and History Model

**Background**: リアルタイムバトルと履歴バトルで異なる表示ロジックが必要だが、UIの統一性は維持したい

**Design Intent**: `BattleSessionModel`で履歴・リアルタイム両方を扱い、`IsHistoricalMode`フラグで表示制御

**Considerations**:
- 履歴バトルでは接続管理が不要だが、表示形式は統一
- 履歴データから擬似的なクライアント情報を生成し、元の接続タイプを再現
- メモリ効率を考慮した履歴データの遅延読み込み

```csharp
// 統一バトルモデルの例
public class BattleSessionModel
{
    public bool IsHistoricalBattle { get; init; } = false;
    public BattleHistory? BattleHistory { get; init; }

    // リアルタイム・履歴両対応のファクトリーメソッド
    public static BattleSessionModel CreateHistorical(
        string battleId, string groupName, string serverUrl,
        List<BattleClient> clients, List<BattleReplayData> replayData);
}
```

## Data Persistence Strategy

### Battle History Management

**Background**: WebAssemblyアプリケーションはブラウザリロードで状態が失われるため、バトル結果の永続化が必要

**Design Intent**: IndexedDBによるクライアントサイドデータ永続化で、ブラウザリロード後もバトル履歴を保持

**Implementation Approach**:
- C#側の`BattleHistoryService`とJavaScript側の`battleStorage.js`でIndexedDB操作を分離
- バトル完了時の自動保存とオンデマンドでの履歴読み込み
- 軽量なサマリーデータと完全なリプレイデータの分離保存

**Data Size Considerations**:
- バトルリプレイデータは大容量になるため、チャンク分割とインデックス最適化
- サマリー一覧表示時は軽量データのみ読み込み、詳細表示時に完全データを読み込み
- ストレージ容量管理とクリーンアップ機能の提供

### JavaScript IndexedDB Interface

```javascript
// IndexedDB操作の抽象化例
window.battleStorage = {
    async saveBattle(battleHistoryData) {
        // データサイズ計算と保存処理
        const dataSize = new Blob([JSON.stringify(battleHistoryData)]).size;
        battleHistoryData.dataSizeBytes = dataSize;
        // IndexedDB保存実装
    },

    async getBattleList(limit = 50) {
        // 軽量なサマリー情報のみ取得
        // 新しい順でページネーション対応
    }
};
```

## Battle Field Visualization

### Real-time Field Rendering

**Background**: バトル進行をユーザーが理解しやすい形で可視化する必要

**Design Intent**: 20x20座標系をスケーラブルなピクセル表示に変換し、エンティティの動きと状態変化を直感的に表示

**Implementation Strategy**:
- フィールドサイズに応じた座標スケーリング（デフォルト200px四方）
- エンティティタイプ別の視覚的区別（プレイヤー・敵サイズ別）
- ツールチップによる詳細情報表示（HP、位置、ID等）

```csharp
// フィールド座標変換の例
private double scaleX => (FieldSize - 4) / 20.0; // 20x20座標系をピクセルにスケール
private int entitySize => Math.Max(4, (int)(scaleX * 0.8)); // 最小4px、スケールに応じたサイズ
```

### Historical Replay System

**Background**: 保存されたバトル履歴を再生可能な形で提供する必要

**Design Intent**: 履歴データから時系列再生を可能にし、任意の時点での状態確認を実現

**Replay Features**:
- 再生・一時停止・フレーム単位のシーク操作
- 可変速度再生（0.25x - 2.0x）
- タイムライン表示と任意時点ジャンプ
- 履歴モードでのコントロール無効化

**Data Flow Considerations**:
```csharp
// 履歴クライアントの作成例
public static BattleClient CreateHistoricalClient(
    string playerId, string groupName,
    List<BattleReplayData> replayData,
    ConnectionType connectionType = ConnectionType.SignalR)
{
    // 接続タイプを保持して元のバトル構成を再現
    var client = new BattleClient(null!, null!)
    {
        HistoricalConnectionType = connectionType
    };
    // 全ターンデータを事前変換・保存
}
```

## User Interface Architecture

### Progressive Enhancement Approach

**Background**: 初回訪問者から継続利用者まで、段階的な機能提供が必要

**Design Intent**:
1. **Immediate Access**: 設定不要でデフォルトサーバーに接続可能
2. **Battle Management**: 複数バトルの同時管理とクライアント追加・削除
3. **History Integration**: リアルタイムと履歴の統一インターフェース
4. **Advanced Configuration**: サーバーURL設定とデータ管理

### State Management Strategy

**Loading State Granularity**:
- 全体ローディング: 初回バトル作成、履歴バトル読み込み時
- 部分ローディング: 個別クライアント追加時（SignalR/MagicOnion別）
- フィールド状態: リアルタイムデータ受信による逐次更新

```csharp
// 個別クライアント追加時のローディング状態例
private bool isAddingSignalR;
private bool isAddingMagicOnion;

private async Task AddClient(ConnectionType type)
{
    // 該当する接続タイプのみローディング状態に設定
    if (type == ConnectionType.SignalR)
        isAddingSignalR = true;
    // 全体ローディングではなく、個別制御
}
```

### Error Handling and User Feedback

**Connection Error Scenarios**:
- サーバー未起動: 明確なエラーメッセージと再試行オプション
- ネットワーク切断: 自動再接続試行とユーザー通知
- プロトコル不一致: 接続タイプ別のエラー情報提供

**Data Persistence Error Handling**:
- IndexedDB操作失敗: 代替手段の提示とエラー詳細
- ストレージ容量不足: クリーンアップ機能の案内
- データ破損: 復旧可能性の確認と安全な削除オプション

## Performance Considerations

### Memory Management

**Background**: WebAssemblyの制約下で大容量のバトルリプレイデータを効率的に扱う必要

**Optimization Strategies**:
- **Lazy Loading**: サマリー表示時は軽量データのみ、詳細表示時に完全データ読み込み
- **Data Streaming**: 大きなリプレイデータのチャンク分割処理
- **Component Disposal**: 画面遷移時の適切なリソース解放

```csharp
// リソース解放の例
public void Dispose()
{
    if (battle != null)
    {
        battle.OnStatusChanged -= OnBattleStatusChanged;
    }
    // イベントハンドラーの確実な解放
}
```

### Network Efficiency

**Connection Pooling**: 複数クライアント間でのコネクション効率化
**Data Compression**: 大容量リプレイデータの圧縮転送（将来実装）
**Caching Strategy**: IndexedDBを活用した適切なキャッシュ戦略

## Deployment and Configuration

### Build Configuration

```xml
<!-- 実装例：WasmClient.csproj -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Shared\Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
    <PackageReference Include="MagicOnion.Client" />
    <PackageReference Include="Grpc.Net.Client.Web" />
  </ItemGroup>
</Project>
```

### Service Registration

```csharp
// 実装例：Program.cs でのDI設定
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSingleton<IConnectionFactory, ConnectionFactory>();
builder.Services.AddSingleton<BattleSessionManager>();
builder.Services.AddSingleton<BattleHistoryService>();
builder.Services.AddSingleton<SettingsService>();

// ログ設定
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
});
```

## Future Enhancements

**フィールド表示**
- 接続されたクライアントごとに200px四方のフィールドを表示
- フィールドは格子状に配置し、最大5つまで表示
- 各フィールドには接続情報（クライアントID、接続方式）を表示

**バトル進行**
- クライアントが5つ揃うと自動的にバトル開始
- リアルタイムでフィールド座標とエンティティ位置を更新
- バトル完了後、「削除」ボタンでクライアントを個別削除可能

#### 3. バトルフィールドの表示仕様

**フィールドサイズ**
- 各クライアントフィールド: 200px × 200px（20×20のゲーム座標を200px四方にスケール: 1座標 = 10px）

**エンティティ表示**
- プレイヤー: 青色の円 (直径8px)
- 敵: 赤色の円 (サイズは敵タイプにより可変)
- HP情報はマウスホバーで表示

**更新頻度**
- バトル進行中は200ms間隔で座標更新 (CliClientのBattleReplayDefines.ReplayFrameTimeMs)
- フレーム落ちを防ぐためRequestAnimationFrameを使用

### Component Hierarchy

```razor
<App>
  <Router>
    <RouteView RouteData="routeData" DefaultLayout="MainLayout">
      <!-- Home Page -->
      <BattleListPage>
        <BattleCard />
        <CreateBattleButton />
        <OptionsLink />
      </BattleListPage>

      <!-- Battle Detail Page -->
      <BattleDetailPage>
        <ClientList>
          <ClientCard />
        </ClientList>
        <AddClientButtons>
          <AddSignalRButton />
          <AddMagicOnionButton />
        </AddClientButtons>
        <BattleFieldGrid>
          <BattleField /> <!-- 200px × 200px -->
        </BattleFieldGrid>
      </BattleDetailPage>

      <!-- Options Page -->
      <OptionsPage>
        <ServerUrlSettings />
      </OptionsPage>
    </RouteView>
  </Router>
</App>
```

## Advantages over CliClient

1. **Visual Representation**: リアルタイムでバトルの進行状況を視覚的に確認
2. **Multiple Battles**: 複数のバトルセッションを同時実行・監視
3. **Interactive Controls**: マウスホバーでの詳細表示、リプレイの一時停止・再生制御
4. **No Installation Required**: ブラウザ上で動作するため配布・実行が容易
5. **Cross-Platform**: Windows, Mac, Linuxのブラウザで実行可能
6. **Real-time Updates**: WebAssemblyの高いパフォーマンスでリアルタイム更新
7. **Intuitive UI**: GUIベースの直感的な操作インターフェース
8. **Real-time Field Visualization**: 200px四方のフィールドで複数バトルを同時監視
9. **Persistent Battle History**: IndexedDBによるブラウザリロード後もアクセス可能なバトル履歴
10. **Battle History Management**: 過去のバトルの詳細確認、削除、統計情報表示機能
11. **Historical Battle Replay**: 保存されたバトル履歴の完全なリプレイ再生機能
12. **Advanced Replay Controls**: 再生速度調整、フレーム単位のシーク、一時停止・再開機能

## Implementation Notes

- CliClientのConstants（BattleReplayDefines等）を共有して一貫性を保つ
- SignalR接続はWebSocketsを、MagicOnion接続はgRPCを使用
- リプレイデータの蓄積と再生にはCliClientと同じフレームレート（5fps）を使用
- 接続エラー処理とリトライロジックをCliClientから移植
- IndexedDBを使用したバトル履歴の永続化により、ブラウザリロード後もデータアクセス可能
- JSInteropを使用してC#からIndexedDB操作を行い、型安全性を維持
- 350KB程度/バトルの大容量データを効率的に管理するためのチャンク処理
- バトル履歴の削除・統計表示機能により、ストレージ容量の管理が可能
- 履歴バトルのリプレイ機能では、読み取り専用モードで完全な戦闘再現が可能
- リプレイコントロールによる柔軟な再生操作（再生速度調整、フレーム単位の制御）
- 動的プロパティアクセスによる型安全でない部分の最小化と例外処理の充実
