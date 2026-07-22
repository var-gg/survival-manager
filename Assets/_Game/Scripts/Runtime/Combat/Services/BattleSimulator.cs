using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Ids;

namespace SM.Combat.Services;

public sealed class BattleSimulator
{
    public const float DefaultFixedStepSeconds = 0.1f;
    public const int DefaultMaxSteps = 300;

    private const float SpawnArrivalThreshold = 0.05f;
    private const float HomeArrivalThreshold = 0.12f;
    private const float ActionRangeTolerance = 0.35f;

    private readonly List<BattleEvent> _events = new();
    private readonly int _maxSteps;

    public BattleSimulator(BattleState state, int maxSteps = DefaultMaxSteps)
        : this(state, maxSteps, null)
    {
    }

    internal BattleSimulator(BattleState state, int maxSteps, IBattleDiagnosticObserver? diagnosticObserver)
    {
        State = state;
        _maxSteps = Math.Max(1, maxSteps);
        State.AttachDiagnosticObserver(diagnosticObserver);
        BattleTelemetryRecorder.RecordBattleStarted(State);
        // Phase 3: battle-start triggers + synergy activation fire BEFORE the initial read model is
        // built, so their effects (barrier, statuses) and beats are visible at step 0 — the acceptance
        // contract "BattleStart effects appear at step 0".
        CombatTriggerEngine.OnBattleStart(State);
        SynergyService.EmitActivationBeats(State);
        CurrentStep = BattleReadModelBuilder.BuildStep(
            State,
            Array.Empty<BattleEvent>(),
            isFinished: false,
            winner: null,
            beats: State.DrainStepBeats());
    }

    public BattleState State { get; }
    public bool IsFinished { get; private set; }
    public TeamSide? Winner { get; private set; }
    public BattleSimulationStep CurrentStep { get; private set; }

    public BattleSimulationStep Step()
    {
        if (IsFinished)
        {
            return CurrentStep;
        }

        State.ResetStepMotions();
        State.ResetStepCombatEvents();
        var stepEvents = new List<BattleEvent>();
        var processedKillVictimIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in State.AllUnits)
        {
            unit.AdvanceTick();
        }
        State.AdvanceGroupDispersalLocks();

        StatusResolutionService.AdvanceStatuses(State, stepEvents);
        ProcessKillEvents(stepEvents, processedKillVictimIds);
        State.ScheduleOwnedEntityDespawnIfOwnerDead();
        State.AdvanceOwnedEntityDespawns();
        if (CheckForWinner())
        {
            State.AdvanceStep();
            CurrentStep = BattleReadModelBuilder.BuildStep(State, stepEvents, IsFinished, Winner, State.StepMotions, State.StepCombatEvents, State.DrainStepBeats());
            return CurrentStep;
        }

        // Phase 2 팀 블랙보드: 유닛 루프 전에 양 팀을 고정 순서(Ally→Enemy)로 갱신해 step 내 모든
        // 의사결정이 같은 스냅샷을 읽는다(0.5s cadence — TeamBlackboardService.CadenceSteps).
        State.RefreshTeamBlackboardsIfDue();

        var orderedUnits = State.AllUnits
            .Where(unit => unit.IsAlive)
            .OrderByDescending(unit => unit.Speed)
            .ThenBy(unit => unit.Side)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToList();

        foreach (var actor in orderedUnits)
        {
            if (!actor.IsAlive)
            {
                continue;
            }

            if (actor.IsStunned)
            {
                EmitCanceledIfPending(actor, "actor_stunned");
                actor.ClearTarget(applySwitchDelay: false);
                actor.SetActionState(CombatActionState.AcquireTarget);
                continue;
            }

            if (TryAdvanceSpawn(actor))
            {
                continue;
            }

            if (actor.ActionState == CombatActionState.ExecuteAction)
            {
                if (TryResolvePendingAction(actor, stepEvents) && CheckForWinner())
                {
                    break;
                }

                continue;
            }

            var shouldConsumeCadence = actor.NeedsReevaluation;
            var evaluated = TacticEvaluator.Evaluate(State, actor);
            if (shouldConsumeCadence)
            {
                actor.ConsumeReevaluation();
            }

            if (evaluated.ActionType == BattleActionType.WaitDefend || evaluated.Target == null || !evaluated.Target.IsAlive)
            {
                HandleDefendOrReposition(actor, stepEvents);
                continue;
            }

            var reasonCode = BattleTelemetryRecorder.ResolveDecisionReason(actor, evaluated);
            actor.SetPendingDecisionReason(reasonCode);
            var previousTarget = State.FindUnit(actor.CurrentTargetId);
            actor.SetCurrentTarget(evaluated.Target.Id);
            BattleTelemetryRecorder.RecordTargetEvent(State, actor, previousTarget, evaluated.Target, reasonCode);
            if (actor.SetPositioningIntent(evaluated.PositioningIntent, evaluated.ReevaluationReason))
            {
                BattleTelemetryRecorder.RecordPositioningIntent(State, actor, evaluated.Target);
            }

            var preIntentTarget = evaluated.Target;

            // Phase 1 tactical brain: choose what the unit is trying to do (its CombatIntent) now that its
            // target is set. The movement executor reads it (e.g. AnchorFire holds the backline anchor); the
            // attack rule below is unchanged — intent biases positioning, not hit validity.
            RoleBrain.ResolveIntent(State, actor);

            // Phase 1 narrow retarget seam: a Dive/Peel intent overrides the target that the begin-gate and
            // movement both read, so the duelist visibly dives the backline (and the vanguard peels the diver)
            // instead of the melee-nearest baseline. Default (non-Dive/Peel) targeting is untouched.
            var intentOverrideApplied = TacticEvaluator.TryApplyIntentTargetOverride(
                State,
                actor,
                evaluated,
                out var retargetedEvaluation);
            if (intentOverrideApplied)
            {
                evaluated = retargetedEvaluation;
                actor.SetCurrentTarget(evaluated.Target.Id);
            }
            BattleDiagnosticRecorder.RecordIntentOverride(
                State,
                actor,
                preIntentTarget,
                intentOverrideApplied,
                evaluated.Target);

            // A scoped pack-pursuit approach is an intentional movement override, not an attack or Dive gate.
            // Resolve it after RoleBrain has had the normal chance to open Dive, but before the current frontline
            // target's in-range check can commit another center-fight swing and make the flank route unreachable.
            if (MovementResolver.TryMovePackPursuitApproach(State, actor))
            {
                continue;
            }

            // Phase 0 single attack rule: an action begins when the target is within action range
            // (edge ≤ R + 0.05) and the cooldown is ready. No range-band / slot-ready / start-tolerance
            // gating — those produced the out-of-range hover and the slot thrash. When not in range the
            // unit pursues (MoveForIntent). This rule is identical for ally support and enemy attacks.
            var canBeginAction = MovementResolver.IsInActionRange(actor, evaluated.Target, ResolveEvaluatedActionRange(actor, evaluated));
            if (canBeginAction && evaluated.Mobility == null)
            {
                if (evaluated.ActionType == BattleActionType.BasicAttack && !StatusResolutionService.CanUseBasicAttack(actor))
                {
                    actor.SetActionState(CombatActionState.AcquireTarget);
                    continue;
                }

                if (actor.CooldownRemaining <= 0f)
                {
                    BattleTelemetryRecorder.RecordActionStarted(State, actor, evaluated);
                    actor.BeginWindup(evaluated.ActionType, evaluated.Target.Id, evaluated.Skill?.Id);
                    EmitWindupStarted(actor, evaluated);
                    BattleDiagnosticRecorder.RecordDisplacementCastStarted(
                        State,
                        actor,
                        evaluated.Target,
                        evaluated.Skill);
                }
                else
                {
                    actor.SetActionState(CombatActionState.Recover);
                }
            }
            else
            {
                if (evaluated.Mobility != null)
                {
                    BattleTelemetryRecorder.RecordMobility(State, actor, evaluated.Target, evaluated.Mobility);
                }

                MovementResolver.MoveForIntent(State, actor, evaluated);
            }
        }

        // 콤보 추가타가 만든 사망도 아래의 단일 kill-trigger 경로로 수렴시킨다. ProcessStep은
        // 원본 이벤트 수만 순회하므로 payoff/kill 이벤트 append가 콤보 재진입을 만들지 않는다.
        CombatComboService.ProcessStep(State, stepEvents);

        ProcessKillEvents(stepEvents, processedKillVictimIds);

        CombatTriggerEngine.OnPostStep(State);

        MovementResolver.ResolveFormationSpacing(State);
        State.ActivityTelemetry.RecordClusterTradeoff(EffectMembershipSampler.SampleStep(State));
        State.ActivityTelemetry.RecordStep(State);
        State.AdvanceStep();

        foreach (var stepEvent in stepEvents)
        {
            _events.Add(stepEvent);
        }

        if (!IsFinished && (State.StepIndex >= _maxSteps || !State.LivingAllies.Any() || !State.LivingEnemies.Any()))
        {
            FinishBattle();
        }

        CurrentStep = BattleReadModelBuilder.BuildStep(State, stepEvents, IsFinished, Winner, State.StepMotions, State.StepCombatEvents, State.DrainStepBeats());
        return CurrentStep;
    }

    private void ProcessKillEvents(
        IReadOnlyList<BattleEvent> stepEvents,
        HashSet<string> processedKillVictimIds)
    {
        // 한 phase의 다중 사망은 victim stable id → killer stable id 전순서로 소비한다. action append 순서나
        // combo payoff append 위치가 달라도 OnKill/OnTeamKill/OnAllyDeath 발화 순서는 cross-process 동일하다.
        var killEvents = stepEvents
            .Where(resolvedEvent => resolvedEvent.EventKind == BattleEventKind.Kill)
            .Where(resolvedEvent => !processedKillVictimIds.Contains(
                resolvedEvent.KillPayload?.ActualVictim.Value
                ?? resolvedEvent.TargetId?.Value
                ?? string.Empty))
            .OrderBy(
                resolvedEvent => resolvedEvent.KillPayload?.ActualVictim.Value
                                 ?? resolvedEvent.TargetId?.Value
                                 ?? string.Empty,
                StringComparer.Ordinal)
            .ThenBy(
                resolvedEvent => resolvedEvent.KillPayload?.ActualKiller.Value
                                 ?? resolvedEvent.ActorId.Value,
                StringComparer.Ordinal)
            .ToList();
        foreach (var resolvedEvent in killEvents)
        {
            var stableVictimId = resolvedEvent.KillPayload?.ActualVictim.Value
                                 ?? resolvedEvent.TargetId?.Value
                                 ?? string.Empty;
            if (!processedKillVictimIds.Add(stableVictimId))
            {
                continue;
            }

            var killerId = resolvedEvent.KillPayload?.ActualKiller ?? resolvedEvent.ActorId;
            var killer = State.FindUnit(killerId);
            if (killer != null)
            {
                CombatTriggerEngine.OnKill(State, killer);
            }

            if (resolvedEvent.KillPayload?.ActualVictim is { } victimId
                && State.FindUnit(victimId) is { } victim)
            {
                CombatTriggerEngine.OnTeamKill(State, victim);
                CombatTriggerEngine.OnAllyDeath(State, victim);
            }
        }
    }

    // onStep: 매 스텝을 관전자에게 흘려보낸다(헤드리스가 BattleHighlightLedger로 formation payoff를 집계하는 통로).
    // 순수 관찰 — 반환 BattleResult는 onStep 유무와 무관하게 동일하다.
    public BattleResult RunToEnd(Action<BattleSimulationStep>? onStep = null)
    {
        // step 0(개전) 관찰 — 시너지 발동·BattleStart 트리거 beat은 생성자에서 CurrentStep에 실린다(line 30).
        // 이 첫 흘려보내기가 없으면 관찰자(BattleHighlightLedger)가 Step()=step 1부터 읽어 step-0-only인
        // 시너지 발동을 통째로 놓친다 → payoff SynergyActivations가 항상 0으로 undercount(보상화면·헤드리스 리포트 공통).
        // 순수 관찰이라 BattleResult byte-identity 무영향.
        onStep?.Invoke(CurrentStep);
        while (!IsFinished)
        {
            var step = Step();
            onStep?.Invoke(step);
        }

        return new BattleResult(
            Winner ?? TeamSide.Ally,
            State.StepIndex,
            State.ElapsedSeconds,
            _events.ToList(),
            CurrentStep.Units,
            State.TelemetryEvents.ToList(),
            State.ActivityTelemetry.BuildSnapshot(State),
            State.Beats.ToList());
    }

    private bool TryAdvanceSpawn(UnitSnapshot actor)
    {
        if (actor.ActionState is not (CombatActionState.Spawn or CombatActionState.AdvanceToAnchor))
        {
            return false;
        }

        // A pack-pursuit escort owns a content-scoped flank approach instead of the ordinary center-facing spawn
        // advance. Hand it to MovementResolver here so the generic spawn envelope cannot keep dragging it toward
        // the contact mass before normal intent evaluation begins. Non-tagged units retain the exact old path.
        if (MovementResolver.TryMovePackPursuitApproach(State, actor))
        {
            return true;
        }

        var home = MovementResolver.ResolveHomePosition(State, actor);
        if (actor.Position.DistanceTo(home) <= SpawnArrivalThreshold)
        {
            var spawnArrivalFrom = actor.Position;
            actor.SetPosition(home);
            if (spawnArrivalFrom.DistanceTo(home) > 1e-5f)
            {
                State.RecordMotion(actor.Id, BattleMotionKind.Approach, spawnArrivalFrom, home, isDiscrete: false);
            }

            actor.SetActionState(CombatActionState.AcquireTarget);
            return true;
        }

        actor.StopDefending();
        actor.SetActionState(CombatActionState.AdvanceToAnchor);
        var spawnAdvanceFrom = actor.Position;
        var next = CombatVector2.MoveTowards(actor.Position, home, Math.Max(0.01f, actor.MoveSpeed * State.FixedStepSeconds));
        actor.SetPosition(next);
        if (spawnAdvanceFrom.DistanceTo(next) > 1e-5f)
        {
            State.RecordMotion(actor.Id, BattleMotionKind.Approach, spawnAdvanceFrom, next, isDiscrete: false);
        }

        return true;
    }

    private bool TryResolvePendingAction(UnitSnapshot actor, List<BattleEvent> stepEvents)
    {
        var target = State.FindUnit(actor.PendingTargetId);
        if (target == null || !target.IsAlive)
        {
            EmitCanceledIfPending(actor, "target_missing_or_dead");
            actor.ClearTarget(applySwitchDelay: true);
            actor.SetActionState(CombatActionState.AcquireTarget);
            return false;
        }

        var desiredRange = actor.ResolveActionRange(actor.PendingSkillId);
        if (actor.PendingActionType != BattleActionType.BasicAttack
            && !MovementResolver.IsInActionRange(actor, target, desiredRange + ActionRangeTolerance))
        {
            // A basic-attack windup is COMMITTED once begun: it never cancels for range, so a target that
            // back-pedals during the swing no longer triggers the per-tick cancel→re-approach "treadmill" —
            // the swing simply connects on resolve. Skills/casts still abort if the target leaves the cast
            // envelope mid-windup (a deliberate whiff path), keeping their original behavior.
            EmitCanceledIfPending(actor, "target_left_cast_range");
            actor.ClearTarget(applySwitchDelay: true);
            actor.SetActionState(CombatActionState.AcquireTarget);
            return false;
        }

        if (actor.ActionTimerRemaining > 0f)
        {
            return false;
        }

        // Capture the in-flight action-choreography identity BEFORE Resolve runs (Resolve -> StartRecovery
        // mutates the actor's pending fields). The Contacted intent is then emitted at the authoritative
        // resolve tick, paired to the Started intent by ActionInstanceId.
        var pendingActionInstanceId = actor.PendingActionInstanceId;
        var pendingWindupStartTick = actor.PendingWindupStartTick;
        var pendingContactTick = actor.PendingContactTick;
        var pendingDelivery = actor.PendingActionDelivery;
        var pendingKind = actor.PendingActionType == BattleActionType.ActiveSkill ? CombatEventKind.Skill : CombatEventKind.BasicAttack;
        var pendingSkillId = actor.PendingSkillId;
        var pendingTargetId = actor.PendingTargetId;
        var pendingSkill = actor.ResolveSkill(pendingSkillId);
        var actorPositionBefore = actor.Position;
        var targetPositionBefore = target.Position;

        var resolveEvents = CombatActionResolver.Resolve(State, actor);
        stepEvents.AddRange(resolveEvents);
        BattleDiagnosticRecorder.RecordDisplacementResolved(
            State,
            actor,
            target,
            pendingSkill,
            pendingActionInstanceId.Value,
            actorPositionBefore,
            targetPositionBefore);
        EmitContacted(
            actor,
            pendingActionInstanceId,
            pendingWindupStartTick,
            pendingContactTick,
            pendingDelivery,
            pendingKind,
            pendingSkillId,
            pendingTargetId,
            resolveEvents);
        return true;
    }

    // === Action choreography seam (C1): emit the combat-event-intent channel. Pure observation —
    // these helpers never change sim numbers (position/RNG/damage/timing), so gameplay stays
    // byte-identical. ContactTick is the canonical integer fixed at windup (GPT Pro J20). ===

    private void EmitWindupStarted(UnitSnapshot actor, EvaluatedAction evaluated)
    {
        // This step is labelled state.StepIndex + 1 (AdvanceStep increments after the action loop), so the
        // windup begins at that tick and the contact lands ActionTicksTotal ticks later. The windup countdown
        // is now an integer (AdvanceTick decrements ActionTicksRemaining by 1/step from ActionTicksTotal), so
        // windupStartTick + ActionTicksTotal == the real resolve step by construction
        // (Phase 2.2c — retires the float StepsUntilResolve drift).
        var windupStartTick = State.StepIndex + 1;
        var contactTick = windupStartTick + actor.ActionTicksTotal;
        var actionInstanceId = State.AllocateActionInstanceId();
        var delivery = evaluated.Skill?.Delivery ?? SkillDelivery.Melee;
        actor.SetPendingActionInstance(actionInstanceId, windupStartTick, contactTick, delivery);

        var kind = evaluated.ActionType == BattleActionType.ActiveSkill ? CombatEventKind.Skill : CombatEventKind.BasicAttack;
        State.RecordCombatEvent(new BattleCombatEventIntent(
            windupStartTick,
            actionInstanceId,
            actor.Id,
            kind,
            delivery,
            windupStartTick,
            contactTick,
            CombatEventIntentStatus.Started,
            evaluated.Target?.Id,
            null,
            evaluated.Skill?.Id,
            null));
    }

    private void EmitContacted(
        UnitSnapshot actor,
        ActionInstanceId actionInstanceId,
        int windupStartTick,
        int contactTick,
        SkillDelivery delivery,
        CombatEventKind kind,
        string? skillId,
        EntityId? initialTargetId,
        IReadOnlyList<BattleEvent> resolveEvents)
    {
        if (!actionInstanceId.IsValid)
        {
            return;
        }

        var contactStepTick = State.StepIndex + 1;
        var contacts = new List<BattleContactIntent>();
        var contactIndex = 0;
        foreach (var resolveEvent in resolveEvents)
        {
            if (!IsContactEvent(resolveEvent))
            {
                continue;
            }

            // One ContactGroupIndex (0) per action — a single hit frame. Multi-hit assigns a fresh group
            // per frame in a later stage; AOE keeps one group across its N targets (J22).
            contacts.Add(new BattleContactIntent(
                contactIndex++,
                0,
                contactStepTick,
                resolveEvent.TargetId,
                ResolveOutcome(resolveEvent),
                resolveEvent.Value,
                resolveEvent.LogCode == BattleLogCode.ActiveSkillHeal,
                ResolveAccent(resolveEvent)));
        }

        State.RecordCombatEvent(new BattleCombatEventIntent(
            contactStepTick,
            actionInstanceId,
            actor.Id,
            kind,
            delivery,
            windupStartTick,
            contactTick,
            CombatEventIntentStatus.Contacted,
            initialTargetId,
            null,
            skillId,
            contacts));

        actor.ClearPendingActionInstance();
    }

    private void EmitCanceledIfPending(UnitSnapshot actor, string reason)
    {
        if (!actor.PendingActionInstanceId.IsValid)
        {
            return;
        }

        BattleDiagnosticRecorder.RecordDisplacementAborted(
            State,
            actor,
            State.FindUnit(actor.PendingTargetId),
            actor.ResolveSkill(actor.PendingSkillId),
            reason);

        var cancelTick = State.StepIndex + 1;
        var kind = actor.PendingActionType == BattleActionType.ActiveSkill ? CombatEventKind.Skill : CombatEventKind.BasicAttack;
        State.RecordCombatEvent(new BattleCombatEventIntent(
            cancelTick,
            actor.PendingActionInstanceId,
            actor.Id,
            kind,
            actor.PendingActionDelivery,
            actor.PendingWindupStartTick,
            actor.PendingContactTick,
            CombatEventIntentStatus.Canceled,
            actor.PendingTargetId,
            cancelTick,
            actor.PendingSkillId,
            null));

        actor.ClearPendingActionInstance();
    }

    private static bool IsContactEvent(BattleEvent resolveEvent)
    {
        return resolveEvent.LogCode is BattleLogCode.BasicAttackDamage
            or BattleLogCode.ActiveSkillDamage
            or BattleLogCode.ActiveSkillHeal;
    }

    private static CombatOutcome ResolveOutcome(BattleEvent resolveEvent)
    {
        // Stage 1 transitional: the sim self-labels its own resolution result from the event it just
        // produced. Stage 2 replaces this with a typed outcome returned directly by CombatActionResolver
        // so no Note string is read (GPT Pro J8 no-inference).
        var note = resolveEvent.Note ?? string.Empty;
        if (note.Contains("miss", StringComparison.OrdinalIgnoreCase)) return CombatOutcome.Miss;
        if (note.Contains("dodge", StringComparison.OrdinalIgnoreCase)) return CombatOutcome.Dodge;
        if (note.Contains("knockdown", StringComparison.OrdinalIgnoreCase)) return CombatOutcome.Knockdown;
        if (note.Contains("block", StringComparison.OrdinalIgnoreCase)) return CombatOutcome.Block;
        if (note.Contains("crit", StringComparison.OrdinalIgnoreCase)) return CombatOutcome.Crit;
        return CombatOutcome.Hit;
    }

    // P2 극적 순간 self-label — ResolveOutcome과 같은 Stage 1 transitional 패턴: sim이 방금 자기가
    // 만든 note 토큰(P0 positional consequence/detector)을 typed accent로 승격한다. presentation은
    // 이 typed 채널만 읽는다(J8). public: FastUnit 골든이 매핑을 고정한다(SM.Combat은 InternalsVisibleTo 없음).
    public static CombatContactAccent ResolveAccent(BattleEvent resolveEvent)
    {
        var note = resolveEvent.Note ?? string.Empty;
        var accent = CombatContactAccent.None;
        if (note.Contains("rear", StringComparison.Ordinal))
        {
            accent |= CombatContactAccent.Rear;
        }
        else if (note.Contains("flank", StringComparison.Ordinal))
        {
            accent |= CombatContactAccent.Flank;
        }

        if (note.Contains("screened", StringComparison.Ordinal))
        {
            accent |= CombatContactAccent.Screened;
        }

        if (note.Contains("save_moment", StringComparison.Ordinal))
        {
            accent |= CombatContactAccent.SaveMoment;
        }

        return accent;
    }

    private static float ResolveEvaluatedActionRange(UnitSnapshot actor, EvaluatedAction evaluated)
    {
        return evaluated.Skill?.Range ?? actor.AttackRange;
    }

    private void HandleDefendOrReposition(UnitSnapshot actor, List<BattleEvent> stepEvents)
    {
        var home = MovementResolver.ResolveHomePosition(State, actor);
        if (actor.Position.DistanceTo(home) <= HomeArrivalThreshold)
        {
            if (!actor.IsDefending)
            {
                actor.SetDefending();
                stepEvents.Add(CombatActionResolver.BuildEvent(State, actor, BattleActionType.WaitDefend, BattleLogCode.WaitDefend, actor, 0f));
            }
            else
            {
                actor.SetDefending();
            }

            return;
        }

        actor.ClearTarget(applySwitchDelay: false);
        actor.StopDefending();
        MovementResolver.MoveForIntent(State, actor, new EvaluatedAction(
            BattleActionType.WaitDefend,
            null,
            null,
            new TacticRule(999, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            new FloatRange(0f, 0f),
            CombatActionState.Reposition,
            ReevaluationReason.None,
            null));
    }

    private bool CheckForWinner()
    {
        if (!State.LivingAllies.Any() || !State.LivingEnemies.Any())
        {
            FinishBattle();
            return true;
        }

        return false;
    }

    private void FinishBattle()
    {
        if (IsFinished)
        {
            return;
        }

        IsFinished = true;
        foreach (var unit in State.AllUnits)
        {
            if (unit.PendingActionInstanceId.IsValid)
            {
                BattleDiagnosticRecorder.RecordDisplacementAborted(
                    State,
                    unit,
                    State.FindUnit(unit.PendingTargetId),
                    unit.ResolveSkill(unit.PendingSkillId),
                    "battle_ended");
            }
        }
        State.DespawnNonRosterEntities();
        foreach (var unit in State.AllUnits.Where(unit => !unit.IsAlive))
        {
            BattleTelemetryRecorder.RecordDespawn(State, unit);
        }

        if (State.LivingAllies.Any() && !State.LivingEnemies.Any())
        {
            Winner = TeamSide.Ally;
            BattleTelemetryRecorder.RecordBattleEnded(State, Winner.Value, State.StepIndex >= _maxSteps);
            return;
        }

        if (State.LivingEnemies.Any() && !State.LivingAllies.Any())
        {
            Winner = TeamSide.Enemy;
            BattleTelemetryRecorder.RecordBattleEnded(State, Winner.Value, State.StepIndex >= _maxSteps);
            return;
        }

        var allyHealth = State.LivingAllies.Sum(unit => unit.CurrentHealth);
        var enemyHealth = State.LivingEnemies.Sum(unit => unit.CurrentHealth);
        Winner = allyHealth >= enemyHealth ? TeamSide.Ally : TeamSide.Enemy;
        BattleTelemetryRecorder.RecordBattleEnded(State, Winner.Value, State.StepIndex >= _maxSteps);
    }
}
