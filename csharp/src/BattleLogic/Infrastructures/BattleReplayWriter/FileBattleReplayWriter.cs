using BattleLogic.Models;
using Microsoft.Extensions.Logging;
using Shared.BattleLogic.Models;
using System.Text.Json;

namespace BattleLogic.Infrastructures.BattleReplayWriter;

/// <summary>
/// File-based implementation of IBattleReplayWriter
/// Writes battle replay data to JSON Lines (.jsonl) files
/// </summary>
internal class FileBattleReplayWriter : IBattleReplayWriter
{
    private readonly string _outputDirectory;
    private readonly ILogger<FileBattleReplayWriter> _logger;
    private readonly bool _enableLogging;
    private StreamWriter? _writer;
    private Guid? _battleId;
    private int? _seed;

    public FileBattleReplayWriter(BattleReplayOptions options, ILogger<FileBattleReplayWriter> logger)
    {
        _outputDirectory = options.FileOutputDirectory;
        _enableLogging = options.EnableLogging;
        _logger = logger;
    }

    public async Task InitializeAsync(Guid battleId, int seed)
    {
        _battleId = battleId;
        _seed = seed;

        // Create directory if it doesn't exist
        Directory.CreateDirectory(_outputDirectory);

        // Create file for writing with timestamp in filename for uniqueness
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var filePath = Path.Combine(_outputDirectory, $"battle_{battleId}_{timestamp}.jsonl");
        _writer = new StreamWriter(filePath);

        // Write metadata as the first line
        var metadata = new WriterMetadata(battleId, seed, DateTime.UtcNow);
        var metadataJson = JsonSerializer.Serialize(metadata);
        await _writer.WriteLineAsync(metadataJson);

        if (_enableLogging)
        {
            _logger.LogInformation("Battle replay file writer initialized - BattleId: {BattleId}, Seed: {Seed}, FilePath: {FilePath}", battleId, seed, filePath);
        }
    }

    public async Task WriteFrameAsync(BattleStatus frame)
    {
        if (_writer == null)
        {
            throw new InvalidOperationException("Writer not initialized. Call InitializeAsync first.");
        }

        var json = JsonSerializer.Serialize(frame);
        await _writer.WriteLineAsync(json);
        await _writer.FlushAsync();
    }

    public async Task WriteAllFramesAsync(IEnumerable<BattleStatus> frames)
    {
        if (_writer == null)
        {
            throw new InvalidOperationException("Writer not initialized. Call InitializeAsync first.");
        }

        foreach (var frame in frames)
        {
            var json = JsonSerializer.Serialize(frame);
            await _writer.WriteLineAsync(json);
        }
        await _writer.FlushAsync();

        if (_enableLogging && _battleId != null)
        {
            _logger.LogInformation("All battle frames written - BattleId: {BattleId}, Seed: {Seed}", _battleId, _seed);
        }
    }

    public async Task<List<BattleStatus>> LoadReplayAsync(Guid battleId)
    {
        try
        {
            var filePath = Path.Combine(_outputDirectory, $"{battleId}.jsonl");

            if (!File.Exists(filePath))
            {
                if (_enableLogging)
                {
                    _logger.LogWarning("Battle replay file not found for battle {BattleId}", battleId);
                }
                return new List<BattleStatus>();
            }

            var replayData = new List<BattleStatus>();
            var lines = await File.ReadAllLinesAsync(filePath);

            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var status = JsonSerializer.Deserialize<BattleStatus>(line);
                    if (status != null)
                    {
                        replayData.Add(status);
                    }
                }
            }

            if (_enableLogging)
            {
                _logger.LogInformation("Battle replay loaded for battle {BattleId}, {Count} entries", battleId, replayData.Count);
            }
            return replayData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load battle replay for battle {BattleId}", battleId);
            throw;
        }
    }

    public async Task FinalizeAsync()
    {
        if (_writer != null)
        {
            await _writer.FlushAsync();

            if (_enableLogging && _battleId != null)
            {
                var filePath = Path.Combine(_outputDirectory, $"battle_{_battleId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jsonl");
                _logger.LogInformation("Battle replay file writing completed - BattleId: {BattleId}, Seed: {Seed}, FilePath: {FilePath}", _battleId, _seed, filePath);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_writer != null)
        {
            await _writer.DisposeAsync();
            _writer = null;
        }
    }
}
