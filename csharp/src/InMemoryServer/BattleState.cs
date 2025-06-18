using Shared;
using System.Collections.Concurrent;
using System.Text.Json;

namespace InMemoryServer;

/// <summary>
/// Represents a battle state
/// </summary>
public partial class BattleState
{
    // 行動選択に関する定数
    private const float ATTACK_ADJACENT_REWARD = 10.0f;
    private const float ATTACK_LOW_HP_BONUS = 3.0f;
    private const float DEFEND_LOW_HP_REWARD = 8.0f;
    private const float DEFEND_ENEMIES_NEARBY_REWARD = 5.0f;
    private const float MOVE_TO_NEAREST_REWARD = 3.0f;
    private const float MOVE_TO_LOWEST_HP_REWARD = 2.5f;
    private const float MOVE_TO_SURROUND_REWARD = 4.0f;
    private const int NEARBY_DISTANCE_THRESHOLD = 2; // 敵が近くにいると判断する距離の閾値

    /// <summary>
    /// 行動の報酬を格納する内部構造体
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
        _battleField = new string[Constants.BattleFieldHeight, Constants.BattleFieldWidth];

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
            var maxHp = _random.Next(Constants.PlayerHp - 70, Constants.PlayerHp + 70);
            var player = new EntityInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Player{i + 1}",
                Type = "Player",
                CurrentHp = maxHp, // Start at full health
                MaxHp = maxHp,
                // Players get slightly better stats than enemies for balance
                Attack = _random.Next(Constants.MinAttackPower, Constants.MaxAttackPower + 6),
                Defense = _random.Next(Constants.MinDefensePower + 2, Constants.MaxDefensePower + 4),
                Speed = _random.Next(Constants.MinMovementSpeed, Constants.MaxMovementSpeed + 1),
                IsDefending = false
            };
            _players.Add(player);
        }

        // Create enemies
        int enemyCount = _random.Next(Constants.MinEnemyCount, Constants.MaxEnemyCount);
        string[] enemyTypes = [.. Constants.EnemyHpByType.Keys];

        for (int i = 0; i < enemyCount; i++)
        {
            var enemyType = enemyTypes[_random.Next(enemyTypes.Length)];
            var maxHp = _random.Next(Constants.EnemyHpByType[enemyType], Constants.EnemyHpByType[enemyType] + 50);
            var enemy = new EntityInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{enemyType}Enemy{i + 1}",
                Type = enemyType,
                CurrentHp = maxHp, // Start at full health
                MaxHp = maxHp,
                // Enemies get slightly weaker stats for balance
                Attack = _random.Next(Constants.MinAttackPower - 5, Constants.MaxAttackPower - 3),
                Defense = _random.Next(Constants.MinDefensePower - 2, Constants.MaxDefensePower),
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
                int x = _random.Next(Constants.BattleFieldWidth);
                int y = _random.Next(0, 7); // Top 7 rows
                if (y >= 0 && y < Constants.BattleFieldHeight && x >= 0 && x < Constants.BattleFieldWidth && _battleField[y, x] == null)
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
        Directory.CreateDirectory(Constants.BattleReplayDirectory);

        // Store all turn data for later transmission to clients (pre-allocate estimated size)
        var allTurnData = new List<BattleStatus>(_totalTurns + 1);

        // Open file for battle replay
        using (var replayFile = File.CreateText(Path.Combine(Constants.BattleReplayDirectory, $"{_battleId}.jsonl")))
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
                if (CheckBattleOver())
                {
                    _isCompleted = true;
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
            allTurnData.Add(GetStatusSnapshot()); // Store final state with deep copies
        }

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        _logger.LogInformation($"Battle {_battleId}: Pre-computation completed in {duration.TotalSeconds:F2} seconds");
        _logger.LogInformation($"Battle {_battleId}: Processed {_currentTurn} turns with final result: {(_players.Any(p => p.CurrentHp > 0) ? "Victory" : "Defeat")}");
        _logger.LogInformation($"Battle {_battleId}: Replay file saved to {Path.Combine(Constants.BattleReplayDirectory, $"{_battleId}.jsonl")}");

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
        _allTurnData.Clear(); _players.Clear();
        _enemies.Clear();
        _battleLogs.Clear();

        // Clear battle field references
        for (int y = 0; y < Constants.BattleFieldHeight; y++)
        {
            for (int x = 0; x < Constants.BattleFieldWidth; x++)
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
        // 各行動の報酬を計算
        var possibleActions = EvaluateAllActions(entity, adjacentTarget);

        // 最も報酬の高い行動を選択
        var bestAction = possibleActions.OrderByDescending(a => a.Reward).First();

        _logger.LogDebug("Entity {EntityName} chose {Action} with reward {Reward}", entity.Name, bestAction.Action, bestAction.Reward);

        return bestAction.Action;
    }

    /// <summary>
    /// 全ての可能な行動を評価し、それぞれの報酬を計算する
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
    /// 攻撃行動の評価
    /// </summary>
    private void EvaluateAttackAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget)
    {
        if (adjacentTarget != null)
        {
            // Base reward
            float reward = ATTACK_ADJACENT_REWARD;

            // Bonus for attacking low HP targets
            float hpRatio = (float)adjacentTarget.Value.CurrentHp / adjacentTarget.Value.MaxHp;
            reward += (1 - hpRatio) * ATTACK_LOW_HP_BONUS;

            actions.Add(new ActionReward("attack", reward, adjacentTarget));
        }
        else
        {
            // Remove attack action if no adjacent target
            actions.Add(new ActionReward("attack", -100f));
        }
    }    /// <summary>
    /// 防御行動の評価
    /// </summary>
    private void EvaluateDefendAction(EntityInfo entity, List<ActionReward> actions)
    {
        // Base reward for defending - starting with a very low base value
        float reward = 0.5f;

        // Increase reward if entity's HP is critically low (only when below 25%)
        float hpRatio = (float)entity.CurrentHp / entity.MaxHp;
        if (hpRatio < 0.25f) // HP閾値を下げる（40%→25%）
        {
            reward += (1 - hpRatio) * DEFEND_LOW_HP_REWARD;
        }

        // Check if there are enemies within a certain distance threshold
        bool enemiesNearby = AreEnemiesNearby(entity, NEARBY_DISTANCE_THRESHOLD);
        if (enemiesNearby)
        {
            // 敵が隣接している場合にのみ防御を検討
            var adjacentTarget = FindAdjacentTarget(entity);
            if (adjacentTarget != null)
            {
                reward += DEFEND_ENEMIES_NEARBY_REWARD;
            }
            else
            {
                // 敵が近くにいるが隣接していない場合、防御よりも移動を優先
                reward *= 0.3f;
            }
        }
        else
        {
            // 敵が近くにいない場合は防御の意味がほとんどない
            reward *= 0.1f;
        }

        // エンティティタイプに基づいて防御の確率を調整
        // プレイヤーは防御を多少利用し、敵は攻撃的に
        if (entity.Type != "Player")
        {
            // 敵は攻撃的な行動を優先
            reward *= 0.5f;
        }

        actions.Add(new ActionReward("defend", reward));
    }

    /// <summary>
    /// 移動行動の評価
    /// </summary>
    private void EvaluateMoveAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget)
    {
        if (adjacentTarget != null)
        {
            // Move action is less valuable if adjacent target exists
            actions.Add(new ActionReward("move", 1.0f));
            return;
        }

        // Find nearest target for evaluation
        var nearestTarget = FindNearestTarget(entity);

        // Find the lowest HP target for evaluation
        var lowestHpTarget = FindLowestHpTarget(entity);

        if (nearestTarget != null)
        {
            float reward = MOVE_TO_NEAREST_REWARD;

            // Check if the entity can surround the nearest enemy
            if (CanSurroundEnemy(entity, nearestTarget.Value))
            {
                reward += MOVE_TO_SURROUND_REWARD;
            }

            actions.Add(new ActionReward("move", reward, nearestTarget));
        }

        // Lowest HP target evaluation (for future extensibility)
        if (lowestHpTarget != null && (nearestTarget == null || lowestHpTarget.Value.Id != nearestTarget.Value.Id))
        {
            float reward = MOVE_TO_LOWEST_HP_REWARD;
            float hpRatio = (float)lowestHpTarget.Value.CurrentHp / lowestHpTarget.Value.MaxHp;
            reward += (1 - hpRatio) * 2.0f;

            actions.Add(new ActionReward("move", reward, lowestHpTarget));
        }

        // Random move action to ensure entity can always act
        if (nearestTarget == null && lowestHpTarget == null)
        {
            actions.Add(new ActionReward("move", 1.0f));
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
    /// 移動方向のリストを取得
    /// </summary>
    private List<(int dx, int dy, int priority)> GetMovementDirections(EntityInfo entity, EntityInfo? targetEntity)
    {
        var directions = new List<(int dx, int dy, int priority)>();

        // ターゲットがない場合は完全にランダムな方向を返す
        if (targetEntity is null)
        {
            // ランダムな8方向を生成し、すべて同じ優先度で返す
            int[] randomDirs = [-1, 0, 1];
            for (int randDx = -1; randDx <= 1; randDx++)
            {
                for (int randDy = -1; randDy <= 1; randDy++)
                {
                    if (randDx == 0 && randDy == 0) continue; // 自分自身はスキップ
                    directions.Add((randDx, randDy, 1)); // すべて同じ優先度
                }
            }

            // ランダムに並べ替えて返す
            return directions.OrderBy(_ => _random.Next()).ToList();
        }

        // ターゲットが存在する場合、ターゲットへの方向を計算
        int dx = Math.Sign(targetEntity.Value.Position.X - entity.Position.X);
        int dy = Math.Sign(targetEntity.Value.Position.Y - entity.Position.Y);

        // 敵との距離を計算
        int xDistance = Math.Abs(targetEntity.Value.Position.X - entity.Position.X);
        int yDistance = Math.Abs(targetEntity.Value.Position.Y - entity.Position.Y);

        // 両方の距離が0の場合（同じ位置にいる場合）、ランダムな方向を選択
        if (xDistance == 0 && yDistance == 0)
        {
            // ランダムな方向を選択
            int[] randomDirs = [-1, 0, 1];
            int randDx = randomDirs[_random.Next(randomDirs.Length)];
            int randDy = randomDirs[_random.Next(randomDirs.Length)];

            // (0,0)は避ける
            if (randDx == 0 && randDy == 0) randDx = 1;

            directions.Add((randDx, randDy, 1));
            directions.Add((randDy, randDx, 2)); // 90度回転
            directions.Add((-randDx, randDy, 3)); // 別方向も試す
            directions.Add((randDx, -randDy, 4));
        }
        else if (xDistance > yDistance)
        {
            // X方向の距離が大きい場合、横方向の移動を優先
            // dxが0の場合は1か-1を選択
            if (dx == 0) dx = xDistance == 0 ? (_random.Next(2) == 0 ? 1 : -1) : Math.Sign(xDistance);

            directions.Add((dx, 0, 1));
            directions.Add((dx, dy, 2));
            directions.Add((0, dy, 3));
        }
        else
        {
            // Y方向の距離が大きい場合、縦方向の移動を優先
            // dyが0の場合は1か-1を選択
            if (dy == 0) dy = yDistance == 0 ? (_random.Next(2) == 0 ? 1 : -1) : Math.Sign(yDistance);

            directions.Add((0, dy, 1));
            directions.Add((dx, dy, 2));
            directions.Add((dx, 0, 3));
        }

        // ダイアゴナル方向や逆方向も選択肢として追加（ただし優先度は低い）
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
    /// ランダムな方向を生成
    /// </summary>
    private List<(int dx, int dy)> GenerateRandomDirections()
    {
        var randomDirections = new List<(int dx, int dy)>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // 自分自身はスキップ
                randomDirections.Add((dx, dy));
            }
        }

        // ランダムに並べ替え
        return randomDirections.OrderBy(_ => _random.Next()).ToList();
    }

    /// <summary>
    /// 指定された方向のリストに従って移動を試みる
    /// </summary>
    private bool TryMoveInDirections(EntityInfo entity, List<(int dx, int dy, int priority)> directions, EntityInfo? targetEntity)
    {
        foreach (var direction in directions.OrderBy(d => d.priority))
        {
            int newX = entity.Position.X + direction.dx;
            int newY = entity.Position.Y + direction.dy;

            // 新しい位置が有効で空いているかチェック
            if (IsValidEmptyPosition(newX, newY))
            {
                // エンティティの位置を更新
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
    /// ランダムな方向のリストに従って移動を試みる
    /// </summary>
    private bool TryMoveInRandomDirections(EntityInfo entity, List<(int dx, int dy)> randomDirections)
    {
        foreach (var (dx, dy) in randomDirections)
        {
            int newX = entity.Position.X + dx;
            int newY = entity.Position.Y + dy;

            // 新しい位置が有効で空いているかチェック
            if (IsValidEmptyPosition(newX, newY))
            {
                // エンティティの位置を更新
                UpdateEntityPosition(entity, newX, newY);
                _battleLogs.Add($"{entity.Name} randomly moves from ({entity.Position.X},{entity.Position.Y}) to ({newX},{newY}).");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 指定された位置が有効で空いているかチェック
    /// </summary>
    private bool IsValidEmptyPosition(int x, int y)
    {
        return x >= 0 && x < Constants.BattleFieldWidth &&
               y >= 0 && y < Constants.BattleFieldHeight &&
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
            damage = damage * (100 - Constants.DefenseDamageReductionPercent) / 100;
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
        var cells = new string?[Constants.BattleFieldHeight][];
        for (int y = 0; y < Constants.BattleFieldHeight; y++)
        {
            cells[y] = new string?[Constants.BattleFieldWidth];
            for (int x = 0; x < Constants.BattleFieldWidth; x++)
            {
                cells[y][x] = _battleField[y, x];
            }
        }

        var rowMemories = new ReadOnlyMemory<string?>[Constants.BattleFieldHeight];
        for (int y = 0; y < Constants.BattleFieldHeight; y++)
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
            FieldWidth = Constants.BattleFieldWidth,
            FieldHeight = Constants.BattleFieldHeight,
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
            FieldWidth = Constants.BattleFieldWidth,
            FieldHeight = Constants.BattleFieldHeight,
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
    /// 最も近い敵を見つける
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
    /// 最もHPが低い敵を見つける
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
    /// 指定した距離内に敵がいるかをチェック
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
    /// 敵を囲むように移動できるかをチェック
    /// </summary>
    private bool CanSurroundEnemy(EntityInfo entity, EntityInfo target)
    {
        // 味方の位置を取得
        var allies = entity.Type == "Player" ?
            _players.Where(p => p.Id != entity.Id && p.CurrentHp > 0) :
            _enemies.Where(e => e.Id != entity.Id && e.CurrentHp > 0);

        // 敵の周りの位置をチェック
        int surroundCount = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; // 敵自身の位置はスキップ

                int checkX = target.Position.X + dx;
                int checkY = target.Position.Y + dy;

                // 位置が有効かチェック
                if (checkX >= 0 && checkX < Constants.BattleFieldWidth &&
                    checkY >= 0 && checkY < Constants.BattleFieldHeight)
                {
                    // 味方がその位置にいるかチェック
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

        // 敵を半分以上囲んでいるか、または囲める可能性があるかを判断
        return surroundCount >= 3;
    }

    /// <summary>
    /// マンハッタン距離を計算
    /// </summary>
    private int CalculateManhattanDistance(Vector2 a, Vector2 b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
