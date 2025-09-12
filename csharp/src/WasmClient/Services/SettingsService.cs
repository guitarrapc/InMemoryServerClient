namespace WasmClient.Services;

public class SettingsService
{
    public string ServerUrl { get; set; } = "http://localhost";
    public int SignalRPort { get; set; } = 5000;
    public int MagicOnionPort { get; set; } = 5001;
    public bool ShowDebugInfo { get; set; } = false;
    public bool ShowHealthBars { get; set; } = true;
    public int FieldSize { get; set; } = 225;

    /// <summary>
    /// Get full SignalR URL with port
    /// </summary>
    public string SignalRUrl => $"{ServerUrl}:{SignalRPort}";

    /// <summary>
    /// Get full MagicOnion URL with port
    /// </summary>
    public string MagicOnionUrl => $"{ServerUrl}:{MagicOnionPort}";

    // Battle replay FPS settings
    private int _replayFps = 5;
    public int ReplayFps
    {
        get => _replayFps;
        set
        {
            if (_replayFps != value)
            {
                _replayFps = value;
                OnReplayFpsChanged?.Invoke(value);
            }
        }
    }
    public bool AutoReplay { get; set; } = false;
    public int MinReplayFps { get; } = 1;
    public int MaxReplayFps { get; } = 20;

    /// <summary>
    /// Event triggered when replay FPS changes
    /// </summary>
    public event Action<int>? OnReplayFpsChanged;

    /// <summary>
    /// Get replay frame time in milliseconds based on current FPS setting
    /// </summary>
    public int ReplayFrameTimeMs => 1000 / ReplayFps;

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
        ServerUrl = "http://localhost";
        SignalRPort = 5000;
        MagicOnionPort = 5001;
        ShowDebugInfo = false;
        ShowHealthBars = true;
        FieldSize = 225;
        ReplayFps = 5;
        AutoReplay = false;
    }
}
