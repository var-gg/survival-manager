using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

public static class MovementResolver
{
    private const float ArenaHalfWidth = 8f;
    private const float ArenaHalfHeight = 3.2f;
    private const float ObstacleClearancePadding = 0.06f;
    private const float ObstacleCorridorPadding = 0.18f;
    private const float ObstacleDetourPadding = 0.28f;
    private const float PreImpactRangeTolerance = 0.12f;
    private const float BacklineMaxLaneOffset = 1.1f;
    private const float BaseLaneGap = 1.8f;
    private const float BaseRowGap = 2.1f;
    internal const float ActionStartRangeTolerance = 0.12f;

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
        var context = state.GetTacticContext(actor.Side);
        var intent = ResolvePostureHomePosition(state, actor, context);
        return ResolveBestZoneCandidate(
            state,
            actor,
            null,
            new FloatRange(0f, 0f),
            intent,
            intent,
            context,
            "home");
    }

    private static CombatVector2 ResolvePostureHomePosition(BattleState state, UnitSnapshot actor, TacticContext context)
    {
        var direction = actor.Side == TeamSide.Ally ? 1f : -1f;
        var weakLane = ResolveWeakLane(state, actor.Side);
        var posture = context.Posture;

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

    internal static BasicAttackPreImpactStepResult TryApplyBasicAttackPreImpactStep(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        if (actor.IsRooted)
        {
            return BasicAttackPreImpactStepResult.None(BasicAttackActionProfileResolver.Resolve(actor).Profile);
        }

        var profile = BasicAttackActionProfileResolver.Resolve(actor);
        if (!profile.AllowsPreImpactStep)
        {
            return BasicAttackPreImpactStepResult.None(profile.Profile);
        }

        var distance = ComputeEdgeDistance(actor, target);
        if (distance <= profile.ContactRange + 0.05f || distance > profile.LogicalRange + PreImpactRangeTolerance)
        {
            return BasicAttackPreImpactStepResult.None(profile.Profile);
        }

        var direction = (target.Position - actor.Position).Normalized;
        if (direction.SqrLength <= 0.0001f)
        {
            direction = actor.Side == TeamSide.Ally
                ? new CombatVector2(1f, 0f)
                : new CombatVector2(-1f, 0f);
        }

        var travel = Math.Min(profile.PreImpactStepDistance, Math.Max(0f, distance - profile.ContactRange));
        if (travel <= 0.01f)
        {
            return BasicAttackPreImpactStepResult.None(profile.Profile);
        }

        var desired = actor.Position + (direction * travel);
        var next = ClampToLeash(state, actor, ClampToArena(desired));
        next = ResolveCollisionAwareStep(state, actor, desired, next, travel);
        var movedDistance = actor.Position.DistanceTo(next);
        if (movedDistance <= 0.01f)
        {
            return BasicAttackPreImpactStepResult.None(profile.Profile);
        }

        actor.SetPosition(next);
        return new BasicAttackPreImpactStepResult(profile.Profile, movedDistance, BasicAttackActionProfileResolver.ToNoteToken(profile));
    }

    internal static PostAttackRepositionResult TryApplyPostAttackReposition(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        if (!actor.IsAlive || !target.IsAlive || actor.IsRooted)
        {
            return PostAttackRepositionResult.None;
        }

        var profile = BasicAttackActionProfileResolver.Resolve(actor);
        if (profile.Profile == BasicAttackActionProfile.StationaryStrike)
        {
            if (ShouldUseSideAnchoredRangedPosition(actor, actor.PreferredRangeBand))
            {
                var maintainRangeDesired = ResolveDesiredPosition(state, actor, target, actor.PreferredRangeBand);
                var next = CombatVector2.MoveTowards(actor.Position, maintainRangeDesired, Math.Max(0.01f, actor.MoveSpeed * state.FixedStepSeconds));
                next = ResolveCollisionAwareStep(state, actor, maintainRangeDesired, ClampToArena(ClampToLeash(state, actor, next)), Math.Max(0.01f, actor.MoveSpeed * state.FixedStepSeconds));
                var moved = actor.Position.DistanceTo(next);
                if (moved > 0.01f)
                {
                    actor.SetPosition(next);
                    return new PostAttackRepositionResult(true, moved, "post_attack_maintain_range");
                }
            }

            return PostAttackRepositionResult.None;
        }

        var context = state.GetTacticContext(actor.Side);
        var probability = TacticContext.Clamp01(0.25f + (0.20f * context.Width) + (0.15f * context.FlankBias) - (0.20f * context.Compactness));
        var roll = ResolveDeterministic01(state, actor, target, "post_attack_reposition", 0);
        if (roll > probability)
        {
            return PostAttackRepositionResult.None;
        }

        var away = actor.Position - target.Position;
        var awayDirection = away.SqrLength <= 0.0001f
            ? (actor.Side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f))
            : away.Normalized;
        var lateral = new CombatVector2(-awayDirection.Y, awayDirection.X);
        var lateralDistance = Lerp(0.15f, 0.35f, TacticContext.Clamp01(context.Width));
        lateralDistance *= actor.Definition.ClassId switch
        {
            "vanguard" => 0.55f,
            "duelist" => 1.25f,
            _ => 1f,
        };
        var lateralChoice = ResolvePostAttackLateralChoice(state, actor, target, lateral, lateralDistance);
        var lateralSign = lateralChoice.Sign;
        var desired = actor.Position + (lateral * lateralSign * lateralDistance) + (awayDirection * 0.10f);
        var nextPosition = ResolveCollisionAwareStep(
            state,
            actor,
            desired,
            ClampToArena(ClampToLeash(state, actor, desired)),
            lateralDistance + 0.10f);
        var movedDistance = actor.Position.DistanceTo(nextPosition);
        if (movedDistance <= 0.01f)
        {
            return PostAttackRepositionResult.None;
        }

        actor.SetPosition(nextPosition);
        state.ActivityTelemetry.RecordHandednessLateralReset(lateralChoice.Label);
        return new PostAttackRepositionResult(true, movedDistance, $"post_attack_reposition+{lateralChoice.Label}");
    }

    public static (int Sign, string Label) ResolvePostAttackLateralChoice(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        CombatVector2 lateral,
        float lateralDistance)
    {
        var preferredSign = HandednessDecisionService.ResolvePreferredSideSign(actor.Definition.DominantHand, actor.Side);
        if (preferredSign == 0)
        {
            var ambidextrousSign = HandednessDecisionService.ResolveAmbidextrousLeastCrowdedSign(state, actor, lateral, lateralDistance);
            return (ambidextrousSign, "hand_ambidextrous_least_crowded");
        }

        var preferredClear = IsLateralResetClear(state, actor, target, lateral, preferredSign, lateralDistance);
        var oppositeClear = IsLateralResetClear(state, actor, target, lateral, -preferredSign, lateralDistance);
        if (preferredClear && !oppositeClear)
        {
            return (preferredSign, $"hand_{HandednessDecisionService.ResolveHandLabel(actor.Definition.DominantHand)}_preferred");
        }

        if (!preferredClear && oppositeClear)
        {
            return (-preferredSign, $"hand_{HandednessDecisionService.ResolveHandLabel(actor.Definition.DominantHand)}_opposite_clear");
        }

        if (preferredClear && oppositeClear)
        {
            var roll = ResolveDeterministic01(state, actor, target, "post_attack_handedness_lateral", 0);
            return roll < 0.65f
                ? (preferredSign, $"hand_{HandednessDecisionService.ResolveHandLabel(actor.Definition.DominantHand)}_weighted")
                : (-preferredSign, "hand_stable_hash_weighted");
        }

        var fallbackSign = HandednessDecisionService.ResolveStableSign(state, actor, "post_attack_lateral_blocked");
        return (fallbackSign, "hand_blocked_hash");
    }

    private static bool IsLateralResetClear(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        CombatVector2 lateral,
        int lateralSign,
        float lateralDistance)
    {
        var candidate = ClampToArena(ClampToLeash(state, actor, actor.Position + (lateral * lateralSign * lateralDistance)));
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || obstacle.Id == target.Id || !obstacle.IsAlive)
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

        if (currentDistance > rangeBand.ClampedMax + approachBuffer
            || (evaluated.DesiredPhase == CombatActionState.Approach
                && currentDistance > rangeBand.ClampedMax + ActionStartRangeTolerance))
        {
            var desiredPosition = ResolveDesiredPosition(state, actor, target, rangeBand);
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

    public static void ApplyKnockback(BattleState state, UnitSnapshot actor, UnitSnapshot target, bool isCritical)
    {
        if (!target.IsAlive)
        {
            return;
        }

        var direction = (target.Position - actor.Position).Normalized;
        if (direction.SqrLength <= 0.0001f)
        {
            direction = actor.Side == TeamSide.Ally
                ? new CombatVector2(1f, 0f)
                : new CombatVector2(-1f, 0f);
        }

        var angleRoll = KnockbackRoll(state, actor, target, "kb:angle");
        var angle = (angleRoll - 0.5f) * (MathF.PI * 0.5f);
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var rotated = new CombatVector2(
            (direction.X * cos) - (direction.Y * sin),
            (direction.X * sin) + (direction.Y * cos));

        var distRoll = KnockbackRoll(state, actor, target, "kb:dist");
        var stabilityFactor = MathF.Max(0.2f, 1f - target.Behavior.Stability);
        var critFactor = isCritical ? 1.6f : 1f;
        var distance = 0.28f * stabilityFactor * (0.6f + (distRoll * 0.6f)) * critFactor;

        var next = ClampToArena(ClampToLeash(state, target, target.Position + (rotated * distance)));
        target.SetPosition(next);
    }

    private static float KnockbackRoll(BattleState state, UnitSnapshot actor, UnitSnapshot target, string context)
    {
        unchecked
        {
            var hash = state.Seed;
            hash = (hash * 397) ^ state.StepIndex;
            hash = (hash * 397) ^ StableHash(actor.Id.Value);
            hash = (hash * 397) ^ StableHash(target.Id.Value);
            hash = (hash * 397) ^ StableHash(context);
            var remainder = Math.Abs(hash % 10000);
            return remainder / 10000f;
        }
    }

    private static CombatVector2 ResolveDesiredPosition(BattleState state, UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand)
    {
        var context = state.GetTacticContext(actor.Side);
        var intent = ResolveRawDesiredPosition(state, actor, target, rangeBand, context);
        var home = ResolvePostureHomePosition(state, actor, context);
        return ResolveBestZoneCandidate(
            state,
            actor,
            target,
            rangeBand,
            intent,
            home,
            context,
            "desired");
    }

    private static CombatVector2 ResolveRawDesiredPosition(BattleState state, UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand, TacticContext context)
    {
        var centerDistance = rangeBand.Midpoint + actor.NavigationRadius + target.NavigationRadius;
        if (ShouldUseSideAnchoredRangedPosition(actor, rangeBand))
        {
            var sideDirection = actor.Side == TeamSide.Ally ? -1f : 1f;
            var home = ResolvePostureHomePosition(state, actor, context);
            var laneOffset = Math.Clamp(home.Y - target.Position.Y, -BacklineMaxLaneOffset, BacklineMaxLaneOffset);
            return new CombatVector2(target.Position.X + (sideDirection * centerDistance), target.Position.Y + laneOffset);
        }

        var directionToTarget = (target.Position - actor.Position).Normalized;
        if (directionToTarget.SqrLength <= 0.0001f)
        {
            directionToTarget = actor.Side == TeamSide.Ally ? new CombatVector2(1f, 0f) : new CombatVector2(-1f, 0f);
        }

        return target.Position - (directionToTarget * centerDistance);
    }

    private static CombatVector2 ResolveBestZoneCandidate(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target,
        FloatRange rangeBand,
        CombatVector2 intent,
        CombatVector2 home,
        TacticContext context,
        string intentId)
    {
        var candidates = BuildZoneCandidates(state, actor, intent, context).ToList();
        var best = candidates.First();
        var bestScore = ScoreZoneCandidate(state, actor, target, rangeBand, best, intent, home, context, intentId, 0);
        for (var i = 1; i < candidates.Count; i++)
        {
            var score = ScoreZoneCandidate(state, actor, target, rangeBand, candidates[i], intent, home, context, intentId, i);
            if (score < bestScore)
            {
                best = candidates[i];
                bestScore = score;
            }
        }

        return best;
    }

    private static IEnumerable<CombatVector2> BuildZoneCandidates(BattleState state, UnitSnapshot actor, CombatVector2 intent, TacticContext context)
    {
        var compactness = state.IsUnderGroupDispersalLock(actor) ? 0f : context.Compactness;
        var zoneWidth = BaseLaneGap * TacticContext.Clamp(0.55f + (0.45f * context.Width) - (0.25f * compactness) + ResolveQuirkSpread(actor), 0.45f, 1.35f);
        var zoneDepth = BaseRowGap * TacticContext.Clamp(0.35f + (0.45f * context.Depth) - (0.15f * compactness) + ResolvePostureDepth(context.Posture, actor), 0.35f, 1.20f);
        var forward = actor.Side == TeamSide.Ally ? new CombatVector2(1f, 0f) : new CombatVector2(-1f, 0f);
        var lateral = new CombatVector2(0f, 1f);
        var offsets = new[]
        {
            CombatVector2.Zero,
            lateral * (zoneWidth * 0.45f),
            lateral * (-zoneWidth * 0.45f),
            forward * (zoneDepth * 0.42f),
            forward * (-zoneDepth * 0.42f),
            (forward * (zoneDepth * 0.32f)) + (lateral * (zoneWidth * 0.32f)),
            (forward * (zoneDepth * 0.32f)) + (lateral * (-zoneWidth * 0.32f)),
            (forward * (-zoneDepth * 0.24f)) + (lateral * (zoneWidth * 0.28f)),
            (forward * (-zoneDepth * 0.24f)) + (lateral * (-zoneWidth * 0.28f)),
        };

        foreach (var offset in offsets)
        {
            yield return ClampToArena(ClampToLeash(state, actor, intent + offset));
        }
    }

    private static float ScoreZoneCandidate(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target,
        FloatRange rangeBand,
        CombatVector2 candidate,
        CombatVector2 intent,
        CombatVector2 home,
        TacticContext context,
        string intentId,
        int candidateIndex)
    {
        var distanceToIntent = candidate.DistanceTo(intent);
        var collisionPenalty = ResolveCollisionPenalty(state, actor, candidate);
        var lanePreference = ResolveLanePreference(actor, candidate, home, context);
        var targetAccess = target == null ? 0f : ResolveTargetAccess(actor, target, candidate, rangeBand);
        var rolePenalty = ResolveRolePenalty(actor, candidate, home, context);
        var tieBreak = ResolveDeterministic01(state, actor, target, intentId, candidateIndex) * 0.0001f;
        return distanceToIntent + collisionPenalty + lanePreference + targetAccess + rolePenalty + tieBreak;
    }

    private static float ResolveCollisionPenalty(BattleState state, UnitSnapshot actor, CombatVector2 candidate)
    {
        var penalty = 0f;
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || !obstacle.IsAlive)
            {
                continue;
            }

            var requiredClearance = actor.NavigationRadius + obstacle.NavigationRadius + ObstacleClearancePadding;
            var overlap = Math.Max(0f, requiredClearance - candidate.DistanceTo(obstacle.Position));
            penalty += overlap * overlap * 12f;
        }

        return penalty;
    }

    private static float ResolveLanePreference(UnitSnapshot actor, CombatVector2 candidate, CombatVector2 home, TacticContext context)
    {
        var preferredLane = home.Y + (context.FlankBias * actor.Anchor.LaneIndex() * 0.24f);
        var laneWeight = actor.Behavior.FormationLine == SM.Core.Contracts.FormationLine.Backline ? 0.18f : 0.28f;
        return MathF.Abs(candidate.Y - preferredLane) * laneWeight;
    }

    private static float ResolveTargetAccess(UnitSnapshot actor, UnitSnapshot target, CombatVector2 candidate, FloatRange rangeBand)
    {
        var centerDistance = candidate.DistanceTo(target.Position);
        var edgeDistance = Math.Max(0f, centerDistance - actor.NavigationRadius - target.NavigationRadius);
        var rangeMiss = edgeDistance > rangeBand.ClampedMax
            ? edgeDistance - rangeBand.ClampedMax
            : Math.Max(0f, rangeBand.ClampedMin - edgeDistance);
        return rangeMiss * 0.65f;
    }

    private static float ResolveRolePenalty(UnitSnapshot actor, CombatVector2 candidate, CombatVector2 home, TacticContext context)
    {
        var forward = actor.Side == TeamSide.Ally ? 1f : -1f;
        var forwardDelta = (candidate.X - home.X) * forward;
        var penalty = actor.Behavior.FormationLine switch
        {
            SM.Core.Contracts.FormationLine.Frontline => Math.Max(0f, -forwardDelta - 0.15f) * 0.34f,
            SM.Core.Contracts.FormationLine.Backline => Math.Max(0f, forwardDelta - 0.20f) * 0.46f,
            _ => Math.Abs(forwardDelta) * 0.08f,
        };

        if (context.ProtectCarryBias > 0f && actor.Behavior.FormationLine == SM.Core.Contracts.FormationLine.Backline)
        {
            penalty += MathF.Abs(candidate.Y) * context.ProtectCarryBias * 0.12f;
        }

        return penalty;
    }

    private static float ResolveQuirkSpread(UnitSnapshot actor)
    {
        return actor.Definition.RoleVariant switch
        {
            RoleVariantTag.Diver or RoleVariantTag.Harrier or RoleVariantTag.Executioner => 0.12f,
            RoleVariantTag.Sniper or RoleVariantTag.Battery => 0.06f,
            RoleVariantTag.Peeler => -0.08f,
            _ => actor.Definition.ClassId switch
            {
                "duelist" => 0.08f,
                "ranger" => 0.06f,
                "vanguard" => -0.05f,
                _ => 0f,
            },
        };
    }

    private static float ResolvePostureDepth(TeamPostureType posture, UnitSnapshot actor)
    {
        var postureDepth = posture switch
        {
            TeamPostureType.HoldLine => -0.05f,
            TeamPostureType.ProtectCarry => actor.Anchor.IsBackRow() ? -0.08f : 0.02f,
            TeamPostureType.CollapseWeakSide => 0.04f,
            TeamPostureType.AllInBackline => 0.12f,
            _ => 0f,
        };

        return postureDepth + (actor.Definition.RoleVariant switch
        {
            RoleVariantTag.Diver or RoleVariantTag.Executioner => 0.05f,
            RoleVariantTag.Sniper or RoleVariantTag.Battery => -0.04f,
            _ => 0f,
        });
    }

    private static bool ShouldUseSideAnchoredRangedPosition(UnitSnapshot actor, FloatRange rangeBand)
    {
        if (actor.Behavior.FormationLine != FormationLine.Backline)
        {
            return false;
        }

        return actor.Behavior.RangeDiscipline is RangeDiscipline.KiteBackward or RangeDiscipline.AnchorNearFrontline or RangeDiscipline.SideStepHold
               || rangeBand.ClampedMin >= 1.8f
               || actor.AttackRange >= 2.2f;
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
        var primaryBlocker = FindPrimaryBlockingObstacle(state, actor, targetPosition, forward);
        if (primaryBlocker != null)
        {
            AddDetourCandidates(candidates, state, actor, targetPosition, forward, lateral, primaryBlocker, sidePreference, stepDistance);
        }

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
        var closestClearance = float.MaxValue;
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || !obstacle.IsAlive)
            {
                continue;
            }

            var requiredClearance = actor.NavigationRadius + obstacle.NavigationRadius + ObstacleClearancePadding;
            var distance = candidate.DistanceTo(obstacle.Position);
            var overlap = Math.Max(0f, requiredClearance - distance);
            overlapPenalty += overlap * overlap;
            closestClearance = Math.Min(closestClearance, distance - requiredClearance);
        }

        var forward = (targetPosition - actor.Position).Normalized;
        var progress = forward.SqrLength <= 0.0001f
            ? 0f
            : CombatVector2.Dot(candidate - actor.Position, forward);
        var backtrackPenalty = progress < 0f ? Math.Abs(progress) * 4f : 0f;
        var clearancePenalty = closestClearance < 0.08f ? (0.08f - closestClearance) * 18f : 0f;
        var targetDistance = candidate.DistanceTo(targetPosition);
        var travelDistance = candidate.DistanceTo(actor.Position);
        return (overlapPenalty * 420f) + clearancePenalty + backtrackPenalty + targetDistance - (travelDistance * 0.05f) - (progress * 0.18f);
    }

    private static bool IsClearOfNavigationObstacles(BattleState state, UnitSnapshot actor, CombatVector2 candidate)
    {
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || !obstacle.IsAlive)
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

    private static void AddDetourCandidates(
        ICollection<CombatVector2> candidates,
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 targetPosition,
        CombatVector2 forward,
        CombatVector2 lateral,
        UnitSnapshot primaryBlocker,
        float sidePreference,
        float stepDistance)
    {
        var relative = primaryBlocker.Position - actor.Position;
        var currentSide = CombatVector2.Dot(relative, lateral);
        var preferredSide = Math.Abs(currentSide) > 0.05f
            ? MathF.Sign(currentSide)
            : sidePreference;
        AddDetourCandidatesForSide(candidates, state, actor, targetPosition, forward, lateral, primaryBlocker, preferredSide, stepDistance);
        AddDetourCandidatesForSide(candidates, state, actor, targetPosition, forward, lateral, primaryBlocker, -preferredSide, stepDistance);
    }

    private static void AddDetourCandidatesForSide(
        ICollection<CombatVector2> candidates,
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 targetPosition,
        CombatVector2 forward,
        CombatVector2 lateral,
        UnitSnapshot blocker,
        float side,
        float stepDistance)
    {
        var requiredClearance = actor.NavigationRadius + blocker.NavigationRadius + ObstacleDetourPadding;
        var sideVector = lateral * side;
        var anchors = new[]
        {
            blocker.Position + (sideVector * requiredClearance) + (forward * (actor.NavigationRadius * 0.45f)),
            blocker.Position + (sideVector * (requiredClearance + actor.SeparationRadius * 0.35f)) + (forward * (actor.NavigationRadius * 0.2f)),
            targetPosition + (sideVector * Math.Max(actor.SeparationRadius, requiredClearance * 0.55f)),
        };

        foreach (var anchor in anchors)
        {
            var direction = (anchor - actor.Position).Normalized;
            if (direction.SqrLength <= 0.0001f)
            {
                continue;
            }

            candidates.Add(ClampToLeash(state, actor, ClampToArena(actor.Position + (direction * stepDistance))));
        }

        var sidestep = Math.Max(stepDistance, actor.NavigationRadius * 0.48f);
        candidates.Add(ClampToLeash(state, actor, ClampToArena(actor.Position + (sideVector.Normalized * sidestep))));
    }

    private static UnitSnapshot? FindPrimaryBlockingObstacle(
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 targetPosition,
        CombatVector2 forward)
    {
        var path = targetPosition - actor.Position;
        var pathLength = path.Length;
        if (pathLength <= 0.0001f || forward.SqrLength <= 0.0001f)
        {
            return null;
        }

        UnitSnapshot? best = null;
        var bestProjection = float.MaxValue;
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || !obstacle.IsAlive)
            {
                continue;
            }

            var toObstacle = obstacle.Position - actor.Position;
            var projection = CombatVector2.Dot(toObstacle, forward);
            if (projection <= 0.01f || projection >= pathLength + obstacle.NavigationRadius)
            {
                continue;
            }

            var closest = actor.Position + (forward * projection);
            var corridorClearance = actor.NavigationRadius + obstacle.NavigationRadius + ObstacleCorridorPadding;
            if (closest.DistanceTo(obstacle.Position) >= corridorClearance)
            {
                continue;
            }

            if (projection < bestProjection
                || (Math.Abs(projection - bestProjection) <= 0.001f && best != null && string.CompareOrdinal(obstacle.Id.Value, best.Id.Value) < 0))
            {
                best = obstacle;
                bestProjection = projection;
            }
        }

        return best;
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

    private static float Lerp(float from, float to, float t)
    {
        return from + ((to - from) * TacticContext.Clamp01(t));
    }

    private static float ResolveDeterministic01(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? target,
        string intentId,
        int candidateIndex)
    {
        unchecked
        {
            var hash = state.Seed;
            hash = (hash * 397) ^ state.StepIndex;
            hash = (hash * 397) ^ StableHash(actor.Id.Value);
            hash = (hash * 397) ^ StableHash(target?.Id.Value ?? string.Empty);
            hash = (hash * 397) ^ StableHash(intentId);
            hash = (hash * 397) ^ candidateIndex;
            var normalized = Math.Abs(hash % 10000);
            return normalized / 9999f;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return hash;
        }
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

internal readonly record struct BasicAttackPreImpactStepResult(
    BasicAttackActionProfile Profile,
    float MovedDistance,
    string NoteToken)
{
    public bool Moved => MovedDistance > 0.01f;

    public static BasicAttackPreImpactStepResult None(BasicAttackActionProfile profile)
    {
        return new BasicAttackPreImpactStepResult(profile, 0f, string.Empty);
    }
}

internal readonly record struct PostAttackRepositionResult(
    bool Moved,
    float MovedDistance,
    string NoteToken)
{
    public static PostAttackRepositionResult None => new(false, 0f, string.Empty);
}
