using System;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Numerics;
using SM.Core.Stats;

namespace SM.Combat.Services;

public sealed record HitResolutionResult(
    float Value,
    bool WasDodged,
    bool WasCritical,
    bool WasBlocked,
    float MitigationValue,
    string Note,
    string ScreeningUnitId = "",
    bool WasRearFlank = false);

public static class HitResolutionService
{
    /// <summary>Scaling constant for armor/resist damage reduction: reduction = mitigation / (mitigation + K).</summary>
    private const float ArmorScalingK = 10f;

    /// <summary>Maximum fraction of damage that block mitigation can absorb.</summary>
    private const float MaxBlockMitigationFraction = 0.9f;
    private const int HealthPercentScale = 100;
    private const int ExecuteTargetHealthPercent = 35;
    private const int ExecuteDamageMultiplierPercent = 125;

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

        // Phase 4: damage 곱셈 체인을 Hp64×Fixed32 결정 산술로. stat/behavior 값은 아직 float이라(StatBlock fixed화는
        // 후속) 각 입력을 사용 직전 한 번 양자화하고 곱·clamp는 fixed로 한다 — float 곱셈 체인의 cross-platform
        // 분기(FMA contraction/재배열)를 제거. multiplier 순서(crit→block→mitigation→incoming→focus)와 min-damage
        // 1.0 HP floor는 종전과 동일. 결과 damage는 read-model용 float로 project한다.
        var powerHp = Hp64.FromFloatQuantized(basePower);
        var critMultiplier = critical
            ? Fixed32.One + Fixed32.Max(Fixed32.Zero, Fixed32.FromFloatQuantized(actor.Stats.Get(StatKey.CritMultiplier)))
            : Fixed32.One;
        powerHp *= critMultiplier;

        // Execute is evaluated from the target's authoritative pre-hit HP snapshot and joins immediately after
        // crit. The 1.25 multiplier is constructed in integer Q16.16 space; no new float branch or RNG is added.
        var executeApplied = actor.HasBehaviorTag(CombatBehaviorTags.ExecuteLowHp)
                             && target.IsHealthRatioAtOrBelow(ExecuteTargetHealthPercent, HealthPercentScale);
        if (executeApplied)
        {
            powerHp *= Fixed32.FromRaw(
                Fixed32.OneRaw * ExecuteDamageMultiplierPercent / HealthPercentScale);
        }

        var blocked = target.CanAttemptBlock && ChanceHits(state, actor, target, $"{actionType}:block", target.Behavior.BlockChance);
        if (blocked)
        {
            target.TriggerBlockCooldown();
            var blockKept = Fixed32.One - Fixed32.FromFloatQuantized(Math.Clamp(target.Behavior.BlockMitigation, 0f, MaxBlockMitigationFraction));
            powerHp *= blockKept;
        }

        var mitigation = ResolveEffectiveMitigation(actor, target, damageType);
        var mitigationFixed = Fixed32.FromFloatQuantized(mitigation);
        var reductionFactor = mitigation <= 0f
            ? Fixed32.One
            : Fixed32.One - (mitigationFixed / (mitigationFixed + Fixed32.FromInt((int)ArmorScalingK)));
        var incomingMultiplier = Fixed32.FromFloatQuantized(target.GetIncomingDamageMultiplier());
        var oneHp = Hp64.FromInt(1);
        var baseResolved = Hp64.Max(oneHp, (powerHp * reductionFactor) * incomingMultiplier);
        var focusMultiplier = Fixed32.FromFloatQuantized(ResolveFocusDamageMultiplier(state, actor, target, skill));
        var resolved = Hp64.Max(oneHp, baseResolved * focusMultiplier);
        state.ActivityTelemetry.RecordFocusDamageContribution((resolved - baseResolved).ToFloat());

        // P0 positional consequence — 위치가 결과를 바꾸는 항. 판정 진실은 BattleFormationConsequence,
        // 곱셈 체인 합류(crit→execute→block→mitigation→incoming→focus→screen→flank)와 1 HP floor는 여기가 소유.
        var note = critical ? "crit" : string.Empty;
        if (executeApplied)
        {
            note = ComposeNoteToken(note, "execute");
        }

        if (blocked)
        {
            note = ComposeNoteToken(note, "block");
        }
        var screenMitigation = BattleFormationConsequence.ResolveScreenMitigation(state, actor, target, out var screeningUnit);
        if (screenMitigation > 0f)
        {
            var screened = Hp64.Max(oneHp, resolved * (Fixed32.One - Fixed32.FromFloatQuantized(screenMitigation)));
            state.ActivityTelemetry.RecordScreenAbsorb((resolved - screened).ToFloat());
            resolved = screened;
            note = ComposeNoteToken(note, "screened");
        }

        var flank = BattleFormationConsequence.ResolveFlankArc(state, actor, target);
        if (flank.IsFlanking)
        {
            var flanked = Hp64.Max(oneHp, resolved * (Fixed32.One + Fixed32.FromFloatQuantized(flank.DamageBonus)));
            state.ActivityTelemetry.RecordFlankStrike((flanked - resolved).ToFloat(), isRear: flank.NoteToken == "rear");
            resolved = flanked;
            note = ComposeNoteToken(note, flank.NoteToken);
        }

        return new HitResolutionResult(
            resolved.ToFloat(),
            false,
            critical,
            blocked,
            mitigation,
            note,
            screeningUnit?.Id.Value ?? string.Empty,
            flank.NoteToken == "rear");
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
            : target.IsMarkedTarget
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

    // 정수 확률 판정(결정적, ADR-0029 §우선순위 ③). 정수 hash remainder ∈ [0,10000)을 probability의
    // basis-point threshold와 비교한다. probability는 아직 stat/behavior float이지만 아래 변환은 float
    // 산술을 사용하지 않아 backend의 중간값 반올림 차이가 판정에 유입되지 않는다.
    private static bool ChanceHits(BattleState state, UnitSnapshot actor, UnitSnapshot target, string context, float probability)
    {
        return RollBasisPoints(state, actor, target, context) < ProbabilityToBasisPoints(probability);
    }

    private static int ProbabilityToBasisPoints(float probability)
    {
        if (float.IsNaN(probability) || probability <= 0f)
        {
            return 0;
        }

        if (probability >= 1f)
        {
            return 10000;
        }

        // positive finite IEEE-754 binary32 = significand * 2^-denominatorShift.
        // Scale and round-to-nearest (half up) entirely in integer space so Mono and .NET 8
        // produce the same threshold for every possible float bit pattern.
        var bits = unchecked((uint)BitConverter.SingleToInt32Bits(probability));
        var exponent = (int)((bits >> 23) & 0xffu);
        var significand = bits & 0x7fffffu;
        var denominatorShift = 149;
        if (exponent != 0)
        {
            significand |= 0x800000u;
            denominatorShift = 150 - exponent;
        }

        var scaledSignificand = (ulong)significand * 10000UL;
        if (denominatorShift >= 64)
        {
            return 0;
        }

        var halfDenominator = 1UL << (denominatorShift - 1);
        return (int)((scaledSignificand + halfDenominator) >> denominatorShift);
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

    private static string ComposeNoteToken(string note, string token)
    {
        return string.IsNullOrEmpty(note) ? token : $"{note}+{token}";
    }
}
