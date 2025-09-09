namespace WasmClient.Services;

/// <summary>
/// Performance monitoring service for battle field visualization
/// </summary>
public class FieldPerformanceMonitor
{
    private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
    private readonly Dictionary<string, int> _frameRates = new();
    private readonly Dictionary<string, int> _entityCounts = new();

    /// <summary>
    /// Record an update for a specific battle field
    /// </summary>
    /// <param name="fieldId">Unique identifier for the field</param>
    /// <param name="entityCount">Number of entities in the field</param>
    public void RecordUpdate(string fieldId, int entityCount)
    {
        var now = DateTime.Now;

        _entityCounts[fieldId] = entityCount;

        if (_lastUpdateTimes.TryGetValue(fieldId, out var lastUpdate))
        {
            var timeDiff = now - lastUpdate;
            if (timeDiff.TotalMilliseconds > 0)
            {
                var fps = (int)(1000 / timeDiff.TotalMilliseconds);
                _frameRates[fieldId] = Math.Min(fps, 60); // Cap at 60 FPS
            }
        }

        _lastUpdateTimes[fieldId] = now;
    }

    /// <summary>
    /// Get performance statistics for a specific field
    /// </summary>
    /// <param name="fieldId">Field identifier</param>
    /// <returns>Performance stats</returns>
    public FieldPerformanceStats GetStats(string fieldId)
    {
        return new FieldPerformanceStats
        {
            FieldId = fieldId,
            CurrentFPS = _frameRates.GetValueOrDefault(fieldId, 0),
            EntityCount = _entityCounts.GetValueOrDefault(fieldId, 0),
            LastUpdate = _lastUpdateTimes.GetValueOrDefault(fieldId, DateTime.MinValue)
        };
    }

    /// <summary>
    /// Get overall performance summary
    /// </summary>
    /// <returns>Performance summary</returns>
    public PerformanceSummary GetSummary()
    {
        var activeBattles = _frameRates.Count;
        var avgFPS = _frameRates.Values.Any() ? (int)_frameRates.Values.Average() : 0;
        var totalEntities = _entityCounts.Values.Sum();
        var minFPS = _frameRates.Values.Any() ? _frameRates.Values.Min() : 0;

        return new PerformanceSummary
        {
            ActiveBattles = activeBattles,
            AverageFPS = avgFPS,
            MinimumFPS = minFPS,
            TotalEntities = totalEntities,
            IsPerformanceGood = minFPS >= 4 // Close to target 5 FPS
        };
    }

    /// <summary>
    /// Clean up old field data
    /// </summary>
    /// <param name="fieldId">Field identifier to remove</param>
    public void RemoveField(string fieldId)
    {
        _lastUpdateTimes.Remove(fieldId);
        _frameRates.Remove(fieldId);
        _entityCounts.Remove(fieldId);
    }

    /// <summary>
    /// Clear all performance data
    /// </summary>
    public void Clear()
    {
        _lastUpdateTimes.Clear();
        _frameRates.Clear();
        _entityCounts.Clear();
    }
}

/// <summary>
/// Performance statistics for a single battle field
/// </summary>
public record FieldPerformanceStats
{
    public string FieldId { get; init; } = string.Empty;
    public int CurrentFPS { get; init; }
    public int EntityCount { get; init; }
    public DateTime LastUpdate { get; init; }
}

/// <summary>
/// Overall performance summary for all battle fields
/// </summary>
public record PerformanceSummary
{
    public int ActiveBattles { get; init; }
    public int AverageFPS { get; init; }
    public int MinimumFPS { get; init; }
    public int TotalEntities { get; init; }
    public bool IsPerformanceGood { get; init; }
}
