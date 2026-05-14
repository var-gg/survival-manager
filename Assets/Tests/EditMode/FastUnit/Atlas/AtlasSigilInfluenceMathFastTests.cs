using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AtlasSigilInfluenceMathFastTests
{
    [Test]
    public void DenseAndWideProfiles_DifferentiateFocusedAndBroadReward()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var anchor = region.SigilAnchorSlots.Single(slot => slot.AnchorId == "anchor_middle_pressure");
        var dense = new AtlasPlacedSigil("sigil_elite_relic", ResolveAnchorHex(region, anchor), anchor.AnchorId);
        var wide = new AtlasPlacedSigil("sigil_beast_spoils", ResolveAnchorHex(region, anchor), anchor.AnchorId);

        var denseEvaluation = AtlasSigilInfluenceMathService.Evaluate(region, dense, AtlasTraversalMode.StoryRevisit, currentStageIndex: 2)!;
        var wideEvaluation = AtlasSigilInfluenceMathService.Evaluate(region, wide, AtlasTraversalMode.StoryRevisit, currentStageIndex: 2)!;

        Assert.That(denseEvaluation.ProfileId, Is.EqualTo("RewardBias.Cluster.Dense"));
        Assert.That(wideEvaluation.ProfileId, Is.EqualTo("RewardBias.Cluster.Wide"));
        Assert.That(denseEvaluation.Cells.Count, Is.EqualTo(3));
        Assert.That(wideEvaluation.Cells.Count, Is.EqualTo(6));
        Assert.That(denseEvaluation.EffectivePotency, Is.GreaterThan(wideEvaluation.EffectivePotency));
        Assert.That(wideEvaluation.InfluenceMass, Is.GreaterThan(denseEvaluation.InfluenceMass));
    }

    [Test]
    public void LowImportanceAndOffBoardCells_ConsumeMassWithoutCreatingEffect()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var anchor = region.SigilAnchorSlots.Single(slot => slot.AnchorId == "anchor_middle_pressure");
        var placement = new AtlasPlacedSigil("sigil_ruin_scholar", ResolveAnchorHex(region, anchor), anchor.AnchorId);

        var evaluation = AtlasSigilInfluenceMathService.Evaluate(region, placement, AtlasTraversalMode.StoryRevisit, currentStageIndex: 3)!;
        var offBoardCells = evaluation.Cells.Where(cell => cell.Role == AtlasCellRole.OffBoard).ToArray();

        Assert.That(offBoardCells.Length, Is.GreaterThan(0));
        Assert.That(offBoardCells.All(cell => !cell.HasEffect), Is.True);
        Assert.That(offBoardCells.All(cell => cell.MassContribution > 0.0), Is.True);
        Assert.That(evaluation.MassPotencyMultiplier, Is.LessThanOrEqualTo(1.25));
    }

    [Test]
    public void RewardAboveSafeCap_RequiresThreatOrNativeDanger()
    {
        var region = CreateSingleNodeMathRegion(AtlasNodeKind.Cache);
        var rewardOnly = AtlasSigilInfluenceMathService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("reward_dense", new AtlasHexCoordinate(0, 0), "anchor"),
        }, AtlasTraversalMode.StoryRevisit, currentStageIndex: 1);
        var rewardWithThreat = AtlasSigilInfluenceMathService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("reward_dense", new AtlasHexCoordinate(0, 0), "anchor"),
            new AtlasPlacedSigil("threat_hard", new AtlasHexCoordinate(0, 0), "anchor"),
        }, AtlasTraversalMode.StoryRevisit, currentStageIndex: 1);

        var safeReward = FindEffect(rewardOnly, AtlasModifierCategory.RewardBias);
        var greedyReward = FindEffect(rewardWithThreat, AtlasModifierCategory.RewardBias);
        var threat = FindEffect(rewardWithThreat, AtlasModifierCategory.ThreatPressure);

        Assert.That(safeReward.Percent, Is.LessThanOrEqualTo(15.001));
        Assert.That(threat.Percent, Is.GreaterThan(0.0));
        Assert.That(greedyReward.Percent, Is.GreaterThan(safeReward.Percent));
        Assert.That(greedyReward.Percent, Is.LessThanOrEqualTo(40.001));
    }

    [Test]
    public void SameCategoryStacking_UsesDiminishingBeforeCap()
    {
        var region = CreateTripleRewardMathRegion();
        var resolution = AtlasSigilInfluenceMathService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("reward_a", new AtlasHexCoordinate(0, 0), "anchor"),
            new AtlasPlacedSigil("reward_b", new AtlasHexCoordinate(0, 0), "anchor"),
            new AtlasPlacedSigil("reward_c", new AtlasHexCoordinate(0, 0), "anchor"),
        }, AtlasTraversalMode.StoryRevisit, currentStageIndex: 1);
        var reward = FindEffect(resolution, AtlasModifierCategory.RewardBias);

        Assert.That(reward.SourceCount, Is.EqualTo(3));
        Assert.That(reward.SameCategoryDiminished, Is.True);
        Assert.That(reward.HardCapped, Is.True);
        Assert.That(reward.Percent, Is.LessThanOrEqualTo(40.001));
    }

    [Test]
    public void DefaultGrayboxSimulation_ProfilesStayInsideTelemetryGuardrails()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var report = AtlasSigilInfluenceMathService.BuildSimulationReport(region, AtlasTraversalMode.StoryRevisit, currentStageIndex: 2);

        foreach (var metric in report.ProfileMetrics)
        {
            TestContext.WriteLine(
                $"{metric.ProfileId}: samples={metric.SampleCount}, clamp={metric.ClampHitRate:0.00}, mass={metric.AverageNormalizedMass:0.00}, potency={metric.AverageEffectivePotency:0.0}, useful={metric.AverageUsefulCoverage:0.0}, dead={metric.AverageDeadCoverage:0.0}");
            Assert.That(metric.SampleCount, Is.GreaterThan(0), metric.ProfileId);
            Assert.That(metric.ClampHitRate, Is.LessThan(1.0), metric.ProfileId);
            Assert.That(metric.AverageUsefulCoverage, Is.GreaterThan(0.0), metric.ProfileId);
        }

        var rewardCaps = report.NodeEffects
            .Where(effect => effect.Category == AtlasModifierCategory.RewardBias)
            .Count(effect => effect.HardCapped);
        Assert.That(rewardCaps, Is.GreaterThan(0));
    }

    [Test]
    public void SyntheticBoardSweep_NineteenAndThirtySevenHexes_DoNotCollapseToOneProfile()
    {
        foreach (var radius in new[] { 2, 3 })
        {
            var region = CreateDiscMathRegion(radius);
            var report = AtlasSigilInfluenceMathService.BuildSimulationReport(region, AtlasTraversalMode.EndlessRegion, currentStageIndex: 2);
            var profiles = report.ProfileMetrics.ToArray();
            var maxPotency = profiles.Max(profile => profile.AverageEffectivePotency);
            var minPotency = profiles.Min(profile => profile.AverageEffectivePotency);

            TestContext.WriteLine($"radius={radius}, profiles={profiles.Length}, potency-ratio={maxPotency / minPotency:0.00}");
            foreach (var metric in profiles)
            {
                TestContext.WriteLine(
                    $"  {metric.ProfileId}: clamp={metric.ClampHitRate:0.00}, mass={metric.AverageNormalizedMass:0.00}, potency={metric.AverageEffectivePotency:0.0}, useful={metric.AverageUsefulCoverage:0.0}, dead={metric.AverageDeadCoverage:0.0}");
                Assert.That(metric.ClampHitRate, Is.LessThan(1.0), $"{metric.ProfileId} radius {radius}");
                Assert.That(metric.AverageUsefulCoverage, Is.GreaterThan(0.0), $"{metric.ProfileId} radius {radius}");
            }

            Assert.That(profiles.Length, Is.EqualTo(6));
            Assert.That(maxPotency / minPotency, Is.LessThan(3.0));
        }
    }

    private static AtlasNodeSigilMathEffect FindEffect(AtlasSigilMathResolution resolution, AtlasModifierCategory category)
    {
        return resolution.NodeEffects.Single(effect => effect.Category == category && effect.NodeId == "target");
    }

    private static AtlasHexCoordinate ResolveAnchorHex(AtlasRegionDefinition region, SigilAnchorSlot anchor)
    {
        return region.Nodes.Single(node => node.NodeId == anchor.HexId).Hex;
    }

    private static AtlasRegionDefinition CreateSingleNodeMathRegion(AtlasNodeKind targetKind)
    {
        return CreateMathRegion(
            new AtlasRegionNode("target", new AtlasHexCoordinate(1, 0), targetKind, "Target", "enemy", "reward", "answer", 0),
            new[]
            {
                new AtlasSigilDefinition(
                    "reward_dense",
                    "Reward Dense",
                    1,
                    "reward",
                    new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "reward", 100) },
                    AtlasModifierCategory.RewardBias,
                    "RewardBias.Cluster.Dense",
                    2),
                new AtlasSigilDefinition(
                    "threat_hard",
                    "Threat Hard",
                    1,
                    "threat",
                    new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "threat", 100) },
                    AtlasModifierCategory.ThreatPressure,
                    "ThreatPressure.Lane.Hard",
                    2),
            });
    }

    private static AtlasRegionDefinition CreateTripleRewardMathRegion()
    {
        return CreateMathRegion(
            new AtlasRegionNode("target", new AtlasHexCoordinate(1, 0), AtlasNodeKind.Skirmish, "Target", "enemy", "reward", "answer", 0),
            new[]
            {
                new AtlasSigilDefinition("reward_a", "Reward A", 1, "reward", new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "reward A", 100) }, AtlasModifierCategory.RewardBias, "RewardBias.Cluster.Dense", 2),
                new AtlasSigilDefinition("reward_b", "Reward B", 1, "reward", new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "reward B", 100) }, AtlasModifierCategory.RewardBias, "RewardBias.Cluster.Dense", 2),
                new AtlasSigilDefinition("reward_c", "Reward C", 1, "reward", new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "reward C", 100) }, AtlasModifierCategory.RewardBias, "RewardBias.Cluster.Dense", 2),
            });
    }

    private static AtlasRegionDefinition CreateMathRegion(AtlasRegionNode target, AtlasSigilDefinition[] sigils)
    {
        var nodes = new[]
        {
            new AtlasRegionNode("anchor", new AtlasHexCoordinate(0, 0), AtlasNodeKind.SigilAnchor, "Anchor", "anchor", "sigil", "route_read"),
            target,
            new AtlasRegionNode("support", new AtlasHexCoordinate(0, -1), AtlasNodeKind.Cache, "Support", "cache", "cache", "route_read"),
        };

        return new AtlasRegionDefinition(
            "math_region",
            "Math Region",
            nodes,
            new[] { new AtlasHexCoordinate(0, 0) },
            sigils,
            Array.Empty<AtlasCharacterPreview>(),
            new[] { new AtlasStageCandidate(1, "1A", target.NodeId, Array.Empty<string>()) },
            new[]
            {
                new SigilAnchorSlot(
                    "anchor",
                    "anchor",
                    "Outer",
                    "stage_1_2",
                    "Approach",
                    AtlasHexDirection.East,
                    new[] { target.NodeId },
                    new[] { "CampaignFirstClear", "Revisit" }),
            });
    }

    private static AtlasRegionDefinition CreateDiscMathRegion(int radius)
    {
        var nodes = new List<AtlasRegionNode>();
        for (var q = -radius; q <= radius; q++)
        {
            for (var r = -radius; r <= radius; r++)
            {
                var coordinate = new AtlasHexCoordinate(q, r);
                if (Math.Max(Math.Max(Math.Abs(coordinate.Q), Math.Abs(coordinate.R)), Math.Abs(coordinate.S)) > radius)
                {
                    continue;
                }

                nodes.Add(new AtlasRegionNode(
                    $"hex_{q}_{r}".Replace("-", "m"),
                    coordinate,
                    ResolveDiscNodeKind(radius, coordinate),
                    $"Hex {q},{r}",
                    "enemy",
                    "reward",
                    "answer",
                    SiteNodeIndex: -1));
            }
        }

        var anchorCoordinates = radius == 2
            ? new[] { new AtlasHexCoordinate(-1, 1), new AtlasHexCoordinate(0, 1), new AtlasHexCoordinate(1, -2) }
            : new[] { new AtlasHexCoordinate(-2, 1), new AtlasHexCoordinate(0, 1), new AtlasHexCoordinate(1, -2), new AtlasHexCoordinate(2, -2) };
        var anchorIds = anchorCoordinates
            .Select((coordinate, index) => (Coordinate: coordinate, Id: $"anchor_{index}"))
            .ToArray();

        nodes = nodes
            .Select(node =>
            {
                var anchor = anchorIds.FirstOrDefault(item => item.Coordinate.Equals(node.Hex));
                return string.IsNullOrEmpty(anchor.Id)
                    ? node
                    : node with { Kind = AtlasNodeKind.SigilAnchor, Label = $"Anchor {anchor.Id}" };
            })
            .ToList();

        var stageCandidates = new[]
        {
            CreateStageCandidate(nodes, 1, "1A", new AtlasHexCoordinate(-radius, 0)),
            CreateStageCandidate(nodes, 2, "2A", new AtlasHexCoordinate(-1, 0)),
            CreateStageCandidate(nodes, 2, "2B", new AtlasHexCoordinate(0, -1)),
            CreateStageCandidate(nodes, 3, "3A", new AtlasHexCoordinate(1, -1)),
            CreateStageCandidate(nodes, 4, "보스", new AtlasHexCoordinate(radius - 1, 0)),
            CreateStageCandidate(nodes, 5, "추출", new AtlasHexCoordinate(radius, -1)),
        };

        var anchorSlots = anchorIds
            .Select((anchor, index) => new SigilAnchorSlot(
                anchor.Id,
                FindNodeId(nodes, anchor.Coordinate),
                index == 0 ? "Outer" : index == anchorIds.Length - 1 ? "Inner" : "Middle",
                index == 0 ? "stage_1_2" : "stage_2_3",
                index == 0 ? "Approach" : "Pressure",
                index % 2 == 0 ? AtlasHexDirection.East : AtlasHexDirection.NorthEast,
                stageCandidates.Select(candidate => candidate.HexId).ToArray(),
                new[] { "CampaignFirstClear", "Revisit", "EndlessRegion" }))
            .ToArray();

        return new AtlasRegionDefinition(
            $"disc_radius_{radius}",
            $"Disc Radius {radius}",
            nodes,
            anchorCoordinates,
            CreateSixMathSigils(),
            Array.Empty<AtlasCharacterPreview>(),
            stageCandidates,
            anchorSlots);
    }

    private static AtlasNodeKind ResolveDiscNodeKind(int radius, AtlasHexCoordinate coordinate)
    {
        if (coordinate.Equals(new AtlasHexCoordinate(radius - 1, 0)))
        {
            return AtlasNodeKind.Boss;
        }

        if (coordinate.Equals(new AtlasHexCoordinate(radius, -1)))
        {
            return AtlasNodeKind.Extract;
        }

        if ((coordinate.Q + coordinate.R) % 5 == 0)
        {
            return AtlasNodeKind.Elite;
        }

        if ((coordinate.Q - coordinate.R) % 4 == 0)
        {
            return AtlasNodeKind.Cache;
        }

        return AtlasNodeKind.Skirmish;
    }

    private static AtlasStageCandidate CreateStageCandidate(
        IReadOnlyList<AtlasRegionNode> nodes,
        int stageIndex,
        string badge,
        AtlasHexCoordinate coordinate)
    {
        return new AtlasStageCandidate(stageIndex, badge, FindNodeId(nodes, coordinate), Array.Empty<string>());
    }

    private static string FindNodeId(IReadOnlyList<AtlasRegionNode> nodes, AtlasHexCoordinate coordinate)
    {
        return nodes.Single(node => node.Hex.Equals(coordinate)).NodeId;
    }

    private static AtlasSigilDefinition[] CreateSixMathSigils()
    {
        return new[]
        {
            new AtlasSigilDefinition("reward_dense", "Reward Dense", 1, "reward", new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "reward dense", 100) }, AtlasModifierCategory.RewardBias, "RewardBias.Cluster.Dense", 2),
            new AtlasSigilDefinition("reward_wide", "Reward Wide", 1, "reward", new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "reward wide", 100) }, AtlasModifierCategory.RewardBias, "RewardBias.Cluster.Wide", 1),
            new AtlasSigilDefinition("threat_hard", "Threat Hard", 1, "threat", new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "threat hard", 100) }, AtlasModifierCategory.ThreatPressure, "ThreatPressure.Lane.Hard", 2),
            new AtlasSigilDefinition("threat_long", "Threat Long", 1, "threat", new[] { new AtlasSigilModifier(AtlasModifierCategory.ThreatPressure, "threat long", 100) }, AtlasModifierCategory.ThreatPressure, "ThreatPressure.Lane.Long", 1),
            new AtlasSigilDefinition("affinity_deep", "Affinity Deep", 1, "affinity", new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "affinity deep", 100) }, AtlasModifierCategory.AffinityBoost, "AffinityBoost.ScoutArc.Deep", 1),
            new AtlasSigilDefinition("affinity_wide", "Affinity Wide", 1, "affinity", new[] { new AtlasSigilModifier(AtlasModifierCategory.AffinityBoost, "affinity wide", 100) }, AtlasModifierCategory.AffinityBoost, "AffinityBoost.ScoutArc.Wide", 1),
        };
    }
}
