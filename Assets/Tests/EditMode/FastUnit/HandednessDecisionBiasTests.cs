using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class HandednessDecisionBiasTests
{
    [Test]
    public void HandednessSlotWeight_AppliesWidthCompactnessClamp()
    {
        var tactic = new TeamTacticProfile(
            "wide",
            "Wide",
            TeamPostureType.StandardAdvance,
            Compactness: 0.2f,
            Width: 1.2f);
        var state = BattleFactory.Create(
            new[] { CreateUnit("duelist", DominantHand.Right, tactic, classId: "duelist") },
            new[] { CreateUnit("target", DominantHand.Right, tactic, TeamSide.Enemy) },
            seed: 3);
        var actor = state.Allies[0];
        var context = state.GetTacticContext(actor.Side);

        var weight = HandednessDecisionService.ResolveHandednessSlotWeight(actor, context);
        var expectedClamp = (0.45f + (0.55f * context.Width)) * (1f - (0.60f * context.Compactness));

        Assert.That(weight, Is.EqualTo(0.12f * expectedClamp).Within(0.001f));
    }

    [Test]
    public void HandednessSlotWeight_IsDampenedByCompactness()
    {
        var wide = new TeamTacticProfile("wide", "Wide", TeamPostureType.StandardAdvance, Compactness: 0f, Width: 1.25f);
        var compact = wide with { Id = "compact", Compactness = 0.9f, Width = 0.45f };
        var wideState = BattleFactory.Create(
            new[] { CreateUnit("duelist", DominantHand.Right, wide, classId: "duelist") },
            new[] { CreateUnit("target", DominantHand.Right, wide, TeamSide.Enemy) },
            seed: 4);
        var compactState = BattleFactory.Create(
            new[] { CreateUnit("duelist", DominantHand.Right, compact, classId: "duelist") },
            new[] { CreateUnit("target", DominantHand.Right, compact, TeamSide.Enemy) },
            seed: 4);

        var wideWeight = HandednessDecisionService.ResolveHandednessSlotWeight(wideState.Allies[0], wideState.GetTacticContext(TeamSide.Ally));
        var compactWeight = HandednessDecisionService.ResolveHandednessSlotWeight(compactState.Allies[0], compactState.GetTacticContext(TeamSide.Ally));

        Assert.That(compactWeight, Is.LessThan(wideWeight * 0.55f));
    }

    [Test]
    public void PostAttackReposition_UsesHandednessPreferredSideWhenClear()
    {
        var tactic = new TeamTacticProfile("wide", "Wide", TeamPostureType.StandardAdvance, Compactness: 0f, Width: 1f);
        var state = CreatePostAttackState(tactic, DominantHand.Right, seed: 17);
        var actor = state.Allies[0];
        var target = state.Enemies[0];
        var lateral = new CombatVector2(0f, 1f);
        var preferred = HandednessDecisionService.ResolvePreferredSideSign(actor.Definition.DominantHand, actor.Side);

        var choice = MovementResolver.ResolvePostAttackLateralChoice(state, actor, target, lateral, 0.25f);

        Assert.That(choice.Sign, Is.EqualTo(preferred));
        Assert.That(choice.Label, Does.Contain("hand_right"));
    }

    [Test]
    public void PostAttackReposition_AmbidextrousChoosesLeastCrowdedSide()
    {
        var tactic = new TeamTacticProfile("wide", "Wide", TeamPostureType.StandardAdvance, Compactness: 0f, Width: 1f);
        var state = CreatePostAttackState(tactic, DominantHand.Ambidextrous, seed: 19);
        var actor = state.Allies[0];
        var target = state.Enemies[0];
        state.Enemies[1].SetPosition(actor.Position + new CombatVector2(0f, 0.22f));

        var choice = MovementResolver.ResolvePostAttackLateralChoice(state, actor, target, new CombatVector2(0f, 1f), 0.25f);

        Assert.That(choice.Sign, Is.EqualTo(-1));
        Assert.That(choice.Label, Is.EqualTo("hand_ambidextrous_least_crowded"));
    }

    [Test]
    public void AttackArcTags_FollowDominantHandWithoutChangingStepDistance()
    {
        var tactic = new TeamTacticProfile("standard", "Standard", TeamPostureType.StandardAdvance);
        var rightState = BattleFactory.Create(
            new[] { CreateUnit("right", DominantHand.Right, tactic, classId: "duelist") },
            new[] { CreateUnit("target", DominantHand.Right, tactic, TeamSide.Enemy) },
            seed: 8);
        var leftState = BattleFactory.Create(
            new[] { CreateUnit("left", DominantHand.Left, tactic, classId: "duelist") },
            new[] { CreateUnit("target", DominantHand.Right, tactic, TeamSide.Enemy) },
            seed: 8);

        var right = BasicAttackActionProfileResolver.Resolve(rightState.Allies[0]);
        var left = BasicAttackActionProfileResolver.Resolve(leftState.Allies[0]);

        Assert.That(right.PreImpactStepDistance, Is.EqualTo(left.PreImpactStepDistance).Within(0.001f));
        Assert.That(BasicAttackActionProfileResolver.ToNoteToken(right), Does.Contain("WeaponTrailSide:right"));
        Assert.That(BasicAttackActionProfileResolver.ToNoteToken(left), Does.Contain("WeaponTrailSide:left"));
        Assert.That(right.StepInArcSign, Is.Not.EqualTo(left.StepInArcSign));
    }

    [Test]
    public void Handedness_DoesNotChangeDamageOrDefensiveRolls()
    {
        var tactic = new TeamTacticProfile("standard", "Standard", TeamPostureType.StandardAdvance);
        var rightState = CreatePostAttackState(tactic, DominantHand.Right, seed: 31);
        var leftState = CreatePostAttackState(tactic, DominantHand.Left, seed: 31);

        var right = HitResolutionService.ResolveBasicAttack(rightState, rightState.Allies[0], rightState.Enemies[0]);
        var left = HitResolutionService.ResolveBasicAttack(leftState, leftState.Allies[0], leftState.Enemies[0]);

        Assert.That(left.Value, Is.EqualTo(right.Value).Within(0.001f));
        Assert.That(left.WasDodged, Is.EqualTo(right.WasDodged));
        Assert.That(left.WasBlocked, Is.EqualTo(right.WasBlocked));
        Assert.That(left.WasCritical, Is.EqualTo(right.WasCritical));
    }

    [Test]
    public void MixedHandednessSlotting_RecordsPreferenceTelemetryDeterministically()
    {
        var tactic = new TeamTacticProfile("wide", "Wide", TeamPostureType.StandardAdvance, Compactness: 0.1f, Width: 1.25f);
        var state = BattleFactory.Create(
            new[]
            {
                CreateUnit("right", DominantHand.Right, tactic, classId: "duelist"),
                CreateUnit("left", DominantHand.Left, tactic, classId: "duelist"),
                CreateUnit("ambi", DominantHand.Ambidextrous, tactic, classId: "duelist"),
            },
            new[] { CreateUnit("target", DominantHand.Right, tactic, TeamSide.Enemy) },
            seed: 42);
        var target = state.Enemies[0];
        foreach (var actor in state.Allies)
        {
            actor.SetCurrentTarget(target.Id);
            EngagementSlotService.Resolve(state, actor, target, actor.PreferredRangeBand, PositioningIntentKind.Frontline);
        }

        var snapshot = state.ActivityTelemetry.BuildSnapshot(state);

        Assert.That(snapshot.HandednessSlotPreferenceHitRatio, Is.GreaterThan(0f));
        Assert.That(state.ActivityTelemetry.BuildSnapshot(state).ReplayHash, Is.EqualTo(snapshot.ReplayHash));
    }

    private static BattleState CreatePostAttackState(TeamTacticProfile tactic, DominantHand hand, int seed)
    {
        var state = BattleFactory.Create(
            new[] { CreateUnit("actor", hand, tactic, classId: "duelist") },
            new[]
            {
                CreateUnit("target", DominantHand.Right, tactic, TeamSide.Enemy),
                CreateUnit("blocker", DominantHand.Right, tactic, TeamSide.Enemy),
            },
            seed: seed);
        state.Allies[0].SetPosition(new CombatVector2(-0.2f, 0f));
        state.Enemies[0].SetPosition(new CombatVector2(0.45f, 0f));
        state.Enemies[1].SetPosition(new CombatVector2(2f, 2f));
        return state;
    }

    private static BattleUnitLoadout CreateUnit(
        string id,
        DominantHand hand,
        TeamTacticProfile tactic,
        TeamSide side = TeamSide.Ally,
        string classId = "vanguard")
    {
        var anchor = side == TeamSide.Ally ? DeploymentAnchorId.FrontTop : DeploymentAnchorId.FrontCenter;
        var basic = new BattleBasicAttackSpec(
            $"{id}:basic",
            "Basic",
            DamageType.Physical,
            new TargetRule(),
            ActionProfile: BasicAttackActionProfile.Auto,
            WeaponHandedness: WeaponHandednessProfile.OneHand);
        return CombatTestFactory.CreateLoopAUnit(
            id,
            classId: classId,
            anchor: anchor,
            physPower: 5f,
            armor: 0f,
            behavior: new BehaviorProfile(0.25f, 0.1f, 0f, 0f, 0.5f, 1f, 0f, 0f, 0f, 1f),
            basicAttack: basic) with
        {
            TeamTactic = tactic,
            DominantHand = hand,
            BaseStats = new Dictionary<StatKey, float>(CombatTestFactory.CreateLoopAUnit(id).BaseStats)
            {
                [StatKey.Armor] = 0f,
                [StatKey.AttackRange] = 1.2f,
            },
        };
    }
}
