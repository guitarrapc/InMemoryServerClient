using BattleLogic.Constans;

namespace BattleLogic.Services;

/// <summary>
/// Handles combat calculations and damage processing
/// </summary>
internal class BattleCombat(Random random, BattleField battleField, BattleUtilities utilities)
{
    /// <summary>
    /// Execute attack between entities
    /// </summary>
    public void ExecuteAttack(EntityInfo attacker, EntityInfo target, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs)
    {
        // Apply flavor variations to stats for this attack
        var attackerAccuracy = BattleCalculations.ApplyAccuracyFlavor(attacker.Accuracy, random);
        var targetEvasion = BattleCalculations.ApplyEvasionFlavor(target.Evasion, random);
        var attackerAttack = BattleCalculations.ApplyAttackFlavor(attacker.Attack, random);
        var targetDefense = BattleCalculations.ApplyDefenseFlavor(target.Defense, random);

        // Hit chance calculation: Final hit chance = Attacker's Accuracy - Target's Evasion
        var finalHitChance = Math.Max(0, attackerAccuracy - targetEvasion);
        int hitRoll = random.Next(1, 101); // 1-100の乱数

        if (hitRoll > finalHitChance)
        {
            // Attack missed/evaded
            battleLogs.Add($"{attacker.Name} attacks {target.Name} but {(targetEvasion > 0 ? "it's evaded" : "misses")}! (Hit chance: {finalHitChance}% = {attackerAccuracy}% ACC - {targetEvasion}% EVA)");
            return;
        }

        // Attack hits - Calculate damage
        int damage = Math.Max(1, attackerAttack - (target.IsDefending ? targetDefense * 2 : targetDefense) / 2);

        // Apply damage reduction if target is defending
        if (target.IsDefending)
        {
            damage = damage * (100 - BattleSystemDefines.DefenseDamageReductionPercent) / 100;
            damage = Math.Max(1, damage); // Minimum 1 damage
        }

        // Apply damage
        int newHp = Math.Max(0, target.CurrentHp - damage);
        utilities.UpdateEntityHp(target, newHp, players, enemies);

        // Log the attack
        battleLogs.Add($"{attacker.Name} attacks {target.Name} for {damage} damage! (ATK: {attackerAttack}, DEF: {targetDefense})" + (target.IsDefending ? " (Reduced by defense)" : ""));

        if (newHp <= 0)
        {
            HandleEntityDefeat(target, players, enemies, battleLogs);
        }
        else
        {
            battleLogs.Add($"{target.Name} has {newHp}/{target.MaxHp} HP remaining.");
        }
    }

    /// <summary>
    /// Handle entity defeat
    /// </summary>
    private void HandleEntityDefeat(EntityInfo defeatedEntity, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs)
    {
        battleLogs.Add($"{defeatedEntity.Name} has been defeated!");

        // Clear the defeated entity from the battle field
        battleField.RemoveEntity(defeatedEntity.Position);

        // Update the entity's position to invalid coordinates
        utilities.UpdateEntityPosition(defeatedEntity, Vector2.InvalidPosition, players, enemies);
    }

    /// <summary>
    /// Execute defend action
    /// </summary>
    public void ExecuteDefend(EntityInfo entity, List<EntityInfo> players, List<EntityInfo> enemies, List<string> battleLogs)
    {
        utilities.UpdateEntityDefending(entity, true, players, enemies);
        battleLogs.Add($"{entity.Name} takes a defensive stance, reducing incoming damage by {BattleSystemDefines.DefenseDamageReductionPercent}%.");
    }
}
