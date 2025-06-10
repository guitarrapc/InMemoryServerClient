using Shared;
using System.Text.Json;

namespace InMemoryServer;

/// <summary>
/// Represents a battle state
/// </summary>
public partial class BattleState
{
    private readonly string _battleId;
    private readonly GroupInfo _group;
    private readonly Random _random = new Random();
    private readonly List<EntityInfo> _players = new List<EntityInfo>();
    private readonly List<EntityInfo> _enemies = new List<EntityInfo>();
    private readonly List<string> _battleLogs = new List<string>();
    private readonly string?[,] _battleField;
    private int _currentTurn = 0;
    private int _totalTurns;
    private bool _isCompleted = false;

    public BattleState(string battleId, GroupInfo group)
    {
        _battleId = battleId;
        _group = group;
        _battleField = new string[Constants.BattleFieldHeight, Constants.BattleFieldWidth];

        // Initialize battle
        InitializeBattle();
    }

    /// <summary>
    /// Initialize the battle state
    /// </summary>
    private void InitializeBattle()
    {
        // Create players (one for each connection)
        for (int i = 0; i < _group.ConnectionCount; i++)
        {
            var player = new EntityInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Player{i+1}",
                Type = "Player",
                CurrentHp = Constants.PlayerHp,
                MaxHp = Constants.PlayerHp,
                Attack = _random.Next(Constants.MinAttackPower, Constants.MaxAttackPower + 1),
                Defense = _random.Next(Constants.MinDefensePower, Constants.MaxDefensePower + 1),
                Speed = _random.Next(Constants.MinMovementSpeed, Constants.MaxMovementSpeed + 1),
                IsDefending = false
            };
            _players.Add(player);
        }

        // Create enemies
        int enemyCount = _random.Next(Constants.MinEnemyCount, Constants.MaxEnemyCount + 1);
        string[] enemyTypes = Constants.EnemyHpByType.Keys.ToArray();
        for (int i = 0; i < enemyCount; i++)
        {
            var enemyType = enemyTypes[_random.Next(enemyTypes.Length)];
            var enemy = new EntityInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{enemyType}Enemy{i+1}",
                Type = enemyType,
                CurrentHp = Constants.EnemyHpByType[enemyType],
                MaxHp = Constants.EnemyHpByType[enemyType],
                Attack = _random.Next(Constants.MinAttackPower, Constants.MaxAttackPower + 1),
                Defense = _random.Next(Constants.MinDefensePower, Constants.MaxDefensePower + 1),
                Speed = _random.Next(Constants.MinMovementSpeed, Constants.MaxMovementSpeed + 1),
                IsDefending = false
            };
            _enemies.Add(enemy);
        }

        // Set total turns for battle (balance to ensure it finishes in reasonable time)
        _totalTurns = _random.Next(Constants.MinBattleTurns, Constants.MaxBattleTurns + 1);

        // Initialize battle field and place entities
        InitializeBattleField();

        // Add initial battle log
        _battleLogs.Add($"Battle started with {_players.Count} players and {_enemies.Count} enemies!");
    }

    /// <summary>
    /// Initialize battle field and place entities
    /// </summary>
    private void InitializeBattleField()
    {
        // Clear battle field
        for (int y = 0; y < Constants.BattleFieldHeight; y++)
        {
            for (int x = 0; x < Constants.BattleFieldWidth; x++)
            {
                _battleField[y, x] = null;
            }
        }

        // Place players in the bottom rows
        for (int i = 0; i < _players.Count; i++)
        {
            int attempts = 0;
            while (attempts < 100) // Prevent infinite loop
            {
                int x = _random.Next(Constants.BattleFieldWidth);
                int y = Constants.BattleFieldHeight - _random.Next(1, 4); // Bottom 3 rows
                if (y >= 0 && y < Constants.BattleFieldHeight && x >= 0 && x < Constants.BattleFieldWidth &&
                    _battleField[y, x] == null)
                {
                    _battleField[y, x] = _players[i].Id;
                    _players[i].PositionX = x;
                    _players[i].PositionY = y;
                    break;
                }
                attempts++;
            }
        }

        // Place enemies in the top rows
        for (int i = 0; i < _enemies.Count; i++)
        {
            int attempts = 0;
            while (attempts < 100) // Prevent infinite loop
            {
                int x = _random.Next(Constants.BattleFieldWidth);
                int y = _random.Next(0, 7); // Top 7 rows
                if (y >= 0 && y < Constants.BattleFieldHeight && x >= 0 && x < Constants.BattleFieldWidth &&
                    _battleField[y, x] == null)
                {
                    _battleField[y, x] = _enemies[i].Id;
                    _enemies[i].PositionX = x;
                    _enemies[i].PositionY = y;
                    break;
                }
                attempts++;
            }
        }
    }

    /// <summary>
    /// Run the battle simulation
    /// </summary>
    public async Task RunBattleAsync(Func<BattleStatus, Task> statusCallback)
    {
        // Create directory for battle replays if it doesn't exist
        Directory.CreateDirectory(Constants.BattleReplayDirectory);

        // Open file for battle replay
        using (var replayFile = File.CreateText(Path.Combine(Constants.BattleReplayDirectory, $"{_battleId}.jsonl")))
        {
            // Write initial state
            await WriteReplayFrameAsync(replayFile);

            // Process each turn
            while (_currentTurn < _totalTurns && !_isCompleted)
            {
                _currentTurn++;
                ProcessTurn();

                // Write turn state to replay file
                await WriteReplayFrameAsync(replayFile);

                // Send status update to clients (every 5 turns to avoid flooding)
                if (_currentTurn % 5 == 0 || _isCompleted)
                {
                    await statusCallback(GetStatus());
                }

                // Check if battle is over
                if (CheckBattleOver())
                {
                    _isCompleted = true;
                    break;
                }

                // Short delay between turns (for processing, not real-time)
                await Task.Delay(10);
            }

            // Add final battle log
            if (_players.Any(p => p.CurrentHp > 0))
            {
                _battleLogs.Add("Victory! All enemies have been defeated!");
            }
            else
            {
                _battleLogs.Add("Defeat! All players have been defeated!");
            }

            // Write final state
            await WriteReplayFrameAsync(replayFile);
        }
    }

    /// <summary>
    /// Process a single turn of battle
    /// </summary>
    private void ProcessTurn()
    {
        _battleLogs.Add($"Turn {_currentTurn} begins!");

        // Reset defending status for all entities
        foreach (var player in _players)
        {
            player.IsDefending = false;
        }

        foreach (var enemy in _enemies)
        {
            enemy.IsDefending = false;
        }

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

            // Decide action: move, attack, or defend
            var action = DecideAction(entity);
            switch (action)
            {
                case "move":
                    MoveEntity(entity);
                    break;
                case "attack":
                    AttackWithEntity(entity);
                    break;
                case "defend":
                    DefendWithEntity(entity);
                    break;
            }
        }

        _battleLogs.Add($"Turn {_currentTurn} ends!");

        // Limit battle log size
        while (_battleLogs.Count > 50)
        {
            _battleLogs.RemoveAt(0);
        }
    }

    /// <summary>
    /// Decide what action an entity should take
    /// </summary>
    private string DecideAction(EntityInfo entity)
    {
        // Check if any enemy is adjacent (for attack)
        var adjacentTarget = FindAdjacentTarget(entity);
        if (adjacentTarget != null)
        {
            // 70% chance to attack if enemy is adjacent
            if (_random.Next(100) < 70)
            {
                return "attack";
            }
        }

        // If HP is low, higher chance to defend
        if (entity.CurrentHp < entity.MaxHp * 0.3)
        {
            if (_random.Next(100) < 60)
            {
                return "defend";
            }
        }

        // If no adjacent enemies or didn't choose to attack/defend, move
        if (adjacentTarget == null)
        {
            return "move";
        }

        // Random choice between move and defend
        return _random.Next(2) == 0 ? "move" : "defend";
    }

    /// <summary>
    /// Find an adjacent target for attack
    /// </summary>
    private EntityInfo? FindAdjacentTarget(EntityInfo entity)
    {
        int x = entity.PositionX;
        int y = entity.PositionY;

        // Check all adjacent positions (including diagonals)
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; // Skip self

                int checkX = x + dx;
                int checkY = y + dy;

                // Check if position is valid
                if (checkX >= 0 && checkX < Constants.BattleFieldWidth &&
                    checkY >= 0 && checkY < Constants.BattleFieldHeight &&
                    _battleField[checkY, checkX] != null)
                {
                    string targetId = _battleField[checkY, checkX]!;
                    EntityInfo? target = null;

                    // Find entity with matching ID
                    if (entity.Type == "Player")
                    {
                        target = _enemies.FirstOrDefault(e => e.Id == targetId && e.CurrentHp > 0);
                    }
                    else
                    {
                        target = _players.FirstOrDefault(p => p.Id == targetId && p.CurrentHp > 0);
                    }

                    if (target != null)
                    {
                        return target;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Move entity towards the nearest enemy
    /// </summary>
    private void MoveEntity(EntityInfo entity)
    {
        // Find nearest target
        EntityInfo? nearestTarget = null;
        int minDistance = int.MaxValue;

        var targets = entity.Type == "Player" ?
            _enemies.Where(e => e.CurrentHp > 0) :
            _players.Where(p => p.CurrentHp > 0);

        foreach (var target in targets)
        {
            int distance = Math.Abs(entity.PositionX - target.PositionX) +
                          Math.Abs(entity.PositionY - target.PositionY);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestTarget = target;
            }
        }

        if (nearestTarget == null)
        {
            _battleLogs.Add($"{entity.Name} has no targets to move towards.");
            return;
        }

        // Determine movement direction towards target
        int dx = Math.Sign(nearestTarget.PositionX - entity.PositionX);
        int dy = Math.Sign(nearestTarget.PositionY - entity.PositionY);

        // Try to move in that direction
        int newX = entity.PositionX + dx;
        int newY = entity.PositionY + dy;

        // Check if new position is valid and empty
        if (newX >= 0 && newX < Constants.BattleFieldWidth &&
            newY >= 0 && newY < Constants.BattleFieldHeight &&
            _battleField[newY, newX] == null)
        {
            // Update battle field
            _battleField[entity.PositionY, entity.PositionX] = null;
            _battleField[newY, newX] = entity.Id;

            // Update entity position
            _battleLogs.Add($"{entity.Name} moves from ({entity.PositionX},{entity.PositionY}) to ({newX},{newY})");
            entity.PositionX = newX;
            entity.PositionY = newY;
        }
        else
        {
            // Try alternative directions if direct path is blocked
            int[] dxOptions = { dx, 0, -dx };
            int[] dyOptions = { dy, 0, -dy };
            bool moved = false;

            foreach (int altDx in dxOptions)
            {
                foreach (int altDy in dyOptions)
                {
                    // Skip no movement
                    if (altDx == 0 && altDy == 0) continue;

                    // Skip original blocked direction
                    if (altDx == dx && altDy == dy) continue;

                    int altX = entity.PositionX + altDx;
                    int altY = entity.PositionY + altDy;

                    if (altX >= 0 && altX < Constants.BattleFieldWidth &&
                        altY >= 0 && altY < Constants.BattleFieldHeight &&
                        _battleField[altY, altX] == null)
                    {
                        // Update battle field
                        _battleField[entity.PositionY, entity.PositionX] = null;
                        _battleField[altY, altX] = entity.Id;

                        // Update entity position
                        _battleLogs.Add($"{entity.Name} moves from ({entity.PositionX},{entity.PositionY}) to ({altX},{altY})");
                        entity.PositionX = altX;
                        entity.PositionY = altY;
                        moved = true;
                        break;
                    }
                }
                if (moved) break;
            }

            if (!moved)
            {
                _battleLogs.Add($"{entity.Name} cannot move, all paths are blocked.");
            }
        }
    }

    /// <summary>
    /// Attack with entity
    /// </summary>
    private void AttackWithEntity(EntityInfo entity)
    {
        var target = FindAdjacentTarget(entity);
        if (target == null)
        {
            _battleLogs.Add($"{entity.Name} tries to attack but there are no adjacent targets.");
            return;
        }

        // Calculate damage
        int damage = Math.Max(1, entity.Attack - (target.IsDefending ? target.Defense * 2 : target.Defense) / 2);

        // Apply damage reduction if target is defending
        if (target.IsDefending)
        {
            damage = damage * (100 - Constants.DefenseDamageReductionPercent) / 100;
            damage = Math.Max(1, damage); // Minimum 1 damage
        }

        // Apply damage
        target.CurrentHp = Math.Max(0, target.CurrentHp - damage);

        // Log the attack
        _battleLogs.Add($"{entity.Name} attacks {target.Name} for {damage} damage!" +
                       (target.IsDefending ? " (Reduced by defense)" : ""));

        if (target.CurrentHp <= 0)
        {
            _battleLogs.Add($"{target.Name} has been defeated!");
            _battleField[target.PositionY, target.PositionX] = null;
        }
        else
        {
            _battleLogs.Add($"{target.Name} has {target.CurrentHp}/{target.MaxHp} HP remaining.");
        }
    }

    /// <summary>
    /// Defend with entity
    /// </summary>
    private void DefendWithEntity(EntityInfo entity)
    {
        entity.IsDefending = true;
        _battleLogs.Add($"{entity.Name} takes a defensive stance, reducing incoming damage by {Constants.DefenseDamageReductionPercent}%.");
    }

    /// <summary>
    /// Check if battle is over
    /// </summary>
    private bool CheckBattleOver()
    {
        // Battle is over if all players or all enemies are defeated
        bool allPlayersDead = _players.All(p => p.CurrentHp <= 0);
        bool allEnemiesDead = _enemies.All(e => e.CurrentHp <= 0);
        return allPlayersDead || allEnemiesDead;
    }

    /// <summary>
    /// Write a frame to the battle replay file
    /// </summary>
    private async Task WriteReplayFrameAsync(StreamWriter writer)
    {
        var frame = new
        {
            TurnNumber = _currentTurn,
            Players = _players,
            Enemies = _enemies,
            Field = GetBattleFieldSnapshot(),
            Logs = _battleLogs.TakeLast(10).ToList()
        };

        await writer.WriteLineAsync(JsonSerializer.Serialize(frame));
        await writer.FlushAsync();
    }        /// <summary>
    /// Get a snapshot of the battle field
    /// </summary>
    private List<List<string>> GetBattleFieldSnapshot()
    {
        var snapshot = new List<List<string>>();
        for (int y = 0; y < Constants.BattleFieldHeight; y++)
        {
            var row = new List<string>();
            for (int x = 0; x < Constants.BattleFieldWidth; x++)
            {
                row.Add(_battleField[y, x] ?? string.Empty);
            }
            snapshot.Add(row);
        }
        return snapshot;
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
            Players = _players.ToList(),
            Enemies = _enemies.ToList(),
            Field = new BattleFieldInfo
            {
                Width = Constants.BattleFieldWidth,
                Height = Constants.BattleFieldHeight,
                Cells = GetBattleFieldSnapshot()
            },
            RecentLogs = _battleLogs.TakeLast(10).ToList()
        };
    }
}
