using BattleLogic.Models;

namespace BattleLogic.Interfaces;

/// <summary>
/// Interface for battle field management
/// </summary>
public interface IBattleField
{
    /// <summary>
    /// Clear the battle field
    /// </summary>
    void ClearField();

    /// <summary>
    /// Place entities on the battle field
    /// </summary>
    /// <param name="players">List of players to place</param>
    /// <param name="enemies">List of enemies to place</param>
    void PlaceEntities(List<EntityInfo> players, List<EntityInfo> enemies);

    /// <summary>
    /// Check if position is valid
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>True if position is valid</returns>
    bool IsValidPosition(int x, int y);

    /// <summary>
    /// Check if position is empty
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>True if position is empty</returns>
    bool IsPositionEmpty(int x, int y);

    /// <summary>
    /// Move entity on the field
    /// </summary>
    /// <param name="entityId">ID of the entity to move</param>
    /// <param name="oldPosition">Current position</param>
    /// <param name="newPosition">New position</param>
    void MoveEntity(string entityId, Vector2 oldPosition, Vector2 newPosition);

    /// <summary>
    /// Remove entity from field (when defeated)
    /// </summary>
    /// <param name="position">Position to clear</param>
    void RemoveEntity(Vector2 position);

    /// <summary>
    /// Get entity ID at position
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>Entity ID at position, or null if empty</returns>
    string? GetEntityAt(int x, int y);

    /// <summary>
    /// Get field snapshot for serialization
    /// </summary>
    /// <returns>Read-only snapshot of the field</returns>
    ReadOnlyMemory<ReadOnlyMemory<string?>> GetFieldSnapshot();
}
