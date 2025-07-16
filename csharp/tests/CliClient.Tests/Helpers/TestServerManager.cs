using InMemoryServer.Http1Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CliClient.Tests.Helpers;

/// <summary>
/// テスト用サーバーの管理とヘルパーメソッド
/// 実際のHTTPサーバーインスタンスを起動して、外部クライアントからの接続を可能にする
/// </summary>
public class TestServerManager : IDisposable
{
    private IHost? _host;
    private HttpClient? _httpClient;

    public string ServerUrl { get; private set; } = string.Empty;

    /// <summary>
    /// テスト用サーバーを起動し、URLを取得
    /// </summary>
    public void StartServer()
    {
        // 利用可能なポートを見つける
        var port = GetAvailablePort();
        ServerUrl = $"http://127.0.0.1:{port}";

        Console.WriteLine($"Starting test server on port {port}");

        // 実際のHostインスタンスを作成して起動
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(ServerUrl);
                webBuilder.UseEnvironment("Testing");
                webBuilder.ConfigureAppConfiguration((context, config) =>
                {
                    // 設定ファイルによる上書きを防ぐため、明示的に設定をクリア
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"Kestrel:Endpoints:Http:Url", ServerUrl}
                    });
                });
                webBuilder.ConfigureServices(services =>
                {
                    // Add the same services as in Program.RunServerAsync
                    services.AddSignalR();
                    services.AddMagicOnion();
                    services.AddSingleton<InMemoryServer.InMemoryState>();
                    services.AddSingleton<InMemoryServer.Services.ConnectionManager>();
                    services.AddSingleton<InMemoryServer.Services.GroupManager>();
                    services.AddSingleton<InMemoryServer.Services.MagicOnionGroupService>();
                    services.AddSingleton<InMemoryServer.Services.CrossProtocolNotificationService>();
                    services.AddSingleton<InMemoryHub>();
                    services.AddSingleton<BattleLogic.Infrastructures.BattleReplayWriter.BattleReplayWriterFactory>();

                    // Configure test logging with reduced verbosity
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Warning); // テストノイズを減らすため
                    });
                });
                webBuilder.Configure(app =>
                {
                    // Configure the same pipeline as in Program.RunServerAsync
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHub<InMemoryHub>(Shared.Constants.SystemDefines.HubRoute);
                        endpoints.MapMagicOnionService();
                        endpoints.MapGet("/health", () => "Healthy");
                    });
                });
            });

        _host = builder.Build();

        // サーバーをバックグラウンドで起動
        _ = Task.Run(async () =>
        {
            try
            {
                await _host.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test server error: {ex.Message}");
            }
        });

        // サーバーが起動するまで少し待つ
        Thread.Sleep(1000);

        // HTTPクライアントを作成
        _httpClient = new HttpClient();

        Console.WriteLine($"Test server started at: {ServerUrl}");
    }

    /// <summary>
    /// 利用可能なポートを取得
    /// </summary>
    private static int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// テスト用HTTPクライアントを作成
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        if (_httpClient == null)
            throw new InvalidOperationException("Server not started. Call StartServer() first.");

        return _httpClient;
    }

    /// <summary>
    /// サーバーが利用可能かチェック
    /// </summary>
    public async Task<bool> IsServerAvailableAsync()
    {
        if (string.IsNullOrEmpty(ServerUrl) || _httpClient == null)
            return false;

        try
        {
            var response = await _httpClient.GetAsync($"{ServerUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            _httpClient?.Dispose();
            _host?.StopAsync(TimeSpan.FromSeconds(5)).Wait();
            _host?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing test server: {ex.Message}");
        }
    }
}
