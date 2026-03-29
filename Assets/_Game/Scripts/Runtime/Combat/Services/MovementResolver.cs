using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class MovementResolver
{
    private const float SameTeamSpacing = 0.7f;
    private const float ArenaHalfWidth = 8f;
    private const float ArenaHalfHeight = 3.2f;

    public static bool IsInActionRange(UnitSnapshot actor, UnitSnapshot target, float desiredRange)
    {
        return actor.Position.DistanceTo(target.Position) <= desiredRange + 0.05f;
    }

    public static CombatVector2 ResolveHomePosition(BattleState state, UnitSnapshot actor)
    {
        var direction = actor.Side == TeamSide.Ally ? 1f : -1f;
        var weakLane = ResolveWeakLane(state, actor.Side);
        var posture = state.GetPosture(actor.Side);

        var xOffset = posture switch
        {
            TeamPostureType.HoldLine => actor.Anchor.IsFrontRow() ? 0.15f * direction : -0.05f * direction,
            TeamPostureType.StandardAdvance => actor.Anchor.IsFrontRow() ? 0.45f * direction : 0.15f * direction,
            TeamPostureType.ProtectCarry => actor.Anchor.IsFrontRow() ? 0.2f * direction : -0.2f * direction,
            TeamPostureType.CollapseWeakSide => actor.Anchor.IsFrontRow() ? 0.35f * direction : 0.05f * direction,
            TeamPostureType.AllInBackline => actor.Anchor.IsFrontRow() ? 0.8f * direction : 0.3f * direction,
            _ => 0f,
        };

        var yOffset = posture == TeamPostureType.CollapseWeakSide
            ? weakLane * 0.35f
            : 0f;

        return actor.AnchorPosition + new CombatVector2(xOffset, yOffset);
    }

    public static void MoveForIntent(BattleState state, UnitSnapshot actor, EvaluatedAction evaluated)
    {
        if (evaluated.ActionType == BattleActionType.WaitDefend || evaluated.Target == null)
        {
            MoveTowards(state, actor, ResolveHomePosition(state, actor), CombatActionState.Reposition);
            return;
        }

        actor.StopDefending();

        var target = evaluated.Target;
        var desiredRange = evaluated.DesiredRange;
        var directionToTarget = (target.Position - actor.Position).Normalized;
        if (directionToTarget.SqrLength <= 0.0001f)
        {
            directionToTarget = actor.Side == TeamSide.Ally ? new CombatVector2(1f, 0f) : new CombatVector2(-1f, 0f);
        }

        var desiredPosition = target.Position - (directionToTarget * desiredRange);
        var currentDistance = actor.Position.DistanceTo(target.Position);
        var shouldRetreat = desiredRange > 1.6f && currentDistance < desiredRange * 0.65f;
        if (shouldRetreat)
        {
            desiredPosition = actor.Position - directionToTarget;
            MoveTowards(state, actor, desiredPosition, CombatActionState.Retreat);
            return;
        }

        MoveTowards(state, actor, desiredPosition, CombatActionState.MoveToEngage);
    }

    public static void ResolveFormationSpacing(BattleState state)
    {
        ResolveTeamSpacing(state.Allies);
        ResolveTeamSpacing(state.Enemies);
    }

    private static void ResolveTeamSpacing(IReadOnlyList<UnitSnapshot> team)
    {
        for (var i = 0; i < team.Count; i++)
        {
            var left = team[i];
            if (!left.IsAlive)
            {
                continue;
            }

            for (var j = i + 1; j < team.Count; j++)
            {
                var right = team[j];
                if (!right.IsAlive)
                {
                    continue;
                }

                var delta = right.Position - left.Position;
                var distance = delta.Length;
                if (distance <= 0.0001f || distance >= SameTeamSpacing)
                {
                    continue;
                }

                var push = (SameTeamSpacing - distance) * 0.5f;
                var direction = delta.Normalized;
                left.SetPosition(left.Position - (direction * push));
                right.SetPosition(right.Position + (direction * push));
            }
        }
    }

    private static void MoveTowards(BattleState state, UnitSnapshot actor, CombatVector2 targetPosition, CombatActionState actionState)
    {
        var stepDistance = Math.Max(0.01f, actor.MoveSpeed * state.FixedStepSeconds);
        var next = CombatVector2.MoveTowards(actor.Position, targetPosition, stepDistance);
        next = ClampToLeash(state, actor, next);
        next = ClampToArena(next);
        actor.SetPosition(next);
        actor.SetActionState(actionState);
    }

    private static CombatVector2 ClampToLeash(BattleState state, UnitSnapshot actor, CombatVector2 position)
    {
        var postureMultiplier = state.GetPosture(actor.Side) switch
        {
            TeamPostureType.HoldLine => 0.8f,
            TeamPostureType.ProtectCarry => actor.Anchor.IsBackRow() ? 0.7f : 0.95f,
            TeamPostureType.AllInBackline => 1.35f,
            _ => 1f,
        };

        var leash = actor.LeashDistance * postureMultiplier;
        var origin = actor.AnchorPosition;
        var offset = position - origin;
        if (offset.Length <= leash)
        {
            return position;
        }

        return origin + (offset.Normalized * leash);
    }

    private static CombatVector2 ClampToArena(CombatVector2 position)
    {
        return new CombatVector2(
            Math.Clamp(position.X, -ArenaHalfWidth, ArenaHalfWidth),
            Math.Clamp(position.Y, -ArenaHalfHeight, ArenaHalfHeight));
    }

    private static int ResolveWeakLane(BattleState state, TeamSide side)
    {
        var enemies = state.GetOpponents(side).Where(unit => unit.IsAlive).ToList();
        var top = enemies.Where(unit => unit.Anchor.LaneIndex() > 0).Sum(unit => unit.CurrentHealth);
        var bottom = enemies.Where(unit => unit.Anchor.LaneIndex() < 0).Sum(unit => unit.CurrentHealth);
        if (Math.Abs(top - bottom) < 0.01f)
        {
            return 0;
        }

        return top < bottom ? 1 : -1;
    }
}
