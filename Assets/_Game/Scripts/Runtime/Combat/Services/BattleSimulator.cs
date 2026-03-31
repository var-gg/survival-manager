using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public sealed class BattleSimulator
{
    public const float DefaultFixedStepSeconds = 0.1f;
    private readonly List<BattleEvent> _events = new();
    private readonly int _maxSteps;

    public BattleSimulator(BattleState state, int maxSteps = 300)
    {
        State = state;
        _maxSteps = Math.Max(1, maxSteps);
        CurrentStep = BattleReadModelBuilder.BuildStep(
            State,
            Array.Empty<BattleEvent>(),
            isFinished: false,
            winner: null);
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

        var stepEvents = new List<BattleEvent>();
        foreach (var unit in State.AllUnits)
        {
            unit.AdvanceTime(State.FixedStepSeconds);
        }

        StatusResolutionService.AdvanceStatuses(State, stepEvents);
        if (CheckForWinner())
        {
            State.AdvanceStep();
            CurrentStep = BattleReadModelBuilder.BuildStep(State, stepEvents, IsFinished, Winner);
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
                actor.ClearTarget(applySwitchDelay: false);
                actor.SetActionState(CombatActionState.SeekTarget);
                continue;
            }

            if (TryAdvanceSpawn(actor))
            {
                continue;
            }

            if (actor.ActionState == CombatActionState.Windup)
            {
                if (TryResolvePendingAction(actor, stepEvents) && CheckForWinner())
                {
                    break;
                }

                continue;
            }

            var evaluated = TacticEvaluator.Evaluate(State, actor);
            if (evaluated.ActionType == BattleActionType.WaitDefend || evaluated.Target == null || !evaluated.Target.IsAlive)
            {
                HandleDefendOrReposition(actor, stepEvents);
                continue;
            }

            actor.SetCurrentTarget(evaluated.Target.Id);
            if (MovementResolver.IsInActionRange(actor, evaluated.Target, evaluated.DesiredRange))
            {
                if (actor.CooldownRemaining <= 0f)
                {
                    actor.BeginWindup(evaluated.ActionType, evaluated.Target.Id, evaluated.Skill?.Id);
                }
                else
                {
                    actor.SetActionState(CombatActionState.Recovery);
                }
            }
            else
            {
                MovementResolver.MoveForIntent(State, actor, evaluated);
            }
        }

        MovementResolver.ResolveFormationSpacing(State);
        State.AdvanceStep();

        foreach (var stepEvent in stepEvents)
        {
            _events.Add(stepEvent);
        }

        if (!IsFinished && (State.StepIndex >= _maxSteps || !State.LivingAllies.Any() || !State.LivingEnemies.Any()))
        {
            FinishBattle();
        }

        CurrentStep = BattleReadModelBuilder.BuildStep(State, stepEvents, IsFinished, Winner);
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
            CurrentStep.Units);
    }

    private bool TryAdvanceSpawn(UnitSnapshot actor)
    {
        if (actor.ActionState is not (CombatActionState.Spawn or CombatActionState.AdvanceToAnchor))
        {
            return false;
        }

        var home = MovementResolver.ResolveHomePosition(State, actor);
        if (actor.Position.DistanceTo(home) <= 0.05f)
        {
            actor.SetPosition(home);
            actor.SetActionState(CombatActionState.SeekTarget);
            return true;
        }

        actor.StopDefending();
        actor.SetActionState(CombatActionState.AdvanceToAnchor);
        var next = CombatVector2.MoveTowards(actor.Position, home, Math.Max(0.01f, actor.MoveSpeed * State.FixedStepSeconds));
        actor.SetPosition(next);
        return true;
    }

    private bool TryResolvePendingAction(UnitSnapshot actor, List<BattleEvent> stepEvents)
    {
        var target = State.FindUnit(actor.PendingTargetId);
        if (target == null || !target.IsAlive)
        {
            actor.ClearTarget(applySwitchDelay: true);
            actor.SetActionState(CombatActionState.SeekTarget);
            return false;
        }

        var desiredRange = actor.ResolveActionRange(actor.PendingSkillId);
        if (!MovementResolver.IsInActionRange(actor, target, desiredRange + 0.2f))
        {
            actor.ClearTarget(applySwitchDelay: true);
            actor.SetActionState(CombatActionState.SeekTarget);
            return false;
        }

        if (actor.ActionTimerRemaining > 0f)
        {
            return false;
        }

        stepEvents.AddRange(CombatActionResolver.Resolve(State, actor));
        return true;
    }

    private void HandleDefendOrReposition(UnitSnapshot actor, List<BattleEvent> stepEvents)
    {
        var home = MovementResolver.ResolveHomePosition(State, actor);
        if (actor.Position.DistanceTo(home) <= 0.12f)
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
            0f));
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
        if (State.LivingAllies.Any() && !State.LivingEnemies.Any())
        {
            Winner = TeamSide.Ally;
            return;
        }

        if (State.LivingEnemies.Any() && !State.LivingAllies.Any())
        {
            Winner = TeamSide.Enemy;
            return;
        }

        var allyHealth = State.LivingAllies.Sum(unit => unit.CurrentHealth);
        var enemyHealth = State.LivingEnemies.Sum(unit => unit.CurrentHealth);
        Winner = allyHealth >= enemyHealth ? TeamSide.Ally : TeamSide.Enemy;
    }
}
