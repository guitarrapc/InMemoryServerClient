# 型安全なバトルログサービス使用例

## 従来の方式 vs 型安全版

### 従来の方式（object?[]使用）
```csharp
// object?[]を使用 - 型安全性に欠ける
_logger.LogBattleInfo(svc => svc.FormatMemberJoined(data.ConnectionId, data.GroupName));

// 内部的には以下のようになる
(string message, object?[] args) = service.FormatMemberJoined(connectionId, groupName);
// args = [connectionId, groupName]; // object?[]
```

### 型安全版（ジェネリクス使用）
```csharp
// 強い型付けを使用 - 完全に型安全
_logger.LogBattleInfo(svc => svc.FormatMemberJoined(data.ConnectionId, data.GroupName));

// 内部的には以下のようになる
LogMessage<(string ConnectionId, string GroupName)> logMessage = service.FormatMemberJoined(connectionId, groupName);
// logMessage.Args = (connectionId, groupName); // 強い型付き
```

## 使用方法

### 引数なしのメッセージ
```csharp
// 引数なし
_logger.LogBattleInfo(svc => svc.FormatGroupFull());
_logger.LogBattleInfo(svc => svc.FormatConnected());
_logger.LogBattleInfo(svc => svc.FormatConfirmingConnection());
```

### 単一引数のメッセージ
```csharp
// string引数
_logger.LogBattleInfo(svc => svc.FormatConnecting("http://localhost:5000"));
_logger.LogBattleInfo(svc => svc.FormatActionLog("Player attacked Enemy"));

// bool引数
_logger.LogBattleInfo(svc => svc.FormatConnectionConfirmed(true));
```

### 複数引数のメッセージ（ValueTuple使用）
```csharp
// 2つの引数
_logger.LogBattleInfo(svc => svc.FormatMemberJoined("conn123", "group456"));
// 型: LogMessage<(string ConnectionId, string GroupName)>

// 5つの引数
_logger.LogBattleInfo(svc => svc.FormatReplayChunkReceived(1, 3, 50, battleId, seed));
// 型: LogMessage<(int ChunkIndex, int TotalChunks, int TurnCount, Guid BattleId, long Seed)>

// 9つの引数
_logger.LogBattleInfo(svc => svc.FormatPlayerInfo("Player1", " (Warrior)", 80, 100, "████████░░", 25, 15, 2, "(10,5)"));
// 型: LogMessage<(string PlayerName, string JobInfo, int CurrentHp, int MaxHp, string HealthBar, int Attack, int Defense, int Speed, string Position)>
```

## 型安全性の利点

### 1. コンパイル時の型チェック
```csharp
// ❌ コンパイルエラー - 型が一致しない
_logger.LogBattleInfo(svc => svc.FormatMemberJoined(123, "group")); // int, string
// 期待される型: (string ConnectionId, string GroupName)

// ✅ 正しい型
_logger.LogBattleInfo(svc => svc.FormatMemberJoined("conn123", "group456")); // string, string
```

### 2. IntelliSenseサポート
```csharp
var logMessage = service.FormatMemberJoined("conn123", "group456");
// logMessage.Args.ConnectionId  <- IntelliSenseで利用可能
// logMessage.Args.GroupName     <- IntelliSenseで利用可能
```

### 3. リファクタリング安全性
```csharp
// メソッドシグネチャを変更した場合、コンパイラが自動的に検出
// 従来版: object?[]では実行時まで型不一致が検出されない
// 型安全版: コンパイル時に型不一致を検出
```

## パフォーマンス比較

### メモリ使用量
```csharp
// 従来版: object[]のヒープ割り当て
object?[] args = [connectionId, groupName]; // ヒープ割り当て

// 型安全版: ValueTuple（スタック割り当て）
(string ConnectionId, string GroupName) args = (connectionId, groupName); // スタック割り当て
```

### GC圧迫の軽減
- ValueTupleはスタック上に配置されるため、GCの対象にならない
- object[]は必ずヒープ割り当てが発生する

## 移行ガイド

### 段階的移行
1. **新しいサービスを並行導入**
2. **既存コードとの共存**
3. **段階的置き換え**
4. **旧サービスの削除**

### 移行例
```csharp
// Before (従来版)
using CliClient.Extensions;
_logger.LogBattleInfo(svc => svc.FormatMemberJoined(data.ConnectionId, data.GroupName));

// After (型安全版)
using CliClient.Extensions; // TypeSafeBattleLoggerExtensionsに変更
_logger.LogBattleInfo(svc => svc.FormatMemberJoined(data.ConnectionId, data.GroupName));
```

## 実装の詳細

### LogMessage構造体
```csharp
// 引数ありバージョン
public readonly record struct LogMessage<T>
{
    public required string Message { get; init; }
    public required T Args { get; init; }
}

// 引数なしバージョン
public readonly record struct LogMessage
{
    public required string Message { get; init; }
    public static LogMessage Create(string message) => new() { Message = message };
}
```

### 型解決メカニズム
拡張メソッドは引数の型に基づいて適切なログ出力方法を選択：

1. **プリミティブ型**: 直接ログに渡す
2. **ValueTuple**: リフレクションでフィールドを抽出
3. **複合型**: フィールドを配列に変換してログに渡す
