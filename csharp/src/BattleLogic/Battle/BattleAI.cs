using BattleLogic.Models;
using Microsoft.Extensions.Logging;

namespace BattleLogic.Battle;

/// <summary>
/// Handles AI decision making for battle entities
/// </summary>
public class BattleAI(BattleUtilities utilities, ILogger logger)
{
    /// <summary>
    /// Decide what action an entity should take
    /// </summary>
    public (string action, EntityInfo? target) DecideAction(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField)
    {
        var adjacentTarget = utilities.FindAdjacentTarget(entity, players, enemies, battleField);
        var possibleActions = EvaluateAllActions(entity, adjacentTarget, players, enemies, battleField);

        var bestAction = possibleActions.OrderByDescending(a => a.Reward).First();
        logger.LogDebug("Entity {EntityName} chose {Action} with reward {Reward}", entity.Name, bestAction.Action, bestAction.Reward);

        return (bestAction.Action, bestAction.TargetEntity);
    }

    /// <summary>
    /// Evaluate all possible actions and calculate their rewards
    /// </summary>
    private List<ActionReward> EvaluateAllActions(EntityInfo entity, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField)
    {
        var actions = new List<ActionReward>();

        EvaluateAttackAction(entity, actions, adjacentTarget, players, enemies);
        EvaluateDefendAction(entity, actions, players, enemies, battleField);
        EvaluateMoveAction(entity, actions, adjacentTarget, players, enemies);

        return actions;
    }

    /// <summary>
    /// Evaluate attack action
    /// </summary>
    private void EvaluateAttackAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        var targets = entity.Type == EntityType.Player ?
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
            var allies = entity.Type == EntityType.Player ?
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
            switch (adjacentTarget.Value.Type)
            {
                case EntityType.SmallEnemy:
                    reward *= BattleAIDefines.SmallEnemyAttackMultiplier;
                    break;
                case EntityType.LargeEnemy:
                    reward *= BattleAIDefines.LargeEnemyAttackMultiplier;
                    break;
            }

            // Check if attack can potentially defeat the enemy
            int estimatedDamage = Math.Max(1, entity.Attack - adjacentTarget.Value.Defense / 2);
            var finalHitChance = Math.Max(0, entity.Accuracy - adjacentTarget.Value.Evasion);
            float expectedDamage = estimatedDamage * (finalHitChance / 100.0f);

            if (adjacentTarget.Value.IsDefending)
            {
                expectedDamage = expectedDamage * (100 - BattleBasicDefines.DefenseDamageReductionPercent) / 100.0f;
                expectedDamage = Math.Max(1.0f, expectedDamage);
            }

            if (expectedDamage >= adjacentTarget.Value.CurrentHp)
            {
                reward *= BattleAIDefines.OneHitKillMultiplier;
            }

            // Adjust aggressiveness based on entity type
            if (entity.Type != EntityType.Player)
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
    private void EvaluateDefendAction(EntityInfo entity, List<ActionReward> actions, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField)
    {
        var targets = entity.Type == EntityType.Player ?
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
        var allies = entity.Type == EntityType.Player ?
            players.Where(p => p.CurrentHp > 0) :
            enemies.Where(e => e.CurrentHp > 0);

        if (allies.Count() == 1)
        {
            actions.Add(new ActionReward("defend", -50f));
            return;
        }

        float reward = 0.1f; // Very low base reward for defense

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
        if (entity.Type != EntityType.Player)
        {
            reward *= BattleAIDefines.NonPlayerDefendMultiplier;
        }

        actions.Add(new ActionReward("defend", reward));
    }

    /// <summary>
    /// Evaluate move action
    /// </summary>
    private void EvaluateMoveAction(EntityInfo entity, List<ActionReward> actions, EntityInfo? adjacentTarget, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        var targets = entity.Type == EntityType.Player ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

        if (!targets.Any())
        {
            actions.Add(new ActionReward("move", 0.1f));
            return;
        }

        bool isLastEnemy = targets.Count() == 1;
        var allies = entity.Type == EntityType.Player ?
            players.Where(p => p.CurrentHp > 0) :
            enemies.Where(e => e.CurrentHp > 0);
        bool isLastAlly = allies.Count() == 1;

        if (adjacentTarget != null)
        {
            float hpRatio = (float)entity.CurrentHp / entity.MaxHp;
            float enemyHpRatio = (float)adjacentTarget.Value.CurrentHp / adjacentTarget.Value.MaxHp;

            if (isLastEnemy || isLastAlly)
            {
                actions.Add(new ActionReward("move", 0.1f));
            }
            else if (hpRatio < BattleAIDefines.LowHpRatio && enemyHpRatio > BattleAIDefines.HighHpRatio)
            {
                actions.Add(new ActionReward("move", 3.0f));
            }
            else
            {
                actions.Add(new ActionReward("move", 0.5f));
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
                reward *= (1.0f + (1.0f - hpRatio));
            }

            if (utilities.CanSurroundEnemy(entity, nearestTarget.Value, players, enemies))
            {
                reward += BattleAIDefines.MoveToSurroundReward;
            }

            if (entity.Type != EntityType.Player)
            {
                reward *= BattleAIDefines.NonPlayerMoveMultiplier;
            }

            actions.Add(new ActionReward("move", reward, nearestTarget));
        }

        if (lowestHpTarget != null && (nearestTarget == null || lowestHpTarget.Value.Id != nearestTarget.Value.Id))
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
                reward *= (5.0f / (distanceToLowest + 1));
            }

            if (entity.Type != EntityType.Player)
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
