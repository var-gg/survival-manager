using System;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Numerics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class MovementResolverTests
{
    private static UnitSnapshot MakeUnit(
        string id,
        TeamSide side,
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter,
        string classId = "vanguard",
        float hp = 20f,
        float attackRange = 1.2f,
        float moveSpeed = 1.9f,
        BehaviorProfile? behavior = null,
        FootprintProfile? footprint = null,
        BattleBasicAttackSpec? basicAttack = null)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(
            id,
            classId: classId,
            anchor: anchor,
            hp: hp,
            attackRange: attackRange,
            moveSpeed: moveSpeed,
            behavior: behavior,
            footprint: footprint,
            basicAttack: basicAttack);
        return new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, anchor),
            BattleFactory.ResolveSpawnPosition(side, anchor));
    }

    private static BattleState MakeState(
        UnitSnapshot[] allies,
        UnitSnapshot[] enemies,
        TeamPostureType allyPosture = TeamPostureType.StandardAdvance,
        TeamPostureType enemyPosture = TeamPostureType.StandardAdvance)
    {
        return new BattleState(allies, enemies, allyPosture, enemyPosture, BattleSimulator.DefaultFixedStepSeconds, 7);
    }

    // ── ComputeEdgeDistance ──

    [Test]
    public void EdgeDistance_SubtractsNavigationRadii()
    {
        var a = MakeUnit("a", TeamSide.Ally);
        var b = MakeUnit("b", TeamSide.Enemy);
        a.SetPosition(new CombatVector2(0f, 0f));
        b.SetPosition(new CombatVector2(3f, 0f));

        var edge = MovementResolver.ComputeEdgeDistance(a, b);
        var center = a.Position.DistanceTo(b.Position);

        Assert.That(edge, Is.LessThan(center), "Edge distance should be less than center distance");
        Assert.That(edge, Is.EqualTo(center - a.NavigationRadius - b.NavigationRadius).Within(0.001f));
    }

    [Test]
    public void EdgeDistance_NeverNegative_WhenOverlapping()
    {
        var a = MakeUnit("a", TeamSide.Ally);
        var b = MakeUnit("b", TeamSide.Enemy);
        a.SetPosition(new CombatVector2(0f, 0f));
        b.SetPosition(new CombatVector2(0f, 0f));

        Assert.That(MovementResolver.ComputeEdgeDistance(a, b), Is.GreaterThanOrEqualTo(0f));
    }

    // Phase 3.2c: 거리 권위는 FixedPosition 기반 Fixed32(ComputeEdgeDistanceFixed)이고, float
    // ComputeEdgeDistance는 그 .ToFloat() projection이다 — 둘이 갈라지지 않도록(권위 단일) 못박는다.
    [Test]
    public void EdgeDistanceFixed_IsAuthority_FloatIsItsExactProjection()
    {
        var a = MakeUnit("a", TeamSide.Ally);
        var b = MakeUnit("b", TeamSide.Enemy);
        a.SetPosition(new CombatVector2(0f, 0f));
        b.SetPosition(new CombatVector2(3f, 0f));

        var fixedEdge = MovementResolver.ComputeEdgeDistanceFixed(a, b);
        Assert.That(MovementResolver.ComputeEdgeDistance(a, b), Is.EqualTo(fixedEdge.ToFloat()),
            "float ComputeEdgeDistance는 fixed 권위의 정확한 projection이어야 한다");
    }

    [Test]
    public void EdgeDistanceFixed_FloorsAtZero_WhenOverlapping()
    {
        var a = MakeUnit("a", TeamSide.Ally);
        var b = MakeUnit("b", TeamSide.Enemy);
        a.SetPosition(new CombatVector2(0f, 0f));
        b.SetPosition(new CombatVector2(0f, 0f));

        Assert.That(MovementResolver.ComputeEdgeDistanceFixed(a, b).Raw, Is.GreaterThanOrEqualTo(0));
    }

    // ── IsInActionRange ──

    [Test]
    public void IsInActionRange_TrueWhenWithinRange_FalseWhenOutside()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, attackRange: 1.2f);
        var target = MakeUnit("target", TeamSide.Enemy);
        actor.SetPosition(new CombatVector2(0f, 0f));

        var navRadii = actor.NavigationRadius + target.NavigationRadius;
        target.SetPosition(new CombatVector2(navRadii + 1.0f, 0f));
        Assert.That(MovementResolver.IsInActionRange(actor, target, 1.2f), Is.True, "Should be in range");

        target.SetPosition(new CombatVector2(navRadii + 2.0f, 0f));
        Assert.That(MovementResolver.IsInActionRange(actor, target, 1.2f), Is.False, "Should be out of range");
    }

    [Test]
    public void BasicAttackProfileResolver_ClassifiesMeleeAndRangedAutoProfiles()
    {
        var vanguard = MakeUnit("vanguard", TeamSide.Ally, attackRange: 1.2f);
        var duelist = MakeUnit("duelist", TeamSide.Ally, classId: "duelist", attackRange: 1.25f);
        var ranger = MakeUnit("ranger", TeamSide.Ally, classId: "ranger", attackRange: 5.6f);

        Assert.That(BasicAttackActionProfileResolver.Resolve(vanguard).Profile, Is.EqualTo(BasicAttackActionProfile.StepInStrike));
        Assert.That(BasicAttackActionProfileResolver.Resolve(duelist).Profile, Is.EqualTo(BasicAttackActionProfile.LungeStrike));
        var rangedProfile = BasicAttackActionProfileResolver.Resolve(ranger);
        Assert.That(rangedProfile.Profile, Is.EqualTo(BasicAttackActionProfile.StationaryStrike));
        Assert.That(rangedProfile.ContactRange, Is.EqualTo(rangedProfile.LogicalRange).Within(0.001f));
    }

    [Test]
    public void MeleeFootprint_DefaultsMatchCombatMeterBaseline()
    {
        var vanguard = MakeUnit("vanguard", TeamSide.Ally, classId: "vanguard", attackRange: 1.3f);
        var duelist = MakeUnit("duelist", TeamSide.Ally, classId: "duelist", attackRange: 1.3f);

        Assert.That(vanguard.NavigationRadius, Is.InRange(0.42f, 0.5f));
        Assert.That(vanguard.CombatReach, Is.LessThanOrEqualTo(0.68f));
        Assert.That(vanguard.PreferredRangeBand.ClampedMin, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(vanguard.Footprint.EngagementSlotRadius, Is.EqualTo(0.95f).Within(0.001f));

        Assert.That(duelist.NavigationRadius, Is.InRange(0.38f, 0.44f));
        Assert.That(duelist.CombatReach, Is.LessThanOrEqualTo(0.58f));
        Assert.That(duelist.PreferredRangeBand.ClampedMin, Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(duelist.Footprint.EngagementSlotRadius, Is.EqualTo(0.9f).Within(0.001f));
    }

    [Test]
    public void BasicAttackProfiles_HaveDistinctLogicalRangeContactAndPreImpactBudgets()
    {
        var stationary = MakeUnit("stationary", TeamSide.Ally, classId: "ranger", attackRange: 5.6f);
        var stepIn = MakeUnit("step", TeamSide.Ally, classId: "vanguard", attackRange: 1.3f);
        var lunge = MakeUnit("lunge", TeamSide.Ally, classId: "duelist", attackRange: 1.3f);
        var dash = MakeUnit(
            "dash",
            TeamSide.Ally,
            classId: "duelist",
            attackRange: 1.8f,
            basicAttack: new BattleBasicAttackSpec(
                "dash:basic",
                "Dash Basic",
                ActionProfile: BasicAttackActionProfile.DashStrike));

        var stationaryProfile = BasicAttackActionProfileResolver.Resolve(stationary);
        var stepProfile = BasicAttackActionProfileResolver.Resolve(stepIn);
        var lungeProfile = BasicAttackActionProfileResolver.Resolve(lunge);
        var dashProfile = BasicAttackActionProfileResolver.Resolve(dash);

        Assert.That(stationaryProfile.ContactRange, Is.EqualTo(stationaryProfile.LogicalRange).Within(0.001f));
        Assert.That(stationaryProfile.PreImpactStepDistance, Is.Zero);

        Assert.That(stepProfile.ContactRange, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(lungeProfile.ContactRange, Is.EqualTo(0.52f).Within(0.001f));
        Assert.That(dashProfile.ContactRange, Is.EqualTo(0.68f).Within(0.001f));

        Assert.That(stepProfile.PreImpactStepDistance, Is.GreaterThan(0.65f));
        Assert.That(lungeProfile.PreImpactStepDistance, Is.GreaterThan(stepProfile.PreImpactStepDistance));
        Assert.That(dashProfile.PreImpactStepDistance, Is.GreaterThan(lungeProfile.PreImpactStepDistance));
    }

    [Test]
    public void ApproachOffset_MeleeBaselinePlacesAttackerOnOwnSideAtContactEdge()
    {
        // Phase 2: 슬롯 lease 폐지 — 결정적 접근 offset이 정지점을 제안한다. 단독 공격자는 정면(index 0),
        // 자기 진영 쪽에서, edge ≈ +0.15(접촉 간격)에 선다. 공격 적법성은 사거리 규칙만이 가진다.
        var actor = MakeUnit("actor", TeamSide.Ally, classId: "duelist", attackRange: 1.3f);
        var target = MakeUnit("target", TeamSide.Enemy, classId: "vanguard", attackRange: 1.3f);
        var state = MakeState(new[] { actor }, new[] { target });
        actor.SetCurrentTarget(target.Id);

        var point = ApproachOffsetService.TryResolveDesiredApproachPoint(state, actor, target);

        Assert.That(point, Is.Not.Null);
        var edgeDistance = point!.Value.DistanceTo(target.Position) - actor.NavigationRadius - target.NavigationRadius;
        Assert.That(edgeDistance, Is.InRange(0.10f, 0.20f), "stop point sits a contact gap away from the target");
        Assert.That(point.Value.X, Is.LessThan(target.Position.X), "a lone ally attacker approaches from its own side (direct front)");
    }

    // ── IsWithinRangeBand ──

    [Test]
    public void IsWithinRangeBand_RespectsHysteresis()
    {
        var actor = MakeUnit("actor", TeamSide.Ally);
        var target = MakeUnit("target", TeamSide.Enemy);
        actor.SetPosition(new CombatVector2(0f, 0f));

        var navRadii = actor.NavigationRadius + target.NavigationRadius;
        var band = new FloatRange(1.0f, 2.0f);

        target.SetPosition(new CombatVector2(navRadii + 1.5f, 0f));
        Assert.That(MovementResolver.IsWithinRangeBand(actor, target, band, 0f), Is.True, "Midpoint should be in band");

        target.SetPosition(new CombatVector2(navRadii + 0.8f, 0f));
        Assert.That(MovementResolver.IsWithinRangeBand(actor, target, band, 0f), Is.False, "Below min without hysteresis");
        Assert.That(MovementResolver.IsWithinRangeBand(actor, target, band, 0.3f), Is.True, "Below min but within hysteresis");
    }

    // ── ResolveHomePosition ──

    [Test]
    public void HomePosition_StandardAdvance_FrontIsAheadOfBack()
    {
        var front = MakeUnit("front", TeamSide.Ally, anchor: DeploymentAnchorId.FrontCenter);
        var back = MakeUnit("back", TeamSide.Ally, anchor: DeploymentAnchorId.BackCenter);
        var state = MakeState(new[] { front, back }, Array.Empty<UnitSnapshot>());

        var frontHome = MovementResolver.ResolveHomePosition(state, front);
        var backHome = MovementResolver.ResolveHomePosition(state, back);

        Assert.That(frontHome.X, Is.GreaterThan(backHome.X),
            "Ally front home should be further right (toward enemy) than back home");
    }

    [TestCase(TeamPostureType.HoldLine)]
    [TestCase(TeamPostureType.StandardAdvance)]
    [TestCase(TeamPostureType.ProtectCarry)]
    [TestCase(TeamPostureType.CollapseWeakSide)]
    [TestCase(TeamPostureType.AllInBackline)]
    public void HomePosition_AllPostures_ProduceValidPositions(TeamPostureType posture)
    {
        var unit = MakeUnit("unit", TeamSide.Ally, anchor: DeploymentAnchorId.FrontCenter);
        var state = MakeState(new[] { unit }, Array.Empty<UnitSnapshot>(), allyPosture: posture);

        var home = MovementResolver.ResolveHomePosition(state, unit);

        Assert.That(home.X, Is.InRange(-8f, 8f), "Home X should be within arena");
        Assert.That(home.Y, Is.InRange(-3.2f, 3.2f), "Home Y should be within arena");
    }

    // ── FormationSpacing ──

    // Stage B (GPT Pro): separation is now a damped relaxation into an allowed overlap band, not a
    // one-step snap to full separation. A deep overlap is corrected gradually and settles around softMin
    // (85% of minSeparation); pairs already inside the band are left alone (no per-step micro-shoving).
    [Test]
    public void FormationSpacing_RelaxesDeepOverlap_TowardDeadzoneBand_Damped()
    {
        var a = MakeUnit("a", TeamSide.Ally);
        var b = MakeUnit("b", TeamSide.Ally);
        a.SetPosition(new CombatVector2(0f, 0f));
        b.SetPosition(new CombatVector2(0.02f, 0f));
        a.SetActionState(CombatActionState.AcquireTarget);
        b.SetActionState(CombatActionState.AcquireTarget);
        var state = MakeState(new[] { a, b }, Array.Empty<UnitSnapshot>());
        var minSep = a.SeparationRadius + b.SeparationRadius;

        MovementResolver.ResolveFormationSpacing(state);
        var distOneStep = a.Position.DistanceTo(b.Position);
        Assert.That(distOneStep, Is.GreaterThan(0.02f), "deep overlap is pushed apart");
        Assert.That(distOneStep, Is.LessThan(minSep), "but damped — not a one-step snap to full separation");

        for (var i = 0; i < 40; i++)
        {
            MovementResolver.ResolveFormationSpacing(state);
        }

        var distConverged = a.Position.DistanceTo(b.Position);
        Assert.That(distConverged, Is.InRange(minSep * 0.85f - 0.05f, minSep + 0.05f),
            "settles within the allowed overlap band, no overshoot");
    }

    [Test]
    public void FormationSpacing_WithinDeadzoneBand_DoesNotShove()
    {
        var a = MakeUnit("a", TeamSide.Ally);
        var b = MakeUnit("b", TeamSide.Ally);
        var minSep = a.SeparationRadius + b.SeparationRadius;
        a.SetPosition(new CombatVector2(0f, 0f));
        b.SetPosition(new CombatVector2(minSep * 0.90f, 0f)); // inside the band (push only below 85%)
        a.SetActionState(CombatActionState.AcquireTarget);
        b.SetActionState(CombatActionState.AcquireTarget);
        var state = MakeState(new[] { a, b }, Array.Empty<UnitSnapshot>());

        var aBefore = a.Position;
        var bBefore = b.Position;
        MovementResolver.ResolveFormationSpacing(state);

        Assert.That(a.Position.DistanceTo(aBefore), Is.LessThan(1e-4f), "no shove inside the deadzone band");
        Assert.That(b.Position.DistanceTo(bBefore), Is.LessThan(1e-4f), "no shove inside the deadzone band");
    }

    [Test]
    public void FormationSpacing_DoesNotMove_DeadUnits()
    {
        var alive = MakeUnit("alive", TeamSide.Ally);
        var dead = MakeUnit("dead", TeamSide.Ally, hp: 1f);
        alive.SetPosition(new CombatVector2(0f, 0f));
        dead.SetPosition(new CombatVector2(0.01f, 0f));
        dead.TakeDamage(999f);

        var deadPosBefore = dead.Position;
        var state = MakeState(new[] { alive, dead }, Array.Empty<UnitSnapshot>());
        MovementResolver.ResolveFormationSpacing(state);

        Assert.That(dead.Position.X, Is.EqualTo(deadPosBefore.X).Within(0.001f), "Dead units should not be moved");
    }

    // ── MoveForIntent: rooted ──

    [Test]
    public void MoveForIntent_RootedUnit_DoesNotChangePosition()
    {
        var actor = MakeUnit("actor", TeamSide.Ally);
        var target = MakeUnit("target", TeamSide.Enemy);
        actor.SetPosition(new CombatVector2(-2f, 0f));
        target.SetPosition(new CombatVector2(2f, 0f));
        actor.SetActionState(CombatActionState.AcquireTarget);
        actor.ApplyStatus(new StatusApplicationSpec("status.root", "root", 5f, 0f));

        var posBefore = actor.Position;
        var state = MakeState(new[] { actor }, new[] { target });
        var evalAction = new EvaluatedAction(
            BattleActionType.BasicAttack,
            target,
            null,
            new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
            new FloatRange(0f, 1.5f),
            CombatActionState.Approach,
            ReevaluationReason.None,
            null);

        MovementResolver.MoveForIntent(state, actor, evalAction);

        Assert.That(actor.Position.X, Is.EqualTo(posBefore.X).Within(0.001f), "Rooted unit should not move");
    }

    // ── Stage C: guarded progress-gate settle (GPT Pro) ──

    private static EvaluatedAction ApproachFarTarget(UnitSnapshot target)
    {
        return new EvaluatedAction(
            BattleActionType.BasicAttack,
            target,
            null,
            new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
            new FloatRange(0.5f, 1.1f),
            CombatActionState.Approach,
            ReevaluationReason.None,
            null);
    }

    [Test]
    public void ProgressGate_SettlesWhenBlockedByEngagedAlly_EmittingNoMotion()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, attackRange: 1.2f);
        var frontAlly = MakeUnit("front_ally", TeamSide.Ally, attackRange: 1.2f);
        var frontEnemy = MakeUnit("front_enemy", TeamSide.Enemy, attackRange: 1.2f);
        var farTarget = MakeUnit("far_target", TeamSide.Enemy, attackRange: 1.2f);

        actor.SetPosition(new CombatVector2(0f, 0f));
        actor.SetActionState(CombatActionState.AcquireTarget);
        frontAlly.SetPosition(new CombatVector2(0.6f, 0f));   // directly blocks the actor's lane
        frontEnemy.SetPosition(new CombatVector2(1.5f, 0f));  // ...and the ally is in contact with this enemy
        farTarget.SetPosition(new CombatVector2(5f, 0f));     // actor's focus target, unreachable through the ally

        var state = MakeState(new[] { actor, frontAlly }, new[] { frontEnemy, farTarget });
        var before = actor.Position;

        MovementResolver.MoveForIntent(state, actor, ApproachFarTarget(farTarget));

        // The gate holds the unit (no motion); on its deterministic escape-pulse tick it may take one ungated
        // step, but never a forward shuffle through the engaged ally. Either way it does not advance — which is
        // pulse-independent and is the property that kills the in-place walk.
        Assert.That(actor.Position.X, Is.LessThanOrEqualTo(before.X + 1e-4f),
            "blocked behind an engaged ally → never shuffles forward (holds / backs off, no treadmill)");
    }

    [Test]
    public void ProgressGate_DoesNotSettle_WhenBlockedByEnemy()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, attackRange: 1.2f);
        var blockingEnemy = MakeUnit("block_enemy", TeamSide.Enemy, attackRange: 1.2f);
        var farTarget = MakeUnit("far_target", TeamSide.Enemy, attackRange: 1.2f);

        actor.SetPosition(new CombatVector2(0f, 0f));
        actor.SetActionState(CombatActionState.AcquireTarget);
        blockingEnemy.SetPosition(new CombatVector2(0.6f, 0f)); // an ENEMY blocks the lane
        farTarget.SetPosition(new CombatVector2(5f, 0f));

        var state = MakeState(new[] { actor }, new[] { blockingEnemy, farTarget });
        var before = actor.Position;

        MovementResolver.MoveForIntent(state, actor, ApproachFarTarget(farTarget));

        Assert.That(actor.Position.DistanceTo(before), Is.GreaterThan(1e-3f),
            "an enemy in the lane means engage / keep closing — never settle behind it");
    }
}
