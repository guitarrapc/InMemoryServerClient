using Shared;
using System.Collections.Concurrent;
using System.Text.Json;

namespace InMemoryServer;

/// <summary>
/// Represents a battle state
/// </summary>
public partial class BattleState
{
    /// <summary>
    /// Internal structure to store action rewards
    /// </summary>
    private readonly struct ActionReward
    {
        public readonly string Action { get; init; }
        public readonly float Reward { get; init; }
        public readonly EntityInfo? TargetEntity { get; init; }
        public readonly Vector2? TargetPosition { get; init; }

        public ActionReward(string action, float reward, EntityInfo? targetEntity = null, Vector2? targetPosition = null)
        {
            Action = action;
            Reward = reward;
            TargetEntity = targetEntity;
            TargetPosition = targetPosition;
        }
    }

    public enum State
    {
        Connected = 0,
        Ready,
        ReplayCompleted,
    }

    private readonly string _battleId;
    private readonly GroupInfo _group;
    private readonly Random _random = new Random();
    private readonly List<EntityInfo> _players = new(5); // Pre-allocate for max players
    private readonly List<EntityInfo> _enemies = new(15); // Pre-allocate for max enemies
    private readonly List<string> _battleLogs = new(60); // Pre-allocate for battle logs with limit
    private readonly string?[,] _battleField;
    private readonly ILogger<BattleState> _logger;
    private readonly ConcurrentDictionary<string, State> _clients = new();
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
    /// Gets the battle ID
    /// </summary>
    public string BattleId => _battleId;

    /// <summary>
    /// Gets the time when the battle was started
    /// </summary>
    public DateTime StartTime { get; } = DateTime.UtcNow;

    public BattleState(string battleId, GroupInfo group, ILogger<BattleState> logger)
    {
        _battleId = battleId;
        _group = group;
        _logger = logger;
        _battleField = new string[BattleBasicDefines.BattleFieldHeight, BattleBasicDefines.BattleFieldWidth];

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
        // Create players (one for each connection)
        for (int i = 0; i < _group.ConnectionCount; i++)
        {
            // Randomly assign a job
            var jobTypes = Enum.GetValues<PlayerJob>();
            var assignedJob = jobTypes[_random.Next(jobTypes.Length)];
            var jobModifier = BattleBasicDefines.PlayerJobModifiers[assignedJob];

            // Calculate base stats
            var baseMaxHp = _random.Next(BattleBasicDefines.PlayerHp.Min, BattleBasicDefines.PlayerHp.Max);
            var baseAttack = _random.Next(BattleBasicDefines.PlayerAttackPower.Min, BattleBasicDefines.PlayerAttackPower.Max);
            var baseDefense = _random.Next(BattleBasicDefines.PlayerDefencePower.Min, BattleBasicDefines.PlayerDefencePower.Max);
            var baseSpeed = _random.Next(BattleBasicDefines.PlayerMoveSpeed.Min, BattleBasicDefines.PlayerMoveSpeed.Max);

            // Apply job modifiers
            var modifiedMaxHp = Math.Max(1, (int)(baseMaxHp * jobModifier.HpMultiplier) + jobModifier.HpBonus);
            var modifiedAttack = Math.Max(1, (int)(baseAttack * jobModifier.AttackMultiplier) + jobModifier.AttackBonus);
            var modifiedDefense = Math.Max(0, (int)(baseDefense * jobModifier.DefenseMultiplier) + jobModifier.DefenseBonus);
            var modifiedSpeed = Math.Max(1, (int)(baseSpeed * jobModifier.SpeedMultiplier) + jobModifier.SpeedBonus);

            var player = new EntityInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{assignedJob}Player{i + 1}",
                Type = "Player",
                Job = assignedJob,
                CurrentHp = modifiedMaxHp, // Start at full health
                MaxHp = modifiedMaxHp,
                Attack = modifiedAttack,
                Defense = modifiedDefense,
                Speed = modifiedSpeed,
                IsDefending = false
            };
            _players.Add(player);
        }

        // Create enemies
        int enemyCount = _random.Next(BattleBasicDefines.MinEnemyCount, BattleBasicDefines.MaxEnemyCount);
        string[] enemyTypes = Enum.GetNames<EnemyType>();

        for (int i = 0; i < enemyCount; i++)
        {
            var enemyType = Enum.Parse<EnemyType>(enemyTypes[_random.Next(enemyTypes.Length)]);
            var maxHp = _random.Next(BattleBasicDefines.EnemyHpByType[enemyType].Min, BattleBasicDefines.EnemyHpByType[enemyType].Max);
            var enemy = new EntityInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{enemyType}Enemy{i + 1}",
                Type = enemyType.ToString(),
                CurrentHp = maxHp, // Start at full health
                MaxHp = maxHp,
                Attack = _random.Next(BattleBasicDefines.EnemyAttackPower[enemyType].Min, BattleBasicDefines.EnemyAttackPower[enemyType].Max),
                Defense = _random.Next(BattleBasicDefines.EnemyDefencePower[enemyType].Min, BattleBasicDefines.EnemyDefencePower[enemyType].Max),
                Speed = _random.Next(BattleBasicDefines.EnemyMoveSpeed[enemyType].Min, BattleBasicDefines.EnemyMoveSpeed[enemyType].Max),
                IsDefending = false
            };
            _enemies.Add(enemy);
        }

        // Set total turns for battle (balance to ensure it finishes in reasonable time)
        _totalTurns = _random.Next(BattleBasicDefines.MinBattleTurns, BattleBasicDefines.MaxBattleTurns + 1);

        // Initialize battle field and place entities
        InitializeBattleField();

        // Add initial battle log
        _battleLogs.Add($"Battle started with {_players.Count} players and {_enemies.Count} enemies!");

        // Log player job information
        foreach (var player in _players)
        {
            _battleLogs.Add($"{player.Name} (Job: {player.Job}) - HP: {player.MaxHp}, ATK: {player.Attack}, DEF: {player.Defense}, SPD: {player.Speed}");
        }
    }

    /// <summary>
    /// Initialize battle field and place entities
    /// </summary>
    private void InitializeBattleField()
    {
        // Clear battle field
        for (int y = 0; y < BattleBasicDefines.BattleFieldHeight; y++)
        {
            for (int x = 0; x < BattleBasicDefines.BattleFieldWidth; x++)
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
                int x = _random.Next(BattleBasicDefines.BattleFieldWidth);
                int y = BattleBasicDefines.BattleFieldHeight - _random.Next(1, 4); // Bottom 3 rows
                if (y >= 0 && y < BattleBasicDefines.BattleFieldHeight && x >= 0 && x < BattleBasicDefines.BattleFieldWidth &&
_battleField[y, x] == null)
                {
                    _battleField[y, x] = _players[i].Id;
                    _players[i] = _players[i] with { Position = new Vector2(x, y) };
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
                int x = _random.Next(BattleBasicDefines.BattleFieldWidth);
                int y = _random.Next(0, 7); // Top 7 rows
                if (y >= 0 && y < BattleBasicDefines.BattleFieldHeight && x >= 0 && x < BattleBasicDefines.BattleFieldWidth && _battleField[y, x] == null)
                {
                    _battleField[y, x] = _enemies[i].Id;
                    _enemies[i] = _enemies[i] with { Position = new Vector2(x, y) };
                    break;
                }
                attempts++;
            }
        }
    }

    /// <summary>
    /// Run the battle simulation (pre-compute all turns)
    /// </summary>
    public async Task RunBattleAsync()
    {
        _logger.LogInformation("Battle {BattleId}: Starting pre-computation of battle simulation with {PlayerCount} players and {EnemyCount} enemies", _battleId, _players.Count, _enemies.Count);
        var startTime = DateTime.UtcNow;

        // Create directory for battle replays if it doesn't exist
        Directory.CreateDirectory(SystemDefines.BattleReplayDirectory);

        // Store all turn data for later transmission to clients (pre-allocate estimated size)
        var allTurnData = new List<BattleStatus>(_totalTurns + 1);

        // Open file for battle replay
        using (var replayFile = File.CreateText(Path.Combine(SystemDefines.BattleReplayDirectory, $"{_battleId}.jsonl")))
        {
            // Write initial state
            await WriteReplayFrameAsync(replayFile);
            allTurnData.Add(GetStatusSnapshot()); // Store initial state with deep copies

            // Process each turn
            while (_currentTurn < _totalTurns && !_isCompleted)
            {
                _currentTurn++;
                await ProcessTurnAsync();

                // Write turn state to replay file
                await WriteReplayFrameAsync(replayFile);
                allTurnData.Add(GetStatusSnapshot()); // Store turn data with deep copies

                // Check if battle is over
                var (isOver, isPlayerVictory) = CheckBattleOver();
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

            // Add final battle log
            if (_playerVictory)
            {
                _battleLogs.Add("Victory! All enemies have been defeated!");
            }
            else
            {
                _battleLogs.Add("❌ Defeat! All players defeated! ❌");
            }

            // Write final state
            await WriteReplayFrameAsync(replayFile);
            allTurnData.Add(GetStatusSnapshot()); // Store final state with deep copies
        }

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        _logger.LogInformation($"Battle {_battleId}: Pre-computation completed in {duration.TotalSeconds:F2} seconds");
        _logger.LogInformation($"Battle {_battleId}: Processed {_currentTurn} turns with final result: {(_playerVictory ? "Victory" : "Defeat")}");
        _logger.LogInformation($"Battle {_battleId}: Replay file saved to {Path.Combine(SystemDefines.BattleReplayDirectory, $"{_battleId}.jsonl")}");

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
        for (int y = 0; y < BattleBasicDefines.BattleFieldHeight; y++)
        {
            for (int x = 0; x < BattleBasicDefines.BattleFieldWidth; x++)
            {
                _battleField[y, x] = null;
            }
        }

        _logger.LogDebug("Battle {BattleId}: Memory cleared for GC optimization", _battleId);
    }

    /// <summary>
    /// Process a single turn of battle
    /// </summary>
    private async Task ProcessTurnAsync()
    {
        _battleLogs.Add($"Turn {_currentTurn} begins!");

        // Reset defending status for all entities
        for (int i = 0; i < _players.Count; i++)
        {
            _players[i] = _players[i] with { IsDefending = false };
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            _enemies[i] = _enemies[i] with { IsDefending = false };
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

            // Find adjacent target for attack/move evaluation
            var adjacentTarget = FindAdjacentTarget(entity);

            // Decide action: move, attack, or defend
            var action = DecideAction(entity, adjacentTarget);
            switch (action)
            {
                case "move":
                    MoveEntity(entity, adjacentTarget);
                    break;
                case "attack":
                    AttackWithEntity(entity, adjacentTarget);
                    break;
                case "defend":
                    DefendWithEntity(entity);
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
    /// Decide what action an entity should take based on reward model
    /// </summary>
    private string DecideAction(EntityInfo entity, EntityInfo? adjacentTarget)
    {
        // Calculate rewards for each possible action
        var possibleActions = EvaluateAllActions(entity, adjacentTarget);

        // Select the action with the highest reward
        var bestAction = possibleActions.OrderByDescending(a => a.Reward).First();

        _logger.LogDebug("Entity {EntityName} chose {Action} with reward {Reward}", entity.Name, bestAction.Action, bestAction.Reward);

        return bestAction.Action;
    }

    /// <summary>
    /// Evaluate all possible actions and calculate their rewards
    /// </summary>
    private List<ActionReward> EvaluateAllActions(EntityInfo entity, EntityInfo? adjacentTarget)
    {
        var actions = new List<ActionReward>();

        // Evaluate attack action
        EvaluateAttackAction(entity, actions, adjacentTarget);

        // Evaluate defend action
        EvaluateDefendAction(entity, actions);

        // Evaluate move action
        EvaluateMoveAction(entity, actions, adjacentTarget);

        return actions;
    }

    /// <summary>
    /// Evaluate attack action
    /// </summary>
    private void EvaluateAttackAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget)
    {
        // Check if there are any surviving enemies
        var targets = entity.Type == "Player" ?
            _enemies.Where(e => e.CurrentHp > 0) :
            _players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            // No attack possible if all enemies are defeated
            actions.Add(new ActionReward("attack", -100f));
            return;
        }

        if (adjacentTarget != null)
        {
            // Base attack reward - highest priority
            float reward = BattleAIDefines.AttackAdjacentReward;

            // Significantly increase attack reward if only one enemy remains
            if (targets.Count() == 1)
            {
                reward *= 3.0f; // Prioritize attack when only one enemy remains
            }

            // Increase attack reward if this entity is the only survivor on its side
            var allies = entity.Type == "Player" ?
                _players.Where(p => p.CurrentHp > 0) :
                _enemies.Where(e => e.CurrentHp > 0);

            if (allies.Count() == 1) // Only this entity remains
            {
                reward *= 2.5f; // Prioritize attack when last survivor
            }

            // Bonus for low HP enemies
            float hpRatio = (float)adjacentTarget.Value.CurrentHp / adjacentTarget.Value.MaxHp;

            // Prioritize attacking enemies with less than 30% HP (finishing blow)
            if (hpRatio < BattleAIDefines.LowHpRatio)
            {
                reward *= 2.0f;  // Significant bonus increase
            }

            reward += (1 - hpRatio) * BattleAIDefines.AttackLowHpBonus;

            // Priority based on enemy type
            // Ensure Type is not null
            if (!string.IsNullOrEmpty(adjacentTarget.Value.Type))
            {
                // Prioritize small enemies as they are easier to defeat
                if (adjacentTarget.Value.Type.StartsWith("Small"))
                {
                    reward *= BattleAIDefines.SmallEnemyAttackMultiplier;
                }
                // Prioritize large enemies as they pose greater threat
                else if (adjacentTarget.Value.Type.StartsWith("Large"))
                {
                    reward *= BattleAIDefines.LargeEnemyAttackMultiplier;
                }
            }

            // Significantly increase reward if attack can potentially defeat the enemy
            int estimatedDamage = Math.Max(1, entity.Attack - adjacentTarget.Value.Defense / 2);
            if (adjacentTarget.Value.IsDefending)
            {
                estimatedDamage = estimatedDamage * (100 - BattleBasicDefines.DefenseDamageReductionPercent) / 100;
                estimatedDamage = Math.Max(1, estimatedDamage);
            }

            if (estimatedDamage >= adjacentTarget.Value.CurrentHp)
            {
                // Highest priority if can defeat in one hit
                reward *= BattleAIDefines.OneHitKillMultiplier;
            }

            // Adjust aggressiveness based on entity type
            if (!string.IsNullOrEmpty(entity.Type) && entity.Type != "Player")
            {
                // Non-player entities are more aggressive
                reward *= BattleAIDefines.NonPlayerAttackMultiplier;
            }

            actions.Add(new ActionReward("attack", reward, adjacentTarget));
        }
        else
        {
            // Attack not possible if no adjacent enemy
            actions.Add(new ActionReward("attack", -100f));
        }
    }

    /// <summary>
    /// Evaluate defend action
    /// </summary>
    private void EvaluateDefendAction(EntityInfo entity, List<ActionReward> actions)
    {
        // No point in defending if all enemies are defeated
        var targets = entity.Type == "Player" ?
            _enemies.Where(e => e.CurrentHp > 0) :
            _players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new ActionReward("defend", -100f)); // Don't defend if no enemies
            return;
        }

        // When only one enemy remains, prioritize attack over defense
        if (targets.Count() == 1)
        {
            actions.Add(new ActionReward("defend", -50f)); // Prioritize attack when only one enemy remains
            return;
        }

        // When this entity is the only survivor on its side, prioritize attack over defense
        var allies = entity.Type == "Player" ?
            _players.Where(p => p.CurrentHp > 0) :
            _enemies.Where(e => e.CurrentHp > 0);

        if (allies.Count() == 1) // Only this entity remains
        {
            actions.Add(new ActionReward("defend", -50f)); // Prioritize attack when last survivor
            return;
        }

        // Base reward for defending - starting with a very low base value
        float reward = 0.1f;  // Very low base reward for defense

        // Increase reward if entity's HP is critically low (only when below 20%)
        float hpRatio = (float)entity.CurrentHp / entity.MaxHp;
        if (hpRatio < BattleAIDefines.CriticalHpRatio)
        {
            reward += (1 - hpRatio) * BattleAIDefines.DefendLowHpReward;
        }

        // Check if there are enemies within a certain distance threshold
        bool enemiesNearby = AreEnemiesNearby(entity, BattleAIDefines.NearbyDistanceThreshold);
        if (enemiesNearby)
        {
            // Check if enemy is adjacent
            var adjacentTarget = FindAdjacentTarget(entity);
            if (adjacentTarget != null)
            {
                // If HP is above 50%, prioritize attack over defense
                if (hpRatio > BattleAIDefines.SufficientHpRatio)
                {
                    reward *= 0.2f; // Significantly reduce defense reward
                }
                else
                {
                    // Enemy is adjacent, consider defense only if HP is critically low
                    reward += BattleAIDefines.DefendEnemiesNearbyReward;
                }
            }
            else
            {
                // Enemy is nearby but not adjacent, prioritize movement over defense
                reward *= 0.2f;
            }
        }
        else
        {
            // No enemies nearby, defense is mostly pointless
            reward *= 0.05f;  // Further reduce defense reward
        }

        // Adjust defense probability based on entity type
        // Players use defense occasionally, enemies are more aggressive
        if (!string.IsNullOrEmpty(entity.Type) && entity.Type != "Player")
        {
            // Non-player entities prioritize aggressive actions
            reward *= BattleAIDefines.NonPlayerDefendMultiplier;
        }

        actions.Add(new ActionReward("defend", reward));
    }

    /// <summary>
    /// Evaluate move action
    /// </summary>
    private void EvaluateMoveAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget)
    {
        // Check if there are any surviving enemies
        var targets = entity.Type == "Player" ?
            _enemies.Where(e => e.CurrentHp > 0) :
            _players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            // If all enemies are defeated, assign lowest priority to movement
            actions.Add(new ActionReward("move", 0.1f));
            return;
        }

        // If only one enemy remains, prioritize movement to attack that enemy
        bool isLastEnemy = targets.Count() == 1;

        // Check if this entity is the last surviving ally
        var allies = entity.Type == "Player" ?
            _players.Where(p => p.CurrentHp > 0) :
            _enemies.Where(e => e.CurrentHp > 0);
        bool isLastAlly = allies.Count() == 1; // Only this entity is surviving

        if (adjacentTarget != null)
        {
            // If there's an adjacent enemy, lower movement priority to favor attack
            // However, don't completely exclude it (movement might be useful in some situations)
            // Consider fleeing if enemy HP is high and own HP is low
            float hpRatio = (float)entity.CurrentHp / entity.MaxHp;
            float enemyHpRatio = (float)adjacentTarget.Value.CurrentHp / adjacentTarget.Value.MaxHp;

            // Don't flee if this is the last enemy or the entity is the last survivor
            if (isLastEnemy || isLastAlly)
            {
                actions.Add(new ActionReward("move", 0.1f)); // Significantly reduce movement reward to prioritize attack
            }
            else if (hpRatio < BattleAIDefines.LowHpRatio && enemyHpRatio > BattleAIDefines.HighHpRatio)
            {
                // Consider fleeing if HP is in danger and enemy is healthy
                actions.Add(new ActionReward("move", 3.0f));
            }
            else
            {
                actions.Add(new ActionReward("move", 0.5f));
            }
            return;
        }

        // Find nearest target for evaluation
        var nearestTarget = FindNearestTarget(entity);

        // 敵が残り一体またはプレイヤーが自分しかいない場合、移動の優先度を上げる
        float moveMultiplier = 1.0f;
        if (isLastEnemy || isLastAlly)
        {
            moveMultiplier = 5.0f; // 最終決戦状態では移動の優先度を大幅に上げる
        }

        // Find the lowest HP target for evaluation
        var lowestHpTarget = FindLowestHpTarget(entity);

        // HPが低い敵の方が優先度が高い場合がある
        // 距離とHPの両方を考慮した戦略的な選択
        if (nearestTarget != null)
        {
            // 基本報酬値
            float reward = BattleAIDefines.MoveToNearestReward * moveMultiplier;

            // 最も近い敵までの距離
            int distanceToNearest = CalculateManhattanDistance(entity.Position, nearestTarget.Value.Position);

            // 距離が1または2の場合（次の移動で攻撃可能または近づける）は報酬を増加
            if (distanceToNearest == 2)
            {
                reward *= BattleAIDefines.NextTurnAttackPositionMultiplier;
            }
            else if (distanceToNearest == 3)
            {
                reward *= BattleAIDefines.TwoTurnsAttackPositionMultiplier;
            }

            // 敵のHPが低い場合のボーナス
            float hpRatio = (float)nearestTarget.Value.CurrentHp / nearestTarget.Value.MaxHp;
            if (hpRatio < BattleAIDefines.SufficientHpRatio)
            {
                reward *= (1.0f + (1.0f - hpRatio)); // HPが低いほど報酬が高くなる
            }

            // 敵を囲む戦略（協調行動）のボーナス
            if (CanSurroundEnemy(entity, nearestTarget.Value))
            {
                reward += BattleAIDefines.MoveToSurroundReward;
            }

            // エンティティタイプによる攻撃性調整
            if (!string.IsNullOrEmpty(entity.Type) && entity.Type != "Player")
            {
                // 敵は特に移動攻撃的に
                reward *= BattleAIDefines.NonPlayerMoveMultiplier;
            }

            actions.Add(new ActionReward("move", reward, nearestTarget));
        }

        // 最もHPの低い敵に対する評価
        if (lowestHpTarget != null && (nearestTarget == null || lowestHpTarget.Value.Id != nearestTarget.Value.Id))
        {
            float reward = BattleAIDefines.MoveToLowestHpReward * moveMultiplier;
            float hpRatio = (float)lowestHpTarget.Value.CurrentHp / lowestHpTarget.Value.MaxHp;

            // HPが非常に低い敵（20%未満）への移動は高い優先度
            if (hpRatio < BattleAIDefines.CriticalHpRatio)
            {
                reward *= BattleAIDefines.LowHpEnemyMoveMultiplier;
            }
            else
            {
                reward += (1 - hpRatio) * 4.0f; // ボーナスを増加
            }

            // 距離を考慮
            int distanceToLowest = CalculateManhattanDistance(entity.Position, lowestHpTarget.Value.Position);
            if (distanceToLowest <= 3) // 近い敵は優先
            {
                reward *= (5.0f / (distanceToLowest + 1)); // 距離が近いほど報酬が高くなる
            }

            // エンティティタイプによる攻撃性調整
            if (!string.IsNullOrEmpty(entity.Type) && entity.Type != "Player")
            {
                reward *= BattleAIDefines.NonPlayerMoveMultiplier;
            }

            actions.Add(new ActionReward("move", reward, lowestHpTarget));
        }

        // ランダムな移動（他に良い選択肢がない場合のフォールバック）
        if (nearestTarget == null && lowestHpTarget == null)
        {
            actions.Add(new ActionReward("move", 3.0f)); // 何もしないよりは移動した方がいい
        }
    }

    /// <summary>
    /// Find an adjacent target for attack
    /// </summary>
    private EntityInfo? FindAdjacentTarget(EntityInfo entity)
    {
        int x = entity.Position.X;
        int y = entity.Position.Y;

        // Check all adjacent positions (including diagonals)
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; // Skip self

                int checkX = x + dx;
                int checkY = y + dy;

                // Check if position is valid
                if (checkX >= 0 && checkX < BattleBasicDefines.BattleFieldWidth &&
                    checkY >= 0 && checkY < BattleBasicDefines.BattleFieldHeight &&
                    _battleField[checkY, checkX] != null)
                {
                    string targetId = _battleField[checkY, checkX]!;
                    EntityInfo? target = null;

                    // Find entity with matching ID
                    if (!string.IsNullOrEmpty(entity.Type) && entity.Type == "Player")
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
    /// Move entity towards the nearest enemy or lowest HP enemy
    /// </summary>
    private void MoveEntity(EntityInfo entity, EntityInfo? adjacentTarget)
    {
        // 移動先のターゲットを決定（最も近い敵か最もHPが低い敵）
        EntityInfo? targetEntity = null;

        // 各行動の報酬を計算した結果から、最も良い移動先を選択
        var possibleActions = new List<ActionReward>();
        EvaluateMoveAction(entity, possibleActions, adjacentTarget);

        // 移動の価値が最も高いものを選択
        var bestMoveAction = possibleActions
            .OrderByDescending(a => a.Reward)
            .FirstOrDefault();

        targetEntity = bestMoveAction.TargetEntity;

        // 移動方向のリストを取得
        var directions = GetMovementDirections(entity, targetEntity);

        // 各方向を試して移動を試みる
        bool moved = TryMoveInDirections(entity, directions, targetEntity);

        // すべての方向が塞がれている場合は完全ランダムな方向で再試行
        if (!moved)
        {
            _battleLogs.Add($"{entity.Name} cannot move in preferred directions, trying random directions.");

            // ランダムな方向を生成
            var randomDirections = GenerateRandomDirections();

            // ランダムな方向で移動を試みる
            moved = TryMoveInRandomDirections(entity, randomDirections);
        }

        if (!moved)
        {
            _battleLogs.Add($"{entity.Name} cannot move, all paths are blocked.");
        }
    }

    /// <summary>
    /// Get movement directions list
    /// </summary>
    private List<(int dx, int dy, int priority)> GetMovementDirections(EntityInfo entity, EntityInfo? targetEntity)
    {
        var directions = new List<(int dx, int dy, int priority)>();

        // If there's no target, return completely random directions
        if (targetEntity is null)
        {
            // Generate random 8 directions, all with the same priority
            int[] randomDirs = [-1, 0, 1];
            for (int randDx = -1; randDx <= 1; randDx++)
            {
                for (int randDy = -1; randDy <= 1; randDy++)
                {
                    if (randDx == 0 && randDy == 0) continue; // Skip self
                    directions.Add((randDx, randDy, 1)); // All same priority
                }
            }

            // Return randomly shuffled
            return directions.OrderBy(_ => _random.Next()).ToList();
        }

        // If a target exists, calculate direction towards target
        int dx = Math.Sign(targetEntity.Value.Position.X - entity.Position.X);
        int dy = Math.Sign(targetEntity.Value.Position.Y - entity.Position.Y);

        // Calculate distance to enemy
        int xDistance = Math.Abs(targetEntity.Value.Position.X - entity.Position.X);
        int yDistance = Math.Abs(targetEntity.Value.Position.Y - entity.Position.Y);

        // If both distances are 0 (at the same position), choose random direction
        if (xDistance == 0 && yDistance == 0)
        {
            // Choose random direction
            int[] randomDirs = [-1, 0, 1];
            int randDx = randomDirs[_random.Next(randomDirs.Length)];
            int randDy = randomDirs[_random.Next(randomDirs.Length)];

            // Avoid (0,0)
            if (randDx == 0 && randDy == 0) randDx = 1;

            directions.Add((randDx, randDy, 1));
            directions.Add((randDy, randDx, 2)); // Rotated 90 degrees
            directions.Add((-randDx, randDy, 3)); // Try other directions too
            directions.Add((randDx, -randDy, 4));
        }
        else if (xDistance > yDistance)
        {
            // If X distance is greater, prioritize horizontal movement
            // If dx is 0, choose 1 or -1
            if (dx == 0) dx = xDistance == 0 ? (_random.Next(2) == 0 ? 1 : -1) : Math.Sign(xDistance);

            directions.Add((dx, 0, 1));
            directions.Add((dx, dy, 2));
            directions.Add((0, dy, 3));
        }
        else
        {
            // If Y distance is greater, prioritize vertical movement
            // If dy is 0, choose 1 or -1
            if (dy == 0) dy = yDistance == 0 ? (_random.Next(2) == 0 ? 1 : -1) : Math.Sign(yDistance);

            directions.Add((0, dy, 1));
            directions.Add((dx, dy, 2));
            directions.Add((dx, 0, 3));
        }

        // Add diagonal and opposite directions as lower priority options
        if (dx != 0 && dy != 0)
        {
            directions.Add((dx, -dy, 4));
            directions.Add((-dx, dy, 5));
        }
        directions.Add((-dx, 0, 6));
        directions.Add((0, -dy, 7));
        directions.Add((-dx, -dy, 8));

        return directions;
    }

    /// <summary>
    /// Generate random directions
    /// </summary>
    private List<(int dx, int dy)> GenerateRandomDirections()
    {
        var randomDirections = new List<(int dx, int dy)>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // Skip self
                randomDirections.Add((dx, dy));
            }
        }

        // Shuffle randomly
        return randomDirections.OrderBy(_ => _random.Next()).ToList();
    }

    /// <summary>
    /// Try to move in the specified directions list
    /// </summary>
    private bool TryMoveInDirections(EntityInfo entity, List<(int dx, int dy, int priority)> directions, EntityInfo? targetEntity)
    {
        foreach (var direction in directions.OrderBy(d => d.priority))
        {
            int newX = entity.Position.X + direction.dx;
            int newY = entity.Position.Y + direction.dy;

            // Check if the new position is valid and empty
            if (IsValidEmptyPosition(newX, newY))
            {
                // Update entity position
                UpdateEntityPosition(entity, newX, newY);

                if (targetEntity != null)
                {
                    _battleLogs.Add($"{entity.Name} moves from ({entity.Position.X},{entity.Position.Y}) to ({newX},{newY}) towards {targetEntity.Value.Name}.");
                }
                else
                {
                    _battleLogs.Add($"{entity.Name} moves from ({entity.Position.X},{entity.Position.Y}) to ({newX},{newY}).");
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Try to move in random directions
    /// </summary>
    private bool TryMoveInRandomDirections(EntityInfo entity, List<(int dx, int dy)> randomDirections)
    {
        foreach (var (dx, dy) in randomDirections)
        {
            int newX = entity.Position.X + dx;
            int newY = entity.Position.Y + dy;

            // Check if the new position is valid and empty
            if (IsValidEmptyPosition(newX, newY))
            {
                // Update entity position
                UpdateEntityPosition(entity, newX, newY);
                _battleLogs.Add($"{entity.Name} randomly moves from ({entity.Position.X},{entity.Position.Y}) to ({newX},{newY}).");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if the specified position is valid and empty
    /// </summary>
    private bool IsValidEmptyPosition(int x, int y)
    {
        return x >= 0 && x < BattleBasicDefines.BattleFieldWidth &&
               y >= 0 && y < BattleBasicDefines.BattleFieldHeight &&
               _battleField[y, x] == null;
    }

    /// <summary>
    /// Update entity position in the appropriate list
    /// </summary>
    private void UpdateEntityPosition(EntityInfo entity, int newX, int newY)
    {
        // Update the battle field
        _battleField[entity.Position.Y, entity.Position.X] = null;
        _battleField[newY, newX] = entity.Id;

        Vector2 newPosition = new Vector2(newX, newY);

        if (entity.Type == "Player")
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id == entity.Id)
                {
                    _players[i] = _players[i] with { Position = newPosition };
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Id == entity.Id)
                {
                    _enemies[i] = _enemies[i] with { Position = newPosition };
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Attack with entity
    /// </summary>
    private void AttackWithEntity(EntityInfo entity, EntityInfo? adjacentTarget)
    {
        if (adjacentTarget is null)
        {
            _battleLogs.Add($"{entity.Name} tries to attack but there are no adjacent targets.");
            return;
        }

        var targetValue = adjacentTarget.Value;

        // Calculate damage
        int damage = Math.Max(1, entity.Attack - (targetValue.IsDefending ? targetValue.Defense * 2 : targetValue.Defense) / 2);

        // Apply damage reduction if target is defending
        if (targetValue.IsDefending)
        {
            damage = damage * (100 - BattleBasicDefines.DefenseDamageReductionPercent) / 100;
            damage = Math.Max(1, damage); // Minimum 1 damage
        }

        // Apply damage
        int newHp = Math.Max(0, targetValue.CurrentHp - damage);

        // Update target's HP in the appropriate list
        UpdateEntityHp(targetValue, newHp);

        // Log the attack
        _battleLogs.Add($"{entity.Name} attacks {targetValue.Name} for {damage} damage!" + (targetValue.IsDefending ? " (Reduced by defense)" : ""));

        if (newHp <= 0)
        {
            _battleLogs.Add($"{targetValue.Name} has been defeated!");

            // Clear the defeated entity from the battle field
            _battleField[targetValue.Position.Y, targetValue.Position.X] = null;

            // Update the entity's position to invalid coordinates (-1, -1)
            // This ensures the entity won't be considered in further position checks
            if (targetValue.Type == "Player")
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].Id == targetValue.Id)
                    {
                        _players[i] = _players[i] with { Position = Vector2.InvalidPosition };
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < _enemies.Count; i++)
                {
                    if (_enemies[i].Id == targetValue.Id)
                    {
                        _enemies[i] = _enemies[i] with { Position = Vector2.InvalidPosition };
                        break;
                    }
                }
            }
        }
        else
        {
            _battleLogs.Add($"{targetValue.Name} has {newHp}/{targetValue.MaxHp} HP remaining.");
        }
    }

    /// <summary>
    /// Update entity HP in the appropriate list
    /// </summary>
    private void UpdateEntityHp(EntityInfo entity, int newHp)
    {
        if (entity.Type == "Player")
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id == entity.Id)
                {
                    _players[i] = _players[i] with { CurrentHp = newHp };
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Id == entity.Id)
                {
                    _enemies[i] = _enemies[i] with { CurrentHp = newHp };
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Defend with entity
    /// </summary>
    private void DefendWithEntity(EntityInfo entity)
    {
        // Update defending status in the appropriate list
        if (entity.Type == "Player")
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id == entity.Id)
                {
                    _players[i] = _players[i] with { IsDefending = true };
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Id == entity.Id)
                {
                    _enemies[i] = _enemies[i] with { IsDefending = true };
                    break;
                }
            }
        }

        _battleLogs.Add($"{entity.Name} takes a defensive stance, reducing incoming damage by {BattleBasicDefines.DefenseDamageReductionPercent}%.");
    }

    /// <summary>
    /// Check if battle is over
    /// </summary>
    /// <returns>Tuple of (isOver, isPlayerVictory) where isOver indicates if battle is over, and isPlayerVictory indicates if players won</returns>
    private (bool isOver, bool isPlayerVictory) CheckBattleOver()
    {
        // Battle is over if all players or all enemies are defeated
        bool allPlayersDead = _players.All(p => p.CurrentHp <= 0);
        bool allEnemiesDead = _enemies.All(e => e.CurrentHp <= 0);

        // In case both sides are defeated, consider it a draw and treat as player defeat
        if (allPlayersDead && allEnemiesDead)
        {
            return (true, false); // Battle over, player defeat
        }
        else if (allPlayersDead)
        {
            return (true, false); // Battle over, player defeat
        }
        else if (allEnemiesDead)
        {
            return (true, true); // Battle over, player victory
        }

        return (false, false); // Battle continues
    }

    /// <summary>
    /// Write a frame to the battle replay file
    /// </summary>
    private async Task WriteReplayFrameAsync(StreamWriter writer)
    {
        var frame = new BattleStatus
        {
            BattleId = _battleId,
            IsInProgress = !_isCompleted,
            CurrentTurn = _currentTurn,
            TotalTurns = _totalTurns,
            Players = _players,
            Enemies = _enemies,
            FieldHeight = _battleField.GetLength(0),
            FieldWidth = _battleField.GetLength(1),
            RecentLogs = _battleLogs.TakeLast(10).ToList()
        };

        await writer.WriteLineAsync(JsonSerializer.Serialize(frame));
        await writer.FlushAsync();
    }

    /// <summary>
    /// Get a snapshot of the battle field
    /// </summary>
    private ReadOnlyMemory<ReadOnlyMemory<string?>> GetBattleFieldSnapshot()
    {
        var cells = new string?[BattleBasicDefines.BattleFieldHeight][];
        for (int y = 0; y < BattleBasicDefines.BattleFieldHeight; y++)
        {
            cells[y] = new string?[BattleBasicDefines.BattleFieldWidth];
            for (int x = 0; x < BattleBasicDefines.BattleFieldWidth; x++)
            {
                cells[y][x] = _battleField[y, x];
            }
        }

        var rowMemories = new ReadOnlyMemory<string?>[BattleBasicDefines.BattleFieldHeight];
        for (int y = 0; y < BattleBasicDefines.BattleFieldHeight; y++)
        {
            rowMemories[y] = cells[y].AsMemory();
        }

        return rowMemories.AsMemory();
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
            FieldWidth = BattleBasicDefines.BattleFieldWidth,
            FieldHeight = BattleBasicDefines.BattleFieldHeight,
            RecentLogs = [.. _battleLogs.TakeLast(10)]
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
            FieldWidth = BattleBasicDefines.BattleFieldWidth,
            FieldHeight = BattleBasicDefines.BattleFieldHeight,
            RecentLogs = [.. _battleLogs.TakeLast(10)]
        };
    }

    /// <summary>
    /// Mark a client as having confirmed connection readiness    /// </summary>
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

    /// <summary>
    /// Find the nearest target
    /// </summary>
    private EntityInfo? FindNearestTarget(EntityInfo entity)
    {
        EntityInfo? nearestTarget = null;
        int minDistance = int.MaxValue;

        var targets = entity.Type == "Player" ?
            _enemies.Where(e => e.CurrentHp > 0) :
            _players.Where(p => p.CurrentHp > 0);

        foreach (var target in targets)
        {
            int distance = CalculateManhattanDistance(entity.Position, target.Position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestTarget = target;
            }
        }

        return nearestTarget;
    }

    /// <summary>
    /// Find the target with lowest HP
    /// </summary>
    private EntityInfo? FindLowestHpTarget(EntityInfo entity)
    {
        EntityInfo? lowestHpTarget = null;
        int lowestHp = int.MaxValue;

        var targets = entity.Type == "Player" ?
            _enemies.Where(e => e.CurrentHp > 0) :
            _players.Where(p => p.CurrentHp > 0);

        foreach (var target in targets)
        {
            if (target.CurrentHp < lowestHp)
            {
                lowestHp = target.CurrentHp;
                lowestHpTarget = target;
            }
        }

        return lowestHpTarget;
    }

    /// <summary>
    /// Check if there are enemies within the specified distance threshold
    /// </summary>
    private bool AreEnemiesNearby(EntityInfo entity, int distanceThreshold)
    {
        var targets = entity.Type == "Player" ?
            _enemies.Where(e => e.CurrentHp > 0) :
            _players.Where(p => p.CurrentHp > 0);

        foreach (var target in targets)
        {
            int distance = CalculateManhattanDistance(entity.Position, target.Position);
            if (distance <= distanceThreshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if the entity can surround an enemy
    /// </summary>
    private bool CanSurroundEnemy(EntityInfo entity, EntityInfo target)
    {
        // Get allied positions
        var allies = entity.Type == "Player" ?
            _players.Where(p => p.Id != entity.Id && p.CurrentHp > 0) :
            _enemies.Where(e => e.Id != entity.Id && e.CurrentHp > 0);

        // Check positions around the enemy
        int surroundCount = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; // Skip enemy's own position

                int checkX = target.Position.X + dx;
                int checkY = target.Position.Y + dy;

                // Check if position is valid
                if (checkX >= 0 && checkX < BattleBasicDefines.BattleFieldWidth &&
                    checkY >= 0 && checkY < BattleBasicDefines.BattleFieldHeight)
                {
                    // Check if an ally is at that position
                    foreach (var ally in allies)
                    {
                        if (ally.Position.X == checkX && ally.Position.Y == checkY)
                        {
                            surroundCount++;
                            break;
                        }
                    }
                }
            }
        }

        // Determine if the enemy is surrounded by at least half or if surrounding is possible
        return surroundCount >= 3;
    }

    /// <summary>
    /// Calculate Manhattan distance
    /// </summary>
    private int CalculateManhattanDistance(Vector2 a, Vector2 b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
