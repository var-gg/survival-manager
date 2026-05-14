using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AtlasSigilInfluenceMathSweepFastTests
{
    private enum DistanceBand
    {
        Near = 0,
        Medium = 1,
        Far = 2,
    }

    [Test]
    public void BroadSyntheticSweep_ProfileMetricsStayWithinGuardrails()
    {
        var scenarioCount = 0;
        var worstClampHit = 0.0;
        var worstPotencyRatio = 0.0;
        var worstDeadCoverage = 0.0;
        var worstLabel = string.Empty;

        foreach (var boardSize in new[] { 19, 27, 37 })
        foreach (var mode in new[] { AtlasTraversalMode.StoryFirstClear, AtlasTraversalMode.StoryRevisit, AtlasTraversalMode.EndlessRegion })
        foreach (var candidateCount in new[] { 2, 3, 4 })
        foreach (var anchorCount in new[] { 2, 3, 5 })
        foreach (var bossBand in new[] { DistanceBand.Near, DistanceBand.Medium, DistanceBand.Far })
        foreach (var extractBand in new[] { DistanceBand.Near, DistanceBand.Medium, DistanceBand.Far })
        {
            var label = $"board={boardSize}, mode={mode}, candidates={candidateCount}, anchors={anchorCount}, boss={bossBand}, extract={extractBand}";
            var region = CreateSweepRegion(boardSize, candidateCount, anchorCount, bossBand, extractBand);
            var report = AtlasSigilInfluenceMathService.BuildSimulationReport(region, mode, currentStageIndex: 2);
            var profileMetrics = report.ProfileMetrics.ToArray();
            var maxClamp = profileMetrics.Max(metric => metric.ClampHitRate);
            var potencyRatio = profileMetrics.Max(metric => metric.AverageEffectivePotency) /
                               profileMetrics.Min(metric => metric.AverageEffectivePotency);
            var maxDead = profileMetrics.Max(metric => metric.AverageDeadCoverage);

            scenarioCount++;
            if (maxClamp > worstClampHit)
            {
                worstClampHit = maxClamp;
            }

            if (potencyRatio > worstPotencyRatio)
            {
                worstPotencyRatio = potencyRatio;
                worstLabel = label;
            }

            if (maxDead > worstDeadCoverage)
            {
                worstDeadCoverage = maxDead;
            }

            Assert.That(profileMetrics.Length, Is.EqualTo(6), label);
            Assert.That(profileMetrics.All(metric => metric.AverageUsefulCoverage > 0.0), Is.True, label);
            Assert.That(maxClamp, Is.LessThanOrEqualTo(0.20), label);
            Assert.That(potencyRatio, Is.LessThan(3.25), label);
        }

        TestContext.WriteLine(
            $"scenarios={scenarioCount}, worstClamp={worstClampHit:0.00}, worstPotencyRatio={worstPotencyRatio:0.00}, worstDeadCoverage={worstDeadCoverage:0.0}, worstRatioAt={worstLabel}");
    }

    [Test]
    public void ActiveSetupSweep_CategoryCapsHoldForTwoAndThreeSigils()
    {
        foreach (var mode in new[] { AtlasTraversalMode.StoryFirstClear, AtlasTraversalMode.StoryRevisit, AtlasTraversalMode.EndlessRegion })
        {
            var region = CreateOverlapRegion();
            var anchor = region.SigilAnchorSlots.Single();
            var anchorHex = region.Nodes.Single(node => node.NodeId == anchor.HexId).Hex;
            var setups = new[]
            {
                new[] { "reward_dense" },
                new[] { "reward_dense", "threat_hard" },
                new[] { "reward_dense", "affinity_wide" },
                new[] { "threat_hard", "affinity_deep" },
                new[] { "reward_dense", "reward_wide", "threat_hard" },
                new[] { "reward_dense", "threat_hard", "affinity_wide" },
            };

            foreach (var setup in setups)
            {
                var placements = setup
                    .Select(sigilId => new AtlasPlacedSigil(sigilId, anchorHex, anchor.AnchorId))
                    .ToArray();
                var resolution = AtlasSigilInfluenceMathService.Resolve(region, placements, mode, currentStageIndex: 2);

                foreach (var effect in resolution.NodeEffects)
                {
                    Assert.That(effect.Percent, Is.LessThanOrEqualTo(ResolveCap(effect.Category, mode) + 0.001), $"{mode} {string.Join("+", setup)} {effect.Category}");
                }
            }
        }
    }

    private static AtlasRegionDefinition CreateSweepRegion(
        int boardSize,
        int candidateCount,
        int anchorCount,
        DistanceBand bossBand,
        DistanceBand extractBand)
    {
        var coordinates = CreateCoordinates(boardSize).ToArray();
        var anchorCoordinates = PickAnchorCoordinates(coordinates, anchorCount).ToArray();
        var reserved = new HashSet<AtlasHexCoordinate>(anchorCoordinates);
        var boss = PickByDistance(coordinates, reserved, bossBand);
        reserved.Add(boss);
        var extract = PickByDistance(coordinates, reserved, extractBand);
        reserved.Add(extract);
        var currentCandidates = PickCurrentCandidates(coordinates, reserved, candidateCount).ToArray();

        var nodes = coordinates
            .Select(coordinate =>
            {
                var nodeId = NodeId(coordinate);
                var kind = ResolveKind(coordinate, anchorCoordinates, boss, extract);
                return new AtlasRegionNode(
                    nodeId,
                    coordinate,
                    kind,
                    nodeId,
                    "enemy",
                    "reward",
                    "answer",
                    SiteNodeIndex: -1);
            })
            .ToArray();

        var stageCandidates = currentCandidates
            .Select((coordinate, index) => new AtlasStageCandidate(2, $"2{(char)('A' + index)}", NodeId(coordinate), Array.Empty<string>()))
            .Concat(new[]
            {
                new AtlasStageCandidate(3, "BOSS", NodeId(boss), Array.Empty<string>()),
                new AtlasStageCandidate(4, "EXT", NodeId(extract), Array.Empty<string>()),
            })
            .ToArray();

        var anchorSlots = anchorCoordinates
            .Select((coordinate, index) => new SigilAnchorSlot(
                $"anchor_{index}",
                NodeId(coordinate),
                index == 0 ? "Outer" : index == anchorCoordinates.Length - 1 ? "Inner" : "Middle",
                index == 0 ? "stage_1_2" : "stage_2_3",
                index == 0 ? "Approach" : "Pressure",
                index % 3 == 0 ? AtlasHexDirection.East : index % 3 == 1 ? AtlasHexDirection.NorthEast : AtlasHexDirection.SouthEast,
                stageCandidates.Select(candidate => candidate.HexId).ToArray(),
                new[] { "CampaignFirstClear", "Revisit", "EndlessRegion" }))
            .ToArray();

        return new AtlasRegionDefinition(
            $"sweep_{boardSize}_{candidateCount}_{anchorCount}_{bossBand}_{extractBand}",
            "Sweep Region",
            nodes,
            anchorCoordinates,
            CreateSixSigils(),
            Array.Empty<AtlasCharacterPreview>(),
            stageCandidates,
            anchorSlots);
    }

    private static AtlasRegionDefinition CreateOverlapRegion()
    {
        var nodes = new[]
        {
            new AtlasRegionNode("anchor", new AtlasHexCoordinate(0, 0), AtlasNodeKind.SigilAnchor, "Anchor", "anchor", "sigil", "route_read"),
            new AtlasRegionNode("target", new AtlasHexCoordinate(1, 0), AtlasNodeKind.Elite, "Target", "enemy", "reward", "answer"),
            new AtlasRegionNode("support_a", new AtlasHexCoordinate(0, -1), AtlasNodeKind.Cache, "Support A", "enemy", "reward", "answer"),
            new AtlasRegionNode("support_b", new AtlasHexCoordinate(2, 0), AtlasNodeKind.Boss, "Support B", "enemy", "reward", "answer"),
            new AtlasRegionNode("support_c", new AtlasHexCoordinate(1, -1), AtlasNodeKind.Extract, "Support C", "enemy", "reward", "answer"),
        };

        return new AtlasRegionDefinition(
            "overlap_math_region",
            "Overlap Math Region",
            nodes,
            new[] { new AtlasHexCoordinate(0, 0) },
            CreateSixSigils(),
            Array.Empty<AtlasCharacterPreview>(),
            new[]
            {
                new AtlasStageCandidate(2, "2A", "target", Array.Empty<string>()),
                new AtlasStageCandidate(3, "3A", "support_b", Array.Empty<string>()),
            },
            new[]
            {
                new SigilAnchorSlot(
                    "anchor",
                    "anchor",
                    "Outer",
                    "stage_1_2",
                    "Approach",
                    AtlasHexDirection.East,
                    new[] { "target" },
                    new[] { "CampaignFirstClear", "Revisit", "EndlessRegion" }),
            });
    }

    private static IEnumerable<AtlasHexCoordinate> CreateCoordinates(int boardSize)
    {
        var radius = boardSize <= 19 ? 2 : 3;
        var coordinates = new List<AtlasHexCoordinate>();
        for (var q = -radius; q <= radius; q++)
        {
            for (var r = -radius; r <= radius; r++)
            {
                var coordinate = new AtlasHexCoordinate(q, r);
                if (Math.Max(Math.Max(Math.Abs(coordinate.Q), Math.Abs(coordinate.R)), Math.Abs(coordinate.S)) <= radius)
                {
                    coordinates.Add(coordinate);
                }
            }
        }

        if (boardSize == 27)
        {
            var inner = coordinates.Where(coordinate => coordinate.DistanceTo(new AtlasHexCoordinate(0, 0)) <= 2);
            var outer = coordinates
                .Where(coordinate => coordinate.DistanceTo(new AtlasHexCoordinate(0, 0)) == 3)
                .OrderBy(coordinate => coordinate.Q)
                .ThenBy(coordinate => coordinate.R)
                .Take(8);
            return inner.Concat(outer).OrderBy(coordinate => coordinate.Q).ThenBy(coordinate => coordinate.R);
        }

        return coordinates.OrderBy(coordinate => coordinate.Q).ThenBy(coordinate => coordinate.R);
    }

    private static IEnumerable<AtlasHexCoordinate> PickAnchorCoordinates(IReadOnlyList<AtlasHexCoordinate> coordinates, int anchorCount)
    {
        var preferred = new[]
        {
            new AtlasHexCoordinate(-1, 1),
            new AtlasHexCoordinate(0, 1),
            new AtlasHexCoordinate(1, -2),
            new AtlasHexCoordinate(-1, 2),
            new AtlasHexCoordinate(2, -2),
        };

        return preferred.Where(coordinates.Contains).Take(anchorCount);
    }

    private static AtlasHexCoordinate PickByDistance(
        IReadOnlyList<AtlasHexCoordinate> coordinates,
        ISet<AtlasHexCoordinate> reserved,
        DistanceBand band)
    {
        var origin = new AtlasHexCoordinate(0, 0);
        var maxDistance = coordinates.Max(coordinate => coordinate.DistanceTo(origin));
        var targetDistance = band switch
        {
            DistanceBand.Near => 1,
            DistanceBand.Medium => Math.Max(2, maxDistance - 1),
            _ => maxDistance,
        };

        return coordinates
            .Where(coordinate => !reserved.Contains(coordinate) && coordinate.DistanceTo(origin) > 0)
            .OrderBy(coordinate => Math.Abs(coordinate.DistanceTo(origin) - targetDistance))
            .ThenBy(coordinate => coordinate.Q)
            .ThenBy(coordinate => coordinate.R)
            .First();
    }

    private static IEnumerable<AtlasHexCoordinate> PickCurrentCandidates(
        IReadOnlyList<AtlasHexCoordinate> coordinates,
        ISet<AtlasHexCoordinate> reserved,
        int candidateCount)
    {
        var origin = new AtlasHexCoordinate(0, 0);
        return coordinates
            .Where(coordinate => !reserved.Contains(coordinate) && coordinate.DistanceTo(origin) > 0)
            .OrderBy(coordinate => coordinate.DistanceTo(origin))
            .ThenBy(coordinate => coordinate.Q)
            .ThenBy(coordinate => coordinate.R)
            .Take(candidateCount);
    }

    private static AtlasNodeKind ResolveKind(
        AtlasHexCoordinate coordinate,
        IReadOnlyList<AtlasHexCoordinate> anchors,
        AtlasHexCoordinate boss,
        AtlasHexCoordinate extract)
    {
        if (anchors.Contains(coordinate))
        {
            return AtlasNodeKind.SigilAnchor;
        }

        if (coordinate.Equals(boss))
        {
            return AtlasNodeKind.Boss;
        }

        if (coordinate.Equals(extract))
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

        if ((coordinate.Q + (2 * coordinate.R)) % 6 == 0)
        {
            return AtlasNodeKind.ScoutVantage;
        }

        return AtlasNodeKind.Skirmish;
    }

    private static AtlasSigilDefinition[] CreateSixSigils()
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

    private static double ResolveCap(AtlasModifierCategory category, AtlasTraversalMode mode)
    {
        return category switch
        {
            AtlasModifierCategory.RewardBias => mode switch
            {
                AtlasTraversalMode.StoryFirstClear => 30.0,
                AtlasTraversalMode.EndlessRegion => 45.0,
                _ => 40.0,
            },
            AtlasModifierCategory.ThreatPressure => mode switch
            {
                AtlasTraversalMode.StoryFirstClear => 35.0,
                AtlasTraversalMode.EndlessRegion => 55.0,
                _ => 45.0,
            },
            _ => 35.0,
        };
    }

    private static string NodeId(AtlasHexCoordinate coordinate)
    {
        return $"hex_{coordinate.Q}_{coordinate.R}".Replace("-", "m");
    }
}
