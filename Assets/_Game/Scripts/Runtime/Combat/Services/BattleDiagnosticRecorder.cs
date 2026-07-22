using SM.Combat.Model;

namespace SM.Combat.Services;

/// <summary>
/// Emits optional measurement-only records to the observer attached to a battle state. It owns no collection
/// and never writes canonical telemetry, replay state, or combat truth.
/// </summary>
internal static class BattleDiagnosticRecorder
{
    internal static void RecordIntentOverride(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? preIntentTarget,
        bool overrideApplied,
        UnitSnapshot? finalTarget)
    {
        if (!state.ShouldObserveDiagnostic(BattleDiagnosticKind.IntentOverride, actor.Id.Value))
        {
            return;
        }

        state.RecordDiagnostic(new IntentOverrideDiagnosticEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id.Value,
            preIntentTarget?.Id.Value ?? string.Empty,
            actor.CurrentCombatIntent.Type,
            actor.CurrentCombatIntent.TargetId?.Value ?? string.Empty,
            overrideApplied,
            finalTarget?.Id.Value ?? string.Empty));
    }

    internal static void RecordDisplacementSelected(
        BattleState state,
        UnitSnapshot actor,
        EvaluatedAction evaluated)
    {
        var skill = evaluated.Skill;
        if (skill == null
            || skill.DisplacementKind == SM.Core.Contracts.SkillDisplacementKind.None
            || !state.ShouldObserveDiagnostic(BattleDiagnosticKind.DisplacementLifecycle, actor.Id.Value, skill.Id))
        {
            return;
        }

        RecordDisplacement(
            state,
            actor,
            evaluated.Target,
            skill,
            DisplacementLifecycleStage.Selected,
            0,
            0f,
            0f,
            string.Empty);
    }

    internal static void RecordDisplacementCastStarted(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target,
        BattleSkillSpec? skill)
    {
        if (skill == null
            || skill.DisplacementKind == SM.Core.Contracts.SkillDisplacementKind.None
            || !state.ShouldObserveDiagnostic(BattleDiagnosticKind.DisplacementLifecycle, actor.Id.Value, skill.Id))
        {
            return;
        }

        RecordDisplacement(
            state,
            actor,
            target,
            skill,
            DisplacementLifecycleStage.CastStarted,
            actor.PendingActionInstanceId.Value,
            0f,
            0f,
            string.Empty);
    }

    internal static void RecordDisplacementResolved(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target,
        BattleSkillSpec? skill,
        long actionInstanceId,
        CombatVector2 actorPositionBefore,
        CombatVector2 targetPositionBefore)
    {
        if (skill == null
            || skill.DisplacementKind == SM.Core.Contracts.SkillDisplacementKind.None
            || !state.ShouldObserveDiagnostic(BattleDiagnosticKind.DisplacementLifecycle, actor.Id.Value, skill.Id))
        {
            return;
        }

        RecordDisplacement(
            state,
            actor,
            target,
            skill,
            DisplacementLifecycleStage.Resolved,
            actionInstanceId,
            actor.Position.DistanceTo(actorPositionBefore),
            target == null ? 0f : target.Position.DistanceTo(targetPositionBefore),
            string.Empty);
    }

    internal static void RecordDisplacementAborted(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target,
        BattleSkillSpec? skill,
        string reason)
    {
        if (skill == null
            || skill.DisplacementKind == SM.Core.Contracts.SkillDisplacementKind.None
            || !state.ShouldObserveDiagnostic(BattleDiagnosticKind.DisplacementLifecycle, actor.Id.Value, skill.Id))
        {
            return;
        }

        RecordDisplacement(
            state,
            actor,
            target,
            skill,
            DisplacementLifecycleStage.Aborted,
            actor.PendingActionInstanceId.Value,
            0f,
            0f,
            reason);
    }

    internal static void RecordHealingApplication(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        string skillId,
        string sourceId,
        HealingApplicationResult result)
    {
        if (!state.ShouldObserveDiagnostic(BattleDiagnosticKind.HealingApplication, actor.Id.Value, skillId))
        {
            return;
        }

        state.RecordDiagnostic(new HealingApplicationDiagnosticEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id.Value,
            actor.Definition.ArchetypeId,
            actor.Definition.ClassId,
            target.Id.Value,
            skillId,
            sourceId,
            result.RawAmount,
            result.AttemptedAfterModifiers,
            result.EffectiveAmount,
            result.OverhealAmount));
    }

    private static void RecordDisplacement(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target,
        BattleSkillSpec skill,
        DisplacementLifecycleStage stage,
        long actionInstanceId,
        float actorDisplacement,
        float targetDisplacement,
        string abortReason)
    {
        state.RecordDiagnostic(new DisplacementLifecycleDiagnosticEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id.Value,
            actor.Definition.ArchetypeId,
            actor.Definition.ClassId,
            target?.Id.Value ?? string.Empty,
            target?.Definition.ArchetypeId ?? string.Empty,
            skill.Id,
            skill.DisplacementKind,
            skill.DisplacementDistance,
            stage,
            actionInstanceId,
            target == null ? 0f : MovementResolver.ComputeEdgeDistance(actor, target),
            actorDisplacement,
            targetDisplacement,
            abortReason));
    }
}
