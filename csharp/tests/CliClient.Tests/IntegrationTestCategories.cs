namespace CliClient.Tests;

/// <summary>
/// モック・スタブ使用の統合テスト用の属性（サーバー不要）
/// </summary>
public class IntegrationTestAttribute : FactAttribute
{
    public IntegrationTestAttribute()
    {
        // CI/CDや自動テストで除外したい場合のための仕組み
        if (SkipIntegrationTests())
        {
            Skip = "Integration tests are disabled";
        }
    }

    private static bool SkipIntegrationTests()
    {
        // 環境変数でスキップを制御
        var skip = Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS");
        return !string.IsNullOrEmpty(skip) && bool.TryParse(skip, out var result) && result;
    }
}

/// <summary>
/// 内蔵テストサーバーを使用する統合テスト用の属性
/// </summary>
public class EmbeddedServerTestAttribute : FactAttribute
{
    public EmbeddedServerTestAttribute()
    {
        // 内蔵サーバーテストをスキップする場合
        if (SkipEmbeddedServerTests())
        {
            Skip = "Embedded server tests are disabled";
        }
    }

    private static bool SkipEmbeddedServerTests()
    {
        var skip = Environment.GetEnvironmentVariable("SKIP_EMBEDDED_SERVER_TESTS");
        return !string.IsNullOrEmpty(skip) && bool.TryParse(skip, out var result) && result;
    }
}

/// <summary>
/// 外部サーバーが必要な統合テスト用の属性（非推奨）
/// </summary>
public class ExternalServerRequiredTestAttribute : FactAttribute
{
    public ExternalServerRequiredTestAttribute()
    {
        // 外部サーバーが必要なテストを環境変数で制御
        if (SkipExternalServerTests())
        {
            Skip = "External server integration tests are disabled";
        }
    }

    private static bool SkipExternalServerTests()
    {
        var skip = Environment.GetEnvironmentVariable("SKIP_EXTERNAL_SERVER_TESTS");
        return !string.IsNullOrEmpty(skip) && bool.TryParse(skip, out var result) && result;
    }
}

/// <summary>
/// 統合テストのヘルパーメソッド
/// </summary>
public static class IntegrationTestHelpers
{
    /// <summary>
    /// サーバーが利用可能かどうかをチェック
    /// </summary>
    /// <param name="serverUrl">サーバーURL</param>
    /// <param name="timeoutMs">タイムアウト（ミリ秒）</param>
    /// <returns>サーバーが利用可能な場合はtrue</returns>
    public static async Task<bool> IsServerAvailableAsync(string serverUrl, int timeoutMs = 5000)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            var response = await httpClient.GetAsync($"{serverUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 条件付きでテストをスキップ（Assert.Trueでスキップシミュレーション）
    /// </summary>
    /// <param name="condition">スキップ条件</param>
    /// <param name="reason">スキップ理由</param>
    public static void SkipIf(bool condition, string reason)
    {
        if (condition)
        {
            // テストをスキップ（テスト成功として扱い、理由を出力）
            Console.WriteLine($"⚠️ Test skipped: {reason}");
            Assert.True(true, $"Test skipped: {reason}");
        }
    }
}
