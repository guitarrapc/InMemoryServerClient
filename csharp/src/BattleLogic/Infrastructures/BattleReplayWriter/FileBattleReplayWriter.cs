using BattleLogic.Models;
using Microsoft.Extensions.Logging;
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
    private string? _battleId;

    public FileBattleReplayWriter(BattleReplayOptions options, ILogger<FileBattleReplayWriter> logger)
    {
        _outputDirectory = options.FileOutputDirectory;
        _enableLogging = options.EnableLogging;
        _logger = logger;
    }

    public async Task InitializeAsync(string battleId)
    {
        _battleId = battleId;

        // Create directory if it doesn't exist
        Directory.CreateDirectory(_outputDirectory);

        // Create file for writing
        var filePath = Path.Combine(_outputDirectory, $"{battleId}.jsonl");
        _writer = new StreamWriter(filePath);

        if (_enableLogging)
        {
            _logger.LogInformation("Battle replay file writer initialized: {FilePath}", filePath);
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
            _logger.LogInformation("All battle frames written for battle {BattleId}", _battleId);
        }
    }

    public async Task<List<BattleStatus>> LoadReplayAsync(string battleId)
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
                var filePath = Path.Combine(_outputDirectory, $"{_battleId}.jsonl");
                _logger.LogInformation("Battle replay file writing completed: {FilePath}", filePath);
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
