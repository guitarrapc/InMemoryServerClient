using Shared.BattleLogic.Models;

namespace BattleLogic.Services;

/// <summary>
/// Handles entity movement logic
/// </summary>
internal class BattleMovement(Random random, BattleField battleField, BattleUtilities utilities)
{
    /// <summary>
    /// Move entity towards target or in random direction
    /// </summary>
    public bool MoveEntity(EntityInfo entity, EntityInfo? targetEntity, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs)
    {
        // Get movement directions
        var directions = GetMovementDirections(entity, targetEntity);

        // Try to move in preferred directions
        bool moved = TryMoveInDirections(entity, directions, targetEntity, players, enemies, battleLogs);

        // If failed, try random directions
        if (!moved)
        {
            battleLogs.Add($"{entity.Name} cannot move in preferred directions, trying random directions.");
            var randomDirections = GenerateRandomDirections();
            moved = TryMoveInRandomDirections(entity, randomDirections, players, enemies, battleLogs);
        }

        if (!moved)
        {
            battleLogs.Add($"{entity.Name} cannot move, all paths are blocked.");
        }

        return moved;
    }

    /// <summary>
    /// Move entity and return adjacent target for potential attack after movement
    /// </summary>
    public (bool moved, EntityInfo? adjacentTargetAfterMove) MoveEntityWithAttackCheck(EntityInfo entity, EntityInfo? targetEntity, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs, BattleField battleField, BattleUtilities utilities)
    {
        bool moved = MoveEntity(entity, targetEntity, players, enemies, battleLogs);

        if (!moved)
        {
            return (false, null);
        }

        // Find the updated entity after movement
        EntityInfo? updatedEntity = null;
        if (entity.Type.IsPlayer)
        {
            updatedEntity = players.FirstOrDefault(p => p.EntityId == entity.EntityId);
        }
        else
        {
            updatedEntity = enemies.FirstOrDefault(e => e.EntityId == entity.EntityId);
        }

        if (updatedEntity == null)
        {
            return (moved, null);
        }

        // Check for adjacent targets after movement
        var adjacentTarget = utilities.FindAdjacentTarget(updatedEntity.Value, players, enemies, battleField);

        return (moved, adjacentTarget);
    }

    /// <summary>
    /// Get movement directions based on target
    /// </summary>
    private List<(int dx, int dy, int priority)> GetMovementDirections(EntityInfo entity, EntityInfo? targetEntity)
    {
        var directions = new List<(int dx, int dy, int priority)>();

        if (targetEntity is null)
        {
            // Generate random 8 directions with same priority
            for (int randDx = -1; randDx <= 1; randDx++)
            {
                for (int randDy = -1; randDy <= 1; randDy++)
                {
                    if (randDx == 0 && randDy == 0) continue;
                    directions.Add((randDx, randDy, 1));
                }
            }
            return directions.OrderBy(_ => random.Next()).ToList();
        }

        // Calculate direction towards target
        int dx = Math.Sign(targetEntity.Value.Position.X - entity.Position.X);
        int dy = Math.Sign(targetEntity.Value.Position.Y - entity.Position.Y);

        // Calculate distances
        int xDistance = Math.Abs(targetEntity.Value.Position.X - entity.Position.X);
        int yDistance = Math.Abs(targetEntity.Value.Position.Y - entity.Position.Y);

        if (xDistance == 0 && yDistance == 0)
        {
            // At same position, choose random direction
            int[] randomDirs = [-1, 0, 1];
            int randDx = randomDirs[random.Next(randomDirs.Length)];
            int randDy = randomDirs[random.Next(randomDirs.Length)];

            if (randDx == 0 && randDy == 0) randDx = 1;

            directions.Add((randDx, randDy, 1));
            directions.Add((randDy, randDx, 2));
            directions.Add((-randDx, randDy, 3));
            directions.Add((randDx, -randDy, 4));
        }
        else if (xDistance > yDistance)
        {
            // Prioritize horizontal movement
            if (dx == 0) dx = xDistance == 0 ? random.Next(2) == 0 ? 1 : -1 : Math.Sign(xDistance);

            directions.Add((dx, 0, 1));
            directions.Add((dx, dy, 2));
            directions.Add((0, dy, 3));
        }
        else
        {
            // Prioritize vertical movement
            if (dy == 0) dy = yDistance == 0 ? random.Next(2) == 0 ? 1 : -1 : Math.Sign(yDistance);

            directions.Add((0, dy, 1));
            directions.Add((dx, dy, 2));
            directions.Add((dx, 0, 3));
        }

        // Add diagonal and opposite directions as fallback
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
    /// Generate random movement directions
    /// </summary>
    private List<(int dx, int dy)> GenerateRandomDirections()
    {
        var randomDirections = new List<(int dx, int dy)>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                randomDirections.Add((dx, dy));
            }
        }
        return randomDirections.OrderBy(_ => random.Next()).ToList();
    }

    /// <summary>
    /// Try to move in specified directions
    /// </summary>
    private bool TryMoveInDirections(EntityInfo entity, List<(int dx, int dy, int priority)> directions,
        EntityInfo? targetEntity, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs)
    {
        foreach (var direction in directions.OrderBy(d => d.priority))
        {
            int newX = entity.Position.X + direction.dx;
            int newY = entity.Position.Y + direction.dy;

            if (battleField.IsPositionEmpty(newX, newY))
            {
                // Update entity position
                var oldPosition = entity.Position;
                var newPosition = new Vector2(newX, newY);

                utilities.UpdateEntityPosition(entity, newPosition, players, enemies);
                battleField.MoveEntity(entity.EntityId, oldPosition, newPosition);

                if (targetEntity != null)
                {
                    battleLogs.Add($"{entity.Name} moves from ({oldPosition.X},{oldPosition.Y}) to ({newX},{newY}) towards {targetEntity.Value.Name}.");
                }
                else
                {
                    battleLogs.Add($"{entity.Name} moves from ({oldPosition.X},{oldPosition.Y}) to ({newX},{newY}).");
                }

                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Try to move in random directions
    /// </summary>
    private bool TryMoveInRandomDirections(EntityInfo entity, List<(int dx, int dy)> randomDirections,
        List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs)
    {
        foreach (var (dx, dy) in randomDirections)
        {
            int newX = entity.Position.X + dx;
            int newY = entity.Position.Y + dy;

            if (battleField.IsPositionEmpty(newX, newY))
            {
                var oldPosition = entity.Position;
                var newPosition = new Vector2(newX, newY);

                utilities.UpdateEntityPosition(entity, newPosition, players, enemies);
                battleField.MoveEntity(entity.EntityId, oldPosition, newPosition);

                battleLogs.Add($"{entity.Name} randomly moves from ({oldPosition.X},{oldPosition.Y}) to ({newX},{newY}).");
                return true;
            }
        }
        return false;
    }
}
