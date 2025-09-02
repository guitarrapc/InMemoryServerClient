using BattleLogic.Constants;
using BattleLogic.Models;

namespace BattleLogic.Services;

/// <summary>
/// Handles combat calculations and damage processing
/// </summary>
internal class BattleCombat(Random random, BattleField battleField, BattleUtilities utilities, DamageCalculationFormula damageFormula)
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
        var (damage, isCriticalHit) = CalculateDamage(attackerAttack, targetDefense, attacker.CriticalRate, target.IsDefending);

        // Apply damage
        int newHp = Math.Max(0, target.CurrentHp - damage);
        utilities.UpdateEntityHp(target, newHp, players, enemies);

        // Log the attack
        var criticalText = isCriticalHit ? " [CRITICAL HIT!]" : "";
        battleLogs.Add($"{attacker.Name} attacks {target.Name} for {damage} damage! (ATK: {attackerAttack}, DEF: {targetDefense}){criticalText}" + (target.IsDefending ? " (Reduced by defense)" : ""));

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
    /// Calculate damage including critical hit and defense calculations
    /// </summary>
    /// <param name="attackPower">Attacker's attack power (after flavor)</param>
    /// <param name="defensePower">Target's defense power (after flavor)</param>
    /// <param name="criticalRate">Attacker's critical hit rate</param>
    /// <param name="isDefending">Whether target is defending</param>
    /// <returns>Calculated damage and whether it was a critical hit</returns>
    private (int damage, bool isCriticalHit) CalculateDamage(int attackPower, int defensePower, int criticalRate, bool isDefending)
    {
        return DamageCalculationService.CalculateDamage(
            damageFormula,
            attackPower,
            defensePower,
            criticalRate,
            isDefending,
            random);
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
