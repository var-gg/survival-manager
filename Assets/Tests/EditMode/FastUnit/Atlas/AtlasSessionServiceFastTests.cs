using System;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AtlasSessionServiceFastTests
{
    [Test]
    public void CreateInitial_NormalizesDefaultPlacementsAndModeCap()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var middle = AnchorPlacement(region, "sigil_beast_spoils", "anchor_middle_pressure");
        var inner = AnchorPlacement(region, "sigil_flank_pressure", "anchor_inner_evidence");
        var duplicatedAnchor = AnchorPlacement(region, "sigil_elite_relic", "anchor_inner_evidence");
        var invalid = new AtlasPlacedSigil("missing", new AtlasHexCoordinate(99, 99), "missing_anchor");

        var story = AtlasSessionService.CreateInitial(
            region,
            AtlasSessionIdentity.GrayboxDefault,
            AtlasTraversalMode.StoryRevisit,
            new[] { middle, inner, duplicatedAnchor, invalid });
        var endless = AtlasSessionService.CreateInitial(
            region,
            AtlasSessionIdentity.GrayboxDefault,
            AtlasTraversalMode.EndlessRegion,
            new[] { middle, inner, AnchorPlacement(region, "sigil_ruin_scholar", "anchor_outer_approach") });

        Assert.That(AtlasSessionService.ResolveActiveSigilCap(story.TraversalMode), Is.EqualTo(2));
        Assert.That(story.Placements.Count, Is.EqualTo(2));
        Assert.That(story.Placements.Select(placement => placement.AnchorId), Is.Unique);
        Assert.That(story.Placements.Select(placement => placement.SigilId), Does.Not.Contain("missing"));
        Assert.That(AtlasSessionService.ResolveActiveSigilCap(endless.TraversalMode), Is.EqualTo(3));
        Assert.That(endless.Placements.Count, Is.EqualTo(3));
    }

    [Test]
    public void SelectNode_AdvancesSpinePathAndBossGateDeterministically()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var state = AtlasSessionService.CreateInitial(region, AtlasSessionIdentity.GrayboxDefault, AtlasTraversalMode.StoryFirstClear);

        state = AtlasSessionService.SelectNode(region, state, "hex_m2_1");
        state = AtlasSessionService.SelectNode(region, state, "hex_m1_2");
        var blockedBoss = AtlasSessionService.SelectNode(region, state, "hex_1_0");
        state = AtlasSessionService.SelectNode(region, state, "hex_0_0");
        state = AtlasSessionService.SelectNode(region, state, "hex_1_0");

        Assert.That(blockedBoss.SelectedNodeId, Is.EqualTo("hex_1_0"));
        Assert.That(blockedBoss.SiteSpineIndex, Is.EqualTo(2));
        Assert.That(state.StageCandidatePath, Is.EqualTo(new[] { "hex_m2_1", "hex_m1_2", "hex_0_0" }));
        Assert.That(state.SiteSpineIndex, Is.EqualTo(4));
        Assert.That(state.BossResolved, Is.True);
        Assert.That(AtlasSessionService.ResolveCurrentStageIndex(state), Is.EqualTo(AtlasSpineProgressionService.ExtractStageIndex));
    }

    [Test]
    public void PlaceSelectedSigil_UsesModeActiveCapAndReplacesAnchor()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var story = AtlasSessionService.CreateInitial(region, AtlasSessionIdentity.GrayboxDefault, AtlasTraversalMode.StoryRevisit);
        var endless = AtlasSessionService.CreateInitial(region, AtlasSessionIdentity.GrayboxDefault, AtlasTraversalMode.EndlessRegion);

        story = AtlasSessionService.SelectSigil(region, story, "sigil_beast_spoils");
        story = AtlasSessionService.PlaceSelectedSigil(region, story, "hex_m1_m1");
        story = AtlasSessionService.SelectSigil(region, story, "sigil_flank_pressure");
        story = AtlasSessionService.PlaceSelectedSigil(region, story, "hex_0_1");
        story = AtlasSessionService.SelectSigil(region, story, "sigil_ruin_scholar");
        story = AtlasSessionService.PlaceSelectedSigil(region, story, "hex_1_m2");

        endless = AtlasSessionService.SelectSigil(region, endless, "sigil_beast_spoils");
        endless = AtlasSessionService.PlaceSelectedSigil(region, endless, "hex_m1_m1");
        endless = AtlasSessionService.SelectSigil(region, endless, "sigil_flank_pressure");
        endless = AtlasSessionService.PlaceSelectedSigil(region, endless, "hex_0_1");
        endless = AtlasSessionService.SelectSigil(region, endless, "sigil_ruin_scholar");
        endless = AtlasSessionService.PlaceSelectedSigil(region, endless, "hex_1_m2");

        Assert.That(story.Placements.Count, Is.EqualTo(2));
        Assert.That(story.Placements.Select(placement => placement.SigilId), Does.Not.Contain("sigil_beast_spoils"));
        Assert.That(endless.Placements.Count, Is.EqualTo(3));
        Assert.That(endless.Placements.Select(placement => placement.AnchorId), Is.Unique);
    }

    [Test]
    public void Resolve_BuildsCanonicalMathModifierAndHashSnapshot()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var state = AtlasSessionService.CreateInitial(
            region,
            AtlasSessionIdentity.GrayboxDefault,
            AtlasTraversalMode.StoryRevisit,
            new[]
            {
                AnchorPlacement(region, "sigil_beast_spoils", "anchor_middle_pressure"),
                AnchorPlacement(region, "sigil_flank_pressure", "anchor_inner_evidence"),
            });

        var first = AtlasSessionService.Resolve(region, state);
        var second = AtlasSessionService.Resolve(region, state);

        Assert.That(first.MathResolution.Evaluations.Count, Is.EqualTo(2));
        Assert.That(first.ModifierResolution.NodeStacks.Count, Is.EqualTo(region.Nodes.Count));
        Assert.That(first.SelectedNode.NodeId, Is.EqualTo(state.SelectedNodeId));
        Assert.That(first.SelectedStack.NodeId, Is.EqualTo(state.SelectedNodeId));
        Assert.That(first.StageCandidatePathHash, Is.EqualTo(second.StageCandidatePathHash));
        Assert.That(first.SigilSnapshotHash, Is.EqualTo(second.SigilSnapshotHash));
        Assert.That(first.SigilSnapshotHash, Is.Not.Empty);
    }

    private static AtlasPlacedSigil AnchorPlacement(AtlasRegionDefinition region, string sigilId, string anchorId)
    {
        var slot = region.SigilAnchorSlots.Single(anchor => anchor.AnchorId == anchorId);
        var node = region.Nodes.Single(candidate => candidate.NodeId == slot.HexId);
        return new AtlasPlacedSigil(sigilId, node.Hex, slot.AnchorId);
    }
}
