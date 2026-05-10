using System;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Unity.UI.Atlas;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AtlasV2SpineSigilFastTests
{
    [Test]
    public void SiteSpineIndex_StageCandidateGate()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var bossHex = region.StageCandidates.Single(candidate => candidate.SiteStageIndex == 4).HexId;

        Assert.That(AtlasSpineProgressionService.CanEnterStoryCandidate(region, bossHex, siteSpineIndex: 2, bossResolved: false), Is.False);
        Assert.That(AtlasSpineProgressionService.CanEnterStoryCandidate(region, bossHex, siteSpineIndex: 3, bossResolved: false), Is.True);
    }

    [Test]
    public void FootprintShape_RewardBiasCluster_CenterAdjacentEdge()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var slot = region.SigilAnchorSlots.Single(anchor => anchor.AnchorId == "anchor_middle_pressure");
        var sigil = region.SigilPool.Single(item => item.SigilId == "sigil_beast_spoils");

        var cells = SigilPropagationService.ResolveFootprint(region, slot, sigil);

        Assert.That(cells.Select(cell => cell.FalloffPercent), Has.Member(100));
        Assert.That(cells.Select(cell => cell.FalloffPercent), Has.Member(70));
        Assert.That(cells.Select(cell => cell.FalloffPercent), Has.Member(40));
        Assert.That(cells.Count, Is.InRange(3, 7));
    }

    [Test]
    public void FootprintShape_ThreatPressureLane_OrientationCore()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var slot = region.SigilAnchorSlots.Single(anchor => anchor.AnchorId == "anchor_inner_evidence");
        var sigil = region.SigilPool.Single(item => item.SigilId == "sigil_flank_pressure");

        var cells = SigilPropagationService.ResolveFootprint(region, slot, sigil);

        Assert.That(SigilPropagationService.ResolveFootprintShape(sigil), Is.EqualTo(AtlasFootprintShape.Lane));
        Assert.That(cells.Select(cell => cell.FalloffPercent).Take(3), Is.EqualTo(new[] { 100, 70, 40 }));
    }

    [Test]
    public void SigilSnapshotHash_PlacementOrderInvariant()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var a = new AtlasPlacedSigil("sigil_beast_spoils", new AtlasHexCoordinate(0, 1), "anchor_middle_pressure");
        var b = new AtlasPlacedSigil("sigil_flank_pressure", new AtlasHexCoordinate(1, -2), "anchor_inner_evidence");

        var hash1 = AtlasContextHasher.BuildSigilSnapshotHash(region, "site", "CampaignFirstClear", new[] { a, b }, "salt");
        var hash2 = AtlasContextHasher.BuildSigilSnapshotHash(region, "site", "CampaignFirstClear", new[] { b, a }, "salt");

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void NodeOverlayHash_SameCategoryStackingClamp()
    {
        var region = CreateSingleNodeRegion();
        var resolution = SigilPropagationService.Resolve(region, new[]
        {
            new AtlasPlacedSigil("sigil_alpha", new AtlasHexCoordinate(0, 0), "anchor"),
            new AtlasPlacedSigil("sigil_beta", new AtlasHexCoordinate(0, 0), "anchor"),
        });
        var stack = resolution.FindNode("node")!;
        var modifier = stack.ResolvedModifiers.Single(item => item.Category == AtlasModifierCategory.RewardBias);

        Assert.That(modifier.SameCategoryCapped, Is.True);
        Assert.That(modifier.HardCapped, Is.True);
        Assert.That(modifier.Percent, Is.EqualTo(SigilPropagationService.RewardBiasHardCapPercent));
    }

    [Test]
    public void RoutePolicyMigration_NoLegacyRouteState()
    {
        var state = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build();

        Assert.That(state.GetType().GetProperty("Routes"), Is.Null);
        Assert.That(state.StageCandidates.Select(candidate => candidate.Badge), Has.Member("1A"));
        Assert.That(state.SpineStages.Select(stage => stage.Label), Is.EqualTo(new[] { "진입", "교전", "단서", "보스", "추출" }));
    }

    [Test]
    public void RegionLayer_HasExpectedThirtySevenHexComposition()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();

        Assert.That(region.Nodes.Count, Is.EqualTo(37));
        Assert.That(region.Nodes.Count(node => node.Layer == AtlasRegionLayer.Outer), Is.EqualTo(18));
        Assert.That(region.Nodes.Count(node => node.Layer == AtlasRegionLayer.Middle), Is.EqualTo(12));
        Assert.That(region.Nodes.Count(node => node.Layer == AtlasRegionLayer.Inner), Is.EqualTo(6));
        Assert.That(region.Nodes.Count(node => node.Layer == AtlasRegionLayer.Core), Is.EqualTo(1));
    }

    [Test]
    public void AnchorSlotLayerDistribution_HasExpectedTwelveAnchors()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();

        Assert.That(region.SigilAnchorSlots.Count, Is.EqualTo(12));
        Assert.That(region.SigilAnchorSlots.Count(slot => slot.LayerId == AtlasRegionLayer.Outer.ToString()), Is.EqualTo(6));
        Assert.That(region.SigilAnchorSlots.Count(slot => slot.LayerId == AtlasRegionLayer.Middle.ToString()), Is.EqualTo(4));
        Assert.That(region.SigilAnchorSlots.Count(slot => slot.LayerId == AtlasRegionLayer.Inner.ToString()), Is.EqualTo(2));
        Assert.That(region.SigilAnchorSlots.Count(slot => slot.LayerId == AtlasRegionLayer.Core.ToString()), Is.Zero);
    }

    [Test]
    public void TraversalMode_ActiveSigilCap()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var placements = region.SigilPool
            .Take(4)
            .Zip(region.SigilAnchorSlots.Take(4), (sigil, slot) =>
            {
                var node = region.Nodes.Single(item => item.NodeId == slot.HexId);
                return new AtlasPlacedSigil(sigil.SigilId, node.Hex, slot.AnchorId);
            })
            .ToArray();

        Assert.That(AtlasTraversalContext.ForMode(TraversalMode.CampaignFirstClear).ActiveSigilCap, Is.EqualTo(2));
        Assert.That(AtlasTraversalContext.ForMode(TraversalMode.CampaignResume).ActiveSigilCap, Is.EqualTo(2));
        Assert.That(AtlasTraversalContext.ForMode(TraversalMode.Revisit).ActiveSigilCap, Is.EqualTo(2));
        Assert.That(AtlasTraversalContext.ForMode(TraversalMode.EndlessRegion).ActiveSigilCap, Is.EqualTo(3));
        Assert.Throws<InvalidOperationException>(() => SigilPropagationService.Resolve(region, placements.Take(3).ToArray(), AtlasTraversalContext.ForMode(TraversalMode.CampaignFirstClear)));
        Assert.DoesNotThrow(() => SigilPropagationService.Resolve(region, placements.Take(3).ToArray(), AtlasTraversalContext.ForMode(TraversalMode.EndlessRegion)));
        Assert.Throws<InvalidOperationException>(() => SigilPropagationService.Resolve(region, placements, AtlasTraversalContext.ForMode(TraversalMode.EndlessRegion)));
    }

    [Test]
    public void StageCandidate_LayerAssignment()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var nodes = region.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        Assert.That(region.StageCandidates.Where(candidate => candidate.SiteStageIndex == 1).Select(candidate => nodes[candidate.HexId].Layer), Is.All.EqualTo(AtlasRegionLayer.Outer));
        Assert.That(region.StageCandidates.Where(candidate => candidate.SiteStageIndex == 3).Select(candidate => nodes[candidate.HexId].Layer), Is.All.EqualTo(AtlasRegionLayer.Inner));
        Assert.That(nodes[region.StageCandidates.Single(candidate => candidate.SiteStageIndex == 4).HexId].Layer, Is.EqualTo(AtlasRegionLayer.Core));
    }

    [Test]
    public void LegacyNineteenHexRegion_PreservedForV1Smoke()
    {
        var legacy = AtlasGrayboxDataFactory.CreateLegacyNineteenHexRegion();
        var current = AtlasGrayboxDataFactory.CreateRegion();

        Assert.That(legacy.RegionId, Is.Not.EqualTo(current.RegionId));
        Assert.That(legacy.Nodes.Count, Is.EqualTo(19));
        Assert.That(legacy.SigilAnchorSlots.Count, Is.EqualTo(3));
        Assert.That(legacy.AnchorSlotVersion, Does.Contain("legacy"));
    }

    private static AtlasRegionDefinition CreateSingleNodeRegion()
    {
        return new AtlasRegionDefinition(
            "region",
            "Region",
            new[] { new AtlasRegionNode("node", new AtlasHexCoordinate(0, 0), AtlasNodeKind.Skirmish, "Node", "enemy", "reward", "answer", 0) },
            new[] { new AtlasHexCoordinate(0, 0) },
            new[]
            {
                new AtlasSigilDefinition(
                    "sigil_alpha",
                    "Alpha",
                    2,
                    "reward",
                    new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Alpha reward", 70) },
                    AtlasModifierCategory.RewardBias),
                new AtlasSigilDefinition(
                    "sigil_beta",
                    "Beta",
                    2,
                    "reward",
                    new[] { new AtlasSigilModifier(AtlasModifierCategory.RewardBias, "Beta reward", 60) },
                    AtlasModifierCategory.RewardBias),
            },
            Array.Empty<AtlasCharacterPreview>(),
            new[] { new AtlasStageCandidate(1, "1A", "node", Array.Empty<string>()) },
            new[] { new SigilAnchorSlot("anchor", "node", "Outer", "stage_1_2", "Approach", AtlasHexDirection.East, new[] { "node" }, new[] { "CampaignFirstClear" }) });
    }
}
