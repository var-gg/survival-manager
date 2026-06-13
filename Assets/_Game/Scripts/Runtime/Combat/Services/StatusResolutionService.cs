using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Combat.Services;

public static class StatusResolutionService
{
    public static void AdvanceStatuses(BattleState state, List<BattleEvent> stepEvents)
    {
        foreach (var unit in state.AllUnits.Where(unit => unit.IsAlive))
        {
            foreach (var status in unit.Statuses
                         .Where(status => state.StatusRules.AppliesPeriodicDamage(status.StatusId))
                         .ToList())
            {
                ApplyPeriodicDamage(state, unit, status, stepEvents);
            }

            var removedStatuses = unit.AdvanceStatusTimers();
            foreach (var status in removedStatuses)
            {
                var source = ResolveStatusSourceUnit(state, unit, status.SourceActorId);
                stepEvents.Add(BuildStatusEvent(state, source, unit, BattleEventKind.StatusRemoved, status.StatusId));
                BattleTelemetryRecorder.RecordStatus(state, TelemetryEventKind.StatusRemoved, source, unit, status.StatusId, 0f);
                if (state.StatusRules.IsHardControl(status.StatusId))
                {
                    var controlRule = state.StatusRules.ControlDiminishing;
                    unit.ApplyControlResistWindow(controlRule.WindowSeconds, controlRule.ControlResistMultiplier);
                    stepEvents.Add(BuildStatusEvent(state, unit, unit, BattleEventKind.ControlResistApplied, status.StatusId, controlRule.ControlResistMultiplier));
                }
            }

            if (unit.IsStunned)
            {
                unit.ClearTarget(applySwitchDelay: false);
                unit.SetActionState(CombatActionState.SeekTarget);
            }
        }
    }

    public static void ApplySkillStatuses(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec? skill, List<BattleEvent> stepEvents)
    {
        if (skill == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId))
        {
            ApplyCleanse(state, actor, target, skill, skill.CleanseProfileId, stepEvents);
        }

        foreach (var status in skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
        {
            ApplyStatus(state, actor, target, skill, status, stepEvents);
        }
    }

    public static bool CanUseActiveSkill(UnitSnapshot actor)
    {
        return actor.IsAlive && !actor.IsStunned && !actor.IsSilenced;
    }

    public static bool CanUseSkillSlot(UnitSnapshot actor, BattleSkillSpec skill)
    {
        if (!actor.IsAlive || actor.IsStunned)
        {
            return false;
        }

        if (skill.EffectiveSlotKind is ActionSlotKind.SignatureActive or ActionSlotKind.FlexActive)
        {
            return !actor.IsSilenced;
        }

        return true;
    }

    public static bool CanUseBasicAttack(UnitSnapshot actor)
    {
        return actor.IsAlive && !actor.IsStunned;
    }

    public static bool CanUseMobility(UnitSnapshot actor)
    {
        return actor.IsAlive && !actor.IsStunned && !actor.IsRooted;
    }

    private static void ApplyStatus(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec skill, StatusApplicationSpec spec, List<BattleEvent> stepEvents)
    {
        if (string.IsNullOrWhiteSpace(spec.StatusId))
        {
            return;
        }

        if (state.StatusRules.IsHardControl(spec.StatusId) && target.IsUnstoppable)
        {
            return;
        }

        var adjustedDuration = AdjustDurationForTenacity(state, target, spec.StatusId, spec.DurationSeconds);
        if (target.ControlResistWindow is { } resistWindow && state.StatusRules.IsHardControl(spec.StatusId))
        {
            adjustedDuration *= Math.Max(0.1f, 1f - resistWindow.ResistMultiplier);
        }

        var adjusted = spec with { DurationSeconds = adjustedDuration };
        switch (spec.StatusId)
        {
            case "barrier":
                target.AddBarrier(Math.Max(1f, spec.Magnitude));
                break;
            case "guarded":
            case "unstoppable":
            case "stun":
            case "root":
            case "silence":
            case "slow":
            case "burn":
            case "bleed":
            case "wound":
            case "sunder":
            case "marked":
            case "exposed":
                target.ApplyStatus(adjusted, actor.Id.Value, skill.Id, spec.Id);
                break;
            default:
                target.ApplyStatus(adjusted, actor.Id.Value, skill.Id, spec.Id);
                break;
        }

        stepEvents.Add(BuildStatusEvent(state, actor, target, BattleEventKind.StatusApplied, spec.StatusId, spec.Magnitude));
        BattleTelemetryRecorder.RecordStatus(state, TelemetryEventKind.StatusApplied, actor, target, spec.StatusId, spec.Magnitude);
    }

    private static void ApplyCleanse(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec skill, string cleanseProfileId, List<BattleEvent> stepEvents)
    {
        if (!state.StatusRules.TryGetCleanseProfile(cleanseProfileId, out var cleanseRule))
        {
            return;
        }

        var removed = target.RemoveStatuses(cleanseRule.RemovesStatusIds);
        if (cleanseRule.RemovesOneHardControl)
        {
            var hardControl = target.Statuses
                .Select(status => status.StatusId)
                .FirstOrDefault(state.StatusRules.IsHardControl);
            if (!string.IsNullOrWhiteSpace(hardControl) && target.RemoveStatus(hardControl))
            {
                removed++;
            }
        }

        if (cleanseRule.GrantsUnstoppable)
        {
            target.ApplyStatus(new StatusApplicationSpec(
                $"status.{cleanseRule.Id}.unstoppable",
                "unstoppable",
                Math.Max(0.1f, cleanseRule.GrantedUnstoppableDurationSeconds),
                0f),
                actor.Id.Value,
                skill.Id,
                cleanseRule.Id);
        }

        if (removed > 0 || cleanseRule.GrantsUnstoppable)
        {
            stepEvents.Add(BuildStatusEvent(state, actor, target, BattleEventKind.CleanseTriggered, cleanseProfileId, removed));
            if (cleanseRule.GrantsUnstoppable)
            {
                var controlRule = state.StatusRules.ControlDiminishing;
                target.ApplyControlResistWindow(controlRule.WindowSeconds, controlRule.ControlResistMultiplier);
                stepEvents.Add(BuildStatusEvent(state, actor, target, BattleEventKind.ControlResistApplied, "unstoppable", controlRule.ControlResistMultiplier));
                BattleTelemetryRecorder.RecordStatus(state, TelemetryEventKind.StatusApplied, actor, target, "unstoppable", controlRule.ControlResistMultiplier);
            }
        }
    }

    private static void ApplyPeriodicDamage(BattleState state, UnitSnapshot unit, AppliedStatusState status, List<BattleEvent> stepEvents)
    {
        if (!unit.HasStatus(status.StatusId))
        {
            return;
        }

        var source = ResolveStatusSourceUnit(state, unit, status.SourceActorId);
        var damage = Math.Max(1f, status.Magnitude);
        unit.TakeDamage(damage);
        BattleTelemetryRecorder.RecordStatusTick(state, source, unit, status.StatusId, damage);
        stepEvents.Add(new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            source.Id,
            source.Definition.Name,
            BattleActionType.ActiveSkill,
            BattleLogCode.Generic,
            unit.Id,
            unit.Definition.Name,
            damage,
            BattleEventKind.Action,
            status.StatusId,
            0f,
            "status_tick"));
    }

    private static UnitSnapshot ResolveStatusSourceUnit(BattleState state, UnitSnapshot fallback, string sourceActorId)
    {
        if (string.IsNullOrWhiteSpace(sourceActorId))
        {
            return fallback;
        }

        return state.AllUnits.FirstOrDefault(unit => string.Equals(unit.Id.Value, sourceActorId, StringComparison.Ordinal)) ?? fallback;
    }

    private static float AdjustDurationForTenacity(BattleState state, UnitSnapshot target, string statusId, float durationSeconds)
    {
        var tenacity = Math.Max(0f, target.Stats.Get(StatKey.Tenacity));
        var tenacityScale = state.StatusRules.ResolveTenacityScale(statusId);
        return tenacityScale <= 0f
            ? durationSeconds
            : Math.Max(0.1f, durationSeconds * Math.Max(0.1f, 1f - (tenacity * tenacityScale)));
    }

    private static BattleEvent BuildStatusEvent(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        BattleEventKind kind,
        string payloadId,
        float value = 0f)
    {
        return new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id,
            actor.Definition.Name,
            BattleActionType.ActiveSkill,
            BattleLogCode.Generic,
            target.Id,
            target.Definition.Name,
            value,
            kind,
            payloadId);
    }
}
