using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

/// <summary>
/// 증강·패시브의 트리거 효과(<see cref="CombatTriggeredEffect"/>)를 전투 hook 시점에 평가·실행한다.
/// 효과는 전부 기존 <see cref="UnitSnapshot"/> 연산(ApplyStatus/Heal/AddBarrier)으로 매핑되어
/// 전투에 새로운 스탯 레이어를 추가하지 않는다 (저위험). BattleStart/OnKill/OnHpBelow 트리거 지원.
/// </summary>
public static class CombatTriggerEngine
{
    /// <summary>전투 시작 hook — 모든 유닛의 BattleStart 트리거 발동.</summary>
    public static void OnBattleStart(BattleState state)
    {
        foreach (var unit in state.AllUnits)
        {
            if (!unit.IsAlive)
            {
                continue;
            }

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

    /// <summary>스텝 종료 hook — 체력이 임계 이하로 떨어진 유닛의 OnHpBelow 트리거(전투당 1회) 발동.</summary>
    public static void OnPostStep(BattleState state)
    {
        foreach (var unit in state.AllUnits)
        {
            if (!unit.IsAlive)
            {
                continue;
            }

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
                            RefreshDurationOnReapply: true));
                    }

                    break;
                case TriggeredEffectOp.Heal:
                    target.Heal(effect.Magnitude);
                    break;
                case TriggeredEffectOp.Barrier:
                    target.AddBarrier(effect.Magnitude);
                    break;
                case TriggeredEffectOp.GainEnergy:
                    target.GainEnergy(effect.Magnitude);
                    break;
            }
        }
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
