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
