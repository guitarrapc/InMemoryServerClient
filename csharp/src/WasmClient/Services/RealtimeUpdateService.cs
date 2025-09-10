using System.Timers;

namespace WasmClient.Services;

/// <summary>
/// Service for managing real-time field updates with proper timing
/// </summary>
public class RealtimeUpdateService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly System.Timers.Timer _updateTimer;
    private readonly List<Action> _updateCallbacks = new();
    private readonly ILogger<RealtimeUpdateService> _logger;

    public event Action? OnUpdateTick;

    public RealtimeUpdateService(SettingsService settings, ILogger<RealtimeUpdateService> logger)
    {
        _settings = settings;
        _updateTimer = new System.Timers.Timer(_settings.ReplayFrameTimeMs);
        _updateTimer.Elapsed += OnTimerElapsed;
        _updateTimer.AutoReset = true;
        _logger = logger;

        // Subscribe to FPS changes
        _settings.OnReplayFpsChanged += OnReplayFpsChanged;
    }

    private void OnReplayFpsChanged(int newFps)
    {
        UpdateTimerInterval();
    }

    /// <summary>
    /// Start the real-time update timer with current FPS setting
    /// </summary>
    public void StartUpdates()
    {
        // Update timer interval with current FPS setting
        _updateTimer.Interval = _settings.ReplayFrameTimeMs;
        _updateTimer.Start();
    }

    /// <summary>
    /// Stop the real-time update timer
    /// </summary>
    public void StopUpdates()
    {
        _updateTimer.Stop();
    }

    /// <summary>
    /// Update timer interval based on current FPS settings
    /// </summary>
    public void UpdateTimerInterval()
    {
        var wasRunning = _updateTimer.Enabled;
        if (wasRunning)
        {
            _updateTimer.Stop();
        }

        _updateTimer.Interval = _settings.ReplayFrameTimeMs;

        if (wasRunning)
        {
            _updateTimer.Start();
        }
    }

    /// <summary>
    /// Add a callback to be executed on each update tick
    /// </summary>
    /// <param name="callback">Callback to execute</param>
    public void AddUpdateCallback(Action callback)
    {
        _updateCallbacks.Add(callback);
    }

    /// <summary>
    /// Remove a callback from update tick execution
    /// </summary>
    /// <param name="callback">Callback to remove</param>
    public void RemoveUpdateCallback(Action callback)
    {
        _updateCallbacks.Remove(callback);
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        OnUpdateTick?.Invoke();

        // Execute all registered callbacks
        foreach (var callback in _updateCallbacks.ToList())
        {
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                // Log error but continue processing other callbacks
                _logger.LogError(ex, $"Error in update callback");
            }
        }
    }

    public void Dispose()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateCallbacks.Clear();

        // Unsubscribe from settings changes
        _settings.OnReplayFpsChanged -= OnReplayFpsChanged;
    }
}
