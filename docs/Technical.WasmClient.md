# WasmClient Technical Specification

## Overview

WasmClientは、InMemoryServerとの接続にSignalR/MagicOnionを使用するWebAssemblyベースのバトルクライアントです。CliClientの機能をWebブラウザ上で実現し、より優れた可視化と複数バトルの同時実行を提供します。

## Architecture

### Core Components

```
WasmClient/
├── Components/
│   ├── ConnectionManager.razor      # 接続管理UI
│   ├── BattleViewer.razor          # バトル表示UI
│   ├── BattleList.razor            # 複数バトル管理UI
│   └── ReplayPlayer.razor          # リプレイ再生UI
├── Services/
│   ├── IConnectionFactory.cs       # 接続ファクトリー
│   ├── ConnectionFactory.cs        # 接続ファクトリー実装
│   ├── IBattleConnection.cs        # 統一接続インターフェイス
│   ├── SignalRConnection.cs        # SignalR接続実装
│   ├── MagicOnionConnection.cs     # MagicOnion接続実装
│   └── BattleHistoryService.cs     # IndexedDBバトル履歴管理
├── Models/
│   ├── BattleSessionModel.cs       # バトルセッション管理
│   ├── ConnectionInfo.cs           # 接続情報
│   └── BattleHistoryModel.cs       # バトル履歴データモデル
└── Constants/
    └── BattleReplayDefines.cs      # リプレイ定数
```

## IndexedDB Data Persistence

### Battle History Management

ブラウザリロード後もバトル履歴を保持するため、IndexedDBを使用したデータ永続化を実装します。

### Battle History Service

C#側でIndexedDBとのやり取りを管理するサービス。JSInteropを使用してJavaScript側のIndexedDB操作を呼び出します。

```csharp
public class BattleHistoryService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<BattleHistoryService> _logger;

    public BattleHistoryService(IJSRuntime jsRuntime, ILogger<BattleHistoryService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// バトル完了時に履歴を保存
    /// </summary>
    public async Task SaveBattleHistoryAsync(BattleHistory battleHistory)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("battleStorage.saveBattle", battleHistory);
            _logger.LogInformation("Battle history {BattleId} saved to IndexedDB", battleHistory.BattleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save battle history {BattleId}", battleHistory.BattleId);
            throw;
        }
    }

    /// <summary>
    /// 指定したバトルIDの完全な履歴を取得
    /// </summary>
    public async Task<BattleHistory?> GetBattleHistoryAsync(string battleId)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<BattleHistory?>("battleStorage.getBattle", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve battle history {BattleId}", battleId);
            return null;
        }
    }

    /// <summary>
    /// バトル履歴の一覧を取得（新しい順、サマリー情報のみ）
    /// </summary>
    public async Task<List<BattleHistorySummary>> GetBattleHistoryListAsync(int limit = 50)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<List<BattleHistorySummary>>("battleStorage.getBattleList", limit);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve battle history list");
            return new List<BattleHistorySummary>();
        }
    }

    /// <summary>
    /// 指定したバトル履歴を削除
    /// </summary>
    public async Task DeleteBattleHistoryAsync(string battleId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("battleStorage.deleteBattle", battleId);
            _logger.LogInformation("Battle history {BattleId} deleted from IndexedDB", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete battle history {BattleId}", battleId);
            throw;
        }
    }

    /// <summary>
    /// IndexedDBを完全にクリアして再初期化
    /// </summary>
    public async Task ClearAllBattleHistoryAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("battleStorage.clearAllBattles");
            _logger.LogInformation("All battle history cleared from IndexedDB");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear IndexedDB");
            throw;
        }
    }

    /// <summary>
    /// 保存されているバトル数とディスク使用量を取得
    /// </summary>
    public async Task<BattleHistoryStats> GetStorageStatsAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<BattleHistoryStats>("battleStorage.getStorageStats");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve storage stats");
            return new BattleHistoryStats();
        }
    }
}
```

### Battle History Data Models

バトル履歴データを管理するためのモデル定義。

```csharp
public record BattleHistory
{
    public string BattleId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string ServerUrl { get; init; } = string.Empty;
    public int TotalTurns { get; init; }
    public List<BattleReplayData> ReplayData { get; init; } = new();
    public BattleResult Result { get; init; } = new();
    public List<string> ParticipatingClients { get; init; } = new();
    public int DataSizeBytes { get; init; }
}

public record BattleHistorySummary
{
    public string BattleId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string ServerUrl { get; init; } = string.Empty;
    public int TotalTurns { get; init; }
    public bool IsPlayerVictory { get; init; }
    public int DataSizeKB { get; init; }
    public int ClientCount { get; init; }
    public TimeSpan BattleDuration => CompletedAt - CreatedAt;
}

public record BattleResult
{
    public bool IsPlayerVictory { get; init; }
    public int RemainingPlayers { get; init; }
    public int RemainingEnemies { get; init; }
    public string VictoryCondition { get; init; } = string.Empty;
}

public record BattleHistoryStats
{
    public int TotalBattles { get; init; }
    public long TotalSizeBytes { get; init; }
    public DateTime? OldestBattle { get; init; }
    public DateTime? NewestBattle { get; init; }
    public double AverageBattleSizeKB => TotalBattles > 0 ? (TotalSizeBytes / 1024.0) / TotalBattles : 0;
}
```

### JavaScript IndexedDB Implementation

IndexedDBを操作するJavaScript実装（`wwwroot/js/battleStorage.js`）。

```javascript
// バトル履歴管理用IndexedDB操作
window.battleStorage = {
    dbName: 'WasmBattleClientDB',
    dbVersion: 1,
    storeName: 'battleHistory',

    // データベース初期化
    async initDB() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.dbVersion);

            request.onerror = () => {
                console.error('IndexedDB initialization failed:', request.error);
                reject(request.error);
            };

            request.onsuccess = () => {
                console.log('IndexedDB initialized successfully');
                resolve(request.result);
            };

            request.onupgradeneeded = (event) => {
                const db = event.target.result;

                // バトル履歴テーブル作成
                if (!db.objectStoreNames.contains(this.storeName)) {
                    const store = db.createObjectStore(this.storeName, { keyPath: 'battleId' });

                    // インデックス作成
                    store.createIndex('createdAt', 'createdAt', { unique: false });
                    store.createIndex('groupName', 'groupName', { unique: false });
                    store.createIndex('serverUrl', 'serverUrl', { unique: false });
                    store.createIndex('completedAt', 'completedAt', { unique: false });

                    console.log('Battle history object store created with indexes');
                }
            };
        });
    },

    // バトル履歴保存
    async saveBattle(battleHistoryData) {
        const db = await this.initDB();
        const transaction = db.transaction([this.storeName], 'readwrite');
        const store = transaction.objectStore(this.storeName);

        // データサイズを計算
        const serializedData = JSON.stringify(battleHistoryData);
        const dataSize = new Blob([serializedData]).size;
        battleHistoryData.dataSizeBytes = dataSize;

        return new Promise((resolve, reject) => {
            const request = store.put(battleHistoryData);
            request.onsuccess = () => {
                console.log(`Battle ${battleHistoryData.battleId} saved (${(dataSize/1024).toFixed(1)}KB)`);
                resolve();
            };
            request.onerror = () => {
                console.error('Failed to save battle:', request.error);
                reject(request.error);
            };
        });
    },

    // バトル履歴取得
    async getBattle(battleId) {
        const db = await this.initDB();
        const transaction = db.transaction([this.storeName], 'readonly');
        const store = transaction.objectStore(this.storeName);

        return new Promise((resolve, reject) => {
            const request = store.get(battleId);
            request.onsuccess = () => {
                const result = request.result || null;
                if (result) {
                    console.log(`Battle ${battleId} retrieved (${(result.dataSizeBytes/1024).toFixed(1)}KB)`);
                }
                resolve(result);
            };
            request.onerror = () => {
                console.error('Failed to retrieve battle:', request.error);
                reject(request.error);
            };
        });
    },

    // バトル履歴一覧取得（軽量なサマリー情報のみ）
    async getBattleList(limit = 50) {
        const db = await this.initDB();
        const transaction = db.transaction([this.storeName], 'readonly');
        const store = transaction.objectStore(this.storeName);
        const index = store.index('completedAt');

        return new Promise((resolve, reject) => {
            const battles = [];
            const request = index.openCursor(null, 'prev'); // 新しい順

            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor && battles.length < limit) {
                    const battle = cursor.value;

                    // 軽量なサマリーデータのみ抽出
                    battles.push({
                        battleId: battle.battleId,
                        createdAt: battle.createdAt,
                        completedAt: battle.completedAt,
                        groupName: battle.groupName,
                        serverUrl: battle.serverUrl,
                        totalTurns: battle.totalTurns,
                        isPlayerVictory: battle.result?.isPlayerVictory || false,
                        dataSizeKB: Math.round((battle.dataSizeBytes || 0) / 1024),
                        clientCount: battle.participatingClients?.length || 0
                    });

                    cursor.continue();
                } else {
                    console.log(`Retrieved ${battles.length} battle summaries`);
                    resolve(battles);
                }
            };

            request.onerror = () => {
                console.error('Failed to retrieve battle list:', request.error);
                reject(request.error);
            };
        });
    },

    // バトル履歴削除
    async deleteBattle(battleId) {
        const db = await this.initDB();
        const transaction = db.transaction([this.storeName], 'readwrite');
        const store = transaction.objectStore(this.storeName);

        return new Promise((resolve, reject) => {
            const request = store.delete(battleId);
            request.onsuccess = () => {
                console.log(`Battle ${battleId} deleted`);
                resolve();
            };
            request.onerror = () => {
                console.error('Failed to delete battle:', request.error);
                reject(request.error);
            };
        });
    },

    // 全バトル履歴削除
    async clearAllBattles() {
        const db = await this.initDB();
        const transaction = db.transaction([this.storeName], 'readwrite');
        const store = transaction.objectStore(this.storeName);

        return new Promise((resolve, reject) => {
            const request = store.clear();
            request.onsuccess = () => {
                console.log('All battle history cleared');
                resolve();
            };
            request.onerror = () => {
                console.error('Failed to clear battle history:', request.error);
                reject(request.error);
            };
        });
    },

    // ストレージ統計情報取得
    async getStorageStats() {
        const db = await this.initDB();
        const transaction = db.transaction([this.storeName], 'readonly');
        const store = transaction.objectStore(this.storeName);

        return new Promise((resolve, reject) => {
            let totalSize = 0;
            let totalCount = 0;
            let oldestDate = null;
            let newestDate = null;

            const request = store.openCursor();
            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    const battle = cursor.value;
                    totalSize += battle.dataSizeBytes || 0;
                    totalCount++;

                    const completedAt = new Date(battle.completedAt);
                    if (!oldestDate || completedAt < oldestDate) oldestDate = completedAt;
                    if (!newestDate || completedAt > newestDate) newestDate = completedAt;

                    cursor.continue();
                } else {
                    resolve({
                        totalBattles: totalCount,
                        totalSizeBytes: totalSize,
                        oldestBattle: oldestDate?.toISOString(),
                        newestBattle: newestDate?.toISOString()
                    });
                }
            };

            request.onerror = () => {
                console.error('Failed to calculate storage stats:', request.error);
                reject(request.error);
            };
        });
    }
};

// ページロード時に初期化
document.addEventListener('DOMContentLoaded', () => {
    window.battleStorage.initDB().catch(console.error);
});
```

### Connection Factory Pattern

CliClientの`ClientFactory`パターンを踏襲し、実行時に接続方法を選択できる設計とします。

```csharp
public interface IConnectionFactory
{
    Task<IBattleConnection> CreateSignalRConnectionAsync(ConnectionInfo connectionInfo);
    Task<IBattleConnection> CreateMagicOnionConnectionAsync(ConnectionInfo connectionInfo);
}

public class ConnectionInfo
{
    public string ServerUrl { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string? PlayerId { get; set; }
    public ConnectionType Type { get; set; }
}

public enum ConnectionType
{
    SignalR,
    MagicOnion
}
```

### Unified Battle Connection Interface

CliClientの各クライアント実装を参考に、統一されたインターfaceを定義します。

```csharp
public interface IBattleConnection : IAsyncDisposable
{
    string ConnectionId { get; }
    ConnectionType Type { get; }
    ConnectionInfo Info { get; }
    bool IsConnected { get; }

    // Battle operations (from CliClient)
    Task<BattleStatus> GetBattleStatusAsync();
    Task SendBattleCompleteAsync();
    Task StartBattleAsync();

    // Events (from CliClient SignalR/MagicOnion implementations)
    event Action<BattleReplayData> OnBattleReplayReceived;
    event Action<string> OnBattleComplete;
    event Action<Exception> OnConnectionError;
    event Action OnDisconnected;
}
```

## Battle State Management

### Extended Battle Session Manager

複数バトルとクライアントの統合管理を行うサービス。

```csharp
public class BattleSessionManager
{
    private readonly Dictionary<string, Battle> _battles = new();
    private readonly IConnectionFactory _connectionFactory;
    private readonly BattleHistoryService _battleHistory;
    private readonly ILogger<BattleSessionManager> _logger;

    public BattleSessionManager(
        IConnectionFactory connectionFactory,
        BattleHistoryService battleHistory,
        ILogger<BattleSessionManager> logger)
    {
        _connectionFactory = connectionFactory;
        _battleHistory = battleHistory;
        _logger = logger;
    }

    public async Task<Battle> CreateBattleAsync(string groupName, string serverUrl)
    {
        var battle = new Battle(Guid.NewGuid().ToString(), groupName, serverUrl, _connectionFactory);
        battle.OnBattleCompleted += OnBattleCompleted;
        _battles[battle.Id] = battle;

        _logger.LogInformation("Created battle {BattleId} with group {GroupName}", battle.Id, groupName);
        return battle;
    }

    public Battle? GetBattle(string battleId) => _battles.TryGetValue(battleId, out var battle) ? battle : null;

    public async Task RemoveBattleAsync(string battleId)
    {
        if (_battles.TryGetValue(battleId, out var battle))
        {
            battle.OnBattleCompleted -= OnBattleCompleted;
            await battle.DisposeAsync();
            _battles.Remove(battleId);
            _logger.LogInformation("Removed battle {BattleId}", battleId);
        }
    }

    public IReadOnlyList<Battle> ActiveBattles => _battles.Values.ToList();

    /// <summary>
    /// IndexedDBから過去のバトル履歴一覧を取得
    /// </summary>
    public async Task<List<BattleHistorySummary>> GetBattleHistoryAsync(int limit = 50)
    {
        return await _battleHistory.GetBattleHistoryListAsync(limit);
    }

    /// <summary>
    /// 指定したバトル履歴を読み込み専用バトルとして復元
    /// </summary>
    public async Task<Battle?> LoadBattleFromHistoryAsync(string battleId)
    {
        var history = await _battleHistory.GetBattleHistoryAsync(battleId);
        if (history == null)
        {
            _logger.LogWarning("Battle history {BattleId} not found", battleId);
            return null;
        }

        var battle = new Battle(history.BattleId, history.GroupName, history.ServerUrl, _connectionFactory)
        {
            IsHistoricalBattle = true,
            BattleHistory = history
        };

        _battles[battle.Id] = battle;
        _logger.LogInformation("Loaded historical battle {BattleId}", battleId);
        return battle;
    }

    /// <summary>
    /// バトル履歴を削除
    /// </summary>
    public async Task DeleteBattleHistoryAsync(string battleId)
    {
        await _battleHistory.DeleteBattleHistoryAsync(battleId);
        _logger.LogInformation("Deleted battle history {BattleId}", battleId);
    }

    /// <summary>
    /// バトル完了時にIndexedDBに保存
    /// </summary>
    private async void OnBattleCompleted(Battle battle, BattleResult result)
    {
        try
        {
            var history = new BattleHistory
            {
                BattleId = battle.Id,
                CreatedAt = battle.CreatedAt,
                CompletedAt = DateTime.UtcNow,
                GroupName = battle.GroupName,
                ServerUrl = battle.ServerUrl,
                TotalTurns = battle.TotalTurns,
                ReplayData = battle.ReplayData.ToList(),
                Result = result,
                ParticipatingClients = battle.Clients.Select(c => c.ConnectionId).ToList()
            };

            await _battleHistory.SaveBattleHistoryAsync(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save battle history for {BattleId}", battle.Id);
        }
    }
}

public class Battle : IAsyncDisposable
{
    private readonly List<BattleClient> _clients = new();
    private readonly IConnectionFactory _connectionFactory;

    public string Id { get; }
    public string GroupName { get; }
    public string ServerUrl { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public BattleStatus Status { get; private set; } = BattleStatus.Waiting;
    public IReadOnlyList<BattleClient> Clients => _clients.AsReadOnly();

    // 履歴バトル用プロパティ
    public bool IsHistoricalBattle { get; init; } = false;
    public BattleHistory? BattleHistory { get; init; }

    // バトル進行データ
    public List<BattleReplayData> ReplayData { get; } = new();
    public int TotalTurns => ReplayData.LastOrDefault()?.BattleData?.LastOrDefault()?.CurrentTurn ?? 0;

    // イベント
    public event Action<Battle, BattleResult>? OnBattleCompleted;

    public Battle(string id, string groupName, string serverUrl, IConnectionFactory connectionFactory)
    {
        Id = id;
        GroupName = groupName;
        ServerUrl = serverUrl;
        _connectionFactory = connectionFactory;
    }

    public async Task<BattleClient> AddClientAsync(ConnectionInfo connectionInfo)
    {
        if (_clients.Count >= 5)
            throw new InvalidOperationException("Battle is full (max 5 clients)");

        var connection = connectionInfo.Type switch
        {
            ConnectionType.SignalR => await _connectionFactory.CreateSignalRConnectionAsync(connectionInfo),
            ConnectionType.MagicOnion => await _connectionFactory.CreateMagicOnionConnectionAsync(connectionInfo),
            _ => throw new ArgumentException($"Unsupported connection type: {connectionInfo.Type}")
        };

        var client = new BattleClient(connection);
        _clients.Add(client);

        // 5クライアント揃ったらバトル開始
        if (_clients.Count == 5)
        {
            await StartBattleAsync();
        }

        return client;
    }

    public async Task RemoveClientAsync(BattleClient client)
    {
        if (_clients.Remove(client))
        {
            await client.DisposeAsync();
        }
    }

    private async Task StartBattleAsync()
    {
        Status = BattleStatus.InProgress;

        // 全クライアントにバトル開始通知
        var tasks = _clients.Select(c => c.Connection.StartBattleAsync());
        await Task.WhenAll(tasks);
    }

    public async ValueTask DisposeAsync()
    {
        var disposeTasks = _clients.Select(c => c.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);
        _clients.Clear();
    }
}

public class BattleClient : IAsyncDisposable
{
    public IBattleConnection Connection { get; }
    public string ConnectionId => Connection.ConnectionId;
    public ConnectionType Type => Connection.Type;
    public BattleFieldData? CurrentField { get; private set; }

    public event Action<BattleFieldData>? OnBattleFieldUpdated;

    public BattleClient(IBattleConnection connection)
    {
        Connection = connection;
        Connection.OnBattleReplayReceived += OnReplayReceived;
    }

    private void OnReplayReceived(BattleReplayData replayData)
    {
        // リプレイデータをフィールドデータに変換
        if (replayData.BattleData?.Any() == true)
        {
            var latestTurn = replayData.BattleData.Last();
            CurrentField = ConvertToFieldData(latestTurn);
            OnBattleFieldUpdated?.Invoke(CurrentField);
        }
    }

    private BattleFieldData ConvertToFieldData(BattleStatus battleStatus)
    {
        return new BattleFieldData
        {
            Turn = battleStatus.CurrentTurn,
            Entities = battleStatus.AllEntities.Select(e => new EntityData
            {
                Id = e.EntityId,
                Type = e.IsPlayer ? EntityType.Player : GetEnemyType(e),
                Position = new Position(e.Position.X, e.Position.Y),
                Health = e.CurrentHp,
                MaxHealth = e.MaxHp
            }).ToList()
        };
    }

    private EntityType GetEnemyType(EntityInfo entity)
    {
        return entity.MaxHp switch
        {
            <= 100 => EntityType.Small,
            <= 200 => EntityType.Medium,
            _ => EntityType.Large
        };
    }

    public async ValueTask DisposeAsync()
    {
        Connection.OnBattleReplayReceived -= OnReplayReceived;
        await Connection.DisposeAsync();
    }
}

public enum BattleStatus
{
    Waiting,
    InProgress,
    Completed
}

public record BattleFieldData
{
    public int Turn { get; init; }
    public List<EntityData> Entities { get; init; } = new();
}

public record EntityData
{
    public string Id { get; init; } = string.Empty;
    public EntityType Type { get; init; }
    public Position Position { get; init; }
    public int Health { get; init; }
    public int MaxHealth { get; init; }
}

public enum EntityType
{
    Player,
    Small,
    Medium,
    Large
}

public record Position(int X, int Y);
```

### Settings Service

アプリケーション設定を管理するサービス。

```csharp
public class SettingsService
{
    private const string SettingsKey = "WasmClientSettings";

    public string SignalRUrl { get; set; } = "http://localhost:5000";
    public string MagicOnionUrl { get; set; } = "http://localhost:5001";
    public bool ShowDebugInfo { get; set; } = false;
    public int FieldSize { get; set; } = 100;

    public async Task LoadAsync()
    {
        // LocalStorage から設定を読み込み
        // 実装は省略
    }

    public async Task SaveAsync()
    {
        // LocalStorage に設定を保存
        // 実装は省略
    }

    public void Reset()
    {
        SignalRUrl = "http://localhost:5000";
        MagicOnionUrl = "http://localhost:5001";
        ShowDebugInfo = false;
        FieldSize = 200;
    }
}
```

## Deployment Configuration

### Connection Manager

ユーザーが接続設定を行うためのUIコンポーネント。

```razor
@inject BattleSessionManager SessionManager

<div class="connection-manager">
    <h3>New Battle Connection</h3>

    <div class="form-group">
        <label>Server URL:</label>
        <input @bind="connectionInfo.ServerUrl" placeholder="http://localhost:5000" />
    </div>

    <div class="form-group">
        <label>Connection Type:</label>
        <InputSelect @bind-Value="connectionInfo.Type">
            <option value="@ConnectionType.SignalR">SignalR (WebSocket)</option>
            <option value="@ConnectionType.MagicOnion">MagicOnion (gRPC-Web)</option>
        </InputSelect>
    </div>

    @if (connectionInfo.Type == ConnectionType.SignalR)
    {
        <div class="form-group">
            <label>Group Name (optional):</label>
            <input @bind="connectionInfo.GroupName" placeholder="battle-group-1" />
        </div>
    }

    <div class="form-group">
        <label>Player ID (optional):</label>
        <input @bind="connectionInfo.PlayerId" placeholder="player-001" />
    </div>

    <button @onclick="CreateConnectionAsync" disabled="@isConnecting">
        @(isConnecting ? "Connecting..." : "Create Battle")
    </button>
</div>
```

### Battle List Page

ホーム画面でバトル一覧を表示し、新規バトル作成を管理するコンポーネント。アクティブなバトルと過去のバトル履歴を統合表示します。

```razor
@page "/"
@inject BattleSessionManager SessionManager
@inject NavigationManager Navigation

<div class="battle-list-page">
    <header class="page-header">
        <h1>バトル一覧</h1>
        <nav class="nav-menu">
            <a href="/options">オプション</a>
        </nav>
    </header>

    <div class="battle-sections">
        <!-- アクティブなバトル -->
        <div class="active-battles-section">
            <h2>実行中のバトル</h2>
            <div class="battle-grid">
                @foreach (var battle in SessionManager.ActiveBattles)
                {
                    <BattleCard Battle="battle" OnSelect="NavigateToBattle" OnDelete="RemoveBattle" IsHistorical="false" />
                }

                <div class="add-battle-card">
                    <button class="add-battle-btn" @onclick="ShowCreateBattleDialog">
                        <span class="plus-icon">+</span>
                        <span>新規バトル作成</span>
                    </button>
                </div>
            </div>
        </div>

        <!-- 過去のバトル履歴 -->
        <div class="battle-history-section">
            <h2>バトル履歴</h2>
            @if (battleHistory.Any())
            {
                <div class="battle-grid">
                    @foreach (var historyItem in battleHistory)
                    {
                        <BattleHistoryCard
                            HistoryData="historyItem"
                            OnSelect="LoadHistoricalBattle"
                            OnDelete="DeleteBattleHistory" />
                    }
                </div>
            }
            else
            {
                <p class="no-history">保存されたバトル履歴はありません</p>
            }
        </div>
    </div>

    @if (showCreateDialog)
    {
        <CreateBattleDialog OnCreate="CreateBattle" OnCancel="HideCreateBattleDialog" />
    }
</div>

@code {
    private bool showCreateDialog;
    private List<BattleHistorySummary> battleHistory = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadBattleHistoryAsync();
    }

    private async Task LoadBattleHistoryAsync()
    {
        battleHistory = await SessionManager.GetBattleHistoryAsync();
        StateHasChanged();
    }

    private void ShowCreateBattleDialog() => showCreateDialog = true;
    private void HideCreateBattleDialog() => showCreateDialog = false;

    private async Task CreateBattle(string groupName, string serverUrl)
    {
        var battle = await SessionManager.CreateBattleAsync(groupName, serverUrl);
        HideCreateBattleDialog();
        Navigation.NavigateTo($"/battle/{battle.Id}");
    }

    private void NavigateToBattle(Battle battle)
    {
        Navigation.NavigateTo($"/battle/{battle.Id}");
    }

    private async Task RemoveBattle(Battle battle)
    {
        await SessionManager.RemoveBattleAsync(battle.Id);
        StateHasChanged();
    }

    private async Task LoadHistoricalBattle(BattleHistorySummary historyItem)
    {
        var battle = await SessionManager.LoadBattleFromHistoryAsync(historyItem.BattleId);
        if (battle != null)
        {
            Navigation.NavigateTo($"/battle/{battle.Id}");
        }
    }

    private async Task DeleteBattleHistory(BattleHistorySummary historyItem)
    {
        await SessionManager.DeleteBattleHistoryAsync(historyItem.BattleId);
        await LoadBattleHistoryAsync(); // 履歴を再読み込み
    }
}
```

### Battle History Card Component

過去のバトル履歴を表示するためのカードコンポーネント。

```razor
<div class="battle-history-card">
    <div class="card-header">
        <h4>@HistoryData.GroupName</h4>
        <span class="battle-date">@HistoryData.CompletedAt.ToString("MM/dd HH:mm")</span>
        <button class="delete-btn" @onclick="DeleteHistory" title="削除">×</button>
    </div>

    <div class="card-body">
        <div class="battle-stats">
            <div class="stat-item">
                <span class="label">ターン数:</span>
                <span class="value">@HistoryData.TotalTurns</span>
            </div>
            <div class="stat-item">
                <span class="label">結果:</span>
                <span class="value @(HistoryData.IsPlayerVictory ? "victory" : "defeat")">
                    @(HistoryData.IsPlayerVictory ? "勝利" : "敗北")
                </span>
            </div>
            <div class="stat-item">
                <span class="label">データサイズ:</span>
                <span class="value">@HistoryData.DataSizeKB KB</span>
            </div>
            <div class="stat-item">
                <span class="label">クライアント数:</span>
                <span class="value">@HistoryData.ClientCount</span>
            </div>
            <div class="stat-item">
                <span class="label">バトル時間:</span>
                <span class="value">@HistoryData.BattleDuration.ToString(@"mm\:ss")</span>
            </div>
        </div>
    </div>

    <div class="card-footer">
        <button class="view-replay-btn" @onclick="ViewReplay">リプレイを見る</button>
    </div>
</div>

@code {
    [Parameter] public BattleHistorySummary HistoryData { get; set; } = null!;
    [Parameter] public EventCallback<BattleHistorySummary> OnSelect { get; set; }
    [Parameter] public EventCallback<BattleHistorySummary> OnDelete { get; set; }

    private async Task ViewReplay()
    {
        await OnSelect.InvokeAsync(HistoryData);
    }

    private async Task DeleteHistory()
    {
        if (await JSRuntime.InvokeAsync<bool>("confirm", $"バトル履歴「{HistoryData.GroupName}」を削除しますか？"))
        {
            await OnDelete.InvokeAsync(HistoryData);
        }
    }
}
```

### Battle Detail Page

個別のバトル詳細を表示し、クライアント管理を行うコンポーネント。履歴バトルの場合は読み取り専用のリプレイモードで表示します。

```razor
@page "/battle/{BattleId}"
@inject BattleSessionManager SessionManager
@inject IConnectionFactory ConnectionFactory
@inject NavigationManager Navigation

<div class="battle-detail-page">
    <header class="battle-header">
        <div class="battle-info">
            <h2>@battle.GroupName</h2>
            <span class="battle-status @battle.Status.ToString().ToLower()">@battle.Status</span>
            @if (battle.IsHistoricalBattle)
            {
                <span class="historical-badge">履歴</span>
            }
        </div>
        <div class="battle-actions">
            @if (battle.IsHistoricalBattle)
            {
                <button class="btn-secondary" @onclick="GoBackToHome">
                    ← バトル一覧に戻る
                </button>
            }
            else
            {
                <button class="btn-danger" @onclick="RemoveBattle">
                    バトルを削除
                </button>
            }
        </div>
    </header>

    @if (battle.IsHistoricalBattle)
    {
        <!-- 履歴バトル用のリプレイ表示 -->
        <div class="historical-battle-info">
            <div class="battle-stats">
                <div class="stat-item">
                    <span class="label">開始時刻:</span>
                    <span class="value">@battle.BattleHistory!.CreatedAt.ToString("yyyy/MM/dd HH:mm:ss")</span>
                </div>
                <div class="stat-item">
                    <span class="label">完了時刻:</span>
                    <span class="value">@battle.BattleHistory!.CompletedAt.ToString("yyyy/MM/dd HH:mm:ss")</span>
                </div>
                <div class="stat-item">
                    <span class="label">バトル時間:</span>
                    <span class="value">@((battle.BattleHistory!.CompletedAt - battle.BattleHistory.CreatedAt).ToString(@"mm\:ss"))</span>
                </div>
                <div class="stat-item">
                    <span class="label">総ターン数:</span>
                    <span class="value">@battle.BattleHistory!.TotalTurns</span>
                </div>
                <div class="stat-item">
                    <span class="label">結果:</span>
                    <span class="value @(battle.BattleHistory!.Result.IsPlayerVictory ? "victory" : "defeat")">
                        @(battle.BattleHistory!.Result.IsPlayerVictory ? "勝利" : "敗北")
                    </span>
                </div>
            </div>
        </div>

        <!-- リプレイコントロール -->
        <HistoricalBattleReplayControl
            BattleHistory="battle.BattleHistory!"
            @bind-CurrentFrame="currentReplayFrame"
            @bind-IsPlaying="isReplayPlaying" />
    }
    else
    {
        <!-- アクティブバトル用のクライアント管理 -->
        <div class="client-management">
            <div class="connected-clients">
                @foreach (var client in battle.Clients)
                {
                    <ClientCard Client="client" OnRemove="RemoveClient" />
                }
            </div>

            <div class="add-client-buttons">
                <button class="add-client-btn signalr" @onclick="() => AddClient(ConnectionType.SignalR)">
                    <span class="plus-icon">+</span>
                    <span>SignalR追加</span>
                </button>

                <button class="add-client-btn magiconion" @onclick="() => AddClient(ConnectionType.MagicOnion)">
                    <span class="plus-icon">+</span>
                    <span>MagicOnion追加</span>
                </button>
            </div>
        </div>
    }

    <!-- バトルフィールド表示 -->
    <div class="battle-fields">
        @if (battle.IsHistoricalBattle)
        {
            <!-- 履歴バトル用のリプレイフィールド -->
            <HistoricalBattleFieldGrid
                BattleHistory="battle.BattleHistory!"
                CurrentFrame="currentReplayFrame"
                FieldSize="200" />
        }
        else
        {
            <!-- アクティブバトル用のリアルタイムフィールド -->
            @foreach (var client in battle.Clients)
            {
                <BattleField Client="client" Size="100" />
            }
        }
    </div>
</div>

@code {
    [Parameter] public string BattleId { get; set; } = string.Empty;

    private Battle battle = null!;
    private int currentReplayFrame = 0;
    private bool isReplayPlaying = false;

    protected override async Task OnInitializedAsync()
    {
        battle = SessionManager.GetBattle(BattleId);

        if (battle == null)
        {
            // バトルが見つからない場合、履歴から読み込みを試行
            battle = await SessionManager.LoadBattleFromHistoryAsync(BattleId);

            if (battle == null)
            {
                Navigation.NavigateTo("/");
                return;
            }
        }
    }

    private async Task AddClient(ConnectionType type)
    {
        if (battle.IsHistoricalBattle) return; // 履歴バトルでは無効

        var connectionInfo = new ConnectionInfo
        {
            ServerUrl = battle.ServerUrl,
            GroupName = battle.GroupName,
            Type = type
        };

        await battle.AddClientAsync(connectionInfo);
        StateHasChanged();
    }

    private async Task RemoveClient(BattleClient client)
    {
        if (battle.IsHistoricalBattle) return; // 履歴バトルでは無効

        await battle.RemoveClientAsync(client);
        StateHasChanged();
    }

    private async Task RemoveBattle()
    {
        if (battle.IsHistoricalBattle) return; // 履歴バトルでは無効

        await SessionManager.RemoveBattleAsync(battle.Id);
        Navigation.NavigateTo("/");
    }

    private void GoBackToHome()
    {
        Navigation.NavigateTo("/");
    }
}
```

### Historical Battle Replay Components

履歴バトルのリプレイ表示に特化したコンポーネント群。

#### Historical Battle Replay Control

履歴バトルのリプレイを制御するコンポーネント。再生・停止・シークバーを提供します。

```razor
@using CliClient.Constants
@implements IDisposable

<div class="replay-control-panel">
    <div class="replay-controls">
        <button class="play-pause-btn" @onclick="TogglePlayPause">
            <span class="@(IsPlaying ? "pause-icon" : "play-icon")">
                @(IsPlaying ? "⏸️" : "▶️")
            </span>
        </button>

        <button class="step-btn" @onclick="StepBackward" disabled="@(IsPlaying || CurrentFrame <= 0)">
            ⏮️
        </button>

        <button class="step-btn" @onclick="StepForward" disabled="@(IsPlaying || CurrentFrame >= MaxFrame)">
            ⏭️
        </button>

        <button class="reset-btn" @onclick="ResetToStart" disabled="@IsPlaying">
            ⏪
        </button>
    </div>

    <div class="replay-timeline">
        <input type="range"
               class="timeline-slider"
               @bind="CurrentFrame"
               @oninput="OnTimelineChanged"
               min="0"
               max="@MaxFrame"
               disabled="@IsPlaying" />

        <div class="timeline-info">
            <span class="current-time">@FormatFrameTime(CurrentFrame)</span>
            <span class="separator">/</span>
            <span class="total-time">@FormatFrameTime(MaxFrame)</span>
            <span class="frame-info">(@CurrentFrame/@MaxFrame frames)</span>
        </div>
    </div>

    <div class="playback-speed">
        <label>再生速度:</label>
        <select @bind="PlaybackSpeed">
            <option value="0.25">0.25x</option>
            <option value="0.5">0.5x</option>
            <option value="1.0" selected>1.0x</option>
            <option value="1.5">1.5x</option>
            <option value="2.0">2.0x</option>
        </select>
    </div>
</div>

@code {
    [Parameter] public BattleHistory BattleHistory { get; set; } = null!;
    [Parameter] public int CurrentFrame { get; set; }
    [Parameter] public EventCallback<int> CurrentFrameChanged { get; set; }
    [Parameter] public bool IsPlaying { get; set; }
    [Parameter] public EventCallback<bool> IsPlayingChanged { get; set; }

    private Timer? playbackTimer;
    private double playbackSpeed = 1.0;

    private int MaxFrame => Math.Max(0, BattleHistory.ReplayData.Count - 1);

    private double PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            playbackSpeed = value;
            if (IsPlaying)
            {
                RestartTimer(); // 新しい速度でタイマーを再開
            }
        }
    }

    private async Task TogglePlayPause()
    {
        IsPlaying = !IsPlaying;
        await IsPlayingChanged.InvokeAsync(IsPlaying);

        if (IsPlaying)
        {
            StartPlayback();
        }
        else
        {
            StopPlayback();
        }
    }

    private async Task StepForward()
    {
        if (CurrentFrame < MaxFrame)
        {
            CurrentFrame++;
            await CurrentFrameChanged.InvokeAsync(CurrentFrame);
        }
    }

    private async Task StepBackward()
    {
        if (CurrentFrame > 0)
        {
            CurrentFrame--;
            await CurrentFrameChanged.InvokeAsync(CurrentFrame);
        }
    }

    private async Task ResetToStart()
    {
        CurrentFrame = 0;
        await CurrentFrameChanged.InvokeAsync(CurrentFrame);
    }

    private async Task OnTimelineChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var frame))
        {
            CurrentFrame = Math.Clamp(frame, 0, MaxFrame);
            await CurrentFrameChanged.InvokeAsync(CurrentFrame);
        }
    }

    private void StartPlayback()
    {
        var interval = (int)(BattleReplayDefines.ReplayFrameTimeMs / playbackSpeed);
        playbackTimer = new Timer(OnTimerTick, null, 0, interval);
    }

    private void StopPlayback()
    {
        playbackTimer?.Dispose();
        playbackTimer = null;
    }

    private void RestartTimer()
    {
        StopPlayback();
        StartPlayback();
    }

    private async void OnTimerTick(object? state)
    {
        await InvokeAsync(async () =>
        {
            if (CurrentFrame < MaxFrame)
            {
                CurrentFrame++;
                await CurrentFrameChanged.InvokeAsync(CurrentFrame);
                StateHasChanged();
            }
            else
            {
                // 最後まで到達したら自動停止
                await TogglePlayPause();
            }
        });
    }

    private string FormatFrameTime(int frame)
    {
        var seconds = frame * BattleReplayDefines.ReplayFrameTimeMs / 1000.0;
        return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.f");
    }

    public void Dispose()
    {
        playbackTimer?.Dispose();
    }
}
```

#### Historical Battle Field Grid

履歴バトル用のフィールド表示コンポーネント。指定されたフレームのバトル状況を表示します。

```razor
@using CliClient.Constants

<div class="historical-battle-field-grid">
    @if (currentFrameData != null && currentFrameData.BattleData?.Any() == true)
    {
        @foreach (var (battleData, index) in currentFrameData.BattleData.Select((data, i) => (data, i)))
        {
            <div class="field-container" style="margin: 5px;">
                <HistoricalBattleField
                    BattleData="battleData"
                    FieldSize="FieldSize"
                    ClientIndex="index"
                    Turn="battleData.CurrentTurn" />
            </div>
        }
    }
    else
    {
        <div class="no-data-message">
            このフレームにはバトルデータがありません
        </div>
    }
</div>

@code {
    [Parameter] public BattleHistory BattleHistory { get; set; } = null!;
    [Parameter] public int CurrentFrame { get; set; }
    [Parameter] public int FieldSize { get; set; } = 200;

    private BattleReplayData? currentFrameData;

    protected override void OnParametersSet()
    {
        UpdateCurrentFrameData();
    }

    private void UpdateCurrentFrameData()
    {
        if (BattleHistory.ReplayData.Count > 0 && CurrentFrame >= 0 && CurrentFrame < BattleHistory.ReplayData.Count)
        {
            currentFrameData = BattleHistory.ReplayData[CurrentFrame];
        }
        else
        {
            currentFrameData = null;
        }
    }
}
```

#### Historical Battle Field

単一のバトルフィールドを履歴データから表示するコンポーネント。

```razor
<div class="historical-battle-field"
     style="width: @(FieldSize)px; height: @(FieldSize)px; border: 1px solid #ccc; position: relative; background: #f5f5f5;">

    <div class="field-header" style="font-size: 10px; padding: 2px; background: #e0e0e0;">
        <span class="client-info">クライアント #@(ClientIndex + 1)</span>
        <span class="turn-info">Turn: @Turn</span>
    </div>

    <div class="field-canvas" style="position: relative; width: 100%; height: calc(100% - 20px);">
        @if (BattleData?.AllEntities?.Any() == true)
        {
            @foreach (var entity in BattleData.AllEntities)
            {
                <div class="entity @GetEntityCssClass(entity)"
                     style="position: absolute;
                            left: @(entity.Position.X * scaleX)px;
                            top: @(entity.Position.Y * scaleY)px;
                            width: @entitySize px;
                            height: @entitySize px;
                            border-radius: 50%;"
                     title="@GetEntityTooltip(entity)">
                </div>
            }
        }
    </div>

    <div class="field-footer" style="font-size: 8px; position: absolute; bottom: 2px; left: 2px; right: 2px;">
        <span>Entities: @(BattleData?.AllEntities?.Count ?? 0)</span>
        <span class="players">Players: @GetPlayerCount()</span>
        <span class="enemies">Enemies: @GetEnemyCount()</span>
    </div>
</div>

@code {
    [Parameter] public object BattleData { get; set; } = null!; // BattleStatus from replay data
    [Parameter] public int FieldSize { get; set; } = 200;
    [Parameter] public int ClientIndex { get; set; }
    [Parameter] public int Turn { get; set; }

    private double scaleX => (FieldSize - 4) / 20.0; // 20x20座標を指定サイズにスケール
    private double scaleY => (FieldSize - 24) / 20.0; // ヘッダー・フッター分を考慮
    private int entitySize => Math.Max(4, (int)(scaleX * 0.8));

    private string GetEntityCssClass(object entity)
    {
        // entity の IsPlayer プロパティを動的に取得
        var isPlayerProperty = entity.GetType().GetProperty("IsPlayer");
        var isPlayer = isPlayerProperty?.GetValue(entity) as bool? ?? false;

        return isPlayer ? "player" : "enemy";
    }

    private string GetEntityTooltip(object entity)
    {
        // 動的にプロパティを取得してツールチップを構築
        var entityType = entity.GetType();
        var entityId = entityType.GetProperty("EntityId")?.GetValue(entity)?.ToString() ?? "Unknown";
        var currentHp = entityType.GetProperty("CurrentHp")?.GetValue(entity)?.ToString() ?? "0";
        var maxHp = entityType.GetProperty("MaxHp")?.GetValue(entity)?.ToString() ?? "0";
        var position = entityType.GetProperty("Position")?.GetValue(entity);
        var posX = position?.GetType().GetProperty("X")?.GetValue(position)?.ToString() ?? "0";
        var posY = position?.GetType().GetProperty("Y")?.GetValue(position)?.ToString() ?? "0";

        return $"ID: {entityId} - HP: {currentHp}/{maxHp} - Pos: ({posX}, {posY})";
    }

    private int GetPlayerCount()
    {
        if (BattleData?.GetType().GetProperty("AllEntities")?.GetValue(BattleData) is not IEnumerable<object> entities)
            return 0;

        return entities.Count(entity =>
            entity.GetType().GetProperty("IsPlayer")?.GetValue(entity) as bool? ?? false);
    }

    private int GetEnemyCount()
    {
        if (BattleData?.GetType().GetProperty("AllEntities")?.GetValue(BattleData) is not IEnumerable<object> entities)
            return 0;

        return entities.Count(entity =>
            !(entity.GetType().GetProperty("IsPlayer")?.GetValue(entity) as bool? ?? true));
    }
}
```

### Battle Field Component

200px四方のフィールドでバトル進行を表示するコンポーネント。

```razor
@using CliClient.Constants
@implements IDisposable

<div class="battle-field" style="width: @(Size)px; height: @(Size)px; border: 1px solid #ccc; position: relative; background: #f5f5f5;">
    <div class="field-header" style="font-size: 10px; padding: 2px;">
        <span>@Client.ConnectionId[..8]</span>
        <span class="connection-type">(@Client.Type)</span>
    </div>

    <div class="field-canvas" style="position: relative; width: 100%; height: calc(100% - 16px);">
        @if (fieldData != null)
        {
            @foreach (var entity in fieldData.Entities)
            {
                <div class="entity @entity.Type.ToString().ToLower()"
                     style="position: absolute;
                            left: @(entity.Position.X * scaleX)px;
                            top: @(entity.Position.Y * scaleY)px;
                            width: @entitySize px;
                            height: @entitySize px;
                            border-radius: 50%;"
                     title="@GetEntityTooltip(entity)">
                </div>
            }
        }
    </div>

    <div class="field-status" style="font-size: 8px; position: absolute; bottom: 2px; left: 2px;">
        <span>Turn: @(fieldData?.Turn ?? 0)</span>
        <span>Entities: @(fieldData?.Entities.Count ?? 0)</span>
    </div>
</div>

@code {
    [Parameter] public BattleClient Client { get; set; } = null!;
    [Parameter] public int Size { get; set; } = 200;

    private BattleFieldData? fieldData;
    private double scaleX => (Size - 4) / 20.0; // 20x20座標を縮尺
    private double scaleY => (Size - 20) / 20.0; // ヘッダー分を考慮
    private int entitySize => Math.Max(4, (int)(scaleX * 0.8)); // エンティティサイズ

    protected override void OnInitialized()
    {
        Client.OnBattleFieldUpdated += OnFieldUpdated;
    }

    private void OnFieldUpdated(BattleFieldData data)
    {
        fieldData = data;
        InvokeAsync(StateHasChanged);
    }

    private string GetEntityTooltip(EntityData entity)
    {
        return $"{entity.Type} - HP: {entity.Health}/{entity.MaxHealth} - Pos: ({entity.Position.X}, {entity.Position.Y})";
    }

    public void Dispose()
    {
        Client.OnBattleFieldUpdated -= OnFieldUpdated;
    }
}

<style>
.entity.player { background-color: #4285f4; }
.entity.enemy { background-color: #ea4335; }
.entity.small { opacity: 0.8; }
.entity.medium { opacity: 0.9; }
.entity.large { opacity: 1.0; border: 1px solid #333; }
</style>
```

### Options Page

サーバーURL設定とIndexedDB管理を行うページ。

```razor
@page "/options"
@inject SettingsService Settings
@inject BattleHistoryService BattleHistory
@inject NavigationManager Navigation

<div class="options-page">
    <header class="page-header">
        <h1>設定</h1>
        <button @onclick="GoBack" class="back-btn">戻る</button>
    </header>

    <div class="settings-form">
        <div class="setting-group">
            <h3>SignalR接続設定</h3>
            <div class="form-group">
                <label>デフォルトサーバーURL:</label>
                <input @bind="Settings.SignalRUrl" placeholder="http://localhost:5000" />
            </div>
        </div>

        <div class="setting-group">
            <h3>MagicOnion接続設定</h3>
            <div class="form-group">
                <label>デフォルトサーバーURL:</label>
                <input @bind="Settings.MagicOnionUrl" placeholder="http://localhost:5001" />
            </div>
        </div>

        <div class="setting-group">
            <h3>表示設定</h3>
            <div class="form-group">
                <label>
                    <input type="checkbox" @bind="Settings.ShowDebugInfo" />
                    デバッグ情報を表示
                </label>
            </div>
            <div class="form-group">
                <label>
                    フィールドサイズ:
                    <input type="range" @bind="Settings.FieldSize" min="120" max="300" />
                    @Settings.FieldSize px
                </label>
            </div>
        </div>

        <!-- IndexedDB管理設定 -->
        <div class="setting-group">
            <h3>データ管理</h3>

            @if (storageStats != null)
            {
                <div class="storage-info">
                    <div class="info-item">
                        <span class="label">保存済みバトル数:</span>
                        <span class="value">@storageStats.TotalBattles</span>
                    </div>
                    <div class="info-item">
                        <span class="label">データ使用量:</span>
                        <span class="value">@((storageStats.TotalSizeBytes / 1024.0 / 1024.0).ToString("F2")) MB</span>
                    </div>
                    <div class="info-item">
                        <span class="label">平均バトルサイズ:</span>
                        <span class="value">@(storageStats.AverageBattleSizeKB.ToString("F1")) KB</span>
                    </div>
                    @if (storageStats.OldestBattle.HasValue && storageStats.NewestBattle.HasValue)
                    {
                        <div class="info-item">
                            <span class="label">データ期間:</span>
                            <span class="value">@storageStats.OldestBattle.Value.ToString("MM/dd") - @storageStats.NewestBattle.Value.ToString("MM/dd")</span>
                        </div>
                    }
                </div>
            }

            <div class="form-group">
                <button class="btn-warning" @onclick="RefreshStorageStats">
                    ストレージ情報を更新
                </button>
            </div>

            <div class="form-group">
                <button class="btn-danger" @onclick="ClearIndexedDB" disabled="@isClearingData">
                    @(isClearingData ? "削除中..." : "全バトル履歴を削除")
                </button>
                <p class="help-text">
                    ⚠️ この操作は取り消せません。保存された全てのバトル履歴が削除されます。
                </p>
            </div>
        </div>

        <div class="form-actions">
            <button @onclick="SaveSettings" class="btn-primary">設定を保存</button>
            <button @onclick="ResetSettings" class="btn-secondary">デフォルトに戻す</button>
        </div>
    </div>
</div>

@code {
    private BattleHistoryStats? storageStats;
    private bool isClearingData = false;

    protected override async Task OnInitializedAsync()
    {
        await RefreshStorageStats();
    }

    private async Task SaveSettings()
    {
        await Settings.SaveAsync();
        // Toast notification or similar feedback
    }

    private async Task ResetSettings()
    {
        Settings.Reset();
        await Settings.SaveAsync();
        StateHasChanged();
    }

    private async Task RefreshStorageStats()
    {
        try
        {
            storageStats = await BattleHistory.GetStorageStatsAsync();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            // Handle error - could show toast notification
            Console.WriteLine($"Failed to load storage stats: {ex.Message}");
        }
    }

    private async Task ClearIndexedDB()
    {
        if (!await JSRuntime.InvokeAsync<bool>("confirm",
            "全てのバトル履歴を削除します。この操作は取り消せません。続行しますか？"))
        {
            return;
        }

        isClearingData = true;
        StateHasChanged();

        try
        {
            await BattleHistory.ClearAllBattleHistoryAsync();
            await RefreshStorageStats(); // 統計情報を更新
            // Success notification
        }
        catch (Exception ex)
        {
            // Error notification
            Console.WriteLine($"Failed to clear battle history: {ex.Message}");
        }
        finally
        {
            isClearingData = false;
            StateHasChanged();
        }
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/");
    }
}
```

```razor
@implements IDisposable

<div class="battle-viewer" @onmouseover="ShowDetails" @onmouseout="HideDetails">
    <div class="battle-header">
        <h4>Battle Session: @Session.Id[..8]</h4>
        <span class="connection-type">@Session.Connection.Type</span>
        <button @onclick="DisconnectAsync" class="btn-close">×</button>
    </div>

    <div class="battle-status">
        @if (Session.CurrentStatus != null)
        {
            <div class="status-info">
                <span>Turn: @Session.CurrentStatus.CurrentTurn</span>
                <span>Players: @Session.CurrentStatus.PlayerCount</span>
            </div>
        }
    </div>

    <div class="battle-replay" style="height: 400px;">
        <ReplayPlayer ReplayData="Session.ReplayHistory" />
    </div>

    @if (showDetails)
    {
        <div class="battle-details">
            <h5>Connection Details</h5>
            <p>Server: @Session.Connection.Info.ServerUrl</p>
            <p>Group: @(Session.Connection.Info.GroupName ?? "N/A")</p>
            <p>Replay Frames: @Session.ReplayHistory.Count</p>
        </div>
    }
</div>
```

### Replay Player

CliClientのBattleReplayDefinesを使用してリプレイを再生するコンポーネント。

```razor
@using CliClient.Constants
@implements IDisposable

<div class="replay-player">
    <div class="replay-controls">
        <button @onclick="TogglePlayPause">
            @(isPlaying ? "Pause" : "Play")
        </button>
        <button @onclick="StepForward" disabled="@isPlaying">Step</button>
        <input type="range" @bind="currentFrame" min="0" max="@maxFrame" />
        <span>@currentFrame / @maxFrame</span>
    </div>

    <div class="replay-canvas" style="position: relative; height: 300px;">
        @if (CurrentReplayData != null)
        {
            <BattleFrame FrameData="CurrentReplayData" />
        }
    </div>
</div>

@code {
    [Parameter] public List<BattleReplayData> ReplayData { get; set; } = new();

    private bool isPlaying;
    private int currentFrame;
    private int maxFrame => Math.Max(0, ReplayData.Count - 1);
    private Timer? playbackTimer;

    private BattleReplayData? CurrentReplayData =>
        currentFrame < ReplayData.Count ? ReplayData[currentFrame] : null;

    private void TogglePlayPause()
    {
        isPlaying = !isPlaying;

        if (isPlaying)
        {
            StartPlayback();
        }
        else
        {
            StopPlayback();
        }
    }

    private void StartPlayback()
    {
        playbackTimer = new Timer(OnTimerTick, null, 0, BattleReplayDefines.ReplayFrameTimeMs);
    }

    private void StopPlayback()
    {
        playbackTimer?.Dispose();
        playbackTimer = null;
    }

    private void OnTimerTick(object? state)
    {
        InvokeAsync(() =>
        {
            if (currentFrame < maxFrame)
            {
                currentFrame++;
                StateHasChanged();
            }
            else
            {
                TogglePlayPause(); // Auto-stop at end
            }
        });
    }
}
```

## Deployment Configuration

### Project Structure

```xml
<!-- WasmClient.csproj -->
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
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" PrivateAssets="all" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
    <PackageReference Include="MagicOnion.Client" />
    <PackageReference Include="Grpc.Net.Client.Web" />
  </ItemGroup>
</Project>
```

### Program.cs Configuration

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Services registration
builder.Services.AddSingleton<IConnectionFactory, ConnectionFactory>();
builder.Services.AddSingleton<BattleSessionManager>();
builder.Services.AddSingleton<BattleHistoryService>();
builder.Services.AddSingleton<SettingsService>();

// Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

await builder.Build().RunAsync();
```

## User Flow & UI Structure

### Application Structure

```
WasmClient Application
├── Home Page (バトル一覧)
│   ├── Battle List (既存のバトル一覧)
│   ├── + Button (新規バトル作成)
│   └── Options (設定画面)
├── Battle Detail Page (バトル詳細)
│   ├── Client List (接続済みクライアント一覧)
│   ├── + SignalR Button (SignalRクライアント追加)
│   ├── + MagicOnion Button (MagicOnionクライアント追加)
│   └── Battle Field Grid (200px四方のフィールド表示)
└── Options Page (設定)
    ├── SignalR Server URL Setting
    └── MagicOnion Server URL Setting
```

### User Operation Flow

#### 1. ホーム画面での操作

**バトル一覧表示**
- 既存のバトルセッションが一覧で表示される
- 各バトルには参加クライアント数、バトル状態が表示される

**新規バトル作成**
- 「+ボタン」をクリックしてバトル作成ダイアログを表示
- グループ名とサーバーURLを指定してバトルを作成
- 作成後は自動的にバトル詳細画面に遷移

**設定画面**
- オプション画面で接続方式ごとのデフォルトサーバーURLを設定
- SignalR用URL (例: `http://localhost:5000`)
- MagicOnion用URL (例: `http://localhost:5001`)

#### 2. バトル詳細画面での操作

**クライアント管理**
- 画面上部に現在接続中のクライアント一覧を表示
- SignalR用「+ボタン」とMagicOnion用「+ボタン」を配置
- 各「+ボタン」クリックで対応する方式のクライアントを作成・接続

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
- SignalR接続はWebSocketsを、MagicOnion接続はgRPC-Webを使用
- リプレイデータの蓄積と再生にはCliClientと同じフレームレート（5fps）を使用
- 接続エラー処理とリトライロジックをCliClientから移植
- IndexedDBを使用したバトル履歴の永続化により、ブラウザリロード後もデータアクセス可能
- JSInteropを使用してC#からIndexedDB操作を行い、型安全性を維持
- 350KB程度/バトルの大容量データを効率的に管理するためのチャンク処理
- バトル履歴の削除・統計表示機能により、ストレージ容量の管理が可能
- 履歴バトルのリプレイ機能では、読み取り専用モードで完全な戦闘再現が可能
- リプレイコントロールによる柔軟な再生操作（再生速度調整、フレーム単位の制御）
- 動的プロパティアクセスによる型安全でない部分の最小化と例外処理の充実
