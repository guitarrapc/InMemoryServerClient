using System.Timers;
using WasmClient.Constants;

namespace WasmClient.Services;

/// <summary>
/// Service for managing real-time field updates with proper timing
/// </summary>
public class RealtimeUpdateService : IDisposable
{
    private readonly System.Timers.Timer _updateTimer;
    private readonly List<Action> _updateCallbacks = new();

    public event Action? OnUpdateTick;

    public RealtimeUpdateService()
    {
        _updateTimer = new System.Timers.Timer(BattleReplayDefines.ReplayFrameTimeMs);
        _updateTimer.Elapsed += OnTimerElapsed;
        _updateTimer.AutoReset = true;
    }

    /// <summary>
    /// Start the real-time update timer
    /// </summary>
    public void StartUpdates()
    {
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
                Console.WriteLine($"Error in update callback: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateCallbacks.Clear();
    }
}
