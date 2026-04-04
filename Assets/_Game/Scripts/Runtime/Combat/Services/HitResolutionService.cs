using System;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Combat.Services;

public sealed record HitResolutionResult(
    float Value,
    bool WasDodged,
    bool WasCritical,
    bool WasBlocked,
    float MitigationValue,
    string Note);

public static class HitResolutionService
{
    /// <summary>Scaling constant for armor/resist damage reduction: reduction = mitigation / (mitigation + K).</summary>
    private const float ArmorScalingK = 10f;

    /// <summary>Maximum fraction of damage that block mitigation can absorb.</summary>
    private const float MaxBlockMitigationFraction = 0.9f;

    public static HitResolutionResult ResolveBasicAttack(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        return ResolveDamage(
            state,
            actor,
            target,
            BattleActionType.BasicAttack,
            DamageType.Physical,
            actor.PhysPower,
            canCrit: true);
    }

    public static HitResolutionResult ResolveSkillDamage(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec skill)
    {
        var power = skill.ResolvedPowerFlat;
        var basePower = skill.DamageType == DamageType.Magical
            ? actor.MagPower + power
            : actor.PhysPower + power;
        return ResolveDamage(
            state,
            actor,
            target,
            BattleActionType.ActiveSkill,
            skill.DamageType,
            basePower,
            skill.CanCrit);
    }

    public static float ResolveSupportValue(UnitSnapshot actor, BattleSkillSpec? skill)
    {
        if (skill == null)
        {
            return Math.Max(1f, actor.HealPower);
        }

        return skill.Kind switch
        {
            SkillKind.Heal => Math.Max(1f, actor.HealPower + skill.ResolvedPowerFlat),
            SkillKind.Shield => Math.Max(1f, actor.HealPower + skill.ResolvedPowerFlat),
            _ => Math.Max(1f, actor.HealPower + skill.ResolvedPowerFlat),
        };
    }

    private static HitResolutionResult ResolveDamage(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        BattleActionType actionType,
        DamageType damageType,
        float basePower,
        bool canCrit)
    {
        if (ShouldAvoid(state, actor, target, actionType))
        {
            return new HitResolutionResult(0f, true, false, false, 0f, "dodge");
        }

        var critical = canCrit && Roll(state, actor, target, $"{actionType}:crit") < Math.Clamp(actor.Stats.Get(StatKey.CritChance), 0f, 1f);
        var critMultiplier = critical
            ? 1f + Math.Max(0f, actor.Stats.Get(StatKey.CritMultiplier))
            : 1f;
        var powerAfterCrit = basePower * critMultiplier;

        var blocked = target.CanAttemptBlock && Roll(state, actor, target, $"{actionType}:block") < Math.Clamp(target.Behavior.BlockChance, 0f, 1f);
        if (blocked)
        {
            target.TriggerBlockCooldown();
            powerAfterCrit *= 1f - Math.Clamp(target.Behavior.BlockMitigation, 0f, MaxBlockMitigationFraction);
        }

        var mitigation = damageType == DamageType.Magical ? target.Resist : target.Armor;
        var reductionFactor = 1f - (mitigation / (mitigation + ArmorScalingK));
        var resolved = Math.Max(1f, powerAfterCrit * reductionFactor * target.GetIncomingDamageMultiplier());
        var note = blocked
            ? critical ? "crit+block" : "block"
            : critical ? "crit" : string.Empty;
        return new HitResolutionResult(resolved, false, critical, blocked, mitigation, note);
    }

    private static bool ShouldAvoid(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleActionType actionType)
    {
        if (actionType == BattleActionType.WaitDefend)
        {
            return false;
        }

        return Roll(state, actor, target, $"{actionType}:dodge") < Math.Clamp(target.Behavior.DodgeChance, 0f, 1f);
    }

    private static float Roll(BattleState state, UnitSnapshot actor, UnitSnapshot target, string context)
    {
        unchecked
        {
            var hash = state.Seed;
            hash = (hash * 397) ^ state.StepIndex;
            hash = (hash * 397) ^ actor.Id.Value.GetHashCode();
            hash = (hash * 397) ^ target.Id.Value.GetHashCode();
            hash = (hash * 397) ^ context.GetHashCode();
            var remainder = Math.Abs(hash % 10000);
            return remainder / 10000f;
        }
    }
}
