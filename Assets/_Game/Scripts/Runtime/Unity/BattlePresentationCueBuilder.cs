using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Unity;

public sealed class BattlePresentationCueBuilder
{
    private const float HeavyImpactDamageThreshold = 16f;
    private const float DisplacementTraceDistanceThreshold = 0.35f;

    public IReadOnlyList<BattlePresentationCue> Build(BattleSimulationStep previousStep, BattleSimulationStep currentStep)
    {
        var cues = new List<BattlePresentationCue>();
        var deathCueSubjects = new HashSet<string>(System.StringComparer.Ordinal);
        var previousById = previousStep.Units.ToDictionary(unit => unit.Id);
        var currentById = currentStep.Units.ToDictionary(unit => unit.Id);
        var preImpactEventsByActorId = currentStep.Events
            .Where(IsPreImpactProfileEvent)
            .GroupBy(eventData => eventData.ActorId.Value, System.StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.Ordinal);

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
                var movement = ResolveMovementAnimation(previous, current, currentById);
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.RepositionStart,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    current.PendingActionType,
                    movement.Distance,
                    Note: movement.Note,
                    AnimationSemantic: movement.Semantic,
                    AnimationDirection: movement.Direction,
                    AnimationIntensity: movement.Intensity));
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
                    SubjectAnchor: BattlePresentationAnchorId.Center,
                    AnimationSemantic: BattleAnimationSemantic.Death,
                    AnimationIntensity: BattleAnimationIntensity.Heavy));
            }

            TryAddPreImpactDisplacementTraceCue(cues, preImpactEventsByActorId, previous, current, currentStep.StepIndex);
        }

        foreach (var eventData in currentStep.Events)
        {
            switch (eventData.LogCode)
            {
                case BattleLogCode.BasicAttackDamage:
                {
                    currentById.TryGetValue(eventData.ActorId.Value, out var actor);
                    var attackAnimation = ResolveBasicAttackCommitAnimation(eventData, actor);
                    cues.Add(new BattlePresentationCue(
                        BattlePresentationCueType.ActionCommitBasic,
                        currentStep.StepIndex,
                        eventData.ActorId.Value,
                        eventData.TargetId?.Value,
                        eventData.ActionType,
                        eventData.Value,
                        BattlePresentationAnchorId.Cast,
                        BattlePresentationAnchorId.Center,
                        eventData.Note,
                        attackAnimation.Semantic,
                        attackAnimation.Direction,
                        attackAnimation.Intensity));
                    TryAddTargetCue(cues, currentById, BattlePresentationCueType.ImpactDamage, currentStep.StepIndex, eventData);
                    break;
                }

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
                    BattlePresentationAnchorId.Cast,
                    AnimationSemantic: BattleAnimationSemantic.Death,
                    AnimationIntensity: BattleAnimationIntensity.Heavy));
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

    private static BattleAnimationCueDescriptor ResolveMovementAnimation(
        BattleUnitReadModel previous,
        BattleUnitReadModel current,
        IReadOnlyDictionary<string, BattleUnitReadModel> currentById)
    {
        var deltaX = current.Position.X - previous.Position.X;
        var deltaY = current.Position.Y - previous.Position.Y;
        var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
        if (distanceSquared < 0.0025f)
        {
            return BattleAnimationCueDescriptor.None;
        }

        var distance = (float)System.Math.Sqrt(distanceSquared);
        var intensity = distanceSquared >= 1.44f
            ? BattleAnimationIntensity.Heavy
            : BattleAnimationIntensity.Medium;

        if (current.ActionState == CombatActionState.BreakContact)
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.BackstepDisengage,
                BattleAnimationDirection.Backward,
                intensity,
                "break_contact",
                distance);
        }

        if (!string.IsNullOrWhiteSpace(current.TargetId)
            && currentById.TryGetValue(current.TargetId, out var target))
        {
            var toTargetX = target.Position.X - previous.Position.X;
            var toTargetY = target.Position.Y - previous.Position.Y;
            var targetDistanceSquared = (toTargetX * toTargetX) + (toTargetY * toTargetY);
            if (targetDistanceSquared > 0.0025f)
            {
                var dot = NormalizedDot(deltaX, deltaY, toTargetX, toTargetY);
                var cross = (deltaX * toTargetY) - (deltaY * toTargetX);

                if (dot < -0.25f)
                {
                    return new BattleAnimationCueDescriptor(
                        BattleAnimationSemantic.BackstepDisengage,
                        BattleAnimationDirection.Backward,
                        intensity,
                        "move_away",
                        distance);
                }

                if (System.Math.Abs(dot) < 0.55f)
                {
                    return new BattleAnimationCueDescriptor(
                        BattleAnimationSemantic.LateralStrafe,
                        cross >= 0f ? BattleAnimationDirection.Right : BattleAnimationDirection.Left,
                        intensity,
                        "lateral",
                        distance);
                }

                if (dot > 0.25f)
                {
                    return new BattleAnimationCueDescriptor(
                        BattleAnimationSemantic.DashEngage,
                        BattleAnimationDirection.Forward,
                        intensity,
                        "engage",
                        distance);
                }
            }
        }

        return current.ActionState == CombatActionState.AdvanceToAnchor
            ? new BattleAnimationCueDescriptor(BattleAnimationSemantic.DashEngage, BattleAnimationDirection.Forward, intensity, "advance", distance)
            : new BattleAnimationCueDescriptor(BattleAnimationSemantic.LateralStrafe, BattleAnimationDirection.Lateral, intensity, "reposition", distance);
    }

    private static void TryAddPreImpactDisplacementTraceCue(
        ICollection<BattlePresentationCue> cues,
        IReadOnlyDictionary<string, BattleEvent> preImpactEventsByActorId,
        BattleUnitReadModel previous,
        BattleUnitReadModel current,
        int stepIndex)
    {
        if (IsRepositioning(previous) || IsRepositioning(current))
        {
            return;
        }

        if (!preImpactEventsByActorId.TryGetValue(current.Id, out var eventData))
        {
            return;
        }

        var deltaX = current.Position.X - previous.Position.X;
        var deltaY = current.Position.Y - previous.Position.Y;
        var distance = (float)System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance < DisplacementTraceDistanceThreshold)
        {
            return;
        }

        var animation = ResolveBasicAttackCommitAnimation(eventData);
        if (animation.Semantic == BattleAnimationSemantic.None)
        {
            return;
        }

        cues.Add(new BattlePresentationCue(
            BattlePresentationCueType.RepositionStart,
            stepIndex,
            current.Id,
            eventData.TargetId?.Value,
            eventData.ActionType,
            distance,
            BattlePresentationAnchorId.Feet,
            BattlePresentationAnchorId.Center,
            ComposeCueNote("trace_preimpact", eventData.Note),
            animation.Semantic,
            animation.Direction,
            animation.Intensity));
    }

    private static float NormalizedDot(float ax, float ay, float bx, float by)
    {
        var aLength = System.Math.Sqrt((ax * ax) + (ay * ay));
        var bLength = System.Math.Sqrt((bx * bx) + (by * by));
        if (aLength <= 0.0001d || bLength <= 0.0001d)
        {
            return 0f;
        }

        return (float)(((ax * bx) + (ay * by)) / (aLength * bLength));
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

        var animation = cueType == BattlePresentationCueType.ImpactDamage
            ? ResolveImpactAnimation(eventData)
            : BattleAnimationCueDescriptor.None;

        cues.Add(new BattlePresentationCue(
            cueType,
            stepIndex,
            eventData.TargetId.Value.Value,
            eventData.ActorId.Value,
            eventData.ActionType,
            eventData.Value,
            subjectAnchor,
            BattlePresentationAnchorId.Cast,
            eventData.Note,
            animation.Semantic,
            animation.Direction,
            animation.Intensity));
    }

    private static BattleAnimationCueDescriptor ResolveImpactAnimation(BattleEvent eventData)
    {
        if (HasNote(eventData, "miss"))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.Miss,
                BattleAnimationDirection.Any,
                BattleAnimationIntensity.Light,
                eventData.Note);
        }

        if (HasNote(eventData, "dodge"))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.Dodge,
                BattleAnimationDirection.Any,
                BattleAnimationIntensity.Light,
                eventData.Note);
        }

        if (HasNote(eventData, "knockdown"))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.Knockdown,
                BattleAnimationDirection.Backward,
                BattleAnimationIntensity.Heavy,
                eventData.Note);
        }

        var isCrit = HasNote(eventData, "crit");
        if (HasNote(eventData, "block"))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.BlockImpact,
                BattleAnimationDirection.Any,
                isCrit ? BattleAnimationIntensity.Heavy : BattleAnimationIntensity.Medium,
                eventData.Note);
        }

        if (isCrit)
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.CriticalImpact,
                BattleAnimationDirection.Backward,
                BattleAnimationIntensity.Heavy,
                eventData.Note);
        }

        if (eventData.Value >= HeavyImpactDamageThreshold)
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.HitHeavy,
                BattleAnimationDirection.Backward,
                BattleAnimationIntensity.Heavy,
                eventData.Note);
        }

        return new BattleAnimationCueDescriptor(
            BattleAnimationSemantic.HitLight,
            BattleAnimationDirection.Backward,
            BattleAnimationIntensity.Medium,
            eventData.Note);
    }

    private static BattleAnimationCueDescriptor ResolveBasicAttackCommitAnimation(BattleEvent eventData, BattleUnitReadModel? actor = null)
    {
        if (HasNote(eventData, "profile_dash"))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.DashEngage,
                BattleAnimationDirection.Forward,
                BattleAnimationIntensity.Heavy,
                eventData.Note);
        }

        if (HasNote(eventData, "profile_lunge"))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.DashEngage,
                BattleAnimationDirection.Forward,
                BattleAnimationIntensity.Medium,
                eventData.Note);
        }

        if (HasNote(eventData, "profile_stepin"))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.DashEngage,
                BattleAnimationDirection.Forward,
                BattleAnimationIntensity.Light,
                eventData.Note);
        }

        if (actor != null && IsBowBasicAttacker(actor))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.BowShot,
                BattleAnimationDirection.Forward,
                BattleAnimationIntensity.Medium,
                eventData.Note);
        }

        if (actor != null && IsProjectileBasicAttacker(actor))
        {
            return new BattleAnimationCueDescriptor(
                BattleAnimationSemantic.ProjectileCast,
                BattleAnimationDirection.Forward,
                BattleAnimationIntensity.Medium,
                eventData.Note);
        }

        return BattleAnimationCueDescriptor.None;
    }

    private static bool IsBowBasicAttacker(BattleUnitReadModel actor)
    {
        return IsTag(actor.ClassId, "ranger")
               || IsTag(actor.ArchetypeId, "hunter")
               || IsTag(actor.ArchetypeId, "scout")
               || IsTag(actor.ArchetypeId, "marksman")
               || IsTag(actor.ArchetypeId, "rift_stalker");
    }

    private static bool IsProjectileBasicAttacker(BattleUnitReadModel actor)
    {
        return IsTag(actor.ClassId, "mystic")
               || actor.PreferredRangeMin >= 1.8f
               || actor.PreferredRangeMax >= 2.4f;
    }

    private static bool HasNote(BattleEvent eventData, string token)
    {
        return !string.IsNullOrEmpty(eventData.Note)
               && eventData.Note.Contains(token, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTag(string value, string expected)
    {
        return string.Equals(value, expected, System.StringComparison.Ordinal);
    }

    private static bool IsPreImpactProfileEvent(BattleEvent eventData)
    {
        return eventData.ActionType == BattleActionType.BasicAttack
               && eventData.LogCode == BattleLogCode.BasicAttackDamage
               && (HasNote(eventData, "profile_stepin")
                   || HasNote(eventData, "profile_lunge")
                   || HasNote(eventData, "profile_dash"));
    }

    private static string ComposeCueNote(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} {right}";
    }

    private readonly struct BattleAnimationCueDescriptor
    {
        public static readonly BattleAnimationCueDescriptor None = new(
            BattleAnimationSemantic.None,
            BattleAnimationDirection.Any,
            BattleAnimationIntensity.Any,
            string.Empty);

        public BattleAnimationCueDescriptor(
            BattleAnimationSemantic semantic,
            BattleAnimationDirection direction,
            BattleAnimationIntensity intensity,
            string note,
            float distance = 0f)
        {
            Semantic = semantic;
            Direction = direction;
            Intensity = intensity;
            Note = note;
            Distance = distance;
        }

        public BattleAnimationSemantic Semantic { get; }

        public BattleAnimationDirection Direction { get; }

        public BattleAnimationIntensity Intensity { get; }

        public string Note { get; }

        public float Distance { get; }
    }
}
