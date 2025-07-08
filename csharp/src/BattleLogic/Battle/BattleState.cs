using BattleLogic.Constans;
using BattleLogic.Infrastructures.BattleReplayWriter;
using BattleLogic.Models;
using BattleLogic.Services;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using System.Collections.Concurrent;

namespace BattleLogic.Battle;

/// <summary>
/// Represents a battle state
/// </summary>
public class BattleState
{
    public enum State
    {
        Connected = 0,
        Ready,
        ReplayCompleted,
    }

    private readonly string _battleId;
    private readonly IBattleGroupContext _group;
    private readonly BattleSeed _battleSeed;
    private readonly List<EntityInfo> _players = new(5); // Pre-allocate for max players
    private readonly List<EntityInfo> _enemies = new(15); // Pre-allocate for max enemies
    private readonly List<string> _battleLogs = new(60); // Pre-allocate for battle logs with limit
    private readonly ILogger<BattleState> _logger;
    private readonly ConcurrentDictionary<string, State> _clients = new();

    // Battle components
    private readonly BattleField _battleField;
    private readonly BattleInitializer _battleInitializer;
    private readonly BattleAI _battleAI;
    private readonly BattleMovement _battleMovement;
    private readonly BattleCombat _battleCombat;
    private readonly BattleUtilities _battleUtilities;

    private readonly BattleReplayWriterFactory _replayWriterFactory;

    private int _currentTurn = 0;
    private int _totalTurns;
    private bool _isCompleted = false;
    private bool _playerVictory = false; // Player victory flag
    private int _connectedClientsCount = 0;
    private int _readyClientsCount = 0;

    /// <summary>
    /// Gets the group ID associated with this battle
    /// </summary>
    public string GroupId => _group.Id;

    /// <summary>
    /// Gets the battle seed used for reproducible random generation
    /// </summary>
    public BattleSeed BattleSeed => _battleSeed;

    /// <summary>
    /// Gets the battle ID
    /// </summary>
    public string BattleId => _battleId;

    /// <summary>
    /// Gets the time when the battle was started
    /// </summary>
    public DateTime StartTime { get; } = DateTime.UtcNow;

    public BattleState(string battleId, IBattleGroupContext group, ILogger<BattleState> logger, BattleReplayWriterFactory replayWriterFactory)
    {
        _battleId = battleId;
        _group = group;
        _logger = logger;
        _replayWriterFactory = replayWriterFactory;

        // Use battleId to generate seed if no explicit seed is provided
        _battleSeed = new BattleSeed(battleId);

        // Log the seed for reproducibility
        _logger.LogInformation("Battle {BattleId} initialized with seed {Seed}. " +
            "To reproduce this battle, use battleId: {BattleId} or seed: {Seed}",
            battleId, _battleSeed.Seed, battleId, _battleSeed.Seed);

        // Initialize battle components with deterministic random
        _battleField = new BattleField(_battleSeed.Random);
        _battleUtilities = new BattleUtilities();
        _battleInitializer = new BattleInitializer(_battleSeed);
        _battleAI = new BattleAI(_battleUtilities, logger);
        _battleMovement = new BattleMovement(_battleSeed.Random, _battleField, _battleUtilities);
        _battleCombat = new BattleCombat(_battleSeed.Random, _battleField, _battleUtilities);

        // Store client IDs from the group
        foreach (var clientId in group.ClientIds)
        {
            _clients.AddOrUpdate(clientId, State.Connected, (_, _) => State.Connected);
            Interlocked.Increment(ref _connectedClientsCount);
        }

        // Initialize battle
        InitializeBattle();
    }

    /// <summary>
    /// Initialize the battle state
    /// </summary>
    private void InitializeBattle()
    {
        // Add initial battle log
        _battleLogs.Add($"Battle started!");

        // Initialize players and enemies using the initializer
        var players = _battleInitializer.InitializePlayers(_group.ConnectedCount, _battleLogs);
        var enemies = _battleInitializer.InitializeEnemies(_battleLogs);

        _players.AddRange(players);
        _enemies.AddRange(enemies);

        // Set total turns for battle
        _totalTurns = _battleSeed.Random.Next(BattleSystemDefines.MinBattleTurns, BattleSystemDefines.MaxBattleTurns + 1);

        // Place entities on battle field
        _battleField.PlaceEntities(_players, _enemies);

        _battleLogs.Add($"Battle initialized with {_players.Count} players and {_enemies.Count} enemies!");
    }

    /// <summary>
    /// Process a single turn of battle
    /// </summary>
    private async Task ProcessTurnAsync()
    {
        _battleLogs.Add($"Turn {_currentTurn} begins!");

        // Reset defending status for all entities
        _battleUtilities.ResetDefendingStatus(_players, _enemies);

        // Get all entities ordered by speed (descending) for turn order
        var entities = _players.Where(p => p.CurrentHp > 0)
            .Concat(_enemies.Where(e => e.CurrentHp > 0))
            .OrderByDescending(e => e.Speed)
            .ToList();

        // Process each entity's turn
        foreach (var entity in entities)
        {
            // Skip if entity died during this turn
            if (entity.CurrentHp <= 0) continue;

            // Decide action using AI
            var (action, targetEntity) = _battleAI.DecideAction(entity, _players, _enemies, _battleField);

            switch (action)
            {
                case "move":
                    _battleMovement.MoveEntity(entity, targetEntity, _players, _enemies, _battleLogs);
                    break;
                case "attack":
                    var adjacentTarget = _battleUtilities.FindAdjacentTarget(entity, _players, _enemies, _battleField);
                    if (adjacentTarget != null)
                    {
                        _battleCombat.ExecuteAttack(entity, adjacentTarget.Value, _players, _enemies, _battleLogs);
                    }
                    break;
                case "defend":
                    _battleCombat.ExecuteDefend(entity, _players, _enemies, _battleLogs);
                    break;
            }
        }

        _battleLogs.Add($"Turn {_currentTurn} ends!");

        // Limit battle log size and optimize memory usage
        while (_battleLogs.Count > 50)
        {
            _battleLogs.RemoveAt(0);
        }

        // Clear battle logs during turn processing to reduce memory usage
        if (_currentTurn % 25 == 0 && _battleLogs.Count > 25)
        {
            // Keep only the most recent 25 logs every 25 turns
            var recentLogs = _battleLogs.TakeLast(25).ToList();
            _battleLogs.Clear();
            _battleLogs.AddRange(recentLogs);
        }
    }

    /// <summary>
    /// Run the battle simulation (pre-compute all turns)
    /// </summary>
    public async Task RunBattleAsync()
    {
        _logger.LogInformation("Battle {BattleId}: Starting pre-computation of battle simulation with {PlayerCount} players and {EnemyCount} enemies", _battleId, _players.Count, _enemies.Count);
        var startTime = DateTime.UtcNow;

        // Store all turn data for later transmission to clients (pre-allocate estimated size)
        var allTurnData = new List<BattleStatus>(_totalTurns + 1);

        // Store initial state
        allTurnData.Add(GetStatusSnapshot()); // Store initial state with deep copies

        // Process each turn
        while (_currentTurn < _totalTurns && !_isCompleted)
        {
            _currentTurn++;
            await ProcessTurnAsync();

            // Store turn data with deep copies
            allTurnData.Add(GetStatusSnapshot());

            // Check if battle is over
            var (isOver, isPlayerVictory) = _battleUtilities.CheckBattleOver(_players, _enemies);
            if (isOver)
            {
                _isCompleted = true;
                // Store the battle result (for final log display)
                _playerVictory = isPlayerVictory;
                break;
            }

            // Periodically clear logs to reduce memory pressure
            if (_currentTurn % 25 == 0)
            {
                // Keep only the most recent logs
                if (_battleLogs.Count > 20)
                {
                    _battleLogs.RemoveRange(0, _battleLogs.Count - 20);
                }
            }
        }

        // If battle ended due to turn limit, determine final result
        if (!_isCompleted)
        {
            _isCompleted = true;
            var (_, isPlayerVictory) = _battleUtilities.CheckBattleOver(_players, _enemies);
            _playerVictory = isPlayerVictory;
        }

        // Add final battle log
        if (_playerVictory)
        {
            _battleLogs.Add("Victory! All enemies have been defeated!");
        }
        else
        {
            _battleLogs.Add("❌ Defeat! All players defeated! ❌");
        }

        // Store final state with deep copies
        allTurnData.Add(GetStatusSnapshot());

        // Write all replay data at once for efficiency
        await using var replayWriter = _replayWriterFactory.Create(_battleId);
        await replayWriter.InitializeAsync(_battleId);
        await replayWriter.WriteAllFramesAsync(allTurnData);
        await replayWriter.FinalizeAsync();

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        _logger.LogInformation($"Battle {_battleId}: Pre-computation completed in {duration.TotalSeconds:F2} seconds");
        _logger.LogInformation($"Battle {_battleId}: Processed {_currentTurn} turns with final result: {(_playerVictory ? "Victory" : "Defeat")}");

        // Store all turn data for client transmission
        _allTurnData = allTurnData;
    }

    // Pre-allocate for typical battle length
    private List<BattleStatus> _allTurnData = [];

    /// <summary>
    /// Get all battle turn data for client replay
    /// </summary>
    public List<BattleStatus> GetAllTurnData()
    {
        return _allTurnData;
    }

    /// <summary>
    /// Clear battle data to free memory after client transmission
    /// </summary>
    public void ClearBattleData()
    {
        foreach (var turnData in _allTurnData)
        {
            turnData.Clear();
        }
        _allTurnData.Clear();
        _players.Clear();
        _enemies.Clear();
        _battleLogs.Clear();

        // Clear battle field references
        _battleField.ClearField();

        _logger.LogDebug("Battle {BattleId}: Memory cleared for GC optimization", _battleId);
    }

    /// <summary>
    /// Get a snapshot of the battle field
    /// </summary>
    private ReadOnlyMemory<ReadOnlyMemory<string?>> GetBattleFieldSnapshot()
    {
        return _battleField.GetFieldSnapshot();
    }

    /// <summary>
    /// Get current battle status
    /// </summary>
    public BattleStatus GetStatus()
    {
        return new BattleStatus
        {
            BattleId = _battleId,
            IsInProgress = !_isCompleted,
            CurrentTurn = _currentTurn,
            TotalTurns = _totalTurns,
            Players = [.. _players],
            Enemies = [.. _enemies],
            FieldWidth = BattleSystemDefines.BattleFieldWidth,
            FieldHeight = BattleSystemDefines.BattleFieldHeight,
            RecentLogs = [.. _battleLogs.TakeLast(10)],
            IsPlayerVictory = _isCompleted ? _playerVictory : null
        };
    }

    /// <summary>
    /// Get current battle status with deep copies for turn data storage
    /// </summary>
    private BattleStatus GetStatusSnapshot()
    {
        return new BattleStatus
        {
            BattleId = _battleId,
            IsInProgress = !_isCompleted,
            CurrentTurn = _currentTurn,
            TotalTurns = _totalTurns,
            Players = [.. _players], // structs automatically create copies
            Enemies = [.. _enemies], // structs automatically create copies
            FieldWidth = BattleSystemDefines.BattleFieldWidth,
            FieldHeight = BattleSystemDefines.BattleFieldHeight,
            RecentLogs = [.. _battleLogs.TakeLast(10)],
            IsPlayerVictory = _isCompleted ? _playerVictory : null
        };
    }

    /// <summary>
    /// Mark a client as having confirmed connection readiness
    /// </summary>
    public void MarkConnectionReadyConfirmed(string clientId)
    {
        _clients.AddOrUpdate(clientId, State.Ready, (_, _) => State.Ready);
        var newCount = Interlocked.Increment(ref _readyClientsCount);
        _logger.LogInformation($"Battle {_battleId}: Client {clientId} confirmed connection ready. Ready count: {newCount}/{_connectedClientsCount}");
    }

    /// <summary>
    /// Check if all clients in the group have confirmed connection readiness
    /// </summary>
    public bool AreAllConnectionsReadyConfirmed()
    {
        // Check if all clients have confirmed connection readiness
        return _readyClientsCount == _connectedClientsCount;
    }

}
