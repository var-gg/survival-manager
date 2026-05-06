using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class MovementResolver
{
    private const float ArenaHalfWidth = 8f;
    private const float ArenaHalfHeight = 3.2f;
    private const float ObstacleClearancePadding = 0.06f;

    public static float ComputeEdgeDistance(UnitSnapshot actor, UnitSnapshot target)
    {
        var centerDistance = actor.Position.DistanceTo(target.Position);
        return Math.Max(0f, centerDistance - actor.NavigationRadius - target.NavigationRadius);
    }

    public static bool IsInActionRange(UnitSnapshot actor, UnitSnapshot target, float desiredRange)
    {
        return ComputeEdgeDistance(actor, target) <= desiredRange + 0.05f;
    }

    public static bool IsWithinRangeBand(UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand, float hysteresis)
    {
        return rangeBand.Contains(ComputeEdgeDistance(actor, target), hysteresis);
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

    public static MobilityDecision? BuildMobilityDecision(UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand)
    {
        if (actor.Mobility is not { IsEnabled: true } mobility)
        {
            return null;
        }

        var distance = ComputeEdgeDistance(actor, target);
        var toTarget = (target.Position - actor.Position).Normalized;
        if (toTarget.SqrLength <= 0.0001f)
        {
            toTarget = actor.Side == TeamSide.Ally
                ? new CombatVector2(1f, 0f)
                : new CombatVector2(-1f, 0f);
        }

        switch (mobility.Purpose)
        {
            case MobilityPurpose.Disengage:
            case MobilityPurpose.Evade:
            case MobilityPurpose.MaintainRange:
                if (distance >= rangeBand.ClampedMin - (actor.Behavior.RangeHysteresis * 0.35f))
                {
                    return null;
                }

                var pressureTriggerMax = Math.Max(
                    Math.Max(mobility.TriggerMinDistance, mobility.TriggerMaxDistance),
                    rangeBand.ClampedMin - Math.Max(0.1f, actor.Behavior.RangeHysteresis * 0.5f));
                if (!actor.CanUseMobility(distance, pressureTriggerMax))
                {
                    return null;
                }

                var away = actor.Position - target.Position;
                var awayDirection = away.SqrLength <= 0.0001f
                    ? (actor.Side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f))
                    : away.Normalized;
                var lateral = new CombatVector2(-awayDirection.Y, awayDirection.X) * mobility.LateralBias;
                var escapeDirection = (awayDirection + lateral).Normalized;
                var requiredTravel = Math.Max(
                    mobility.Distance,
                    (rangeBand.ClampedMin - distance) + Math.Max(0.35f, actor.Behavior.RangeHysteresis + 0.15f));
                var escapeDestination = actor.Position + (escapeDirection * requiredTravel);
                return new MobilityDecision(mobility, escapeDestination);

            case MobilityPurpose.Engage:
            case MobilityPurpose.Chase:
                if (distance <= rangeBand.ClampedMax + actor.Behavior.RangeHysteresis)
                {
                    return null;
                }

                if (!actor.CanUseMobility(distance))
                {
                    return null;
                }

                return new MobilityDecision(mobility, actor.Position + (toTarget * mobility.Distance));

            default:
                return null;
        }
    }

    public static void MoveForIntent(BattleState state, UnitSnapshot actor, EvaluatedAction evaluated)
    {
        if (evaluated.ActionType == BattleActionType.WaitDefend || evaluated.Target == null)
        {
            MoveTowards(state, actor, ResolveHomePosition(state, actor), CombatActionState.Reposition);
            return;
        }

        actor.StopDefending();
        actor.SetEngagementSlot(evaluated.SlotAssignment);

        var target = evaluated.Target;
        if (evaluated.Mobility != null)
        {
            var mobileDestination = ClampToArena(ClampToLeash(state, actor, evaluated.Mobility.Destination));
            actor.SetPosition(mobileDestination);
            actor.StartMobilityCooldown();
            actor.SetActionState(evaluated.DesiredPhase);
            return;
        }

        var currentDistance = ComputeEdgeDistance(actor, target);
        var rangeBand = evaluated.DesiredRangeBand;
        var approachBuffer = ResolveMovementBuffer(actor.Behavior.ApproachBuffer, actor.Behavior.RangeHysteresis);
        var retreatBuffer = ResolveMovementBuffer(actor.Behavior.RetreatBuffer, actor.Behavior.RangeHysteresis);

        if (currentDistance < rangeBand.ClampedMin - retreatBuffer)
        {
            var away = actor.Position - target.Position;
            var awayDirection = away.SqrLength <= 0.0001f
                ? (actor.Side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f))
                : away.Normalized;
            var retreatOffset = Math.Max(0.35f, (rangeBand.ClampedMin - currentDistance) + 0.2f);
            MoveTowards(state, actor, actor.Position + (awayDirection * retreatOffset), CombatActionState.BreakContact);
            return;
        }

        if (evaluated.SlotAssignment is { } slotAssignment)
        {
            var slotDistance = actor.Position.DistanceTo(slotAssignment.Position);
            if (slotDistance > Math.Max(0.15f, actor.SeparationRadius * 0.35f))
            {
                MoveTowards(
                    state,
                    actor,
                    slotAssignment.Position,
                    slotAssignment.IsOverflow ? CombatActionState.SecurePosition : CombatActionState.Approach);
                return;
            }
        }

        if (currentDistance > rangeBand.ClampedMax + approachBuffer)
        {
            var desiredPosition = ResolveDesiredPosition(actor, target, rangeBand);
            MoveTowards(state, actor, desiredPosition, evaluated.SlotAssignment != null ? CombatActionState.SecurePosition : CombatActionState.Approach);
            return;
        }

        actor.SetActionState(evaluated.DesiredPhase == CombatActionState.ExecuteAction
            ? CombatActionState.AcquireTarget
            : evaluated.DesiredPhase);
    }

    public static void ResolveFormationSpacing(BattleState state)
    {
        ResolveTeamSpacing(state.Allies);
        ResolveTeamSpacing(state.Enemies);
    }

    private static CombatVector2 ResolveDesiredPosition(UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand)
    {
        var directionToTarget = (target.Position - actor.Position).Normalized;
        if (directionToTarget.SqrLength <= 0.0001f)
        {
            directionToTarget = actor.Side == TeamSide.Ally ? new CombatVector2(1f, 0f) : new CombatVector2(-1f, 0f);
        }

        var centerDistance = rangeBand.Midpoint + actor.NavigationRadius + target.NavigationRadius;
        return target.Position - (directionToTarget * centerDistance);
    }

    private static float ResolveMovementBuffer(float authoredBuffer, float rangeHysteresis)
    {
        var safeHysteresis = Math.Max(0f, rangeHysteresis);
        return safeHysteresis <= 0f
            ? 0f
            : Math.Min(Math.Max(0f, authoredBuffer), safeHysteresis);
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

                var minSeparation = left.SeparationRadius + right.SeparationRadius;
                var delta = right.Position - left.Position;
                var distance = delta.Length;
                if (distance <= 0.0001f || distance >= minSeparation)
                {
                    continue;
                }

                var push = (minSeparation - distance) * 0.5f;
                var direction = delta.Normalized;
                left.SetPosition(ClampToArena(left.Position - (direction * push)));
                right.SetPosition(ClampToArena(right.Position + (direction * push)));
            }
        }
    }

    private static void MoveTowards(BattleState state, UnitSnapshot actor, CombatVector2 targetPosition, CombatActionState actionState)
    {
        if (actor.IsRooted)
        {
            actor.SetActionState(actionState);
            return;
        }

        var stepDistance = Math.Max(0.01f, actor.MoveSpeed * state.FixedStepSeconds);
        var next = CombatVector2.MoveTowards(actor.Position, targetPosition, stepDistance);
        next = ClampToLeash(state, actor, next);
        next = ClampToArena(next);
        next = ResolveCollisionAwareStep(state, actor, targetPosition, next, stepDistance);
        actor.SetPosition(next);
        actor.SetActionState(actionState);
    }

    private static CombatVector2 ResolveCollisionAwareStep(
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 targetPosition,
        CombatVector2 directNext,
        float stepDistance)
    {
        if (IsClearOfNavigationObstacles(state, actor, directNext))
        {
            return directNext;
        }

        var forward = (targetPosition - actor.Position).Normalized;
        if (forward.SqrLength <= 0.0001f)
        {
            forward = actor.Side == TeamSide.Ally
                ? new CombatVector2(1f, 0f)
                : new CombatVector2(-1f, 0f);
        }

        var lateral = new CombatVector2(-forward.Y, forward.X);
        var sidePreference = ResolveSidePreference(actor);
        var candidates = new List<CombatVector2>(12);
        AddSteeringCandidates(candidates, state, actor, forward, lateral * sidePreference, stepDistance);
        AddSteeringCandidates(candidates, state, actor, forward, lateral * -sidePreference, stepDistance);

        if (candidates.Count == 0)
        {
            return actor.Position;
        }

        var best = candidates.First();
        var bestScore = ScoreNavigationCandidate(state, actor, targetPosition, best);
        foreach (var candidate in candidates)
        {
            var score = ScoreNavigationCandidate(state, actor, targetPosition, candidate);
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static void AddSteeringCandidates(
        ICollection<CombatVector2> candidates,
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 forward,
        CombatVector2 lateral,
        float stepDistance)
    {
        var weights = new[] { 0.45f, 0.75f, 1.10f, 1.55f };
        foreach (var weight in weights)
        {
            var direction = (forward + (lateral * weight)).Normalized;
            if (direction.SqrLength <= 0.0001f)
            {
                continue;
            }

            var candidate = actor.Position + (direction * stepDistance);
            candidate = ClampToLeash(state, actor, ClampToArena(candidate));
            candidates.Add(candidate);
        }

        var sidestepDistance = Math.Max(stepDistance, actor.NavigationRadius * 0.35f);
        candidates.Add(ClampToLeash(state, actor, ClampToArena(actor.Position + (lateral.Normalized * sidestepDistance))));
    }

    private static float ScoreNavigationCandidate(BattleState state, UnitSnapshot actor, CombatVector2 targetPosition, CombatVector2 candidate)
    {
        var overlapPenalty = 0f;
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id)
            {
                continue;
            }

            var requiredClearance = actor.NavigationRadius + obstacle.NavigationRadius + ObstacleClearancePadding;
            var distance = candidate.DistanceTo(obstacle.Position);
            var overlap = Math.Max(0f, requiredClearance - distance);
            overlapPenalty += overlap * overlap;
        }

        var targetDistance = candidate.DistanceTo(targetPosition);
        var travelDistance = candidate.DistanceTo(actor.Position);
        return (overlapPenalty * 120f) + targetDistance - (travelDistance * 0.05f);
    }

    private static bool IsClearOfNavigationObstacles(BattleState state, UnitSnapshot actor, CombatVector2 candidate)
    {
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id)
            {
                continue;
            }

            var requiredClearance = actor.NavigationRadius + obstacle.NavigationRadius + ObstacleClearancePadding;
            if (candidate.DistanceTo(obstacle.Position) < requiredClearance)
            {
                return false;
            }
        }

        return true;
    }

    private static float ResolveSidePreference(UnitSnapshot actor)
    {
        var hash = 17;
        foreach (var ch in actor.Id.Value)
        {
            hash = (hash * 31) + ch;
        }

        return (hash & 1) == 0 ? 1f : -1f;
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
