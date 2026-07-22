using System;
using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

/// <summary>
/// Pins the Dive selector's two-stage reachability contract: entry may spend one initial commit approaching the
/// established geometry, while continuation and a no-progress re-entry use the original 5.0/5.5 limits.
/// </summary>
[Category("FastUnit")]
public sealed class DiveSelectorGeometryTests
{
    [Test]
    public void ReachableDuringInitialCommit_SelectsDistantBacklineAndRenewsAfterClosing()
    {
        var (state, diver, target) = BuildReferenceGeometry(moveSpeed: 2.1f);
        var simulator = new BattleSimulator(state, 80);

        simulator.Step();

        Assert.That(diver.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive),
            "a target reachable inside the first commit must be selectable before the diver has approached");
        Assert.That(diver.CurrentCombatIntent.TargetId, Is.EqualTo(target.Id));
        var initialCommitUntil = diver.CurrentCombatIntent.CommitUntilStep;

        for (var step = 0; step < 20 && diver.CurrentCombatIntent.CommitUntilStep <= initialCommitUntil; step++)
        {
            simulator.Step();
        }

        Assert.That(diver.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        Assert.That(diver.CurrentCombatIntent.CommitUntilStep, Is.GreaterThan(initialCommitUntil),
            "the dive may renew only after real movement has brought the target inside continuation geometry");
        Assert.That(diver.Position.DistanceTo(target.Position), Is.LessThanOrEqualTo(5.5f));
    }

    [TestCase(1.0f, false)]
    [TestCase(1.1f, true)]
    public void AssassinCommit_EntryReachPinsMeasuredSpeedBoundary(float moveSpeed, bool expectedDive)
    {
        var (state, diver, _) = BuildReferenceGeometry(moveSpeed);

        RoleBrain.ResolveIntent(state, diver);

        Assert.That(diver.CurrentCombatIntent.Type == CombatIntentType.Dive, Is.EqualTo(expectedDive),
            "the commit-25 fixture must reject at speed 1.0 and accept at 1.1");
    }

    [TestCase(2.2f, false)]
    [TestCase(2.25f, true)]
    public void ShippedCommit12_EntryReachPinsMeasuredSpeedBoundary(float moveSpeed, bool expectedDive)
    {
        var (state, diver, _) = BuildReferenceGeometry(moveSpeed, assassinKeystone: false);

        RoleBrain.ResolveIntent(state, diver);

        Assert.That(diver.CurrentCombatIntent.Type == CombatIntentType.Dive, Is.EqualTo(expectedDive),
            "the shipped commit-12 path must reject at speed 2.2 and accept at 2.25");
    }

    [Test]
    public void ProgressBlockedOutsideContinuationGeometry_DiveDoesNotRearmAcross220Steps()
    {
        var (state, diver, target) = BuildReferenceGeometry(moveSpeed: 2.1f);
        var simulator = new BattleSimulator(state, 240);
        simulator.Step();
        Assert.That(diver.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        Assert.That(diver.CurrentCombatIntent.DiveEntryTargetId, Is.EqualTo(target.Id));

        diver.ApplyStatus(new StatusApplicationSpec("test:dive-root", "root", 30f, 0f));
        var diveEntries = 1;
        var previousType = diver.CurrentCombatIntent.Type;
        WriteRootedTrace(state, diver, target, "initial");
        while (state.StepIndex < 220)
        {
            simulator.Step();
            var currentType = diver.CurrentCombatIntent.Type;
            if (currentType != previousType)
            {
                WriteRootedTrace(state, diver, target, "transition");
            }

            if (currentType == CombatIntentType.Dive && previousType != CombatIntentType.Dive)
            {
                diveEntries++;
            }

            previousType = currentType;
        }

        WriteRootedTrace(state, diver, target, "final");
        Assert.That(diveEntries, Is.EqualTo(1),
            "a no-progress target gets one widened entry window, not a fresh allowance after every commit lapse");
        Assert.That(diver.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive));
    }

    [Test]
    public void AssassinKeystone_MaxFieldDepthAllowanceIsNotPermanent()
    {
        var (state, diver, target) = BuildReferenceGeometry(moveSpeed: 2.05f);
        diver.SetPosition(new CombatVector2(-4.9f, 0f));
        state.Allies[1].SetPosition(new CombatVector2(-4.9f, 0.8f));
        var initialForwardDepth = target.Position.X - diver.Position.X;
        diver.ApplyStatus(new StatusApplicationSpec("test:keystone-root", "root", 30f, 0f));
        var simulator = new BattleSimulator(state, 240);

        simulator.Step();

        Assert.That(initialForwardDepth, Is.EqualTo(9.8f).Within(0.001f));
        Assert.That(diver.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive),
            "the keystone may spend its one physically-derived entry window at maximum field depth");
        var diveEntries = 1;
        var previousType = diver.CurrentCombatIntent.Type;
        while (state.StepIndex < 220)
        {
            simulator.Step();
            var currentType = diver.CurrentCombatIntent.Type;
            if (currentType == CombatIntentType.Dive && previousType != CombatIntentType.Dive)
            {
                diveEntries++;
            }

            previousType = currentType;
        }

        Assert.That(diveEntries, Is.EqualTo(1),
            "10.125/10.625 is an initial allowance; the permanent gate returns to 5.0/5.5 without progress");
        Assert.That(diver.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive));
    }

    [Test]
    public void PackPursuit_DistantTargetKeepsWingApproachUntilOriginalDiveGeometry()
    {
        var (state, diver, _) = BuildReferenceGeometry(
            moveSpeed: 2.05f,
            assassinKeystone: false,
            packPursuit: true);
        var initialPosition = diver.Position;
        var simulator = new BattleSimulator(state, 20);

        simulator.Step();

        Assert.That(diver.CurrentCombatIntent.Type, Is.Not.EqualTo(CombatIntentType.Dive),
            "the widened entry must not cancel the authored pack-pursuit approach");
        Assert.That(Math.Abs(diver.Position.Y), Is.GreaterThan(Math.Abs(initialPosition.Y)));
    }

    [Test]
    public void ActiveDiveTarget_BypassesOnlyTheLoopABasicAttackAcquireLeash()
    {
        var (state, diver, target) = BuildReferenceGeometry(moveSpeed: 2.1f, useLoopA: true);
        RoleBrain.ResolveIntent(state, diver);
        Assert.That(diver.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        diver.SetCurrentTarget(target.Id);
        Assert.That(MovementResolver.ComputeEdgeDistance(diver, target), Is.GreaterThan(2.2f),
            "the fixture must be outside its authored 1.2 + 1.0 stable-target leash");

        var evaluated = TacticEvaluator.Evaluate(state, diver);

        Assert.That(evaluated.ActionType, Is.EqualTo(BattleActionType.BasicAttack));
        Assert.That(evaluated.Target?.Id, Is.EqualTo(target.Id),
            "only the Loop A basic attack may retain the exact live Dive target beyond its ordinary leash");
    }

    [Test]
    public void ActiveDiveTarget_DoesNotBypassAlliedSignatureAcquireRules()
    {
        var shield = new BattleSkillSpec(
            "test:dive-shield",
            "Dive Shield",
            SkillKind.Shield,
            0f,
            4f,
            ResolvedSlotKind: ActionSlotKind.SignatureActive,
            ActivationModel: ActivationModel.Energy,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            TargetRuleData: new TargetRule
            {
                Domain = TargetDomain.AlliedUnit,
                PrimarySelector = TargetSelector.LowestHpPercentAlly,
                FallbackPolicy = TargetFallbackPolicy.Self,
                Filters = TargetFilterFlags.ExcludeUntargetable,
                MaxAcquireRange = 1.2f,
            });
        var (state, diver, target) = BuildReferenceGeometry(
            moveSpeed: 2.1f,
            useLoopA: true,
            signatureActive: shield);
        RoleBrain.ResolveIntent(state, diver);
        Assert.That(diver.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        diver.SetCurrentTarget(target.Id);

        var evaluated = TacticEvaluator.Evaluate(state, diver);

        Assert.That(evaluated.ActionType, Is.EqualTo(BattleActionType.ActiveSkill));
        Assert.That(evaluated.Skill?.Id, Is.EqualTo(shield.Id));
        Assert.That(evaluated.Target?.Side, Is.EqualTo(diver.Side),
            "Shield must fall through to its authored allied selector instead of inheriting the Dive enemy");
    }

    private static (BattleState State, UnitSnapshot Diver, UnitSnapshot Target) BuildReferenceGeometry(
        float moveSpeed,
        bool assassinKeystone = true,
        bool useLoopA = false,
        bool packPursuit = false,
        BattleSkillSpec? signatureActive = null)
    {
        var behaviorTags = new List<RuleModifier>
        {
            new(RuleModifierKind.BehaviorTag, CombatBehaviorTags.DuelistDiveCommit),
        };
        if (assassinKeystone)
        {
            behaviorTags.Add(new RuleModifier(RuleModifierKind.BehaviorTag, CombatBehaviorTags.DiveAssassinKeystone));
        }

        if (packPursuit)
        {
            behaviorTags.Add(new RuleModifier(RuleModifierKind.BehaviorTag, CombatBehaviorTags.PackPursuit));
        }

        var diverAnchor = packPursuit ? DeploymentAnchorId.FrontTop : DeploymentAnchorId.FrontCenter;
        BattleUnitLoadout diver = useLoopA
            ? CombatTestFactory.CreateLoopAUnit(
                "ally_diver",
                classId: "duelist",
                anchor: diverAnchor,
                hp: 120f,
                physPower: 8f,
                moveSpeed: moveSpeed,
                attackRange: 1.2f,
                attackCooldown: 0.7f,
                signatureActive: signatureActive,
                energy: signatureActive == null ? null : new EnergyProfile(100f, 100f),
                basicAttackTargetRule: new TargetRule
                {
                    Domain = TargetDomain.EnemyUnit,
                    PrimarySelector = TargetSelector.NearestReachableEnemy,
                    FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
                    Filters = TargetFilterFlags.InRange | TargetFilterFlags.ExcludeUntargetable,
                    MaxAcquireRange = 1.2f,
                })
            : CombatTestFactory.CreateUnit(
                "ally_diver",
                classId: "duelist",
                anchor: diverAnchor,
                hp: 120f,
                attack: 8f,
                moveSpeed: moveSpeed,
                attackRange: 1.2f,
                attackCooldown: 0.7f);
        diver = diver with
        {
            RulePackages = new[]
            {
                new CombatRuleModifierPackage(
                    "test:assassin-dive",
                    ModifierSource.Other,
                    behaviorTags),
            },
        };
        var support = CombatTestFactory.CreateUnit(
            "ally_support",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontTop,
            hp: 180f,
            attack: 3f,
            moveSpeed: 0f,
            attackRange: 1.2f);
        var enemyFront = CombatTestFactory.CreateUnit(
            "enemy_front",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 240f,
            attack: 2f,
            moveSpeed: 0f,
            attackRange: 1.2f);
        var enemyBackline = CombatTestFactory.CreateUnit(
            "enemy_backline",
            race: "undead",
            classId: "ranger",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 100f,
            attack: 2f,
            moveSpeed: 0f,
            attackRange: 5f);
        var state = CombatTestFactory.CreateBattleState(
            new[] { diver, support },
            new[] { enemyFront, enemyBackline },
            allyPosture: TeamPostureType.AllInBackline,
            enemyPosture: TeamPostureType.StandardAdvance,
            seed: 1729);
        var runtimeDiver = state.Allies[0];
        var runtimeTarget = state.Enemies[1];
        runtimeDiver.SetPosition(new CombatVector2(-2.8f, 0f));
        state.Allies[1].SetPosition(new CombatVector2(-2.8f, 0.8f));
        state.Enemies[0].SetPosition(new CombatVector2(2.8f, 0f));
        runtimeTarget.SetPosition(new CombatVector2(4.9f, 0f));
        foreach (var unit in state.AllUnits)
        {
            unit.SetActionState(CombatActionState.AcquireTarget);
        }

        return (state, runtimeDiver, runtimeTarget);
    }

    private static void WriteRootedTrace(
        BattleState state,
        UnitSnapshot diver,
        UnitSnapshot target,
        string phase)
    {
        TestContext.WriteLine(
            $"{phase} step={state.StepIndex} intent={diver.CurrentCombatIntent.Type} "
            + $"commitUntil={diver.CurrentCombatIntent.CommitUntilStep} "
            + $"dist={diver.Position.DistanceTo(target.Position):0.000} rooted={diver.IsRooted}");
    }
}
