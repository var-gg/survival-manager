using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Numerics;

namespace SM.Combat.Services;

/// <summary>
/// Delivers one deterministic non-primary pressure budget per enemy damage action. This path deliberately
/// bypasses crit, dodge/block rolls, offensive triggers, drain, direct-hit energy, and combo processing.
/// </summary>
internal static class SecondaryPressureService
{
    internal const string EventNote = "endless_heat_secondary_pressure";
    internal const string KillNote = "endless_heat_secondary_pressure_kill";

    public static void Apply(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot primaryTarget,
        BattleActionType actionType,
        BattleSkillSpec? skill,
        DamageType damageType,
        bool isDamageAction,
        float primaryDamageAfterMitigation,
        ICollection<BattleEvent> events)
    {
        if (!isDamageAction
            || actor.Side != TeamSide.Enemy
            || actor.SecondaryPressureFraction.Raw <= 0)
        {
            return;
        }

        var normalizedBudget = HitResolutionService.ResolveActionRawDamageBudget(
            actor,
            skill,
            useSecondaryPressureBaseline: true);
        if (normalizedBudget.Raw <= 0)
        {
            return;
        }

        var primaryRawBudget = HitResolutionService.ResolveActionRawDamageBudget(
            actor,
            skill,
            useSecondaryPressureBaseline: false);
        var recipients = state.GetOpponents(actor.Side)
            .Where(unit => unit.IsAlive && unit.Id != primaryTarget.Id)
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var totalSecondaryBudget = normalizedBudget * actor.SecondaryPressureFraction;
        var recipientTelemetry = new List<SecondaryPressureRecipientTelemetry>(recipients.Length);
        if (recipients.Length > 0 && totalSecondaryBudget.Raw > 0)
        {
            var equalRaw = totalSecondaryBudget.Raw / recipients.Length;
            var remainder = totalSecondaryBudget.Raw % recipients.Length;
            for (var index = 0; index < recipients.Length; index++)
            {
                var target = recipients[index];
                var allocatedRaw = equalRaw + (index < remainder ? 1L : 0L);
                var rawShare = Hp64.FromRaw(allocatedRaw);
                var resolved = HitResolutionService.ResolveSecondaryPressureDamage(
                    actor,
                    target,
                    damageType,
                    rawShare);
                var appliedRaw = Hp64.FromFloatQuantized(resolved.Value).Raw;
                recipientTelemetry.Add(new SecondaryPressureRecipientTelemetry(
                    target.Id.Value,
                    allocatedRaw,
                    appliedRaw));
                if (resolved.Value <= 0f)
                {
                    continue;
                }

                state.RegisterDamage(actor, target);
                target.TakeDamage(resolved.Value);
                BattleTelemetryRecorder.RecordImpact(
                    state,
                    TelemetryEventKind.DamageApplied,
                    actor,
                    target,
                    actionType,
                    skill,
                    resolved.Value,
                    resolved.MitigationValue,
                    EventNote);
                events.Add(CombatActionResolver.BuildEvent(
                    state,
                    actor,
                    actionType,
                    BattleLogCode.SecondaryPressureDamage,
                    target,
                    resolved.Value,
                    rawShare.ToFloat(),
                    EventNote));

                if (!target.IsAlive)
                {
                    events.Add(CombatActionResolver.BuildEvent(
                            state,
                            actor,
                            actionType,
                            BattleLogCode.SecondaryPressureDamage,
                            target,
                            0f,
                            note: KillNote)
                        with
                        {
                            EventKind = BattleEventKind.Kill,
                        });
                }
            }
        }

        state.SecondaryPressureTelemetry.Record(new SecondaryPressureActionTelemetry(
            state.StepIndex,
            actor.Id.Value,
            primaryTarget.Id.Value,
            damageType,
            normalizedBudget.Raw,
            primaryRawBudget.Raw,
            Hp64.FromFloatQuantized(primaryDamageAfterMitigation).Raw,
            recipientTelemetry));
    }
}
