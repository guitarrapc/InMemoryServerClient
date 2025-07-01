using Shared;

namespace InMemoryServer.Battle;

/// <summary>
/// Utility functions for battle operations
/// </summary>
public class BattleUtilities
{
    /// <summary>
    /// Update entity position in the appropriate list
    /// </summary>
    public void UpdateEntityPosition(EntityInfo entity, Vector2 newPosition, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        if (entity.Type == "Player")
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == entity.Id)
                {
                    players[i] = players[i] with { Position = newPosition };
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Id == entity.Id)
                {
                    enemies[i] = enemies[i] with { Position = newPosition };
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Update entity HP in the appropriate list
    /// </summary>
    public void UpdateEntityHp(EntityInfo entity, int newHp, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        if (entity.Type == "Player")
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == entity.Id)
                {
                    players[i] = players[i] with { CurrentHp = newHp };
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Id == entity.Id)
                {
                    enemies[i] = enemies[i] with { CurrentHp = newHp };
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Update entity defending status
    /// </summary>
    public void UpdateEntityDefending(EntityInfo entity, bool isDefending, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        if (entity.Type == "Player")
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == entity.Id)
                {
                    players[i] = players[i] with { IsDefending = isDefending };
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Id == entity.Id)
                {
                    enemies[i] = enemies[i] with { IsDefending = isDefending };
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Find an adjacent target for attack
    /// </summary>
    public EntityInfo? FindAdjacentTarget(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, BattleField battleField)
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
                string? targetId = battleField.GetEntityAt(checkX, checkY);

                if (targetId != null)
                {
                    EntityInfo? target = null;

                    // Find entity with matching ID
                    if (!string.IsNullOrEmpty(entity.Type) && entity.Type == "Player")
                    {
                        target = enemies.FirstOrDefault(e => e.Id == targetId && e.CurrentHp > 0);
                    }
                    else
                    {
                        target = players.FirstOrDefault(p => p.Id == targetId && p.CurrentHp > 0);
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
    /// Find the nearest target
    /// </summary>
    public EntityInfo? FindNearestTarget(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        EntityInfo? nearestTarget = null;
        int minDistance = int.MaxValue;

        var targets = entity.Type == "Player" ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

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
    public EntityInfo? FindLowestHpTarget(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        EntityInfo? lowestHpTarget = null;
        int lowestHp = int.MaxValue;

        var targets = entity.Type == "Player" ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

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
    public bool AreEnemiesNearby(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, int distanceThreshold)
    {
        var targets = entity.Type == "Player" ?
            enemies.Where(e => e.CurrentHp > 0) :
            players.Where(p => p.CurrentHp > 0);

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
    public bool CanSurroundEnemy(EntityInfo entity, EntityInfo target, List<EntityInfo> players, List<EntityInfo> enemies)
    {
        // Get allied positions
        var allies = entity.Type == "Player" ?
            players.Where(p => p.Id != entity.Id && p.CurrentHp > 0) :
            enemies.Where(e => e.Id != entity.Id && e.CurrentHp > 0);

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
    public int CalculateManhattanDistance(Vector2 a, Vector2 b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    /// <summary>
    /// Check if battle is over
    /// </summary>
    public (bool isOver, bool isPlayerVictory) CheckBattleOver(List<EntityInfo> players, List<EntityInfo> enemies)
    {
        bool allPlayersDead = players.All(p => p.CurrentHp <= 0);
        bool allEnemiesDead = enemies.All(e => e.CurrentHp <= 0);

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
    /// Reset defending status for all entities
    /// </summary>
    public void ResetDefendingStatus(List<EntityInfo> players, List<EntityInfo> enemies)
    {
        for (int i = 0; i < players.Count; i++)
        {
            players[i] = players[i] with { IsDefending = false };
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            enemies[i] = enemies[i] with { IsDefending = false };
        }
    }
}
