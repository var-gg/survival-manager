using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class DuelistBehaviorTagFastTests
{
    [Test]
    public void DiveCommit_OpensStandardAdvance_InAtLeastEightyPercentOfThirtyTwoSeeds()
    {
        var dives = 0;
        for (var seed = 1; seed <= 32; seed++)
        {
            var (state, duelist) = BuildDiveScenario(
                seed,
                TeamPostureType.StandardAdvance,
                CombatBehaviorTags.DuelistDiveCommit);

            RoleBrain.ResolveIntent(state, duelist);
            if (duelist.CurrentCombatIntent.Type == CombatIntentType.Dive)
            {
                dives++;
            }
        }

        Assert.That(dives / 32f, Is.GreaterThanOrEqualTo(0.8f));
    }

    [Test]
    public void HoldBruiser_SealsDive_InEveryPosture_EvenWhenDiveTagsAreAlsoPresent()
    {
        foreach (TeamPostureType posture in Enum.GetValues(typeof(TeamPostureType)))
        {
            var (state, duelist) = BuildDiveScenario(
                7,
                posture,
                CombatBehaviorTags.DuelistHoldBruiser,
                CombatBehaviorTags.DuelistDiveCommit,
                CombatBehaviorTags.DiveAssassinKeystone,
                CombatBehaviorTags.ExecuteLowHp);

            RoleBrain.ResolveIntent(state, duelist);

            Assert.That(duelist.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive), posture.ToString());
        }
    }

    [Test]
    public void DiveCommit_UsesInclusiveFortyFivePercentHpBoundary()
    {
        var (atState, atBoundary) = BuildDiveScenario(
            7,
            TeamPostureType.StandardAdvance,
            CombatBehaviorTags.DuelistDiveCommit);
        atBoundary.TakeDamage(33f); // 60 -> 27 = 45%
        RoleBrain.ResolveIntent(atState, atBoundary);

        var (belowState, belowBoundary) = BuildDiveScenario(
            7,
            TeamPostureType.StandardAdvance,
            CombatBehaviorTags.DuelistDiveCommit);
        belowBoundary.TakeDamage(33.01f);
        RoleBrain.ResolveIntent(belowState, belowBoundary);

        Assert.That(atBoundary.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        Assert.That(belowBoundary.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive));
    }

    [Test]
    public void AssassinKeystone_ExtendsDiveCommitToTwentyFiveSteps()
    {
        var (state, duelist) = BuildDiveScenario(
            7,
            TeamPostureType.StandardAdvance,
            CombatBehaviorTags.DuelistDiveCommit,
            CombatBehaviorTags.DiveAssassinKeystone);

        RoleBrain.ResolveIntent(state, duelist);

        Assert.That(duelist.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        Assert.That(duelist.CurrentCombatIntent.CommitUntilStep - state.StepIndex, Is.EqualTo(25));
    }

    [Test]
    public void DuelistPeel_InterceptsWithinThreeMeters_ButNotBeyond()
    {
        var near = BuildPeelScenario(2.9f);
        RoleBrain.ResolveIntent(near.State, near.Duelist);

        var far = BuildPeelScenario(3.1f);
        RoleBrain.ResolveIntent(far.State, far.Duelist);

        Assert.That(near.Duelist.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Peel));
        Assert.That(near.Duelist.CurrentCombatIntent.TargetId, Is.EqualTo(near.Threat.Id));
        Assert.That(far.Duelist.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Peel));
    }

    [Test]
    public void UntaggedAndEmptyRulePackage_BattleHashStreamsAreByteIdentical()
    {
        var left = BuildHashStream(useExplicitEmptyRulePackages: false);
        var right = BuildHashStream(useExplicitEmptyRulePackages: true);

        Assert.That(right, Is.EqualTo(left));
    }

    private static (BattleState State, UnitSnapshot Duelist) BuildDiveScenario(
        int seed,
        TeamPostureType posture,
        params string[] behaviorTags)
    {
        var duelist = WithBehaviorTags(
            CombatTestFactory.CreateUnit(
                "ally_duelist",
                classId: "duelist",
                anchor: DeploymentAnchorId.FrontCenter,
                hp: 60f,
                moveSpeed: 2.1f,
                attackRange: 1.2f),
            behaviorTags);
        var allyVanguard = CombatTestFactory.CreateUnit(
            "ally_vanguard",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontTop,
            hp: 120f,
            moveSpeed: 1.7f,
            attackRange: 1.2f);
        var enemyVanguard = CombatTestFactory.CreateUnit(
            "enemy_vanguard",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 120f,
            moveSpeed: 1.7f,
            attackRange: 1.2f);
        var enemyRanger = CombatTestFactory.CreateUnit(
            "enemy_ranger",
            race: "undead",
            classId: "ranger",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 40f,
            moveSpeed: 1.9f,
            attackRange: 5f);
        var state = CombatTestFactory.CreateBattleState(
            new[] { duelist, allyVanguard },
            new[] { enemyVanguard, enemyRanger },
            posture,
            TeamPostureType.StandardAdvance,
            seed);
        var actor = state.Allies[0];
        actor.SetPosition(new CombatVector2(0f, 0f));
        state.Allies[1].SetPosition(new CombatVector2(-0.8f, 0.6f));
        state.Enemies[0].SetPosition(new CombatVector2(2f, 0f));
        state.Enemies[1].SetPosition(new CombatVector2(4.5f, 0f));
        foreach (var unit in state.AllUnits)
        {
            unit.SetActionState(CombatActionState.AcquireTarget);
        }

        return (state, actor);
    }

    private static (BattleState State, UnitSnapshot Duelist, UnitSnapshot Threat) BuildPeelScenario(float threatDistance)
    {
        var duelist = WithBehaviorTags(
            CombatTestFactory.CreateUnit(
                "ally_duelist",
                classId: "duelist",
                anchor: DeploymentAnchorId.FrontCenter,
                hp: 100f,
                attackRange: 1.2f),
            CombatBehaviorTags.DuelistHoldBruiser,
            CombatBehaviorTags.DuelistPeel);
        var ranger = CombatTestFactory.CreateUnit(
            "ally_ranger",
            classId: "ranger",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 40f,
            attackRange: 5f);
        var enemyFront = CombatTestFactory.CreateUnit(
            "enemy_front",
            race: "undead",
            classId: "vanguard",
            hp: 100f,
            attackRange: 1.2f);
        var threat = CombatTestFactory.CreateUnit(
            "enemy_threat",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.BackTop,
            hp: 60f,
            attackRange: 1.2f);
        var state = CombatTestFactory.CreateBattleState(
            new[] { duelist, ranger },
            new[] { enemyFront, threat },
            TeamPostureType.HoldLine,
            TeamPostureType.StandardAdvance,
            7);
        state.Allies[0].SetPosition(new CombatVector2(0f, 0f));
        state.Allies[1].SetPosition(new CombatVector2(-threatDistance - 0.4f, 0f));
        state.Enemies[0].SetPosition(new CombatVector2(0.8f, 0f));
        state.Enemies[1].SetPosition(new CombatVector2(-threatDistance, 0f));
        foreach (var unit in state.AllUnits)
        {
            unit.SetActionState(CombatActionState.AcquireTarget);
        }

        return (state, state.Allies[0], state.Enemies[1]);
    }

    private static string BuildHashStream(bool useExplicitEmptyRulePackages)
    {
        BattleUnitLoadout Unit(string id, string race, TeamSide side)
        {
            var loadout = CombatTestFactory.CreateUnit(id, race: race, classId: "vanguard", hp: 60f);
            return useExplicitEmptyRulePackages
                ? loadout with { RulePackages = Array.Empty<CombatRuleModifierPackage>() }
                : loadout;
        }

        var state = CombatTestFactory.CreateBattleState(
            new[] { Unit("ally", "human", TeamSide.Ally) },
            new[] { Unit("enemy", "undead", TeamSide.Enemy) },
            seed: 17);
        var hashes = new List<string>();
        var simulator = new BattleSimulator(state, 40);
        hashes.Add(BattleStateCanonicalHash.Compute(state));
        for (var step = 0; step < 40 && !simulator.IsFinished; step++)
        {
            simulator.Step();
            hashes.Add(BattleStateCanonicalHash.Compute(state));
        }

        return string.Join("\n", hashes);
    }

    private static BattleUnitLoadout WithBehaviorTags(BattleUnitLoadout source, params string[] behaviorTags)
    {
        return source with
        {
            RulePackages = new[]
            {
                new CombatRuleModifierPackage(
                    "test:duelist-build",
                    ModifierSource.Other,
                    behaviorTags.Select(tag => new RuleModifier(RuleModifierKind.BehaviorTag, tag)).ToArray()),
            },
        };
    }
}
