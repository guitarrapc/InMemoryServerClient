namespace BattleLogic.Models;

/// <summary>
/// Internal structure to store action rewards for AI decision making
/// </summary>
internal readonly struct BattleAIActionReward(string action, float reward, EntityInfo? targetEntity = null, Vector2? targetPosition = null)
{
    /// <summary>
    /// The action to be taken
    /// </summary>
    public readonly string Action { get; init; } = action;

    /// <summary>
    /// The reward value for this action
    /// </summary>
    public readonly float Reward { get; init; } = reward;

    /// <summary>
    /// The target entity for this action (if applicable)
    /// </summary>
    public readonly EntityInfo? TargetEntity { get; init; } = targetEntity;

    /// <summary>
    /// The target position for this action (if applicable)
    /// </summary>
    public readonly Vector2? TargetPosition { get; init; } = targetPosition;
}
