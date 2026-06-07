using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Numerics;

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
    // Phase 1 HoldLine: a vanguard holds the frontline by clamping its pursuit to within this radius of its
    // formation anchor (master design "do not pursue beyond anchor + 2.0m"), so it engages the line without diving.
    private const float VanguardHoldRadius = 2.0f;

    /// <summary>
    /// 결정적 edge distance (중심거리 − nav 반지름 합, 0 floor). Phase 3.2c부터 sim의 거리 권위는
    /// <see cref="UnitSnapshot.FixedPosition"/> 기반 <see cref="Fixed32"/>다 — float <c>sqrt</c>를 거리 표면에서
    /// 제거해 크로스플랫폼 분기(ADR-0029)를 막는다. nav 반지름은 아직 stat-float라 비교 직전 한 번 양자화한다
    /// (Phase 4에서 fixed화). 행동 게이트(<see cref="IsInActionRange"/>/<see cref="IsWithinRangeBand"/>)는 이 권위로 비교한다.
    /// </summary>
    public static Fixed32 ComputeEdgeDistanceFixed(UnitSnapshot actor, UnitSnapshot target)
    {
        var centerDistance = actor.FixedPosition.DistanceTo(target.FixedPosition);
        var navSum = Fixed32.FromFloatQuantized(actor.NavigationRadius + target.NavigationRadius);
        return Fixed32.Max(Fixed32.Zero, centerDistance - navSum);
    }

    /// <summary>
    /// Egress projection — ordering 키·mobility 크기 등 아직 float인 거리 소비자용. 권위는
    /// <see cref="ComputeEdgeDistanceFixed"/>이고 이 float는 거기서 파생된다. 소비자가 fixed로 옮겨가면 함께 사라진다.
    /// </summary>
    public static float ComputeEdgeDistance(UnitSnapshot actor, UnitSnapshot target)
        => ComputeEdgeDistanceFixed(actor, target).ToFloat();

    public static bool IsInActionRange(UnitSnapshot actor, UnitSnapshot target, float desiredRange)
    {
        // 게이트는 fixed 권위에서 비교한다. desiredRange(+0.05 허용)는 stat-float라 RHS를 한 번 양자화한다.
        return ComputeEdgeDistanceFixed(actor, target) <= Fixed32.FromFloatQuantized(desiredRange + 0.05f);
    }

    public static bool IsWithinRangeBand(UnitSnapshot actor, UnitSnapshot target, FloatRange rangeBand, float hysteresis)
    {
        // FloatRange.Contains를 fixed로 인라인: edge ∈ [ClampedMin − margin, ClampedMax + margin].
        var edge = ComputeEdgeDistanceFixed(actor, target);
        var margin = MathF.Max(0f, hysteresis);
        var lo = Fixed32.FromFloatQuantized(rangeBand.ClampedMin - margin);
        var hi = Fixed32.FromFloatQuantized(rangeBand.ClampedMax + margin);
        return edge >= lo && edge <= hi;
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

        MovePosition(state, actor, next, BattleMotionKind.Approach, isDiscrete: true);
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
            // Phase 0 stand-and-shoot: a ranged/stationary striker holds its ground after firing instead of
            // repositioning to maintain range. The old maintain-range step was the baseline kite that broke
            // melee approach and read as moonwalk. Deliberate skirmish returns as a scout/rift archetype step.
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

        MovePosition(state, actor, nextPosition, BattleMotionKind.Reposition, isDiscrete: false);
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
        var target = evaluated.Target;

        // Mobility dash stays a content-driven gap-closer / reaction (an authored ability), not baseline AI.
        if (evaluated.Mobility != null)
        {
            var mobileDestination = ClampToArena(ClampToLeash(state, actor, evaluated.Mobility.Destination));
            MovePosition(state, actor, mobileDestination, BattleMotionKind.MobilityDash, isDiscrete: true);
            actor.StartMobilityCooldown();
            actor.SetActionState(evaluated.DesiredPhase);
            return;
        }

        // Phase 0 clean pursuit: walk to attack range, then stop and let the sim begin the (committed) swing.
        // No retreat/kite (baseline ranged stands and shoots) and no slot-gated approach (slots no longer gate
        // the attack) — just close the gap. Spread / formation / skirmish return as tactical intent later.
        var actionRange = evaluated.Skill?.Range ?? actor.AttackRange;
        if (IsInActionRange(actor, target, actionRange))
        {
            actor.SetActionState(CombatActionState.AcquireTarget);
            return;
        }

        // Phase 1 backline roles (AnchorFire = ranger, SupportAnchor = mystic) intentionally have NO "hold at the
        // spawn anchor" movement here. Holding home while out of range made a back-liner stand and watch whenever
        // its anchor sat outside firing range — the user-reported idle bystander. The hard invariant is: a unit
        // that is OUT OF RANGE of its target closes to range first; nobody idles out of range. A ranged unit's
        // long range band (ResolveDesiredPosition below) already settles it at stand-off distance behind the
        // frontline, so it gets in range, then holds and fires (Phase 0 stand-and-shoot) without diving into melee.
        // The AnchorFire / SupportAnchor intent stays a role signal (retarget seam, telegraph); it no longer idles.

        // Phase 1 HoldLine (vanguard role drama): the tank holds the frontline anchor band — it advances to
        // engage what comes to the line but clamps its pursuit to within VanguardHoldRadius of the anchor, so it
        // does not over-chase a target deep past the line (diving is the duelist's job). Posture-gated by RoleBrain.
        if (actor.CurrentCombatIntent.Type == CombatIntentType.HoldLine)
        {
            var anchor = ResolveHomePosition(state, actor);
            var holdDesired = ResolveDesiredPosition(state, actor, target, evaluated.DesiredRangeBand);
            var fromAnchor = holdDesired - anchor;
            if (fromAnchor.Length > VanguardHoldRadius)
            {
                holdDesired = anchor + (fromAnchor.Normalized * VanguardHoldRadius);
            }

            MoveTowards(state, actor, holdDesired, CombatActionState.Approach, allowProgressGate: true);
            return;
        }

        // Phase 1 Peel (vanguard role drama): move to the intercept point between the diver and the protected
        // ally (the threat is already the overridden target). If that intercept reaches the threat's attack range
        // the vanguard goes there; otherwise it just pursues the threat normally — never stall on a bad intercept.
        if (actor.CurrentCombatIntent.Type == CombatIntentType.Peel)
        {
            var intercept = actor.CurrentCombatIntent.AnchorPoint;
            if (intercept.DistanceTo(target.Position) <= actor.AttackRange + 0.05f)
            {
                MoveTowards(state, actor, intercept, CombatActionState.Approach, allowProgressGate: true);
                return;
            }

            MoveTowards(state, actor, ResolveDesiredPosition(state, actor, target, evaluated.DesiredRangeBand),
                CombatActionState.Approach, allowProgressGate: true);
            return;
        }

        var desiredPosition = ResolveDesiredPosition(state, actor, target, evaluated.DesiredRangeBand);
        MoveTowards(state, actor, desiredPosition, CombatActionState.Approach, allowProgressGate: true);
    }

    public static void ResolveFormationSpacing(BattleState state)
    {
        ResolveTeamSpacing(state, state.Allies);
        ResolveTeamSpacing(state, state.Enemies);
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

        // Phase 3.3: 넉백 회전각을 정수 roll basis → AngleTurn32(BAM)로 만들고 sin/cos는 turn-LUT로 구한다.
        // (angleRoll−0.5)·π/2 라디안 = (remainder−5000)/40000 turn. MathF 삼각함수(transcendental,
        // cross-platform 분기 위험)를 제거한다. 회전 자체는 아직 float(방향 normalize는 후속 슬라이스).
        var angleBasis = KnockbackRollBasis(state, actor, target, "kb:angle"); // [0, 9999]
        var angle = AngleTurn32.FromRaw(unchecked((uint)((long)(angleBasis - 5000) * 4294967296L / 40000L)));
        var cos = FixedMath.CosTurns(angle).ToFloat();
        var sin = FixedMath.SinTurns(angle).ToFloat();
        var rotated = new CombatVector2(
            (direction.X * cos) - (direction.Y * sin),
            (direction.X * sin) + (direction.Y * cos));

        var distRoll = KnockbackRoll(state, actor, target, "kb:dist");
        var stabilityFactor = MathF.Max(0.2f, 1f - target.Behavior.Stability);
        var critFactor = isCritical ? 1.6f : 1f;
        var distance = 0.28f * stabilityFactor * (0.6f + (distRoll * 0.6f)) * critFactor;

        var next = ClampToArena(ClampToLeash(state, target, target.Position + (rotated * distance)));
        MovePosition(state, target, next, BattleMotionKind.Knockback, isDiscrete: true, sourceActorId: actor.Id);
    }

    // 넉백 결정적 roll의 정수 basis [0, 9999]. 각도(turn-LUT)·거리(float) 모두 이 basis에서 파생한다.
    private static int KnockbackRollBasis(BattleState state, UnitSnapshot actor, UnitSnapshot target, string context)
    {
        unchecked
        {
            var hash = state.Seed;
            hash = (hash * 397) ^ state.StepIndex;
            hash = (hash * 397) ^ StableHash(actor.Id.Value);
            hash = (hash * 397) ^ StableHash(target.Id.Value);
            hash = (hash * 397) ^ StableHash(context);
            return Math.Abs(hash % 10000);
        }
    }

    private static float KnockbackRoll(BattleState state, UnitSnapshot actor, UnitSnapshot target, string context)
        => KnockbackRollBasis(state, actor, target, context) / 10000f;

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

    // GPT Pro Stage B (separation deadzone + damping). The old solver pushed any pair with
    // distance < minSeparation fully apart every step, with no deadzone — in a crowd that is constant
    // micro-shoving (the dominant "Reposition" churn that the treadmill measurement surfaces once slot
    // thrash is fixed). Instead we tolerate a small overlap band (push only below softMin = 85% of
    // minSeparation) and damp the correction so the pair relaxes toward the band edge over a few ticks
    // instead of snapping to full separation. At equilibrium (distance ≈ softMin) the push is ~0, so no
    // motion is emitted and the unit idles instead of walking-in-place. Pure function of truth positions
    // (no RNG, no accumulated state) → deterministic and reproduced exactly on re-sim.
    private const float SeparationDeadzoneScale = 0.85f;
    private const float SeparationDamping = 0.35f;

    private static void ResolveTeamSpacing(BattleState state, IReadOnlyList<UnitSnapshot> team)
    {
        // GPT Pro Phase 3 (HOLD). A unit that is engaged — standing within attack range of its target — is
        // NOT shoved by separation. It holds and fights, which removes the per-tick "engaged units shuffle"
        // that is the bulk of the visible treadmill (with sticky slots they already stand at distinct points,
        // so the residual push was pure jitter). Only a non-holding (traveling) unit yields; if both hold,
        // the small overlap is accepted (a deliberate space-resolve handles severe cases in a later phase).
        var holding = new bool[team.Count];
        for (var k = 0; k < team.Count; k++)
        {
            holding[k] = team[k].IsAlive && IsHoldingPosition(state, team[k]);
        }

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

                if (holding[i] && holding[j])
                {
                    continue; // both standing and fighting — do not jitter them apart
                }

                var minSeparation = left.SeparationRadius + right.SeparationRadius;
                var softMin = minSeparation * SeparationDeadzoneScale;
                var delta = right.Position - left.Position;
                var distance = delta.Length;
                if (distance >= softMin)
                {
                    continue; // inside the allowed overlap band → no shove (kills the per-step jitter)
                }

                // Exact (or near-exact) overlap has no usable delta direction; fall back to a deterministic
                // per-pair axis (stable hash, never RNG) so coincident units still separate reproducibly.
                var direction = distance <= 1e-4f
                    ? ResolveDeterministicSeparationAxis(left, right)
                    : delta.Normalized;
                var push = (softMin - distance) * SeparationDamping;
                if (!holding[i])
                {
                    MovePosition(state, left, ClampToArena(left.Position - (direction * push)), BattleMotionKind.Reposition, isDiscrete: false);
                }

                if (!holding[j])
                {
                    MovePosition(state, right, ClampToArena(right.Position + (direction * push)), BattleMotionKind.Reposition, isDiscrete: false);
                }
            }
        }
    }

    /// <summary>
    /// HOLD predicate (GPT Pro Phase 3): a unit is holding when it is engaged — alive, not still marching out
    /// from spawn, and standing within attack range of a live enemy target. Holding units stand and fight
    /// (no separation shove, no micro-adjust), which is what stops the engaged-unit shuffle. Deterministic
    /// (positions + range only), reproduced on re-sim.
    /// </summary>
    internal static bool IsHoldingPosition(BattleState state, UnitSnapshot unit)
    {
        if (!unit.IsAlive
            || unit.ActionState is CombatActionState.Spawn or CombatActionState.AdvanceToAnchor)
        {
            return false;
        }

        var target = state.FindUnit(unit.CurrentTargetId);
        return target != null
               && target.IsAlive
               && target.Side != unit.Side
               && IsInActionRange(unit, target, unit.AttackRange + ActionStartRangeTolerance);
    }

    private static CombatVector2 ResolveDeterministicSeparationAxis(UnitSnapshot left, UnitSnapshot right)
    {
        // Phase 3.3: 정수 hash → 정수 degree[0,359] → AngleTurn32(BAM)로 변환하고 sin/cos는 turn-LUT로 구한다.
        // MathF 삼각함수(transcendental)를 제거한다. 결과 축은 아직 float separation 수식에 쓰여 float로 project.
        var hash = StableHash(left.Id.Value) ^ StableHash(right.Id.Value);
        var deg = Math.Abs(hash) % 360; // [0, 359]
        var angle = AngleTurn32.FromRaw((uint)((long)deg * 4294967296L / 360L));
        return new CombatVector2(FixedMath.CosTurns(angle).ToFloat(), FixedMath.SinTurns(angle).ToFloat());
    }

    // GPT Pro Stage C (guarded progress-gate settle). After slot-hysteresis (A) and separation deadzone (B),
    // the residual treadmill is Approach-driven: a unit blocked by its own teammates keeps emitting a
    // full-step lateral sidestep toward an unreachable goal. This is the ACTUATOR fix — when a unit cannot
    // make meaningful forward progress AND it is blocked only by allies who are themselves already engaged
    // (the frontline is fighting), it HOLDS instead of shuffling: no MovePosition, no motion intent. Because
    // presentation drives the walk clip purely from per-step net displacement, emitting no motion makes the
    // unit idle, which directly removes the in-place walk. Liveness is preserved (no new deadlock): an enemy
    // blocker or a non-engaged frontline never gates (the unit keeps closing), and a deterministic per-actor
    // escape pulse forces an ungated move on a fixed cadence so nothing can hold forever.
    private const float ProgressGateFraction = 0.25f;     // "meaningful forward progress" = ≥ 1/4 of a step
    private const int ProgressGateEscapePeriod = 8;        // deterministic ungated move every K ticks per actor

    private static void MoveTowards(
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 targetPosition,
        CombatActionState actionState,
        bool allowProgressGate = false)
    {
        if (actor.IsRooted)
        {
            actor.SetActionState(actionState);
            return;
        }

        var stepDistance = Math.Max(0.01f, actor.MoveSpeed * state.FixedStepSeconds);
        var directNext = CombatVector2.MoveTowards(actor.Position, targetPosition, stepDistance);
        directNext = ClampToLeash(state, actor, directNext);
        directNext = ClampToArena(directNext);
        var next = ResolveCollisionAwareStep(state, actor, targetPosition, directNext, stepDistance);
        if (allowProgressGate && ShouldProgressGateSettle(state, actor, targetPosition, directNext, next, stepDistance))
        {
            // Hold position: emit NO motion (net-0 → presentation idles instead of walking-in-place). The
            // engage action state is kept so the unit re-evaluates and resumes the moment the lane clears.
            actor.SetActionState(actionState);
            return;
        }

        MovePosition(state, actor, next, ResolveLocomotionKind(actionState), isDiscrete: false);
        actor.SetActionState(actionState);
    }

    private static bool ShouldProgressGateSettle(
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 targetPosition,
        CombatVector2 directNext,
        CombatVector2 next,
        float stepDistance)
    {
        // 1. Is the resolved step actually making forward progress? If so, never gate — let it move.
        var forward = (targetPosition - actor.Position).Normalized;
        if (forward.SqrLength <= 0.0001f)
        {
            return false;
        }

        var progress = CombatVector2.Dot(next - actor.Position, forward);
        if (progress >= stepDistance * ProgressGateFraction)
        {
            return false;
        }

        // 2. Blocked only by allies, at least one of whom is itself in contact with an enemy (frontline
        //    engaged). An enemy blocker means contact pressure → never gate (keep closing / hold contact).
        var sawAllyBlocker = false;
        var sawEngagedAllyBlocker = false;
        foreach (var obstacle in state.AllUnits)
        {
            if (obstacle.Id == actor.Id || !obstacle.IsAlive)
            {
                continue;
            }

            var clearance = actor.NavigationRadius + obstacle.NavigationRadius + ObstacleClearancePadding;
            if (directNext.DistanceTo(obstacle.Position) >= clearance)
            {
                continue;
            }

            if (obstacle.Side != actor.Side)
            {
                return false; // an enemy is in the way — engage / hold contact, do not settle
            }

            sawAllyBlocker = true;
            if (IsBlockerEngagedWithEnemy(state, obstacle))
            {
                sawEngagedAllyBlocker = true;
            }
        }

        if (!sawAllyBlocker || !sawEngagedAllyBlocker)
        {
            return false; // not stuck behind a fighting frontline — keep advancing so the team can make contact
        }

        // 3. Deterministic escape pulse: a fixed per-actor cadence always allows an ungated move, so no unit
        //    can be held indefinitely (replay-safe — phase is a stable hash of the id, never RNG/time).
        if (((state.StepIndex + StableHash(actor.Id.Value)) % ProgressGateEscapePeriod) == 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsBlockerEngagedWithEnemy(BattleState state, UnitSnapshot blocker)
    {
        foreach (var enemy in state.GetOpponents(blocker.Side))
        {
            if (enemy.IsAlive && IsInActionRange(blocker, enemy, blocker.AttackRange))
            {
                return true;
            }
        }

        return false;
    }

    private const float MotionRecordThreshold = 1e-5f;

    // Additive: captures the truth from/to of a position change and records it as a typed motion
    // intent. SetPosition behaviour is unchanged, no RNG is touched, and the recorded list is a pure
    // side-output, so gameplay results stay byte-identical (verified by the determinism + spatial suites).
    private static void MovePosition(
        BattleState state,
        UnitSnapshot actor,
        CombatVector2 destination,
        BattleMotionKind kind,
        bool isDiscrete,
        EntityId? sourceActorId = null)
    {
        var from = actor.Position;
        actor.SetPosition(destination);
        if (from.DistanceTo(destination) > MotionRecordThreshold)
        {
            state.RecordMotion(actor.Id, kind, from, destination, sourceActorId, isDiscrete);
        }
    }

    private static BattleMotionKind ResolveLocomotionKind(CombatActionState actionState)
    {
        return actionState switch
        {
            CombatActionState.BreakContact => BattleMotionKind.Disengage,
            CombatActionState.Reposition => BattleMotionKind.Reposition,
            _ => BattleMotionKind.Approach,
        };
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

    // Phase 0: the combat leash is removed. The arena bounds (ClampToArena, applied alongside every call) are
    // the only spatial limit, so a unit may pursue its target anywhere on the field — no spawn-anchored radius
    // that pinned a melee short of a backline target (the endgame "treadmill"). Overextension discipline returns
    // as tactical intent scoring in a later phase, not as a hard movement clamp. Kept as an identity pass-through
    // so the existing call sites stay readable; the state/actor parameters are retained for that call shape.
    private static CombatVector2 ClampToLeash(BattleState state, UnitSnapshot actor, CombatVector2 position)
    {
        _ = state;
        _ = actor;
        return position;
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
