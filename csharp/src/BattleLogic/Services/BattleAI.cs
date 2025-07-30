using BattleLogic.Constants;
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
    private List<BattleAIActionReward> EvaluateAllActions(EntityInfo entity, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField)
    {
        var actions = new List<BattleAIActionReward>();
        var previousAction = GetPreviousAction(entity.EntityId);

        EvaluateAttackAction(entity, actions, adjacentTarget, players, enemies);
        EvaluateDefendAction(entity, actions, players, enemies, battleField, previousAction);
        EvaluateMoveAction(entity, actions, adjacentTarget, players, enemies, battleField, previousAction);

        return actions;
    }

    /// <summary>
    /// Evaluate attack action
    /// </summary>
    private void EvaluateAttackAction(EntityInfo entity, List<BattleAIActionReward> actions, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        var targets = entity.Type.IsPlayer ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new BattleAIActionReward("attack", -100f));
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
            // Factor in critical hit chance for expected damage calculation
            float criticalMultiplier = 1.0f + (entity.CriticalRate / 100.0f); // Expected damage includes critical hits
            float expectedDamage = estimatedDamage * (finalHitChance / 100.0f) * criticalMultiplier;

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

            actions.Add(new BattleAIActionReward("attack", reward, adjacentTarget));
        }
        else
        {
            actions.Add(new BattleAIActionReward("attack", -100f));
        }
    }

    /// <summary>
    /// Evaluate defend action
    /// </summary>
    private void EvaluateDefendAction(EntityInfo entity, List<BattleAIActionReward> actions, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField, string? previousAction)
    {
        var targets = entity.Type.IsPlayer ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new BattleAIActionReward("defend", -100f));
            return;
        }

        // When only one enemy remains, prioritize attack over defense
        if (targets.Count() == 1)
        {
            actions.Add(new BattleAIActionReward("defend", -50f));
            return;
        }

        // When this entity is the only survivor, prioritize attack over defense
        var allies = entity.Type.IsPlayer ?
            players.Where(p => p.CurrentHp > 0) :
            enemies.Where(e => e.CurrentHp > 0);

        if (allies.Count() == 1)
        {
            actions.Add(new BattleAIActionReward("defend", -50f));
            return;
        }

        float reward = 0.1f; // Very low base reward for defense

        // MAJOR PENALTY for consecutive defense - significantly reduce stalemates
        if (previousAction == "defend")
        {
            reward *= BattleAIDefines.ConsecutiveDefendPenalty; // Massive 95% reduction
            actions.Add(new BattleAIActionReward("defend", reward));
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

        actions.Add(new BattleAIActionReward("defend", reward));
    }

    /// <summary>
    /// Evaluate move action (with potential for attack after movement)
    /// </summary>
    private void EvaluateMoveAction(EntityInfo entity, List<BattleAIActionReward> actions, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField, string? previousAction)
    {
        var targets = entity.Type.IsPlayer ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new BattleAIActionReward("move", 0.1f));
            return;
        }

        bool isLastEnemy = targets.Count() == 1;
        var allies = entity.Type.IsPlayer ?
            players.Where(p => p.CurrentHp > 0) :
            enemies.Where(e => e.CurrentHp > 0);
        bool isLastAlly = allies.Count() == 1;

        if (adjacentTarget != null)
        {
            // PENALTY for moving away from adjacent targets, but less severe due to move+attack possibility
            float hpRatio = (float)entity.CurrentHp / entity.MaxHp;
            float enemyHpRatio = (float)adjacentTarget.Value.CurrentHp / adjacentTarget.Value.MaxHp;

            if (isLastEnemy || isLastAlly)
            {
                // If only one enemy/ally remains, discourage movement from adjacent position but allow tactical repositioning
                actions.Add(new BattleAIActionReward("move", 0.1f)); // Slightly higher than before
            }
            else if (hpRatio < BattleAIDefines.LowHpRatio && enemyHpRatio > BattleAIDefines.HighHpRatio)
            {
                // Allow movement if entity HP is low and enemy HP is high (retreat scenario)
                // Now more valuable due to potential repositioning for better attack angles
                actions.Add(new BattleAIActionReward("move", 5.0f)); // Increased from 3.0f
            }
            else
            {
                // Reduced penalty for moving away from adjacent targets since move+attack is now possible
                float moveReward = 1.0f * BattleAIDefines.AdjacentMovePenalty; // Reduced penalty from 0.5f
                actions.Add(new BattleAIActionReward("move", moveReward));
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

            // MAJOR BONUS: If moving can result in adjacent attack next turn
            if (distanceToNearest == 2)
            {
                // Can move adjacent and attack in one turn - massive bonus
                reward *= BattleAIDefines.NextTurnAttackPositionMultiplier;

                // Additional bonus if target has low HP (potential one-hit kill)
                float targetHpRatio = (float)nearestTarget.Value.CurrentHp / nearestTarget.Value.MaxHp;
                if (targetHpRatio < BattleAIDefines.LowHpRatio)
                {
                    reward *= 2.0f; // Double reward for potential finishing move
                }
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

            actions.Add(new BattleAIActionReward("move", reward, nearestTarget));
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

            // MAJOR BONUS: If moving can result in adjacent attack for low HP target
            if (distanceToLowest == 2)
            {
                // Can move adjacent and potentially finish off low HP enemy in one turn
                reward *= 4.0f; // Quadruple reward for potential finishing move
            }
            else if (distanceToLowest <= 3)
            {
                reward *= 5.0f / (distanceToLowest + 1);
            }

            if (!entity.Type.IsPlayer)
            {
                reward *= BattleAIDefines.NonPlayerMoveMultiplier;
            }

            actions.Add(new BattleAIActionReward("move", reward, lowestHpTarget));
        }

        if (nearestTarget == null && lowestHpTarget == null)
        {
            actions.Add(new BattleAIActionReward("move", 3.0f));
        }
    }
}
