namespace WasmClient.Services;

public class SettingsService
{
    public string SignalRUrl { get; set; } = "http://localhost:5000";
    public string MagicOnionUrl { get; set; } = "http://localhost:5001";
    public bool ShowDebugInfo { get; set; } = false;
    public int FieldSize { get; set; } = 200;

    public async Task LoadAsync()
    {
        // LocalStorage から設定を読み込み (将来実装)
        await Task.CompletedTask;
    }

    public async Task SaveAsync()
    {
        // LocalStorage に設定を保存 (将来実装)
        await Task.CompletedTask;
    }

    public void Reset()
    {
        SignalRUrl = "http://localhost:5000";
        MagicOnionUrl = "http://localhost:5001";
        ShowDebugInfo = false;
        FieldSize = 200;
    }
}
