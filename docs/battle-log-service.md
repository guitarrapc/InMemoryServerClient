# バトルログ共通化サービス

## 概要

MagicOnionクライアントとSignalRクライアント間でのログの重複を解決するため、バトルログメッセージの標準化サービスを実装しました。これにより、以下の利点があります：

- **一貫性**: 両クライアント間で同じログフォーマットを保証
- **保守性**: ログメッセージの変更が一箇所で管理可能
- **型安全性**: コンパイル時にログメッセージの型チェック
- **テスタビリティ**: ログメッセージの単体テストが可能

## アーキテクチャ

### 1. IBattleLogMessageService

すべてのバトル関連ログメッセージのフォーマットを定義するインターフェース。

```csharp
public interface IBattleLogMessageService
{
    // グループ関連メッセージ
    (string message, object?[] args) FormatMemberJoined(string connectionId, string groupName);
    (string message, object?[] args) FormatGroupMemberCount(int currentCount, int maxMembers);

    // バトル関連メッセージ
    (string message, object?[] args) FormatConnectionsReady(Guid battleId, long seed);
    (string message, object?[] args) FormatBattleStarted(Guid battleId, long seed);

    // その他多数のメッセージフォーマット...
}
```

### 2. BattleLogMessageService

`IBattleLogMessageService`の標準実装。すべてのログメッセージフォーマットを一元管理。

```csharp
public class BattleLogMessageService : IBattleLogMessageService
{
    public (string message, object?[] args) FormatMemberJoined(string connectionId, string groupName)
        => ("[GROUP] 👤 New member joined! Connection ID: {ConnectionId} in group {GroupName}",
            [connectionId, groupName]);
}
```

### 3. BattleLoggerExtensions

`ILogger`の拡張メソッドとして、標準化されたログメソッドを提供。

```csharp
public static class BattleLoggerExtensions
{
    public static void LogBattleInfo(this ILogger logger,
        Func<IBattleLogMessageService, (string message, object?[] args)> messageSelector)
    {
        var service = new BattleLogMessageService();
        var (message, args) = messageSelector(service);
        logger.LogInformation(message, args);
    }
}
```

## 使用方法

### 従来の方法
```csharp
// 従来 - 直接ログメッセージを記述
_logger.LogInformation("[GROUP] 👤 New member joined! Connection ID: {ConnectionId} in group {GroupName}",
    data.ConnectionId, data.GroupName);
_logger.LogInformation("[GROUP] 🔢 Total group members: {MemberCount}/{MaxMembers}",
    data.CurrentMemberCount, data.MaxMembers);
```

### 新しい方法
```csharp
// using CliClient.Extensions を追加

// 新しい方法 - 標準化されたサービスを使用
_logger.LogBattleInfo(svc => svc.FormatMemberJoined(data.ConnectionId, data.GroupName));
_logger.LogBattleInfo(svc => svc.FormatGroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
```

## 実装例

### SignalRクライアントでの実装

```csharp
using CliClient.Extensions;

_connection.On<MemberJoinedData>("MemberJoined", (data) =>
{
    _logger.LogBattleInfo(svc => svc.FormatMemberJoined(data.ConnectionId, data.GroupName));
    _logger.LogBattleInfo(svc => svc.FormatGroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
    if (data.CurrentMemberCount == data.MaxMembers)
    {
        _logger.LogBattleInfo(svc => svc.FormatGroupFull());
    }
    OnMemberJoined?.Invoke(data);
});
```

### MagicOnionクライアントでの適用

同様のパターンでMagicOnionクライアントにも適用可能：

```csharp
public void OnMemberJoined(MemberJoinedData data)
{
    _logger.LogBattleInfo(svc => svc.FormatMemberJoined(data.ConnectionId, data.GroupName));
    _logger.LogBattleInfo(svc => svc.FormatGroupMemberCount(data.CurrentMemberCount, data.MaxMembers));
    if (data.CurrentMemberCount == data.MaxMembers)
    {
        _logger.LogBattleInfo(svc => svc.FormatGroupFull());
    }
}
```

## 利用可能なメッセージ

### グループ関連
- `FormatMemberJoined(connectionId, groupName)`
- `FormatMemberLeft(connectionId, groupName)`
- `FormatGroupMemberCount(currentCount, maxMembers)`
- `FormatGroupFull()`
- `FormatGroupDissolved(groupName, groupId, reason)`
- `FormatGroupExtended(groupName, groupId, extensionCount, maxExtensions, newExpiryTime)`

### バトルライフサイクル
- `FormatConnectionsReady(battleId, seed)`
- `FormatConnectionsReadyDetails(battleId, seed)`
- `FormatBattleStarted(battleId, seed)`
- `FormatBattleStartedDetails(battleId, seed)`
- `FormatConfirmingConnection()`
- `FormatConnectionConfirmed(result)`
- `FormatConnectionConfirmationFailed()`

### バトルリプレイ
- `FormatReplayChunkReceived(chunkIndex, totalChunks, turnCount, battleId, seed)`
- `FormatAllChunksReceived(battleId, seed)`
- `FormatReplayStarting(turnCount, fps, battleId, seed)`
- `FormatReplayCompleted(battleId, seed)`

### バトルステータス表示
- `FormatTurnHeader(currentTurn, totalTurns)`
- `FormatPlayersAlive(alivePlayers, totalPlayers)`
- `FormatEnemiesAlive(aliveEnemies, totalEnemies)`
- `FormatPlayerInfo(playerName, jobInfo, currentHp, maxHp, healthBar, attack, defense, speed, position)`
- `FormatEnemyInfo(enemyName, jobInfo, currentHp, maxHp, healthBar, attack, defense, speed, position)`

### 接続管理
- `FormatConnecting(serverUrl)`
- `FormatConnected()`
- `FormatDisconnecting()`
- `FormatDisconnected()`
- `FormatAutoDisconnecting()`

## テスト

すべてのメッセージフォーマットには対応する単体テストが含まれています：

```csharp
[Fact]
public void FormatMemberJoined_ShouldReturnCorrectFormatAndArgs()
{
    // Arrange
    const string connectionId = "test-connection-123";
    const string groupName = "test-group";

    // Act
    var (message, args) = _service.FormatMemberJoined(connectionId, groupName);

    // Assert
    Assert.Equal("[GROUP] 👤 New member joined! Connection ID: {ConnectionId} in group {GroupName}", message);
    Assert.Equal(2, args.Length);
    Assert.Equal(connectionId, args[0]);
    Assert.Equal(groupName, args[1]);
}
```

## 移行ガイド

### 段階的移行

1. **準備**: `using CliClient.Extensions`を追加
2. **置換**: 既存のログ呼び出しを`LogBattleInfo`形式に変更
3. **テスト**: 動作確認とログ出力の一貫性確認
4. **完了**: 古いログ呼び出しを削除

### 利点

- **保守性向上**: ログメッセージの変更が一箇所で完結
- **一貫性保証**: 両クライアント間で同じメッセージフォーマット
- **型安全性**: コンパイル時の型チェック
- **テスタビリティ**: ログメッセージの単体テスト可能
- **拡張性**: 新しいメッセージタイプの追加が容易

## 今後の改善

1. **設定による多言語対応**: 日本語/英語の切り替え
2. **カスタムフォーマッター**: プロジェクト固有のログフォーマット
3. **ログレベル制御**: メッセージ種別によるログレベル自動設定
4. **パフォーマンス最適化**: メッセージ生成の最適化
