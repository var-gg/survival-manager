using System;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

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
        var ranger = MakeUnit("ranger", TeamSide.Ally, classId: "ranger", attackRange: 3.2f);

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
        var stationary = MakeUnit("stationary", TeamSide.Ally, classId: "ranger", attackRange: 3.2f);
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
    public void EngagementSlot_MeleeBaselinePlacesAttackerAtReadableContactEdge()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, classId: "duelist", attackRange: 1.3f);
        var target = MakeUnit("target", TeamSide.Enemy, classId: "vanguard", attackRange: 1.3f);
        var state = MakeState(new[] { actor }, new[] { target });
        actor.SetCurrentTarget(target.Id);

        var slot = EngagementSlotService.Resolve(
            state,
            actor,
            target,
            actor.PreferredRangeBand,
            PositioningIntentKind.Frontline);

        Assert.That(slot, Is.Not.Null);
        var edgeDistance = slot!.Position.DistanceTo(target.Position) - actor.NavigationRadius - target.NavigationRadius;
        Assert.That(edgeDistance, Is.InRange(0.55f, 0.72f));
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

    [Test]
    public void FormationSpacing_PushesOverlappingAlliesApart()
    {
        var a = MakeUnit("a", TeamSide.Ally);
        var b = MakeUnit("b", TeamSide.Ally);
        a.SetPosition(new CombatVector2(0f, 0f));
        b.SetPosition(new CombatVector2(0.02f, 0f));
        a.SetActionState(CombatActionState.AcquireTarget);
        b.SetActionState(CombatActionState.AcquireTarget);

        var state = MakeState(new[] { a, b }, Array.Empty<UnitSnapshot>());
        MovementResolver.ResolveFormationSpacing(state);

        var distAfter = a.Position.DistanceTo(b.Position);
        var minSep = a.SeparationRadius + b.SeparationRadius;
        Assert.That(distAfter, Is.GreaterThanOrEqualTo(minSep - 0.01f),
            "Overlapping allies should be pushed apart to at least separation distance");
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
            false,
            null,
            null);

        MovementResolver.MoveForIntent(state, actor, evalAction);

        Assert.That(actor.Position.X, Is.EqualTo(posBefore.X).Within(0.001f), "Rooted unit should not move");
    }
}
