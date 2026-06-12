using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Unity;

public sealed class BattlePresentationCueBuilder
{
    private const float HeavyImpactDamageThreshold = 16f;
    private const float DisplacementTraceDistanceThreshold = 0.35f;
    private const float MovementCueDistanceThreshold = 0.05f;

    // 스킬 계열별 VFX 해상용 skillId→presentation 룩업. sim intent의 SkillId는 opaque 키이므로
    // (BattleCombatEventIntent 계약) 해석은 presentation이 소유한다 — 전투 시작 시 로드아웃의
    // 컴파일된 스킬 spec에서 주입(ConfigureSkillPresentations).
    private readonly Dictionary<string, BattleSkillPresentationProfile> _presentationBySkillId = new(System.StringComparer.Ordinal);

    public void ConfigureSkillPresentations(IEnumerable<BattleSkillSpec>? skills)
    {
        _presentationBySkillId.Clear();
        if (skills == null)
        {
            return;
        }

        foreach (var skill in skills)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id))
            {
                continue;
            }

            _presentationBySkillId[skill.Id] = skill.EffectivePresentation;
        }
    }

    public IReadOnlyList<BattlePresentationCue> Build(BattleSimulationStep previousStep, BattleSimulationStep currentStep)
    {
        var cues = new List<BattlePresentationCue>();
        // 사망 cue는 별도 버퍼에 모아 맨 끝에 붙인다 — 같은 step의 살해 ImpactDamage 리액션이
        // 리스트에서 death 뒤에 오면 driver가 막 시작한 death one-shot을 리액션으로 교체해
        // 쓰러지는 모션이 영영 재생되지 않는다. death가 자세의 마지막 발언권을 가져야 한다.
        var deathCues = new List<BattlePresentationCue>();
        var deathCueSubjects = new HashSet<string>(System.StringComparer.Ordinal);
        var previousById = previousStep.Units.ToDictionary(unit => unit.Id);
        var currentById = currentStep.Units.ToDictionary(unit => unit.Id);
        var motionsByActor = BuildMotionsByActor(currentStep);

        foreach (var current in currentStep.Units)
        {
            if (!previousById.TryGetValue(current.Id, out var previous))
            {
                continue;
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
                var guardDirection = ResolveHandednessAnimationDirection(current);
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.GuardEnter,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    BattleActionType.WaitDefend,
                    Note: $"guard_hand_{current.DominantHand}",
                    AnimationSemantic: BattleAnimationSemantic.GuardPose,
                    AnimationDirection: guardDirection));
            }
            else if (previous.IsDefending && !current.IsDefending)
            {
                var guardDirection = ResolveHandednessAnimationDirection(current);
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.GuardExit,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    BattleActionType.WaitDefend,
                    Note: $"guard_hand_{current.DominantHand}",
                    AnimationSemantic: BattleAnimationSemantic.GuardPose,
                    AnimationDirection: guardDirection));
            }

            var wasPresentationMovement = IsPresentationMovementState(previous);
            var isPresentationMovement = IsPresentationMovementState(current);
            if (!wasPresentationMovement && isPresentationMovement)
            {
                var movement = ResolveMovementAnimation(previous, current, currentById, PrimaryLocomotionMotion(motionsByActor, current.Id));
                if (movement.Distance >= MovementCueDistanceThreshold)
                {
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
            }
            else if (wasPresentationMovement && !isPresentationMovement)
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
                AddDeathCue(deathCues, deathCueSubjects, new BattlePresentationCue(
                    BattlePresentationCueType.DeathStart,
                    currentStep.StepIndex,
                    current.Id,
                    current.TargetId,
                    Magnitude: 1f,
                    SubjectAnchor: BattlePresentationAnchorId.Center,
                    AnimationSemantic: BattleAnimationSemantic.Death,
                    AnimationIntensity: BattleAnimationIntensity.Heavy));
            }

            TryAddPreImpactDisplacementTraceCue(cues, motionsByActor, previous, current, currentStep.StepIndex);
        }

        // Action choreography is driven by the combat-event-intent channel (GPT Pro D2 firing): the actor
        // COMMIT (strike) fires at Started/WindupStartTick contact-pinned so its authored contact frame
        // lands on ContactTick; Contacted emits TARGET REACTIONS ONLY (no actor commit — that would double
        // the swing); Canceled fires a presentation-only tombstone so the scheduled commit one-shot is
        // interrupted without any gameplay/reaction cue (no ghost contact, J14 / D2-C1). The sim
        // self-describes outcome/value in the typed channel, so presentation never parses event Notes (J8).
        if (currentStep.CombatEventIntents != null)
        {
            // Phase 4: 같은 step 의 ComboConsumed beat 과 (공격자, 피격자)가 일치하는 contact 는
            // 콤보 임팩트로 승급(텍스트/스케일/히트스톱) — beat 채널이 유일한 판정 소스다.
            var comboContacts = CollectComboContacts(currentStep);
            foreach (var intent in currentStep.CombatEventIntents)
            {
                switch (intent.Status)
                {
                    case CombatEventIntentStatus.Started:
                        AddCommitCue(cues, currentById, intent, _presentationBySkillId);
                        break;
                    case CombatEventIntentStatus.Contacted:
                        AddTargetReactionCues(cues, currentById, motionsByActor, intent, comboContacts);
                        break;
                    case CombatEventIntentStatus.Canceled:
                        AddCanceledCue(cues, intent);
                        break;
                }
            }
        }

        foreach (var eventData in currentStep.Events)
        {
            if (eventData.LogCode == BattleLogCode.WaitDefend)
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.GuardEnter,
                    currentStep.StepIndex,
                    eventData.ActorId.Value,
                    eventData.TargetId?.Value,
                    eventData.ActionType));
            }

            if (eventData.EventKind == BattleEventKind.Kill && eventData.TargetId != null)
            {
                AddDeathCue(deathCues, deathCueSubjects, new BattlePresentationCue(
                    BattlePresentationCueType.DeathStart,
                    currentStep.StepIndex,
                    eventData.TargetId.Value.Value,
                    eventData.ActorId.Value,
                    eventData.ActionType,
                    eventData.Value,
                    BattlePresentationAnchorId.Center,
                    BattlePresentationAnchorId.Cast,
                    AnimationSemantic: BattleAnimationSemantic.Death,
                    AnimationIntensity: BattleAnimationIntensity.Heavy,
                    // P2: 다이브 후열 처치는 typed KillPayload에서 읽는다(J8 — note 비파싱).
                    ContactAccent: eventData.KillPayload?.IsBacklineDiveKill == true
                        ? CombatContactAccent.BacklineDiveKill
                        : CombatContactAccent.None));
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
                    ActionType: winner.PendingActionType,
                    Note: "battle_resolved"));
            }
        }

        // 사망 cue가 같은 step의 모든 리액션/임팩트 뒤에 오도록 마지막에 붙인다 (위 버퍼 주석 참조).
        cues.AddRange(deathCues);
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

    private static bool IsPresentationMovementState(BattleUnitReadModel unit)
    {
        return unit.ActionState is CombatActionState.Approach
            or CombatActionState.SecurePosition
            or CombatActionState.Reposition
            or CombatActionState.AdvanceToAnchor
            or CombatActionState.BreakContact;
    }

    private static BattleAnimationCueDescriptor ResolveMovementAnimation(
        BattleUnitReadModel previous,
        BattleUnitReadModel current,
        IReadOnlyDictionary<string, BattleUnitReadModel> currentById,
        BattleMotionIntent? motion)
    {
        // Distance and left/right direction are geometric, but the movement SEMANTIC
        // (dash / backstep / strafe) comes from the authoritative motion kind — or the action-state
        // fallback when a step carries no motion record — never from a dot/cross classification of
        // the position delta (GPT Pro review I5: no semantic inference from deltas).
        float deltaX;
        float deltaY;
        float distance;
        BattleAnimationSemantic semantic;
        string note;

        if (motion != null)
        {
            deltaX = motion.To.X - motion.From.X;
            deltaY = motion.To.Y - motion.From.Y;
            distance = motion.From.DistanceTo(motion.To);
            semantic = ResolveMotionSemantic(motion.Kind);
            note = $"motion_{motion.Kind}";
        }
        else
        {
            deltaX = current.Position.X - previous.Position.X;
            deltaY = current.Position.Y - previous.Position.Y;
            distance = (float)System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance < 0.05f)
            {
                return BattleAnimationCueDescriptor.None;
            }

            semantic = ResolveActionStateLocomotionSemantic(current.ActionState);
            note = $"state_{current.ActionState}";
        }

        var intensity = distance >= 1.2f
            ? BattleAnimationIntensity.Heavy
            : BattleAnimationIntensity.Medium;
        var direction = ResolveMovementDirection(semantic, deltaX, deltaY, previous, current, currentById);
        return new BattleAnimationCueDescriptor(semantic, direction, intensity, note, distance);
    }

    private static BattleAnimationDirection ResolveMovementDirection(
        BattleAnimationSemantic semantic,
        float deltaX,
        float deltaY,
        BattleUnitReadModel previous,
        BattleUnitReadModel current,
        IReadOnlyDictionary<string, BattleUnitReadModel> currentById)
    {
        switch (semantic)
        {
            case BattleAnimationSemantic.DashEngage:
                return BattleAnimationDirection.Forward;
            case BattleAnimationSemantic.BackstepDisengage:
                return BattleAnimationDirection.Backward;
            case BattleAnimationSemantic.LateralStrafe:
                if (!string.IsNullOrWhiteSpace(current.TargetId)
                    && currentById.TryGetValue(current.TargetId, out var target))
                {
                    var toTargetX = target.Position.X - previous.Position.X;
                    var toTargetY = target.Position.Y - previous.Position.Y;
                    var cross = (deltaX * toTargetY) - (deltaY * toTargetX);
                    return cross >= 0f ? BattleAnimationDirection.Right : BattleAnimationDirection.Left;
                }

                return BattleAnimationDirection.Lateral;
            default:
                return BattleAnimationDirection.Any;
        }
    }

    private static void TryAddPreImpactDisplacementTraceCue(
        ICollection<BattlePresentationCue> cues,
        Dictionary<string, List<BattleMotionIntent>> motionsByActor,
        BattleUnitReadModel previous,
        BattleUnitReadModel current,
        int stepIndex)
    {
        if (IsPresentationMovementState(previous) || IsPresentationMovementState(current))
        {
            return;
        }

        // A pre-impact lunge / step-in is recorded as a discrete Approach (or MobilityDash) motion while
        // the actor is mid-action, not in a sustained movement state. Source the trace from that motion
        // intent rather than a profile_* note (GPT Pro review: DoesNotReadProfileNoteForMotion).
        BattleMotionIntent? motion = null;
        if (motionsByActor.TryGetValue(current.Id, out var actorMotions))
        {
            foreach (var candidate in actorMotions)
            {
                if (candidate.IsDiscrete
                    && (candidate.Kind == BattleMotionKind.Approach || candidate.Kind == BattleMotionKind.MobilityDash))
                {
                    motion = candidate;
                    break;
                }
            }
        }

        if (motion == null)
        {
            return;
        }

        var distance = motion.From.DistanceTo(motion.To);
        if (distance < DisplacementTraceDistanceThreshold)
        {
            return;
        }

        cues.Add(new BattlePresentationCue(
            BattlePresentationCueType.RepositionStart,
            stepIndex,
            current.Id,
            current.TargetId,
            BattleActionType.BasicAttack,
            distance,
            BattlePresentationAnchorId.Feet,
            BattlePresentationAnchorId.Center,
            "trace_preimpact",
            BattleAnimationSemantic.DashEngage,
            BattleAnimationDirection.Forward,
            BattleAnimationIntensity.Medium));
    }

    private static void AddCommitCue(
        ICollection<BattlePresentationCue> cues,
        IReadOnlyDictionary<string, BattleUnitReadModel> currentById,
        BattleCombatEventIntent intent,
        IReadOnlyDictionary<string, BattleSkillPresentationProfile> presentationBySkillId)
    {
        // GPT Pro D2: the actor commit (strike) fires at WindupStartTick carrying the tick schedule, so the
        // driver can pin its authored contact frame onto ContactTick. The commit animation is a function of
        // actor properties only (delivery/class/archetype) — never the contact outcome (Q3-b OutcomeSeparation),
        // which belongs to the per-target reactions emitted at Contacted.
        currentById.TryGetValue(intent.ActorId.Value, out var actor);
        var commit = ResolveCommitAnimation(intent, actor);
        var schedule = new BattleCommitSchedule(
            intent.ActionInstanceId,
            ContactGroupIndex: 0, // one scheduled group per action today; future multi-hit keys per hit frame (J22-D2)
            intent.WindupStartTick,
            intent.ContactTick);
        var presentation = intent.SkillId != null
                           && presentationBySkillId.TryGetValue(intent.SkillId, out var resolvedPresentation)
            ? resolvedPresentation
            : null;

        cues.Add(new BattlePresentationCue(
            ResolveCommitCueType(intent.Kind, isHeal: false),
            intent.StepIndex,
            intent.ActorId.Value,
            intent.InitialTargetId?.Value,
            ResolveActionTypeForKind(intent.Kind),
            0f,
            BattlePresentationAnchorId.Cast,
            BattlePresentationAnchorId.Center,
            string.Empty,
            commit.Semantic,
            commit.Direction,
            commit.Intensity,
            schedule,
            PresentationFamily: presentation?.Family ?? SkillPresentationFamily.Any,
            PresentationSkin: presentation?.Skin ?? SkillPresentationSkin.Any,
            PresentationGesture: presentation?.Gesture ?? SkillPresentationGesture.None));
    }

    private static HashSet<(string AttackerId, string TargetId)> CollectComboContacts(BattleSimulationStep step)
    {
        var pairs = new HashSet<(string, string)>();
        if (step.Beats == null)
        {
            return pairs;
        }

        foreach (var beat in step.Beats)
        {
            if (beat.Type == CombatBeatType.ComboConsumed
                && beat.SourceId is { } source
                && beat.TargetId is { } target)
            {
                pairs.Add((source.Value, target.Value));
            }
        }

        return pairs;
    }

    private static void AddTargetReactionCues(
        ICollection<BattlePresentationCue> cues,
        IReadOnlyDictionary<string, BattleUnitReadModel> currentById,
        Dictionary<string, List<BattleMotionIntent>> motionsByActor,
        BattleCombatEventIntent intent,
        HashSet<(string AttackerId, string TargetId)> comboContacts)
    {
        var contacts = intent.Contacts;
        if (contacts == null || contacts.Count == 0)
        {
            return;
        }

        var actionType = ResolveActionTypeForKind(intent.Kind);

        foreach (var contact in contacts)
        {
            // GPT Pro D2 / J22-D2: Contacted emits TARGET REACTIONS ONLY. The actor commit already fired at
            // WindupStartTick (AddCommitCue); re-emitting it here would produce a duplicate swing.
            if (contact.TargetId == null || !currentById.ContainsKey(contact.TargetId.Value.Value))
            {
                continue;
            }

            if (contact.IsHeal)
            {
                cues.Add(new BattlePresentationCue(
                    BattlePresentationCueType.ImpactHeal,
                    intent.StepIndex,
                    contact.TargetId.Value.Value,
                    intent.ActorId.Value,
                    actionType,
                    contact.Value,
                    BattlePresentationAnchorId.Head,
                    BattlePresentationAnchorId.Cast,
                    ContactAccent: contact.Accent));
                continue;
            }

            var impactAnimation = ResolveImpactAnimation(contact.Outcome, contact.Value);
            // P2: 후방 타격은 히트스톱/리코일 강도를 Heavy로 승급 — 가장 강한 positional 보너스가
            // 몸으로도 읽히게. 판정은 typed accent(J8), 문자열 비파싱.
            var impactIntensity = contact.Accent.HasFlag(CombatContactAccent.Rear)
                ? BattleAnimationIntensity.Heavy
                : impactAnimation.Intensity;
            // Phase 4: 콤보 소비 타격은 Heavy 로 승급 — 보통 타격의 baseline 이 이미 Medium(90ms)이라,
            // 마스터 플랜의 "콤보가 기본 타격보다 굵은 펀치"는 한 단계 위(Heavy 140ms)로만 구현된다.
            // 후방 강타 승급과 같은 급(둘 다 판의 결정적 순간). 강도 대역은 V1 authority 노브.
            var isComboEmphasis = comboContacts.Contains((intent.ActorId.Value, contact.TargetId.Value.Value));
            if (isComboEmphasis)
            {
                impactIntensity = BattleAnimationIntensity.Heavy;
            }

            cues.Add(new BattlePresentationCue(
                BattlePresentationCueType.ImpactDamage,
                intent.StepIndex,
                contact.TargetId.Value.Value,
                intent.ActorId.Value,
                actionType,
                contact.Value,
                BattlePresentationAnchorId.Center,
                BattlePresentationAnchorId.Cast,
                string.Empty,
                impactAnimation.Semantic,
                impactAnimation.Direction,
                impactIntensity,
                ContactAccent: contact.Accent,
                IsComboEmphasis: isComboEmphasis));

            TryAddKnockbackTraceForContact(cues, motionsByActor, intent.ActorId.Value, actionType, contact);
        }
    }

    private static void AddCanceledCue(ICollection<BattlePresentationCue> cues, BattleCombatEventIntent intent)
    {
        // GPT Pro D2-Q2 / D2-C1..C6: a pre-contact cancel emits NO gameplay or reaction cue, but the
        // presentation is told (keyed by ActionInstanceId) so the driver interrupts/tombstones the scheduled
        // commit one-shot. Without this the already-playing swing coasts to a ghost contact pose. This cue
        // never applies a recovery gate and is never serialized as gameplay truth.
        cues.Add(new BattlePresentationCue(
            BattlePresentationCueType.ActionCanceled,
            intent.StepIndex,
            intent.ActorId.Value,
            intent.InitialTargetId?.Value,
            ResolveActionTypeForKind(intent.Kind),
            CommitSchedule: new BattleCommitSchedule(
                intent.ActionInstanceId,
                ContactGroupIndex: 0,
                intent.WindupStartTick,
                intent.ContactTick)));
    }

    private static void TryAddKnockbackTraceForContact(
        ICollection<BattlePresentationCue> cues,
        Dictionary<string, List<BattleMotionIntent>> motionsByActor,
        string actorId,
        BattleActionType actionType,
        BattleContactIntent contact)
    {
        if (contact.TargetId == null || contact.Value <= 0f)
        {
            return;
        }

        // Knockback displacement is sourced from the authoritative Knockback motion intent, never
        // reverse-engineered from a position delta (GPT Pro: UsesMotionIntentForKnockback).
        var targetId = contact.TargetId.Value.Value;
        var motion = FindMotion(motionsByActor, targetId, BattleMotionKind.Knockback);
        if (motion == null)
        {
            return;
        }

        var distance = motion.From.DistanceTo(motion.To);
        if (distance < DisplacementTraceDistanceThreshold)
        {
            return;
        }

        var impact = ResolveImpactAnimation(contact.Outcome, contact.Value);
        var intensity = impact.Intensity == BattleAnimationIntensity.Heavy
            ? BattleAnimationIntensity.Heavy
            : BattleAnimationIntensity.Medium;
        cues.Add(new BattlePresentationCue(
            BattlePresentationCueType.RepositionStart,
            contact.ContactTick,
            targetId,
            actorId,
            actionType,
            distance,
            BattlePresentationAnchorId.Feet,
            BattlePresentationAnchorId.Cast,
            "trace_knockback",
            BattleAnimationSemantic.BackstepDisengage,
            BattleAnimationDirection.Backward,
            intensity));
    }

    private static BattlePresentationCueType ResolveCommitCueType(CombatEventKind kind, bool isHeal)
    {
        if (kind == CombatEventKind.Skill)
        {
            return isHeal ? BattlePresentationCueType.ActionCommitHeal : BattlePresentationCueType.ActionCommitSkill;
        }

        return BattlePresentationCueType.ActionCommitBasic;
    }

    private static BattleActionType ResolveActionTypeForKind(CombatEventKind kind)
    {
        return kind == CombatEventKind.Skill ? BattleActionType.ActiveSkill : BattleActionType.BasicAttack;
    }

    private static BattleAnimationCueDescriptor ResolveImpactAnimation(CombatOutcome outcome, float value)
    {
        // Outcome and magnitude come from the typed combat-event-intent channel — never from parsing
        // event Note strings (GPT Pro J8). Direction matches the prior note-less default: recoil
        // Backward for hits/crits/knockdown, Any for soft outcomes.
        switch (outcome)
        {
            case CombatOutcome.Miss:
                return new BattleAnimationCueDescriptor(BattleAnimationSemantic.Miss, BattleAnimationDirection.Any, BattleAnimationIntensity.Light, string.Empty);
            case CombatOutcome.Dodge:
                return new BattleAnimationCueDescriptor(BattleAnimationSemantic.Dodge, BattleAnimationDirection.Any, BattleAnimationIntensity.Light, string.Empty);
            case CombatOutcome.Knockdown:
                return new BattleAnimationCueDescriptor(BattleAnimationSemantic.Knockdown, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, string.Empty);
            case CombatOutcome.Block:
                return new BattleAnimationCueDescriptor(BattleAnimationSemantic.BlockImpact, BattleAnimationDirection.Any, BattleAnimationIntensity.Medium, string.Empty);
            case CombatOutcome.Crit:
                return new BattleAnimationCueDescriptor(BattleAnimationSemantic.CriticalImpact, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, string.Empty);
            default:
                return value >= HeavyImpactDamageThreshold
                    ? new BattleAnimationCueDescriptor(BattleAnimationSemantic.HitHeavy, BattleAnimationDirection.Backward, BattleAnimationIntensity.Heavy, string.Empty)
                    : new BattleAnimationCueDescriptor(BattleAnimationSemantic.HitLight, BattleAnimationDirection.Backward, BattleAnimationIntensity.Medium, string.Empty);
        }
    }

    private static BattleAnimationDirection ResolveHandednessAnimationDirection(BattleUnitReadModel unit)
    {
        return unit.DominantHand switch
        {
            SM.Core.Contracts.DominantHand.Left => BattleAnimationDirection.Left,
            SM.Core.Contracts.DominantHand.Right => BattleAnimationDirection.Right,
            _ => BattleAnimationDirection.Lateral,
        };
    }

    private static BattleAnimationCueDescriptor ResolveCommitAnimation(BattleCombatEventIntent intent, BattleUnitReadModel? actor)
    {
        // Bow / projectile basic attacks are detected from authoritative actor properties (class /
        // archetype / preferred range), NOT from event Note profiles. GPT Pro J8 removed the profile_*
        // string inference; the pre-impact lunge is now drawn from the motion-intent trace instead.
        if (intent.Kind == CombatEventKind.BasicAttack && actor != null)
        {
            if (IsBowBasicAttacker(actor))
            {
                return new BattleAnimationCueDescriptor(BattleAnimationSemantic.BowShot, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, string.Empty);
            }

            if (IsProjectileBasicAttacker(actor))
            {
                return new BattleAnimationCueDescriptor(BattleAnimationSemantic.ProjectileCast, BattleAnimationDirection.Forward, BattleAnimationIntensity.Medium, string.Empty);
            }
        }

        return BattleAnimationCueDescriptor.None;
    }

    // 활/투사체 분류는 BattleBasicAttackPresentationClassifier로 단일화됐다 — commit 모션 semantic,
    // 무기 손 거치 rig(BattleP09WeaponStanceRig), bow ready idle이 같은 truth를 공유한다.
    private static bool IsBowBasicAttacker(BattleUnitReadModel actor)
    {
        return BattleBasicAttackPresentationClassifier.IsBowBasicAttacker(actor);
    }

    private static bool IsProjectileBasicAttacker(BattleUnitReadModel actor)
    {
        return BattleBasicAttackPresentationClassifier.IsProjectileBasicAttacker(actor);
    }

    private static Dictionary<string, List<BattleMotionIntent>> BuildMotionsByActor(BattleSimulationStep step)
    {
        var byActor = new Dictionary<string, List<BattleMotionIntent>>(System.StringComparer.Ordinal);
        if (step.Motions == null)
        {
            return byActor;
        }

        foreach (var motion in step.Motions)
        {
            if (!byActor.TryGetValue(motion.ActorId.Value, out var list))
            {
                list = new List<BattleMotionIntent>();
                byActor[motion.ActorId.Value] = list;
            }

            list.Add(motion);
        }

        return byActor;
    }

    private static BattleMotionIntent? PrimaryLocomotionMotion(
        Dictionary<string, List<BattleMotionIntent>> byActor,
        string actorId)
    {
        if (!byActor.TryGetValue(actorId, out var list))
        {
            return null;
        }

        BattleMotionIntent? best = null;
        var bestDistance = -1f;
        foreach (var motion in list)
        {
            if (motion.Kind == BattleMotionKind.Knockback)
            {
                continue;
            }

            var distance = motion.From.DistanceTo(motion.To);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = motion;
            }
        }

        return best;
    }

    private static BattleMotionIntent? FindMotion(
        Dictionary<string, List<BattleMotionIntent>> byActor,
        string actorId,
        BattleMotionKind kind)
    {
        if (!byActor.TryGetValue(actorId, out var list))
        {
            return null;
        }

        foreach (var motion in list)
        {
            if (motion.Kind == kind)
            {
                return motion;
            }
        }

        return null;
    }

    private static BattleAnimationSemantic ResolveMotionSemantic(BattleMotionKind kind)
    {
        return kind switch
        {
            BattleMotionKind.MobilityDash => BattleAnimationSemantic.DashEngage,
            BattleMotionKind.Approach => BattleAnimationSemantic.DashEngage,
            BattleMotionKind.Disengage => BattleAnimationSemantic.BackstepDisengage,
            BattleMotionKind.Reposition => BattleAnimationSemantic.LateralStrafe,
            BattleMotionKind.Knockback => BattleAnimationSemantic.BackstepDisengage,
            BattleMotionKind.ForcedDisplacement => BattleAnimationSemantic.LateralStrafe,
            _ => BattleAnimationSemantic.LateralStrafe,
        };
    }

    private static BattleAnimationSemantic ResolveActionStateLocomotionSemantic(CombatActionState actionState)
    {
        return actionState switch
        {
            CombatActionState.BreakContact => BattleAnimationSemantic.BackstepDisengage,
            CombatActionState.Reposition => BattleAnimationSemantic.LateralStrafe,
            CombatActionState.SecurePosition => BattleAnimationSemantic.LateralStrafe,
            _ => BattleAnimationSemantic.DashEngage,
        };
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
