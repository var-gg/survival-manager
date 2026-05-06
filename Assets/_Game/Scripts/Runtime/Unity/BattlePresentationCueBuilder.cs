using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Unity;

public sealed class BattlePresentationCueBuilder
{
    public IReadOnlyList<BattlePresentationCue> Build(BattleSimulationStep previousStep, BattleSimulationStep currentStep)
    {
        var cues = new List<BattlePresentationCue>();
        var deathCueSubjects = new HashSet<string>(System.StringComparer.Ordinal);
        var previousById = previousStep.Units.ToDictionary(unit => unit.Id);
        var currentById = currentStep.Units.ToDictionary(unit => unit.Id);

        foreach (var current in currentStep.Units)
        {
            if (!previousById.TryGetValue(current.Id, out var previous))
            {
                continue;
            }

            if (previous.ActionState != CombatActionState.ExecuteAction && current.ActionState == CombatActionState.ExecuteAction)
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.WindupEnter,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    current.PendingActionType,
                    current.WindupProgress,
                    BattlePresentationAnchorId.Cast,
                    BattlePresentationAnchorId.Center));
            }

            if (!string.Equals(previous.TargetId, current.TargetId, System.StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(current.TargetId))
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.TargetChanged,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    current.PendingActionType,
                    current.WindupProgress,
                    BattlePresentationAnchorId.Cast,
                    BattlePresentationAnchorId.Center));
            }

            if (!previous.IsDefending && current.IsDefending)
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.GuardEnter,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    BattleActionType.WaitDefend));
            }
            else if (previous.IsDefending && !current.IsDefending)
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.GuardExit,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    BattleActionType.WaitDefend));
            }

            var wasRepositioning = IsRepositioning(previous);
            var isRepositioning = IsRepositioning(current);
            if (!wasRepositioning && isRepositioning)
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.RepositionStart,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    current.PendingActionType));
            }
            else if (wasRepositioning && !isRepositioning)
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.RepositionStop,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    current.PendingActionType));
            }

            if (previous.IsAlive && !current.IsAlive)
            {
                AddDeathCue(cues, deathCueSubjects, new BattlePresentationCue(
                    BattlePresentationCueType.DeathStart,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    Magnitude: 1f,
                    SubjectAnchor: BattlePresentationAnchorId.Center));
            }
        }

        foreach (var eventData in currentStep.Events)
        {
            switch (eventData.LogCode)
            {
                case BattleLogCode.BasicAttackDamage:
                    cues.Add(new BattlePresentationCue(
                        BattlePresentationCueType.ActionCommitBasic,
                        currentStep.StepIndex,
                        eventData.ActorId.Value,
                        eventData.TargetId?.Value,
                        eventData.ActionType,
                        eventData.Value,
                        BattlePresentationAnchorId.Cast,
                        BattlePresentationAnchorId.Center));
                    TryAddTargetCue(cues, currentById, BattlePresentationCueType.ImpactDamage, currentStep.StepIndex, eventData);
                    break;

                case BattleLogCode.ActiveSkillDamage:
                    cues.Add(new BattlePresentationCue(
                        BattlePresentationCueType.ActionCommitSkill,
                        currentStep.StepIndex,
                        eventData.ActorId.Value,
                        eventData.TargetId?.Value,
                        eventData.ActionType,
                        eventData.Value,
                        BattlePresentationAnchorId.Cast,
                        BattlePresentationAnchorId.Center));
                    TryAddTargetCue(cues, currentById, BattlePresentationCueType.ImpactDamage, currentStep.StepIndex, eventData);
                    break;

                case BattleLogCode.ActiveSkillHeal:
                    cues.Add(new BattlePresentationCue(
                        BattlePresentationCueType.ActionCommitHeal,
                        currentStep.StepIndex,
                        eventData.ActorId.Value,
                        eventData.TargetId?.Value,
                        eventData.ActionType,
                        eventData.Value,
                        BattlePresentationAnchorId.Cast,
                        BattlePresentationAnchorId.Head));
                    TryAddTargetCue(cues, currentById, BattlePresentationCueType.ImpactHeal, currentStep.StepIndex, eventData, BattlePresentationAnchorId.Head);
                    break;

                case BattleLogCode.WaitDefend:
                    cues.Add(new BattlePresentationCue(
                        BattlePresentationCueType.GuardEnter,
                        currentStep.StepIndex,
                        eventData.ActorId.Value,
                        eventData.TargetId?.Value,
                        eventData.ActionType));
                    break;
            }

            if (eventData.EventKind == BattleEventKind.Kill && eventData.TargetId != null)
            {
                AddDeathCue(cues, deathCueSubjects, new BattlePresentationCue(
                    BattlePresentationCueType.DeathStart,
                    currentStep.StepIndex,
                    eventData.TargetId.Value.Value,
                    eventData.ActorId.Value,
                    eventData.ActionType,
                    eventData.Value,
                    BattlePresentationAnchorId.Center,
                    BattlePresentationAnchorId.Cast));
            }
        }

        if (!previousStep.IsFinished && currentStep.IsFinished)
        {
            foreach (var winner in currentStep.Units.Where(unit => unit.IsAlive))
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.BattleResolved,
                    currentStep.StepIndex,
                    winner.Id,
                    ActionType: winner.PendingActionType));
            }
        }

        return cues;
    }

    private static void AddDeathCue(
        ICollection<BattlePresentationCue> cues,
        ISet<string> deathCueSubjects,
        BattlePresentationCue cue)
    {
        if (deathCueSubjects.Add(cue.SubjectActorId))
        {
            cues.Add(cue);
        }
    }

    private static bool IsRepositioning(BattleUnitReadModel unit)
    {
        return unit.ActionState is CombatActionState.Reposition or CombatActionState.AdvanceToAnchor or CombatActionState.BreakContact;
    }

    private static void TryAddTargetCue(
        ICollection<BattlePresentationCue> cues,
        IReadOnlyDictionary<string, BattleUnitReadModel> currentById,
        BattlePresentationCueType cueType,
        int stepIndex,
        BattleEvent eventData,
        BattlePresentationAnchorId subjectAnchor = BattlePresentationAnchorId.Center)
    {
        if (eventData.TargetId == null || !currentById.ContainsKey(eventData.TargetId.Value.Value))
        {
            return;
        }

        cues.Add(new BattlePresentationCue(
            cueType,
            stepIndex,
            eventData.TargetId.Value.Value,
            eventData.ActorId.Value,
            eventData.ActionType,
            eventData.Value,
            subjectAnchor,
            BattlePresentationAnchorId.Cast));
    }
}
