using BattleLogic.Constants;
using Shared.BattleLogic.Models;

namespace BattleLogic.Services;

/// <summary>
/// Handles battle field management
/// </summary>
internal class BattleField
{
    private readonly Guid?[,] _field;
    private readonly Random _random;

    public BattleField(Random random)
    {
        _field = new Guid?[BattleSystemDefines.BattleFieldSize.Y, BattleSystemDefines.BattleFieldSize.X];
        _random = random;
        ClearField();
    }

    /// <summary>
    /// Clear the battle field
    /// </summary>
    public void ClearField()
    {
        for (int y = 0; y < BattleSystemDefines.BattleFieldSize.Y; y++)
        {
            for (int x = 0; x < BattleSystemDefines.BattleFieldSize.X; x++)
            {
                _field[y, x] = null;
            }
        }
    }

    /// <summary>
    /// Place entities on the battle field
    /// </summary>
    public void PlaceEntities(List<EntityInfo> players, List<EntityInfo> enemies)
    {
        PlacePlayers(players);
        PlaceEnemies(enemies);
    }

    /// <summary>
    /// Place players on the battle field
    /// </summary>
    private void PlacePlayers(List<EntityInfo> players)
    {
        for (int i = 0; i < players.Count; i++)
        {
            int attempts = 0;
            while (attempts < 100) // Prevent infinite loop
            {
                int x = _random.Next(BattleSystemDefines.BattleFieldSize.X);
                int y = BattleSystemDefines.BattleFieldSize.Y - _random.Next(1, 4); // Bottom 3 rows

                if (IsValidPosition(x, y) && _field[y, x] == null)
                {
                    _field[y, x] = players[i].EntityId;
                    players[i] = players[i] with { Position = new Vector2(x, y) };
                    break;
                }
                attempts++;
            }
        }
    }

    /// <summary>
    /// Place enemies on the battle field
    /// </summary>
    private void PlaceEnemies(List<EntityInfo> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            int attempts = 0;
            while (attempts < 100) // Prevent infinite loop
            {
                int x = _random.Next(BattleSystemDefines.BattleFieldSize.X);
                int y = _random.Next(0, 7); // Top 7 rows

                if (IsValidPosition(x, y) && _field[y, x] == null)
                {
                    _field[y, x] = enemies[i].EntityId;
                    enemies[i] = enemies[i] with { Position = new Vector2(x, y) };
                    break;
                }
                attempts++;
            }
        }
    }

    /// <summary>
    /// Check if position is valid
    /// </summary>
    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < BattleSystemDefines.BattleFieldSize.X &&
               y >= 0 && y < BattleSystemDefines.BattleFieldSize.Y;
    }

    /// <summary>
    /// Check if position is empty
    /// </summary>
    public bool IsPositionEmpty(int x, int y)
    {
        return IsValidPosition(x, y) && _field[y, x] == null;
    }

    /// <summary>
    /// Move entity on the field
    /// </summary>
    public void MoveEntity(Guid entityId, Vector2 oldPosition, Vector2 newPosition)
    {
        if (IsValidPosition(oldPosition.X, oldPosition.Y))
        {
            _field[oldPosition.Y, oldPosition.X] = null;
        }

        if (IsValidPosition(newPosition.X, newPosition.Y))
        {
            _field[newPosition.Y, newPosition.X] = entityId;
        }
    }

    /// <summary>
    /// Remove entity from field (when defeated)
    /// </summary>
    public void RemoveEntity(Vector2 position)
    {
        if (IsValidPosition(position.X, position.Y))
        {
            _field[position.Y, position.X] = null;
        }
    }

    /// <summary>
    /// Get entity ID at position
    /// </summary>
    public Guid? GetEntityAt(int x, int y)
    {
        if (IsValidPosition(x, y))
        {
            return _field[y, x];
        }
        return null;
    }

    /// <summary>
    /// Get field snapshot for serialization
    /// </summary>
    public ReadOnlyMemory<ReadOnlyMemory<Guid?>> GetFieldSnapshot()
    {
        var cells = new Guid?[BattleSystemDefines.BattleFieldSize.Y][];
        for (int y = 0; y < BattleSystemDefines.BattleFieldSize.Y; y++)
        {
            cells[y] = new Guid?[BattleSystemDefines.BattleFieldSize.Y];
            for (int x = 0; x < BattleSystemDefines.BattleFieldSize.X; x++)
            {
                cells[y][x] = _field[y, x];
            }
        }

        var rowMemories = new ReadOnlyMemory<Guid?>[BattleSystemDefines.BattleFieldSize.Y];
        for (int y = 0; y < BattleSystemDefines.BattleFieldSize.Y; y++)
        {
            rowMemories[y] = cells[y].AsMemory();
        }

        return rowMemories.AsMemory();
    }
}
