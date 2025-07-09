using BattleLogic.Constans;

namespace BattleLogic.Services;

/// <summary>
/// Handles battle field management
/// </summary>
internal class BattleField
{
    private readonly string?[,] _field;
    private readonly Random _random;

    public BattleField(Random random)
    {
        _field = new string[BattleSystemDefines.BattleFieldHeight, BattleSystemDefines.BattleFieldWidth];
        _random = random;
        ClearField();
    }

    /// <summary>
    /// Clear the battle field
    /// </summary>
    public void ClearField()
    {
        for (int y = 0; y < BattleSystemDefines.BattleFieldHeight; y++)
        {
            for (int x = 0; x < BattleSystemDefines.BattleFieldWidth; x++)
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
                int x = _random.Next(BattleSystemDefines.BattleFieldWidth);
                int y = BattleSystemDefines.BattleFieldHeight - _random.Next(1, 4); // Bottom 3 rows

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
                int x = _random.Next(BattleSystemDefines.BattleFieldWidth);
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
        return x >= 0 && x < BattleSystemDefines.BattleFieldWidth &&
               y >= 0 && y < BattleSystemDefines.BattleFieldHeight;
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
    public void MoveEntity(string entityId, Vector2 oldPosition, Vector2 newPosition)
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
    public string? GetEntityAt(int x, int y)
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
    public ReadOnlyMemory<ReadOnlyMemory<string?>> GetFieldSnapshot()
    {
        var cells = new string?[BattleSystemDefines.BattleFieldHeight][];
        for (int y = 0; y < BattleSystemDefines.BattleFieldHeight; y++)
        {
            cells[y] = new string?[BattleSystemDefines.BattleFieldWidth];
            for (int x = 0; x < BattleSystemDefines.BattleFieldWidth; x++)
            {
                cells[y][x] = _field[y, x];
            }
        }

        var rowMemories = new ReadOnlyMemory<string?>[BattleSystemDefines.BattleFieldHeight];
        for (int y = 0; y < BattleSystemDefines.BattleFieldHeight; y++)
        {
            rowMemories[y] = cells[y].AsMemory();
        }

        return rowMemories.AsMemory();
    }
}
