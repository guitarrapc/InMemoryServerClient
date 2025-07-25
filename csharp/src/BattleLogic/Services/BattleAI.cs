using BattleLogic.Constans;
using BattleLogic.Models;
using Microsoft.Extensions.Logging;

namespace BattleLogic.Services;

/// <summary>
/// Handles AI decision making for battle entities
/// </summary>
internal class BattleAI(BattleUtilities utilities, ILogger logger)
{
    /// <summary>
    /// Dictionary to track previous actions by entity ID
    /// </summary>
    private readonly Dictionary<Guid, string> _previousActions = new();

    /// <summary>
    /// Decide what action an entity should take
    /// </summary>
    public (string action, EntityInfo? target) DecideAction(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField)
    {
        var adjacentTarget = utilities.FindAdjacentTarget(entity, players, enemies, battleField);
        var possibleActions = EvaluateAllActions(entity, adjacentTarget, players, enemies, battleField);

        var bestAction = possibleActions.OrderByDescending(a => a.Reward).First();
        logger.LogDebug("Entity {EntityName} chose {Action} with reward {Reward}", entity.Name, bestAction.Action, bestAction.Reward);

        // Record the action for next turn's decision making
        RecordAction(entity.EntityId, bestAction.Action);

        return (bestAction.Action, bestAction.TargetEntity);
    }

    /// <summary>
    /// Record an entity's action for future reference
    /// </summary>
    private void RecordAction(Guid entityId, string action)
    {
        _previousActions[entityId] = action;
    }

    /// <summary>
    /// Get the previous action of an entity
    /// </summary>
    private string? GetPreviousAction(Guid entityId)
    {
        return _previousActions.TryGetValue(entityId, out var action) ? action : null;
    }

    /// <summary>
    /// Clear action history (call when battle ends)
    /// </summary>
    public void ClearActionHistory()
    {
        _previousActions.Clear();
    }

    /// <summary>
    /// Evaluate all possible actions and calculate their rewards
    /// </summary>
    private List<ActionReward> EvaluateAllActions(EntityInfo entity, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField)
    {
        var actions = new List<ActionReward>();
        var previousAction = GetPreviousAction(entity.EntityId);

        EvaluateAttackAction(entity, actions, adjacentTarget, players, enemies);
        EvaluateDefendAction(entity, actions, players, enemies, battleField, previousAction);
        EvaluateMoveAction(entity, actions, adjacentTarget, players, enemies, previousAction);

        return actions;
    }

    /// <summary>
    /// Evaluate attack action
    /// </summary>
    private void EvaluateAttackAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        var targets = entity.Type.IsPlayer ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new ActionReward("attack", -100f));
            return;
        }

        if (adjacentTarget != null)
        {
            float reward = BattleAIDefines.AttackAdjacentReward;

            // Increase attack reward if only one enemy remains
            if (targets.Count() == 1)
            {
                reward *= 3.0f;
            }

            // Increase attack reward if this entity is the only survivor
            var allies = entity.Type.IsPlayer ?
                players.Where(p => p.CurrentHp > 0) :
                enemies.Where(e => e.CurrentHp > 0);

            if (allies.Count() == 1)
            {
                reward *= 2.5f;
            }

            // Bonus for low HP enemies
            float hpRatio = (float)adjacentTarget.Value.CurrentHp / adjacentTarget.Value.MaxHp;
            if (hpRatio < BattleAIDefines.LowHpRatio)
            {
                reward *= 2.0f;
            }

            reward += (1 - hpRatio) * BattleAIDefines.AttackLowHpBonus;

            // Priority based on enemy type
            if (adjacentTarget.Value.Type.IsEnemy && adjacentTarget.Value.Type.EnemySize.HasValue)
            {
                switch (adjacentTarget.Value.Type.EnemySize.Value)
                {
                    case EnemySize.Small:
                        reward *= BattleAIDefines.SmallEnemyAttackMultiplier;
                        break;
                    case EnemySize.Large:
                        reward *= BattleAIDefines.LargeEnemyAttackMultiplier;
                        break;
                    case EnemySize.Medium:
                        // Default case, no additional multiplier
                        break;
                }
            }

            // Check if attack can potentially defeat the enemy
            int estimatedDamage = Math.Max(1, entity.Attack - adjacentTarget.Value.Defense / 2);
            var finalHitChance = Math.Max(0, entity.Accuracy - adjacentTarget.Value.Evasion);
            float expectedDamage = estimatedDamage * (finalHitChance / 100.0f);

            if (adjacentTarget.Value.IsDefending)
            {
                expectedDamage = expectedDamage * (100 - BattleSystemDefines.DefenseDamageReductionPercent) / 100.0f;
                expectedDamage = Math.Max(1.0f, expectedDamage);
            }

            if (expectedDamage >= adjacentTarget.Value.CurrentHp)
            {
                reward *= BattleAIDefines.OneHitKillMultiplier;
            }

            // Adjust aggressiveness based on entity type
            if (!entity.Type.IsPlayer)
            {
                reward *= BattleAIDefines.NonPlayerAttackMultiplier;
            }

            actions.Add(new ActionReward("attack", reward, adjacentTarget));
        }
        else
        {
            actions.Add(new ActionReward("attack", -100f));
        }
    }

    /// <summary>
    /// Evaluate defend action
    /// </summary>
    private void EvaluateDefendAction(EntityInfo entity, List<ActionReward> actions, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField, string? previousAction)
    {
        var targets = entity.Type.IsPlayer ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new ActionReward("defend", -100f));
            return;
        }

        // When only one enemy remains, prioritize attack over defense
        if (targets.Count() == 1)
        {
            actions.Add(new ActionReward("defend", -50f));
            return;
        }

        // When this entity is the only survivor, prioritize attack over defense
        var allies = entity.Type.IsPlayer ?
            players.Where(p => p.CurrentHp > 0) :
            enemies.Where(e => e.CurrentHp > 0);

        if (allies.Count() == 1)
        {
            actions.Add(new ActionReward("defend", -50f));
            return;
        }

        float reward = 0.1f; // Very low base reward for defense

        // MAJOR PENALTY for consecutive defense - significantly reduce stalemates
        if (previousAction == "defend")
        {
            reward *= BattleAIDefines.ConsecutiveDefendPenalty; // Massive 95% reduction
            actions.Add(new ActionReward("defend", reward));
            return;
        }

        // Increase reward if entity's HP is critically low
        float hpRatio = (float)entity.CurrentHp / entity.MaxHp;
        if (hpRatio < BattleAIDefines.CriticalHpRatio)
        {
            reward += (1 - hpRatio) * BattleAIDefines.DefendLowHpReward;
        }

        // Check if there are enemies nearby
        bool enemiesNearby = utilities.AreEnemiesNearby(entity, players, enemies, BattleAIDefines.NearbyDistanceThreshold);
        if (enemiesNearby)
        {
            var adjacentTarget = utilities.FindAdjacentTarget(entity, players, enemies, battleField);
            if (adjacentTarget != null)
            {
                if (hpRatio > BattleAIDefines.SufficientHpRatio)
                {
                    reward *= 0.2f;
                }
                else
                {
                    reward += BattleAIDefines.DefendEnemiesNearbyReward;
                }
            }
            else
            {
                reward *= 0.2f;
            }
        }
        else
        {
            reward *= 0.05f;
        }

        // Adjust defense probability based on entity type
        if (!entity.Type.IsPlayer)
        {
            reward *= BattleAIDefines.NonPlayerDefendMultiplier;
        }

        actions.Add(new ActionReward("defend", reward));
    }

    /// <summary>
    /// Evaluate move action
    /// </summary>
    private void EvaluateMoveAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies, string? previousAction)
    {
        var targets = entity.Type.IsPlayer ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new ActionReward("move", 0.1f));
            return;
        }

        bool isLastEnemy = targets.Count() == 1;
        var allies = entity.Type.IsPlayer ?
            players.Where(p => p.CurrentHp > 0) :
            enemies.Where(e => e.CurrentHp > 0);
        bool isLastAlly = allies.Count() == 1;

        if (adjacentTarget != null)
        {
            // MAJOR PENALTY for moving away from adjacent targets - avoid meaningless movement
            float hpRatio = (float)entity.CurrentHp / entity.MaxHp;
            float enemyHpRatio = (float)adjacentTarget.Value.CurrentHp / adjacentTarget.Value.MaxHp;

            if (isLastEnemy || isLastAlly)
            {
                // If only one enemy/ally remains, strongly discourage movement from adjacent position
                actions.Add(new ActionReward("move", 0.01f)); // Very low reward
            }
            else if (hpRatio < BattleAIDefines.LowHpRatio && enemyHpRatio > BattleAIDefines.HighHpRatio)
            {
                // Only allow movement if entity HP is low and enemy HP is high (retreat scenario)
                actions.Add(new ActionReward("move", 3.0f));
            }
            else
            {
                // Apply major penalty for moving away from adjacent targets
                float moveReward = 0.5f * BattleAIDefines.AdjacentMovePenalty; // 90% reduction
                actions.Add(new ActionReward("move", moveReward));
            }
            return;
        }

        var nearestTarget = utilities.FindNearestTarget(entity, players, enemies);
        var lowestHpTarget = utilities.FindLowestHpTarget(entity, players, enemies);

        float moveMultiplier = 1.0f;
        if (isLastEnemy || isLastAlly)
        {
            moveMultiplier = 5.0f;
        }

        if (nearestTarget != null)
        {
            float reward = BattleAIDefines.MoveToNearestReward * moveMultiplier;

            int distanceToNearest = utilities.CalculateManhattanDistance(entity.Position, nearestTarget.Value.Position);
            if (distanceToNearest == 2)
            {
                reward *= BattleAIDefines.NextTurnAttackPositionMultiplier;
            }
            else if (distanceToNearest == 3)
            {
                reward *= BattleAIDefines.TwoTurnsAttackPositionMultiplier;
            }

            float hpRatio = (float)nearestTarget.Value.CurrentHp / nearestTarget.Value.MaxHp;
            if (hpRatio < BattleAIDefines.SufficientHpRatio)
            {
                reward *= 1.0f + (1.0f - hpRatio);
            }

            if (utilities.CanSurroundEnemy(entity, nearestTarget.Value, players, enemies))
            {
                reward += BattleAIDefines.MoveToSurroundReward;
            }

            if (!entity.Type.IsPlayer)
            {
                reward *= BattleAIDefines.NonPlayerMoveMultiplier;
            }

            actions.Add(new ActionReward("move", reward, nearestTarget));
        }

        if (lowestHpTarget != null && (nearestTarget == null || lowestHpTarget.Value.EntityId != nearestTarget.Value.EntityId))
        {
            float reward = BattleAIDefines.MoveToLowestHpReward * moveMultiplier;
            float hpRatio = (float)lowestHpTarget.Value.CurrentHp / lowestHpTarget.Value.MaxHp;

            if (hpRatio < BattleAIDefines.CriticalHpRatio)
            {
                reward *= BattleAIDefines.LowHpEnemyMoveMultiplier;
            }
            else
            {
                reward += (1 - hpRatio) * 4.0f;
            }

            int distanceToLowest = utilities.CalculateManhattanDistance(entity.Position, lowestHpTarget.Value.Position);
            if (distanceToLowest <= 3)
            {
                reward *= 5.0f / (distanceToLowest + 1);
            }

            if (!entity.Type.IsPlayer)
            {
                reward *= BattleAIDefines.NonPlayerMoveMultiplier;
            }

            actions.Add(new ActionReward("move", reward, lowestHpTarget));
        }

        if (nearestTarget == null && lowestHpTarget == null)
        {
            actions.Add(new ActionReward("move", 3.0f));
        }
    }
}
