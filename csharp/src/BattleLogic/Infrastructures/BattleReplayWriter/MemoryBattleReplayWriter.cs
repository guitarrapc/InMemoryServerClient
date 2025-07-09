using BattleLogic.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BattleLogic.Infrastructures.BattleReplayWriter;

/// <summary>
/// Memory-based implementation of IBattleReplayWriter
/// Stores battle replay data in memory for testing purposes
/// </summary>
internal class MemoryBattleReplayWriter : IBattleReplayWriter
{
    private readonly ILogger<MemoryBattleReplayWriter> _logger;
    private readonly bool _enableLogging;
    private readonly List<BattleStatus> _frames;
    private Guid? _battleId;
    private int? _seed;

    // Static storage to allow access from tests
    private static readonly ConcurrentDictionary<Guid, List<BattleStatus>> _battleReplays = new();

    public MemoryBattleReplayWriter(BattleReplayOptions options, ILogger<MemoryBattleReplayWriter> logger)
    {
        _enableLogging = options.EnableLogging;
        _logger = logger;
        _frames = new List<BattleStatus>();
    }

    /// <summary>
    /// Get stored replay data for a specific battle (for testing)
    /// </summary>
    public static List<BattleStatus>? GetStoredReplay(Guid battleId)
    {
        return _battleReplays.TryGetValue(battleId, out var replay) ? replay : null;
    }

    /// <summary>
    /// Clear all stored replay data (for testing cleanup)
    /// </summary>
    public static void ClearAllReplays()
    {
        _battleReplays.Clear();
    }

    /// <summary>
    /// Get all stored battle IDs (for testing)
    /// </summary>
    public static IEnumerable<Guid> GetStoredBattleIds()
    {
        return _battleReplays.Keys;
    }

    public Task InitializeAsync(Guid battleId, int seed)
    {
        _battleId = battleId;
        _seed = seed;
        _frames.Clear();

        if (_enableLogging)
        {
            _logger.LogDebug("Memory battle replay writer initialized - BattleId: {BattleId}, Seed: {Seed}", battleId, seed);
        }

        return Task.CompletedTask;
    }

    public Task WriteFrameAsync(BattleStatus frame)
    {
        if (!_battleId.HasValue)
            throw new InvalidOperationException("Writer not initialized. Call InitializeAsync first.");

        // Store a deep copy to avoid reference issues
        var frameCopy = new BattleStatus
        {
            BattleId = frame.BattleId,
            IsInProgress = frame.IsInProgress,
            CurrentTurn = frame.CurrentTurn,
            TotalTurns = frame.TotalTurns,
            Players = frame.Players.ToList(),
            Enemies = frame.Enemies.ToList(),
            FieldWidth = frame.FieldWidth,
            FieldHeight = frame.FieldHeight,
            RecentLogs = frame.RecentLogs.ToList(),
            IsPlayerVictory = frame.IsPlayerVictory
        };

        _frames.Add(frameCopy);
        return Task.CompletedTask;
    }

    public Task WriteAllFramesAsync(IEnumerable<BattleStatus> frames)
    {
        if (!_battleId.HasValue)
            throw new InvalidOperationException("Writer not initialized. Call InitializeAsync first.");

        _frames.Clear();
        foreach (var frame in frames)
        {
            // Store a deep copy to avoid reference issues
            var frameCopy = new BattleStatus
            {
                BattleId = frame.BattleId,
                IsInProgress = frame.IsInProgress,
                CurrentTurn = frame.CurrentTurn,
                TotalTurns = frame.TotalTurns,
                Players = frame.Players.ToList(),
                Enemies = frame.Enemies.ToList(),
                FieldWidth = frame.FieldWidth,
                FieldHeight = frame.FieldHeight,
                RecentLogs = frame.RecentLogs.ToList(),
                IsPlayerVictory = frame.IsPlayerVictory
            };

            _frames.Add(frameCopy);
        }

        if (_enableLogging)
        {
            _logger.LogDebug("All battle frames written for battle {BattleId}, {FrameCount} frames", _battleId.HasValue ? _battleId.Value : "Unknown", _frames.Count);
        }

        return Task.CompletedTask;
    }

    public Task FinalizeAsync()
    {
        if (!_battleId.HasValue)
            throw new InvalidOperationException("Writer not initialized. Call InitializeAsync first.");

        // Store the complete replay data
        _battleReplays[_battleId.Value] = new List<BattleStatus>(_frames);

        if (_enableLogging)
        {
            _logger.LogDebug("Memory battle replay completed for battle: {BattleId}, {FrameCount} frames stored",
                _battleId.Value, _frames.Count);
        }

        return Task.CompletedTask;
    }

    public Task<List<BattleStatus>> LoadReplayAsync(Guid battleId)
    {
        var replay = GetStoredReplay(battleId);
        return Task.FromResult(replay ?? new List<BattleStatus>());
    }

    public ValueTask DisposeAsync()
    {
        _frames.Clear();
        return ValueTask.CompletedTask;
    }
}
