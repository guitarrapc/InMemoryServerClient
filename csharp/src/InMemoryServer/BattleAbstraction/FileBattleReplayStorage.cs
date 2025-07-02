using BattleLogic.Constans;
using Shared.Battle;
using System.Text.Json;

namespace InMemoryServer.BattleAbstraction;

/// <summary>
/// File-based implementation of IBattleReplayStorage
/// </summary>
public class FileBattleReplayStorage : IBattleReplayStorage
{
    private readonly string _replayDirectory;
    private readonly ILogger<FileBattleReplayStorage> _logger;

    public FileBattleReplayStorage(ILogger<FileBattleReplayStorage> logger, string replayDirectory = BattleSystemDefines.BattleReplayDirectory)
    {
        _logger = logger;
        _replayDirectory = replayDirectory;

        // Ensure the replay directory exists
        Directory.CreateDirectory(_replayDirectory);
    }

    /// <summary>
    /// Save battle replay data to file
    /// </summary>
    public async Task SaveBattleReplayAsync(string battleId, IEnumerable<string> replayData)
    {
        try
        {
            var filePath = Path.Combine(_replayDirectory, $"{battleId}.jsonl");

            // Write each JSON line to the file
            await using var writer = new StreamWriter(filePath);
            foreach (var jsonLine in replayData)
            {
                await writer.WriteLineAsync(jsonLine);
            }

            _logger.LogInformation("Battle replay saved for battle {BattleId} to {FilePath}", battleId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save battle replay for battle {BattleId}", battleId);
            throw;
        }
    }

    /// <summary>
    /// Load battle replay data from file
    /// </summary>
    public async Task<List<BattleStatus>> LoadReplayAsync(string battleId)
    {
        try
        {
            var filePath = Path.Combine(_replayDirectory, $"{battleId}.jsonl");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Battle replay file not found for battle {BattleId}", battleId);
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

            _logger.LogInformation("Battle replay loaded for battle {BattleId}, {Count} entries", battleId, replayData.Count);
            return replayData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load battle replay for battle {BattleId}", battleId);
            throw;
        }
    }
}
