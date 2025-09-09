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
│   └── MagicOnionConnection.cs     # MagicOnion接続実装
├── Models/
│   ├── BattleSessionModel.cs       # バトルセッション管理
│   └── ConnectionInfo.cs           # 接続情報
└── Constants/
    └── BattleReplayDefines.cs      # リプレイ定数
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
    private readonly ILogger<BattleSessionManager> _logger;

    public BattleSessionManager(IConnectionFactory connectionFactory, ILogger<BattleSessionManager> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Battle> CreateBattleAsync(string groupName, string serverUrl)
    {
        var battle = new Battle(Guid.NewGuid().ToString(), groupName, serverUrl, _connectionFactory);
        _battles[battle.Id] = battle;

        _logger.LogInformation("Created battle {BattleId} with group {GroupName}", battle.Id, groupName);
        return battle;
    }

    public Battle? GetBattle(string battleId) => _battles.TryGetValue(battleId, out var battle) ? battle : null;

    public async Task RemoveBattleAsync(string battleId)
    {
        if (_battles.TryGetValue(battleId, out var battle))
        {
            await battle.DisposeAsync();
            _battles.Remove(battleId);
            _logger.LogInformation("Removed battle {BattleId}", battleId);
        }
    }

    public IReadOnlyList<Battle> ActiveBattles => _battles.Values.ToList();
}

public class Battle : IAsyncDisposable
{
    private readonly List<BattleClient> _clients = new();
    private readonly IConnectionFactory _connectionFactory;

    public string Id { get; }
    public string GroupName { get; }
    public string ServerUrl { get; }
    public BattleStatus Status { get; private set; } = BattleStatus.Waiting;
    public IReadOnlyList<BattleClient> Clients => _clients.AsReadOnly();

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

ホーム画面でバトル一覧を表示し、新規バトル作成を管理するコンポーネント。

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

    <div class="battle-grid">
        @foreach (var battle in SessionManager.ActiveBattles)
        {
            <BattleCard Battle="battle" OnSelect="NavigateToBattle" />
        }

        <div class="add-battle-card">
            <button class="add-battle-btn" @onclick="ShowCreateBattleDialog">
                <span class="plus-icon">+</span>
                <span>新規バトル作成</span>
            </button>
        </div>
    </div>

    @if (showCreateDialog)
    {
        <CreateBattleDialog OnCreate="CreateBattle" OnCancel="HideCreateBattleDialog" />
    }
</div>

@code {
    private bool showCreateDialog;

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
}
```

### Battle Detail Page

個別のバトル詳細を表示し、クライアント管理を行うコンポーネント。

```razor
@page "/battle/{BattleId}"
@inject BattleSessionManager SessionManager
@inject IConnectionFactory ConnectionFactory

<div class="battle-detail-page">
    <header class="battle-header">
        <h2>@battle.GroupName</h2>
        <span class="battle-status">@battle.Status</span>
    </header>

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

    <div class="battle-fields">
        @foreach (var client in battle.Clients)
        {
            <BattleField Client="client" Size="100" />
        }
    </div>
</div>

@code {
    [Parameter] public string BattleId { get; set; } = string.Empty;

    private Battle battle = null!;

    protected override async Task OnInitializedAsync()
    {
        battle = SessionManager.GetBattle(BattleId) ??
                 throw new InvalidOperationException($"Battle {BattleId} not found");
    }

    private async Task AddClient(ConnectionType type)
    {
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
        await battle.RemoveClientAsync(client);
        StateHasChanged();
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

サーバーURL設定を管理するページ。

```razor
@page "/options"
@inject SettingsService Settings
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

        <div class="form-actions">
            <button @onclick="SaveSettings" class="btn-primary">設定を保存</button>
            <button @onclick="ResetSettings" class="btn-secondary">デフォルトに戻す</button>
        </div>
    </div>
</div>

@code {
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

## Implementation Notes

- CliClientのConstants（BattleReplayDefines等）を共有して一貫性を保つ
- SignalR接続はWebSocketsを、MagicOnion接続はgRPC-Webを使用
- リプレイデータの蓄積と再生にはCliClientと同じフレームレート（5fps）を使用
- 接続エラー処理とリトライロジックをCliClientから移植
- セッション管理
