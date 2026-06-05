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
    private const float SlotArrivalThreshold = 0.15f;
    private const float SlotArrivalRadiusScale = 0.4f;
    private const float HomeArrivalThreshold = 0.12f;
    private const float ActionRangeTolerance = 0.35f;

    private readonly List<BattleEvent> _events = new();
    private readonly int _maxSteps;

    public BattleSimulator(BattleState state, int maxSteps = DefaultMaxSteps)
    {
        State = state;
        _maxSteps = Math.Max(1, maxSteps);
        BattleTelemetryRecorder.RecordBattleStarted(State);
        CurrentStep = BattleReadModelBuilder.BuildStep(
            State,
            Array.Empty<BattleEvent>(),
            isFinished: false,
            winner: null);
        CombatTriggerEngine.OnBattleStart(State);
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
        foreach (var unit in State.AllUnits)
        {
            unit.AdvanceTime(State.FixedStepSeconds);
        }
        State.AdvanceGroupDispersalLocks();

        StatusResolutionService.AdvanceStatuses(State, stepEvents);
        State.ScheduleOwnedEntityDespawnIfOwnerDead();
        State.AdvanceOwnedEntityDespawns();
        if (CheckForWinner())
        {
            State.AdvanceStep();
            CurrentStep = BattleReadModelBuilder.BuildStep(State, stepEvents, IsFinished, Winner, State.StepMotions, State.StepCombatEvents);
            return CurrentStep;
        }

        var orderedUnits = State.AllUnits
            .Where(unit => unit.IsAlive)
            .OrderByDescending(unit => unit.Speed)
            .ThenBy(unit => unit.Side)
            .ThenBy(unit => unit.Id.Value)
            .ToList();

        foreach (var actor in orderedUnits)
        {
            if (!actor.IsAlive)
            {
                continue;
            }

            if (actor.IsStunned)
            {
                EmitCanceledIfPending(actor);
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

            actor.SetEngagementSlot(evaluated.SlotAssignment);

            var inRangeBand = MovementResolver.IsWithinRangeBand(actor, evaluated.Target, evaluated.DesiredRangeBand, actor.Behavior.RangeHysteresis);
            var slotReady = !evaluated.RequiresEngagementSlot
                            || evaluated.SlotAssignment == null
                            || actor.Position.DistanceTo(evaluated.SlotAssignment.Position) <= Math.Max(SlotArrivalThreshold, actor.SeparationRadius * SlotArrivalRadiusScale)
                            || (inRangeBand && MovementResolver.IsInActionRange(actor, evaluated.Target, actor.AttackRange + ActionRangeTolerance));
            var canBeginAction = evaluated.Target.Side == actor.Side
                                 || evaluated.DesiredPhase == CombatActionState.ExecuteAction;
            var actionRangeReady = evaluated.Target.Side == actor.Side
                                   || MovementResolver.IsInActionRange(
                                       actor,
                                       evaluated.Target,
                                       ResolveEvaluatedActionRange(actor, evaluated) + MovementResolver.ActionStartRangeTolerance);
            if (canBeginAction
                && actionRangeReady
                && inRangeBand
                && slotReady
                && evaluated.Mobility == null)
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

        foreach (var resolvedEvent in stepEvents)
        {
            if (resolvedEvent.EventKind != BattleEventKind.Kill)
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
                CombatTriggerEngine.OnAllyDeath(State, victim);
            }
        }

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

        CurrentStep = BattleReadModelBuilder.BuildStep(State, stepEvents, IsFinished, Winner, State.StepMotions, State.StepCombatEvents);
        return CurrentStep;
    }

    public BattleResult RunToEnd()
    {
        while (!IsFinished)
        {
            Step();
        }

        return new BattleResult(
            Winner ?? TeamSide.Ally,
            State.StepIndex,
            State.ElapsedSeconds,
            _events.ToList(),
            CurrentStep.Units,
            State.TelemetryEvents.ToList(),
            State.ActivityTelemetry.BuildSnapshot(State));
    }

    private bool TryAdvanceSpawn(UnitSnapshot actor)
    {
        if (actor.ActionState is not (CombatActionState.Spawn or CombatActionState.AdvanceToAnchor))
        {
            return false;
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
            EmitCanceledIfPending(actor);
            actor.ClearTarget(applySwitchDelay: true);
            actor.SetActionState(CombatActionState.AcquireTarget);
            return false;
        }

        var desiredRange = actor.ResolveActionRange(actor.PendingSkillId);
        if (!MovementResolver.IsInActionRange(actor, target, desiredRange + ActionRangeTolerance))
        {
            EmitCanceledIfPending(actor);
            actor.ClearTarget(applySwitchDelay: true);
            actor.SetActionState(CombatActionState.AcquireTarget);
            return false;
        }

        if (ShouldCancelPendingBasicAttackForPreferredMinimum(actor, target, desiredRange))
        {
            EmitCanceledIfPending(actor);
            actor.ClearTarget(applySwitchDelay: false);
            actor.SetCurrentTarget(target.Id);
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

        var resolveEvents = CombatActionResolver.Resolve(State, actor);
        stepEvents.AddRange(resolveEvents);
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
        // This step is labelled state.StepIndex + 1 (AdvanceStep increments after the action loop), so
        // the windup begins at that tick and the contact lands StepsUntilResolve ticks later — a value
        // that mirrors the float decrement exactly and therefore equals the real resolve step.
        var windupStartTick = State.StepIndex + 1;
        var contactTick = windupStartTick + BattleWindupTickMath.StepsUntilResolve(actor.ActionTimerTotal, State.FixedStepSeconds);
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

            // Stage 1: one ContactGroupIndex (0) per action — a single hit frame. Multi-hit assigns a
            // fresh group per frame in a later stage; AOE keeps one group across its N targets (J22).
            contacts.Add(new BattleContactIntent(
                contactIndex++,
                0,
                contactStepTick,
                resolveEvent.TargetId,
                ResolveOutcome(resolveEvent)));
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

    private void EmitCanceledIfPending(UnitSnapshot actor)
    {
        if (!actor.PendingActionInstanceId.IsValid)
        {
            return;
        }

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

    private static float ResolveEvaluatedActionRange(UnitSnapshot actor, EvaluatedAction evaluated)
    {
        return evaluated.Skill?.Range ?? actor.AttackRange;
    }

    private static bool ShouldCancelPendingBasicAttackForPreferredMinimum(UnitSnapshot actor, UnitSnapshot target, float desiredRange)
    {
        if (actor.PendingActionType != BattleActionType.BasicAttack || actor.IsRooted)
        {
            return false;
        }

        var profile = BasicAttackActionProfileResolver.Resolve(actor);
        var preferredMinimum = ResolvePendingBasicAttackPreferredMinimum(actor, desiredRange);
        if (profile.Profile != BasicAttackActionProfile.StationaryStrike || preferredMinimum < 1.8f)
        {
            return false;
        }

        var edgeDistance = MovementResolver.ComputeEdgeDistance(actor, target);
        return edgeDistance < preferredMinimum - ResolvePendingActionRetreatBuffer(actor);
    }

    private static float ResolvePendingBasicAttackPreferredMinimum(UnitSnapshot actor, float desiredRange)
    {
        var preferredMinimum = actor.Behavior.PreferredRangeMin > 0f
            ? actor.Behavior.PreferredRangeMin
            : actor.PreferredRangeBand.ClampedMin;

        return Math.Min(Math.Max(0f, preferredMinimum), Math.Max(0.4f, desiredRange));
    }

    private static float ResolvePendingActionRetreatBuffer(UnitSnapshot actor)
    {
        var safeHysteresis = Math.Max(0f, actor.Behavior.RangeHysteresis);
        return safeHysteresis <= 0f
            ? 0f
            : Math.Min(Math.Max(0f, actor.Behavior.RetreatBuffer), safeHysteresis);
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
            false,
            null,
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
