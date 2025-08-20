# Kestrel統一ポート構成ガイド

## 概要

GameLift Anywhereの1ポート制約に対応するため、KestrelでHTTP/1（SignalR）とHTTP/2（MagicOnion）を同一ポートで動作させる構成を解説します。

## 技術的制約

### Kestrelの同一ポート要件
- **TLS（HTTPS）必須**: HTTP/1とHTTP/2の同時サポートにはTLSが必要
- **ALPN**: TLSハンドシェイク時のプロトコル交渉で自動選択
- **証明書**: 開発環境では開発証明書、本番環境では正式な証明書が必要

### GameLift Anywhereの制約
- 1サーバープロセスあたり1ポートのみ登録可能
- クライアントは登録されたポートを通じて接続

## 推奨構成

### 1. HTTPS統一ポート構成（推奨）

```json
{
  "Kestrel": {
    "Endpoints": {
      "Unified": {
        "Url": "https://localhost:5001",
        "Protocols": "Http1AndHttp2",
        "Certificate": {
          "Subject": "localhost",
          "Store": "My",
          "Location": "CurrentUser",
          "AllowInvalid": true
        }
      }
    }
  },
  "GameLift": {
    "Anywhere": {
      "WebSocketUrl": "wss://localhost:5001/battlehub"
    }
  }
}
```

**特徴:**
- ✅ GameLiftに5001ポートのみ登録
- ✅ SignalR（WebSocket over TLS）とMagicOnion（HTTP/2 over TLS）が共存
- ✅ ALPN による自動プロトコル選択
- ⚠️ TLS証明書の設定が必要

### 2. 開発環境用の簡単設定

```json
{
  "Kestrel": {
    "Endpoints": {
      "Unified": {
        "Url": "https://localhost:5001",
        "Protocols": "Http1AndHttp2"
      }
    }
  },
  "GameLift": {
    "Anywhere": {
      "WebSocketUrl": "wss://localhost:5001/battlehub"
    }
  }
}
```

**特徴:**
- ✅ ASP.NET Core開発証明書を自動使用
- ✅ 設定が最小限
- ⚠️ 開発環境のみ推奨

## 証明書設定

### 開発環境
```bash
# 開発証明書の作成（初回のみ）
dotnet dev-certs https --trust
```

### 本番環境
- Let's Encrypt
- AWS Certificate Manager
- 社内CA証明書
- 購入したSSL証明書

## クライアント接続設定

### SignalRクライアント
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("wss://localhost:5001/battlehub")  // HTTPS WebSocket
    .Build();
```

### MagicOnionクライアント
```csharp
var channel = GrpcChannel.ForAddress("https://localhost:5001");  // HTTP/2 over TLS
var client = MagicOnionClient.Create<IBattleHub>(channel);
```

## 実装での注意点

### GameLiftAnywhereHostedService
```csharp
private ProcessParameters CreateProcessParameters()
{
    return new ProcessParameters(
        // GameLiftに登録するポートは5001のみ
        new[] { 5001 },
        _options.Anywhere.WebSocketUrl
    );
}
```

### エンドポイント設定の検証
```csharp
public static bool ValidateEndpointConfiguration(IConfiguration configuration)
{
    var kestrelSection = configuration.GetSection("Kestrel:Endpoints");
    var endpoints = kestrelSection.GetChildren();

    // HTTPSエンドポイントがあるか確認
    return endpoints.Any(endpoint =>
        endpoint.GetValue<string>("Url")?.StartsWith("https://") == true);
}
```

## トラブルシューティング

### よくある問題

1. **証明書エラー**
   - 症状: SSL/TLS接続エラー
   - 対処: `dotnet dev-certs https --trust` で開発証明書をインストール

2. **ALPN交渉失敗**
   - 症状: HTTP/2クライアントがHTTP/1.1で接続される
   - 対処: クライアント側でHTTP/2サポートを確認

3. **ポート競合**
   - 症状: サーバー起動時にポートエラー
   - 対処: 他のプロセスがポートを使用していないか確認

### デバッグコマンド
```bash
# ポートの使用状況確認
netstat -an | findstr :5001

# 証明書の確認
certlm.msc  # Windows証明書マネージャー

# TLS接続テスト
curl -v https://localhost:5001/battlehub
```

## パフォーマンス考慮事項

### TLSオーバーヘッド
- 初期ハンドシェイクのコスト
- CPU使用率の増加（暗号化・復号化）
- TLS 1.3の使用を推奨（パフォーマンス向上）

### HTTP/2の利点
- 多重化による効率向上
- バイナリプロトコルによる高速化
- サーバープッシュ対応

## まとめ

GameLift Anywhereの1ポート制約下で最適な構成は：

1. **HTTPS統一ポート（5001）**を使用
2. **Protocols: "Http1AndHttp2"**でプロトコル自動選択
3. **開発環境では開発証明書**、**本番環境では正式証明書**を使用
4. **SignalRとMagicOnion**が同一ポートで共存

この構成により、GameLiftの制約に準拠しながら、両プロトコルをサポートする効率的なサーバーを実現できます。
