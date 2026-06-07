using System;
using System.Linq;
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
        var damageType = actor.EffectiveBasicAttack.DamageType;
        var basePower = damageType == DamageType.Magical
            ? actor.MagPower
            : actor.PhysPower;
        return ResolveDamage(
            state,
            actor,
            target,
            BattleActionType.BasicAttack,
            damageType,
            basePower,
            canCrit: true);
    }

    public static HitResolutionResult ResolveSkillDamage(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec skill)
    {
        var basePower = ResolveSkillDamagePower(actor, skill);
        return ResolveDamage(
            state,
            actor,
            target,
            BattleActionType.ActiveSkill,
            skill.DamageType,
            basePower,
            skill.CanCrit,
            skill);
    }

    public static float ResolveSupportValue(UnitSnapshot actor, BattleSkillSpec? skill)
    {
        if (skill == null)
        {
            return Math.Max(1f, actor.HealPower);
        }

        return Math.Max(1f, ResolveSkillSupportPower(actor, skill));
    }

    private static HitResolutionResult ResolveDamage(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        BattleActionType actionType,
        DamageType damageType,
        float basePower,
        bool canCrit,
        BattleSkillSpec? skill = null)
    {
        if (ShouldAvoid(state, actor, target, actionType))
        {
            return new HitResolutionResult(0f, true, false, false, 0f, "dodge");
        }

        var critical = canCrit && ChanceHits(state, actor, target, $"{actionType}:crit", actor.Stats.Get(StatKey.CritChance));
        var critMultiplier = critical
            ? 1f + Math.Max(0f, actor.Stats.Get(StatKey.CritMultiplier))
            : 1f;
        var powerAfterCrit = basePower * critMultiplier;

        var blocked = target.CanAttemptBlock && ChanceHits(state, actor, target, $"{actionType}:block", target.Behavior.BlockChance);
        if (blocked)
        {
            target.TriggerBlockCooldown();
            powerAfterCrit *= 1f - Math.Clamp(target.Behavior.BlockMitigation, 0f, MaxBlockMitigationFraction);
        }

        var mitigation = ResolveEffectiveMitigation(actor, target, damageType);
        var reductionFactor = mitigation <= 0f
            ? 1f
            : 1f - (mitigation / (mitigation + ArmorScalingK));
        var baseResolved = Math.Max(1f, powerAfterCrit * reductionFactor * target.GetIncomingDamageMultiplier());
        var focusMultiplier = ResolveFocusDamageMultiplier(state, actor, target, skill);
        var resolved = Math.Max(1f, baseResolved * focusMultiplier);
        state.ActivityTelemetry.RecordFocusDamageContribution(resolved - baseResolved);
        var note = blocked
            ? critical ? "crit+block" : "block"
            : critical ? "crit" : string.Empty;
        return new HitResolutionResult(resolved, false, critical, blocked, mitigation, note);
    }

    private static float ResolveSkillDamagePower(UnitSnapshot actor, BattleSkillSpec skill)
    {
        if (!UsesAuthoredCoefficients(skill))
        {
            return skill.DamageType switch
            {
                DamageType.Magical => actor.MagPower + skill.ResolvedPowerFlat,
                DamageType.Healing => actor.HealPower + skill.ResolvedPowerFlat,
                _ => actor.PhysPower + skill.ResolvedPowerFlat,
            };
        }

        return ResolveCoefficientPower(actor, skill);
    }

    private static float ResolveSkillSupportPower(UnitSnapshot actor, BattleSkillSpec skill)
    {
        if (!UsesAuthoredCoefficients(skill))
        {
            return actor.HealPower + skill.ResolvedPowerFlat;
        }

        return ResolveCoefficientPower(actor, skill);
    }

    private static float ResolveCoefficientPower(UnitSnapshot actor, BattleSkillSpec skill)
    {
        return skill.ResolvedPowerFlat
               + (actor.PhysPower * Math.Max(0f, skill.PhysCoeff))
               + (actor.MagPower * Math.Max(0f, skill.MagCoeff))
               + (actor.HealPower * Math.Max(0f, skill.HealCoeff))
               + (actor.MaxHealth * Math.Max(0f, skill.HealthCoeff));
    }

    private static bool UsesAuthoredCoefficients(BattleSkillSpec skill)
    {
        return !IsApproximately(skill.PhysCoeff, 1f)
               || !IsApproximately(skill.MagCoeff, 0f)
               || !IsApproximately(skill.HealCoeff, 0f)
               || !IsApproximately(skill.HealthCoeff, 0f);
    }

    private static float ResolveEffectiveMitigation(UnitSnapshot actor, UnitSnapshot target, DamageType damageType)
    {
        return damageType switch
        {
            DamageType.True => 0f,
            DamageType.Magical => Math.Max(0f, target.Resist - actor.MagPen),
            _ => Math.Max(0f, target.Armor - actor.PhysPen),
        };
    }

    private static float ResolveFocusDamageMultiplier(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec? skill)
    {
        if (target.Side == actor.Side)
        {
            return 1f;
        }

        var committed = state.GetOpponents(target.Side)
            .Count(unit => unit.IsAlive
                           && (unit.Id == actor.Id || unit.CurrentTargetId == target.Id || unit.PendingTargetId == target.Id));
        var focusCount = Math.Min(4, Math.Max(1, committed));
        if (focusCount <= 1)
        {
            return 1f;
        }

        var context = state.GetTacticContext(actor.Side);
        var focusLink = 0.035f + (0.015f * Math.Max(0f, context.FocusModeBias));
        var cap = skill?.AllowsEliteFocusCap == true
            ? 0.30f
            : target.HasStatus("marked")
                ? 0.20f
                : 0.15f;
        var bonus = Math.Min(cap, focusLink * Math.Max(0, focusCount - 1));
        return 1f + bonus;
    }

    private static bool ShouldAvoid(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleActionType actionType)
    {
        if (actionType == BattleActionType.WaitDefend)
        {
            return false;
        }

        return ChanceHits(state, actor, target, $"{actionType}:dodge", target.Behavior.DodgeChance);
    }

    // 정수 확률 판정(결정적, ADR-0029 §우선순위 ③). 정수 hash remainder ∈ [0,10000)을 확률×10000 정수
    // threshold와 비교한다 — float roll(/10000f)·float 비교를 제거. probability는 아직 stat/behavior float이며
    // (Phase 4에서 fixed 전환) 여기서 ×10000 절단으로 basis-point threshold(0..10000)에 단 한 번 양자화한다.
    private static bool ChanceHits(BattleState state, UnitSnapshot actor, UnitSnapshot target, string context, float probability)
    {
        return RollBasisPoints(state, actor, target, context) < ProbabilityToBasisPoints(probability);
    }

    private static int ProbabilityToBasisPoints(float probability)
    {
        return (int)(Math.Clamp(probability, 0f, 1f) * 10000f);
    }

    private static int RollBasisPoints(BattleState state, UnitSnapshot actor, UnitSnapshot target, string context)
    {
        unchecked
        {
            var hash = state.Seed;
            hash = (hash * 397) ^ state.StepIndex;
            hash = (hash * 397) ^ StableHash(actor.Id.Value);
            hash = (hash * 397) ^ StableHash(target.Id.Value);
            hash = (hash * 397) ^ StableHash(context);
            return Math.Abs(hash % 10000);
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return hash;
        }
    }

    private static bool IsApproximately(float left, float right)
    {
        return Math.Abs(left - right) <= 0.0001f;
    }
}
