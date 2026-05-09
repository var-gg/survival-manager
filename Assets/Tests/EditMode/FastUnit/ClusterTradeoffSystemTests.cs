using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class ClusterTradeoffSystemTests
{
    [Test]
    public void BuffPropagation_AppliesTypeCapAndSameCategoryStrongestOnly()
    {
        var snapshot = CreateSnapshot(TeamSide.Ally, new[]
        {
            new CombatVector2(0f, 0f),
            new CombatVector2(0.35f, 0f),
            new CombatVector2(0.6f, 0.1f),
            new CombatVector2(0.8f, 0f),
            new CombatVector2(3f, 0f),
        });

        var result = BuffPropagationService.Resolve(
            snapshot,
            new BuffPropagationRequest("shield_a", TeamSide.Ally, CombatVector2.Zero, 1.0f, BuffPropagationKind.ShieldBarrier, "shield", 100f));

        Assert.That(result.EffectiveCount, Is.EqualTo(4));
        Assert.That(result.EfficacyBonus, Is.EqualTo(0.105f).Within(0.001f));
        Assert.That(result.EffectiveValue, Is.EqualTo(110.5f).Within(0.01f));
        Assert.That(result.MissedByDistanceCount, Is.EqualTo(1));

        var capped = BuffPropagationService.Resolve(
            snapshot,
            new BuffPropagationRequest("shield_c", TeamSide.Ally, CombatVector2.Zero, 1.0f, BuffPropagationKind.ShieldBarrier, "shield", 70f),
            new[]
            {
                result,
                result with { WinningSourceId = "shield_b", BaseValue = 90f, EffectiveValue = 96f },
            });

        Assert.That(capped.WinningSourceId, Is.EqualTo("shield_a"));
        Assert.That(capped.EffectiveValue, Is.EqualTo(110.5f + 48f).Within(0.01f));
        Assert.That(capped.OvercapEvents, Is.EqualTo(1));
    }

    [Test]
    public void BuffPropagation_AuraMembershipLingerKeepsRecentBorderTarget()
    {
        var snapshot = new EffectPositionSnapshot(
            3,
            10f,
            new[]
            {
                new EffectPositionUnit("ally_a", TeamSide.Ally, new CombatVector2(1.24f, 0f), 1f, FormationLine.Frontline, false, false),
            });

        var result = BuffPropagationService.Resolve(
            snapshot,
            new BuffPropagationRequest(
                "aura",
                TeamSide.Ally,
                CombatVector2.Zero,
                1.0f,
                BuffPropagationKind.DamageReductionAura,
                "aura",
                10f,
                LingerEntries: new[] { new AuraLingerEntry("ally_a", 10.25f) },
                AuraMembershipLingerSeconds: 0.35f));

        Assert.That(result.EligibleTargets.Single().UnitId, Is.EqualTo("ally_a"));
        Assert.That(result.EligibleTargets.Single().IsLingering, Is.True);
    }

    [Test]
    public void AoeClusterSelector_DeterministicallyScoresLargestCluster()
    {
        var snapshot = CreateSnapshot(TeamSide.Enemy, new[]
        {
            new CombatVector2(0f, 0f),
            new CombatVector2(0.35f, 0.05f),
            new CombatVector2(0.55f, -0.05f),
            new CombatVector2(4f, 0f),
        });

        var first = AoeClusterSelectorService.Select(snapshot, TeamSide.Enemy, BattleAreaEffectFamily.GroundAoe, 1.1f);
        var second = AoeClusterSelectorService.Select(snapshot, TeamSide.Enemy, BattleAreaEffectFamily.GroundAoe, 1.1f);

        Assert.That(first.Candidate.Score, Is.EqualTo(second.Candidate.Score).Within(0.0001f));
        Assert.That(first.Hits.Count, Is.EqualTo(3));
        Assert.That(first.Candidate.AffectedCount, Is.EqualTo(3));
    }

    [Test]
    public void AoeDistribution_AppliesFamilySpecificMultipliers()
    {
        var targets = CreateSnapshot(TeamSide.Enemy, new[]
        {
            new CombatVector2(0f, 0f),
            new CombatVector2(0.25f, 0f),
            new CombatVector2(0.5f, 0f),
            new CombatVector2(0.75f, 0f),
        }).Units;

        var ground = AoeClusterSelectorService.ResolveDistribution(targets, CombatVector2.Zero, 1.0f, BattleAreaEffectFamily.GroundAoe);
        var splash = AoeClusterSelectorService.ResolveDistribution(targets, CombatVector2.Zero, 1.0f, BattleAreaEffectFamily.PrimarySplash);
        var chain = AoeClusterSelectorService.ResolveDistribution(targets, CombatVector2.Zero, 1.0f, BattleAreaEffectFamily.Chain);
        var knockback = AoeClusterSelectorService.ResolveDistribution(targets, CombatVector2.Zero, 1.0f, BattleAreaEffectFamily.KnockbackWave);

        Assert.That(ground.All(hit => hit.DamageMultiplier is >= 0.70f and <= 1f), Is.True);
        Assert.That(splash[0].DamageMultiplier, Is.EqualTo(1f).Within(0.001f));
        Assert.That(splash[1].DamageMultiplier, Is.LessThan(1f));
        Assert.That(chain.Count, Is.EqualTo(3));
        Assert.That(chain[2].DamageMultiplier, Is.EqualTo(0.5625f).Within(0.001f));
        Assert.That(knockback.All(hit => hit.DamageMultiplier < 0.61f), Is.True);
    }

    [Test]
    public void FocusDamageMultiplier_CapsAtOnePointOneFiveForOrdinaryFocus()
    {
        var tactic = new TeamTacticProfile(
            "focus",
            "Focus",
            TeamPostureType.StandardAdvance,
            FocusModeBias: 1f);
        var allies = new[]
        {
            CreateNoAvoidUnit("a", tactic),
            CreateNoAvoidUnit("b", tactic),
            CreateNoAvoidUnit("c", tactic),
            CreateNoAvoidUnit("d", tactic),
        };
        var enemy = CreateNoAvoidUnit("target", tactic, TeamSide.Enemy) with
        {
            BaseStats = CreateNoAvoidUnit("target", tactic, TeamSide.Enemy).BaseStats.WithStat(SM.Core.Stats.StatKey.Armor, 0f),
        };
        var state = BattleFactory.Create(allies, new[] { enemy }, seed: 101);
        var target = state.Enemies[0];
        foreach (var ally in state.Allies)
        {
            ally.SetCurrentTarget(target.Id);
        }

        var result = HitResolutionService.ResolveBasicAttack(state, state.Allies[0], target);
        var baseResolved = result.Value - state.ActivityTelemetry.FocusDamageContribution;

        Assert.That(result.Value / baseResolved, Is.EqualTo(1.15f).Within(0.001f));
        Assert.That(state.ActivityTelemetry.FocusDamageContribution, Is.EqualTo(baseResolved * 0.15f).Within(0.01f));
    }

    [Test]
    public void GroupDispersalLock_DampensCompactZoneCandidate()
    {
        var tactic = new TeamTacticProfile(
            "compact",
            "Compact",
            TeamPostureType.StandardAdvance,
            Compactness: 0.9f,
            Width: 0.55f,
            Depth: 0.65f);
        var state = BattleFactory.Create(
            new[]
            {
                CreateNoAvoidUnit("actor", tactic),
                CreateNoAvoidUnit("blocker", tactic),
            },
            new[] { CreateNoAvoidUnit("enemy", tactic, TeamSide.Enemy) },
            seed: 5);
        var actor = state.Allies.Single(unit => unit.Definition.Id == "actor");
        var blocker = state.Allies.Single(unit => unit.Definition.Id == "blocker");
        blocker.SetPosition(actor.AnchorPosition + new CombatVector2(0.45f, 0f));

        var compactHome = MovementResolver.ResolveHomePosition(state, actor);
        state.ApplyGroupDispersalLock(actor, actor.Position, 1f, 3);
        var dispersedHome = MovementResolver.ResolveHomePosition(state, actor);

        Assert.That(state.IsUnderGroupDispersalLock(actor), Is.True);
        Assert.That(dispersedHome.DistanceTo(compactHome), Is.GreaterThan(0.01f));
    }

    [Test]
    public void ActivityTelemetry_RecordsClusterTradeoffMetricsAndReplayHash()
    {
        var compact = new TeamTacticProfile(
            "compact_focus",
            "Compact Focus",
            TeamPostureType.ProtectCarry,
            FocusModeBias: 0.8f,
            ProtectCarryBias: 0.6f,
            Compactness: 0.9f,
            Width: 0.55f,
            Depth: 0.65f);
        var wide = compact with
        {
            Id = "wide_spread",
            DisplayName = "Wide Spread",
            FocusModeBias = -0.4f,
            ProtectCarryBias = 0f,
            Compactness = 0f,
            Width = 1.45f,
            Depth = 1.25f,
        };

        var compactTelemetry = RunScenario(compact, 77).ActivityTelemetry!;
        var wideTelemetry = RunScenario(wide, 77).ActivityTelemetry!;

        Assert.That(compactTelemetry.ClusterCohesionIndex, Is.GreaterThan(0f));
        Assert.That(compactTelemetry.BuffCoverageHistogramByType.Count, Is.GreaterThan(0));
        Assert.That(compactTelemetry.AoeCatchCountHistogram.Count, Is.GreaterThan(0));
        Assert.That(compactTelemetry.ClusterTradeoffNetValue, Is.Not.EqualTo(wideTelemetry.ClusterTradeoffNetValue).Within(0.001f));
        Assert.That(compactTelemetry.ReplayHash, Is.Not.Empty);
        Assert.That(RunScenario(compact, 77).ActivityTelemetry!.ReplayHash, Is.EqualTo(compactTelemetry.ReplayHash));
    }

    private static EffectPositionSnapshot CreateSnapshot(TeamSide side, IReadOnlyList<CombatVector2> positions)
    {
        return new EffectPositionSnapshot(
            1,
            1f,
            positions
                .Select((position, index) => new EffectPositionUnit(
                    $"{side}_{index}",
                    side,
                    position,
                    1f,
                    index % 2 == 0 ? FormationLine.Frontline : FormationLine.Backline,
                    false,
                    false))
                .ToList());
    }

    private static BattleUnitLoadout CreateNoAvoidUnit(string id, TeamTacticProfile tactic, TeamSide side = TeamSide.Ally)
    {
        var anchor = side == TeamSide.Ally ? DeploymentAnchorId.FrontTop : DeploymentAnchorId.FrontCenter;
        return CombatTestFactory.CreateLoopAUnit(
            id,
            classId: "vanguard",
            anchor: anchor,
            physPower: 5f,
            armor: 0f,
            behavior: new BehaviorProfile(0.25f, 0.1f, 0f, 0f, 0.5f, 1f, 0f, 0f, 0f, 1f)) with
        {
            TeamTactic = tactic,
        };
    }

    private static BattleResult RunScenario(TeamTacticProfile tactic, int seed)
    {
        var allies = new[]
        {
            CreateNoAvoidUnit("ally_a", tactic),
            CreateNoAvoidUnit("ally_b", tactic),
            CreateNoAvoidUnit("ally_c", tactic),
        };
        var enemies = new[]
        {
            CreateNoAvoidUnit("enemy_a", tactic, TeamSide.Enemy),
            CreateNoAvoidUnit("enemy_b", tactic, TeamSide.Enemy),
            CreateNoAvoidUnit("enemy_c", tactic, TeamSide.Enemy),
        };
        return new BattleSimulator(BattleFactory.Create(allies, enemies, seed: seed), 80).RunToEnd();
    }
}

internal static class ClusterTradeoffTestStatExtensions
{
    public static Dictionary<SM.Core.Stats.StatKey, float> WithStat(
        this Dictionary<SM.Core.Stats.StatKey, float> stats,
        SM.Core.Stats.StatKey key,
        float value)
    {
        var copy = new Dictionary<SM.Core.Stats.StatKey, float>(stats);
        copy[key] = value;
        return copy;
    }
}
