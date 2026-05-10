using System;
using System.Linq;
using System.Text.RegularExpressions;
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
    public void AnchorVisibility_StageBandClassification()
    {
        var presenter = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());
        var initial = presenter.Build();

        Assert.That(FindAnchorState(initial, "hex_m1_m1"), Is.EqualTo("active"));
        Assert.That(FindAnchorState(initial, "hex_0_1"), Is.EqualTo("future"));
        Assert.That(FindAnchorState(initial, "hex_1_m2"), Is.EqualTo("future"));

        presenter.SelectNode("hex_m2_1");
        presenter.SelectNode("hex_m1_2");
        var stageThree = presenter.Build();

        Assert.That(FindAnchorState(stageThree, "hex_m1_m1"), Is.EqualTo("inactive"));
        Assert.That(FindAnchorState(stageThree, "hex_0_1"), Is.EqualTo("active"));
        Assert.That(FindAnchorState(stageThree, "hex_1_m2"), Is.EqualTo("active"));
    }

    [Test]
    public void StageCandidateSurface_NoMoreThanFour()
    {
        var presenter = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());

        Assert.That(presenter.Build().StageCandidates.Count, Is.LessThanOrEqualTo(4));

        presenter.SelectNode("hex_m2_1");
        Assert.That(presenter.Build().StageCandidates.Count, Is.LessThanOrEqualTo(4));
    }

    [Test]
    public void WeaknessContractStub_NotPresentInOnePhaseScope()
    {
        var state = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build();
        var visibleText = string.Join("\n",
            state.RegionTitle,
            state.PlacementSummary,
            state.Preview.JudgementLine,
            state.Preview.ModifierStack,
            state.Preview.RewardPreview,
            state.Preview.BoundaryNote,
            string.Join("\n", state.SigilPool.Select(item => $"{item.DisplayName}\n{item.CategorySummary}")));

        Assert.That(visibleText, Does.Not.Contain("augment"));
        Assert.That(visibleText, Does.Not.Contain("Augment"));
        Assert.That(visibleText, Does.Not.Contain("contract"));
        Assert.That(visibleText, Does.Not.Contain("Contract"));
        Assert.That(visibleText, Does.Not.Contain("계약"));
        Assert.That(visibleText, Does.Not.Contain("마침"));
    }

    [Test]
    public void SigilCardLabel_NoDirectPercentInMain()
    {
        var state = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build();
        var directNumberPattern = new Regex(@"[+]\d|%", RegexOptions.CultureInvariant);

        foreach (var item in state.SigilPool)
        {
            Assert.That(directNumberPattern.IsMatch(item.DisplayName), Is.False, item.DisplayName);
            Assert.That(directNumberPattern.IsMatch(item.CategorySummary), Is.False, item.CategorySummary);
        }
    }

    private static string FindAnchorState(AtlasScreenViewState state, string nodeId)
    {
        return state.Tiles.Single(tile => tile.NodeId == nodeId).AnchorHighlightState;
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
