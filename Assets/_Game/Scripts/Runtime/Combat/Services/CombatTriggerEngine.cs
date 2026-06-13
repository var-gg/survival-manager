using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

/// <summary>
/// 증강·패시브의 트리거 효과(<see cref="CombatTriggeredEffect"/>)를 전투 hook 시점에 평가·실행한다.
/// 효과는 전부 기존 <see cref="UnitSnapshot"/> 연산(ApplyStatus/Heal/AddBarrier)으로 매핑되어
/// 전투에 새로운 스탯 레이어를 추가하지 않는다 (저위험). BattleStart/OnKill/OnHpBelow 트리거 지원.
/// Phase 3: 모든 발동은 <see cref="CombatBeat"/> 을 남기고(숨은 발동 금지), 다중 유닛 hook 은
/// 명시적 결정 순서(speed desc → side → stableId asc — 시뮬레이터 유닛 루프와 동일 계약)로 돈다.
/// </summary>
public static class CombatTriggerEngine
{
    /// <summary>전투 시작 hook — 모든 유닛의 BattleStart 트리거 발동.</summary>
    public static void OnBattleStart(BattleState state)
    {
        foreach (var unit in InStableTriggerOrder(state.AllUnits.Where(unit => unit.IsAlive)))
        {
            FireTriggers(state, unit, CombatTriggerKind.BattleStart);
        }
    }

    /// <summary>처치 hook — 처치자(killer)의 OnKill 트리거 발동.</summary>
    public static void OnKill(BattleState state, UnitSnapshot killer)
    {
        if (killer is not { IsAlive: true })
        {
            return;
        }

        FireTriggers(state, killer, CombatTriggerKind.OnKill);
    }

    /// <summary>사망 hook — 쓰러진 victim 의 생존 아군이 OnAllyDeath 트리거를 발동(같은 팀 한정, 사망마다).</summary>
    public static void OnAllyDeath(BattleState state, UnitSnapshot victim)
    {
        foreach (var unit in InStableTriggerOrder(
                     state.AllUnits.Where(unit => unit.IsAlive && unit.Side == victim.Side)))
        {
            FireTriggers(state, unit, CombatTriggerKind.OnAllyDeath);
        }
    }

    /// <summary>스텝 종료 hook — 체력이 임계 이하로 떨어진 유닛의 OnHpBelow 트리거(전투당 1회) 발동.</summary>
    public static void OnPostStep(BattleState state)
    {
        foreach (var unit in InStableTriggerOrder(state.AllUnits.Where(unit => unit.IsAlive)))
        {
            foreach (var effect in EffectsFor(unit, CombatTriggerKind.OnHpBelow))
            {
                if (unit.HealthRatio > effect.ThresholdRatio)
                {
                    continue;
                }

                if (!unit.TryLatchTrigger($"hpbelow:{effect.SourceId}:{effect.ThresholdRatio}"))
                {
                    continue;
                }

                ApplyEffect(state, unit, effect);
            }
        }
    }

    // 다중 유닛 hook 의 발동 순서 — 시뮬레이터 행동 루프와 같은 결정적 전순서(마스터 플랜
    // "simultaneous triggers: source speed desc → source stableId asc"). 효과 연산은 전부 가산적이라
    // 순서가 수치를 바꾸진 않지만, beat 의 SequenceInStep 이 이 순서를 그대로 물려받는다.
    private static IEnumerable<UnitSnapshot> InStableTriggerOrder(IEnumerable<UnitSnapshot> units)
    {
        return units
            .OrderByDescending(unit => unit.Speed)
            .ThenBy(unit => unit.Side)
            .ThenBy(unit => unit.Id.Value, System.StringComparer.Ordinal);
    }

    private static void FireTriggers(BattleState state, UnitSnapshot owner, CombatTriggerKind trigger)
    {
        foreach (var effect in EffectsFor(owner, trigger))
        {
            ApplyEffect(state, owner, effect);
        }
    }

    private static IEnumerable<CombatTriggeredEffect> EffectsFor(UnitSnapshot owner, CombatTriggerKind trigger)
    {
        return owner.Definition.EffectiveTriggeredEffects.Where(effect => effect.Trigger == trigger);
    }

    private static void ApplyEffect(BattleState state, UnitSnapshot owner, CombatTriggeredEffect effect)
    {
        foreach (var target in ResolveTargets(state, owner, effect.Scope))
        {
            var applied = false;
            switch (effect.Op)
            {
                case TriggeredEffectOp.ApplyStatus:
                    if (!string.IsNullOrEmpty(effect.StatusId))
                    {
                        target.ApplyStatus(new StatusApplicationSpec(
                            $"trig:{effect.SourceId}",
                            effect.StatusId,
                            effect.DurationSeconds,
                            effect.Magnitude,
                            effect.MaxStacks <= 0 ? 1 : effect.MaxStacks,
                            RefreshDurationOnReapply: true),
                            owner.Id.Value,
                            string.Empty,
                            effect.SourceId);
                        applied = true;
                    }

                    break;
                case TriggeredEffectOp.Heal:
                    target.Heal(effect.Magnitude);
                    applied = true;
                    break;
                case TriggeredEffectOp.Barrier:
                    target.AddBarrier(effect.Magnitude);
                    applied = true;
                    break;
                case TriggeredEffectOp.GainEnergy:
                    target.GainEnergy(effect.Magnitude);
                    applied = true;
                    break;
            }

            if (applied)
            {
                // "Every triggered augment emits a beat" — 발동한 효과는 표적 단위로 beat 에 남는다.
                // Tag = 증강/패시브 SourceId(post-fight 로그·presentation 라벨 attribution).
                state.RecordBeat(
                    ResolveBeatType(effect.Trigger),
                    owner.Side,
                    owner.Id,
                    target.Id,
                    chainId: 0,
                    ResolveBeatImportance(effect.Trigger),
                    effect.Magnitude,
                    target.Position,
                    effect.SourceId);
            }
        }
    }

    private static CombatBeatType ResolveBeatType(CombatTriggerKind trigger)
    {
        return trigger switch
        {
            CombatTriggerKind.OnKill => CombatBeatType.OnKillEffect,
            CombatTriggerKind.OnHpBelow => CombatBeatType.HpThresholdEffect,
            CombatTriggerKind.OnAllyDeath => CombatBeatType.AllyDeathEffect,
            _ => CombatBeatType.BattleStartEffect,
        };
    }

    private static int ResolveBeatImportance(CombatTriggerKind trigger)
    {
        return trigger switch
        {
            CombatTriggerKind.OnKill => CombatBeatImportance.OnKillEffect,
            CombatTriggerKind.OnHpBelow => CombatBeatImportance.HpThresholdEffect,
            CombatTriggerKind.OnAllyDeath => CombatBeatImportance.AllyDeathEffect,
            _ => CombatBeatImportance.BattleStartEffect,
        };
    }

    private static IEnumerable<UnitSnapshot> ResolveTargets(BattleState state, UnitSnapshot owner, EffectScope scope)
    {
        switch (scope)
        {
            case EffectScope.AlliedCombatants:
                return state.AllUnits.Where(unit => unit.IsAlive && unit.Side == owner.Side);
            case EffectScope.EnemyCombatants:
                return state.AllUnits.Where(unit => unit.IsAlive && unit.Side != owner.Side);
            case EffectScope.CurrentTarget:
                var target = state.FindUnit(owner.CurrentTargetId);
                return target is { IsAlive: true }
                    ? new[] { target }
                    : System.Array.Empty<UnitSnapshot>();
            case EffectScope.Self:
            default:
                return owner.IsAlive
                    ? new[] { owner }
                    : System.Array.Empty<UnitSnapshot>();
        }
    }
}
