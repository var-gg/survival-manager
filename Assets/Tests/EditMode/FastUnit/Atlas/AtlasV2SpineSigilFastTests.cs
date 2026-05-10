using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Unity.UI.Atlas;
using UnityEngine.UIElements;

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
    public void AnchorVisibilityState_StageBandClassification()
    {
        var presenter = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());
        var initial = presenter.Build();

        Assert.That(FindAnchorState(initial, "hex_m1_m1"), Is.EqualTo(AtlasAnchorVisibilityState.Active));
        Assert.That(FindAnchorState(initial, "hex_0_1"), Is.EqualTo(AtlasAnchorVisibilityState.Future));
        Assert.That(FindAnchorState(initial, "hex_1_m2"), Is.EqualTo(AtlasAnchorVisibilityState.Future));

        presenter.SelectNode("hex_m2_1");
        var stageTwo = presenter.Build();

        Assert.That(FindAnchorState(stageTwo, "hex_m1_m1"), Is.EqualTo(AtlasAnchorVisibilityState.Active));
        Assert.That(FindAnchorState(stageTwo, "hex_0_1"), Is.EqualTo(AtlasAnchorVisibilityState.Active));
        Assert.That(FindAnchorState(stageTwo, "hex_1_m2"), Is.EqualTo(AtlasAnchorVisibilityState.Future));

        presenter.SelectNode("hex_m1_2");
        var stageThree = presenter.Build();

        Assert.That(FindAnchorState(stageThree, "hex_m1_m1"), Is.EqualTo(AtlasAnchorVisibilityState.Inactive));
        Assert.That(FindAnchorState(stageThree, "hex_0_1"), Is.EqualTo(AtlasAnchorVisibilityState.Active));
        Assert.That(FindAnchorState(stageThree, "hex_1_m2"), Is.EqualTo(AtlasAnchorVisibilityState.Active));
    }

    [Test]
    public void StageBadgeVisibility_PerSpineIndex()
    {
        var presenter = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());
        var initial = presenter.Build();

        Assert.That(FindStageBadgeState(initial, "1A"), Is.EqualTo(AtlasStageBadgeVisibility.Highlighted));
        Assert.That(FindStageBadgeState(initial, "1B"), Is.EqualTo(AtlasStageBadgeVisibility.Highlighted));
        Assert.That(FindStageBadgeState(initial, "2A"), Is.EqualTo(AtlasStageBadgeVisibility.Faded));
        Assert.That(FindStageBadgeState(initial, "보스"), Is.EqualTo(AtlasStageBadgeVisibility.Faded));

        presenter.SelectNode("hex_m2_1");
        var stageTwo = presenter.Build();

        Assert.That(FindStageBadgeState(stageTwo, "1A"), Is.EqualTo(AtlasStageBadgeVisibility.Resolved));
        Assert.That(FindStageBadgeState(stageTwo, "2A"), Is.EqualTo(AtlasStageBadgeVisibility.Highlighted));
        Assert.That(FindStageBadgeState(stageTwo, "3A"), Is.EqualTo(AtlasStageBadgeVisibility.Faded));
    }

    [Test]
    public void LayerOverlay_DefaultHidden()
    {
        var root = CreateAtlasRoot();
        var view = new AtlasScreenView(root);
        view.Render(new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build());

        var layer = root.Q<VisualElement>(className: "atlas-layer-focus");

        Assert.That(layer, Is.Not.Null);
        Assert.That(layer!.ClassListContains("is-pulsing"), Is.False);
        Assert.That(layer.style.opacity.value, Is.LessThanOrEqualTo(0.051f));
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

    [Test]
    public void JudgementLabel_NoDirectPercentInMain()
    {
        var state = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build();
        var visibleText = string.Join("\n",
            state.Preview.JudgementLine,
            state.Preview.ModifierStack,
            state.Preview.RewardPreview,
            string.Join("\n", state.StageCandidates.Select(candidate => candidate.Summary)),
            string.Join("\n", state.Tiles.SelectMany(tile => tile.ModifierChips).Select(chip => chip.Label)));
        var directNumberPattern = new Regex(@"[+]\d|%", RegexOptions.CultureInvariant);

        Assert.That(directNumberPattern.IsMatch(visibleText), Is.False, visibleText);
    }

    private static AtlasAnchorVisibilityState FindAnchorState(AtlasScreenViewState state, string nodeId)
    {
        return state.Tiles.Single(tile => tile.NodeId == nodeId).AnchorVisibilityState;
    }

    private static AtlasStageBadgeVisibility FindStageBadgeState(AtlasScreenViewState state, string badge)
    {
        return state.Tiles.Single(tile => tile.StageCandidateBadge == badge).StageBadgeVisibility;
    }

    private static VisualElement CreateAtlasRoot()
    {
        var root = new VisualElement { name = "atlas-root" };
        var content = new VisualElement { name = "atlas-content" };
        var boardPane = new VisualElement { name = "atlas-board-pane" };
        boardPane.Add(new VisualElement { name = "atlas-board" });
        boardPane.Add(new VisualElement { name = "atlas-layer-overlay" });
        boardPane.Add(new VisualElement { name = "atlas-stage-candidate-overlay" });
        content.Add(new VisualElement { name = "atlas-sigil-pool" });
        content.Add(boardPane);
        content.Add(new VisualElement { name = "atlas-stage-candidate-list" });
        root.Add(content);
        root.Add(new VisualElement { name = "atlas-spine-progress-strip" });
        root.Add(new Label { name = "atlas-region-title" });
        root.Add(new Label { name = "atlas-placement-summary" });
        root.Add(new Label { name = "atlas-preview-title" });
        root.Add(new Label { name = "atlas-preview-judgement" });
        root.Add(new Label { name = "atlas-preview-enemy" });
        root.Add(new Label { name = "atlas-preview-modifiers" });
        root.Add(new Label { name = "atlas-preview-reward" });
        root.Add(new Label { name = "atlas-preview-recommendations" });
        root.Add(new Label { name = "atlas-boundary-note" });
        root.Add(new Label { name = "atlas-debug-hash" });
        return root;
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
