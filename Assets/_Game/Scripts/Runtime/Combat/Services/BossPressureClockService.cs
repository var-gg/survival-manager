using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

internal static class BossPressureClockService
{
    private const string EventNote = "boss_pressure_clock";

    public static void Advance(BattleState state, ICollection<BattleEvent> stepEvents)
    {
        var clockOwner = state.LivingEnemies
            .Where(unit => unit.Definition.BossPressureClock?.IsEnabled == true)
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        var clock = clockOwner?.Definition.BossPressureClock;
        if (clockOwner == null
            || clock == null
            || !clock.TryResolvePulse(state.StepIndex, state.FixedStepSeconds, out var pulseIndex))
        {
            return;
        }

        foreach (var target in state.LivingAllies
                     .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
                     .ToList())
        {
            var damage = target.MaxHealth * Math.Clamp(clock.MaxHealthDamageRatio, 0f, 1f);
            if (damage <= 0f)
            {
                continue;
            }

            state.RegisterDamage(clockOwner, target);
            target.TakeDamage(damage);
            target.GainEnergyFromDirectHitTaken();
            BattleTelemetryRecorder.RecordImpact(
                state,
                TelemetryEventKind.DamageApplied,
                clockOwner,
                target,
                BattleActionType.ActiveSkill,
                null,
                damage,
                pulseIndex + 1,
                EventNote);
            stepEvents.Add(CombatActionResolver.BuildEvent(
                state,
                clockOwner,
                BattleActionType.ActiveSkill,
                BattleLogCode.ActiveSkillDamage,
                target,
                damage,
                pulseIndex + 1,
                $"{EventNote}:pulse_{pulseIndex + 1}"));

            if (!target.IsAlive)
            {
                foreach (var killEvent in CombatActionResolver.ResolveKillAndAssist(
                             state,
                             clockOwner,
                             target,
                             BattleActionType.ActiveSkill,
                             null))
                {
                    stepEvents.Add(killEvent);
                }
            }
        }
    }
}
