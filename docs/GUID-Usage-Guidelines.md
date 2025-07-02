# GUID使用ガイドライン

## 概要

InMemoryServerClientプロジェクトでは、バトル再現性とシステム管理の両立を図るため、用途に応じて適切なGUID生成方式を使い分けています。

## 用途別GUID選択戦略

### 1. エンティティID（決定論的GUID v4）

**対象**: バトル内の全エンティティ（プレイヤー、敵）

#### 使用方法
```csharp
var battleSeed = new BattleSeed(battleId);
var entityId = battleSeed.NextEntityId(); // 決定論的GUID v4
```

#### 特徴
- **再現性**: 同一シードで完全に再現可能
- **順序依存**: 呼び出し順序に依存（order-dependent）
- **フォーマット**: GUID v4（version = 4）
- **用途**: バトルリプレイ、デバッグ、テストでの同一結果保証

#### 実装詳細
- スレッドセーフ対応（ロック使用）
- RFC 4122準拠のGUID v4フォーマット
- 決定論的Random生成（battleIdベースのシード）

### 2. タイムスタンプID（GUID v7）

**対象**: バトルID、グループID、ログエントリ、システムイベント

#### 使用方法
```csharp
// 静的メソッド（推奨）
var timestampId = BattleSeed.NewTimestampId();

// または標準的な.NETメソッド
var timestampId = Guid.CreateVersion7();
```

#### 特徴
- **時系列**: ミリ秒精度のタイムスタンプベース
- **順序保証**: 自然な時系列順序
- **パフォーマンス**: データベースパフォーマンス最適化
- **フォーマット**: GUID v7（version = 7）
- **用途**: ログ管理、監査、データベースインデックス

#### 実装詳細
- .NET 9の`Guid.CreateVersion7()`を使用
- 48ビットタイムスタンプ + 12ビットランダム + バージョン/バリアント
- ソート可能な自然順序

## 実装ガイドライン

### ✅ 正しい使用パターン

```csharp
// ✅ エンティティID生成
var battleSeed = new BattleSeed(battleId);
var player = new EntityInfo
{
    Id = battleSeed.NextEntityId().ToString(), // 決定論的
    // ...
};

// ✅ バトルID生成
var battleId = BattleSeed.NewTimestampId().ToString(); // タイムスタンプベース

// ✅ グループID生成
var groupId = Guid.CreateVersion7().ToString(); // タイムスタンプベース
```

### ❌ 避けるべきパターン

```csharp
// ❌ エンティティIDにタイムスタンプID使用（再現性が失われる）
var player = new EntityInfo
{
    Id = Guid.NewGuid().ToString(), // 非決定論的
    // ...
};

// ❌ バトルIDに決定論的GUID使用（時系列管理に不適切）
var battleId = battleSeed.NextEntityId().ToString(); // 順序保証なし
```

### 順序依存性の管理

エンティティID生成では呼び出し順序が重要です：

```csharp
// ✅ 一貫した順序での生成
public List<EntityInfo> InitializePlayers(int count)
{
    var players = new List<EntityInfo>();
    for (int i = 0; i < count; i++) // 一貫した順序
    {
        players.Add(CreatePlayer(i)); // 内部でNextEntityId()使用
    }
    return players;
}

// ❌ 並列処理での非決定論的順序
Parallel.For(0, count, i => // 順序が保証されない
{
    players.Add(CreatePlayer(i)); // 再現性が失われる
});
```

## バージョン検証

GUIDのバージョンを検証する際の実装：

```csharp
public static int ExtractGuidVersion(Guid guid)
{
    var bytes = guid.ToByteArray();

    // Little-endianのバイト配列での位置
    var version6 = (bytes[6] & 0xF0) >> 4; // GUID v4の場合
    var version7 = (bytes[7] & 0xF0) >> 4; // GUID v7の場合

    // 有効なバージョンを返す
    return version6 is 4 or 7 ? version6 : version7;
}
```

## テスト戦略

### 決定論性テスト

```csharp
[Fact]
public void EntityId_ShouldBeReproducible()
{
    const string battleId = "test-battle";
    var seed1 = new BattleSeed(battleId);
    var seed2 = new BattleSeed(battleId);

    var id1 = seed1.NextEntityId();
    var id2 = seed2.NextEntityId();

    Assert.Equal(id1, id2); // 同一シードで同一結果
    Assert.Equal(4, ExtractGuidVersion(id1)); // GUID v4確認
}
```

### 時系列性テスト

```csharp
[Fact]
public void TimestampId_ShouldHaveTimeOrdering()
{
    var id1 = BattleSeed.NewTimestampId();
    Thread.Sleep(10); // 時間経過
    var id2 = BattleSeed.NewTimestampId();

    Assert.True(id1.CompareTo(id2) < 0); // 時系列順序
    Assert.Equal(7, ExtractGuidVersion(id1)); // GUID v7確認
}
```

## パフォーマンス考慮事項

### メモリ効率
- エンティティIDは一度生成後、文字列として保存
- 大量生成時のGC負荷を考慮したバッチ処理
- 不要になったGUIDの適切なクリアアップ

### 計算コスト
- 決定論的生成：暗号学的ランダムより高速
- タイムスタンプ生成：システムクロック依存、高精度

## まとめ

このGUID使用戦略により、以下を実現しています：

1. **バトル再現性**: 決定論的エンティティIDによる完全な再現可能性
2. **時系列管理**: タイムスタンプIDによる自然な順序保証
3. **パフォーマンス**: 用途に最適化されたGUID生成方式
4. **保守性**: 明確な使い分けルールと検証可能な実装

新機能実装や既存コード修正時は、このガイドラインに従って適切なGUID生成方式を選択してください。
