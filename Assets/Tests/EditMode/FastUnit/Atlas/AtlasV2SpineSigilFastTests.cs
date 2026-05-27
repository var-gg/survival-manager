using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Meta.Services;
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
        Assert.That(state.RegionTitle, Does.Not.Contain("그레이박스"));
        Assert.That(state.StageCandidates.Select(candidate => candidate.Badge), Has.Member("1A"));
        Assert.That(state.SpineStages.Select(stage => stage.Label), Is.EqualTo(new[] { "진입", "교전", "단서", "보스", "추출" }));
        Assert.That(state.SpineStages.Select(stage => stage.NodeId), Has.None.Empty);
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
    public void AtlasDecisionPanel_NodeStateModifiers_AreDeclared()
    {
        var state = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build();
        Assert.That(state.Tiles.Any(tile => tile.IsSelected), Is.True);
        Assert.That(state.Tiles.Any(tile => tile.IsCurrentStageCandidate), Is.True);
        Assert.That(state.Tiles.Any(tile => tile.IsSigilAnchor), Is.True);
        Assert.That(state.Tiles.Any(tile => !tile.CanEnter), Is.True);
        Assert.That(state.StageCandidates.Any(candidate => candidate.IsCurrentStage), Is.True);
        Assert.That(state.StageCandidates.Any(candidate => !candidate.CanEnter), Is.True);

        var viewSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/AtlasScreenView.cs");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Atlas/AtlasScreen.uxml");
        var uss = File.ReadAllText("Assets/_Game/UI/Screens/Atlas/AtlasScreen.uss");

        Assert.That(viewSource, Does.Contain("atlas-preview-action-ribbon"));
        Assert.That(viewSource, Does.Contain("RenderMapSurface"));
        Assert.That(viewSource, Does.Contain("AtlasHexOverlayBinder.GetTileCenter"));
        Assert.That(viewSource, Does.Contain("stage.NodeId"));
        Assert.That(viewSource, Does.Contain("is-stage-candidate-current"));
        Assert.That(viewSource, Does.Contain("is-stage-candidate-future"));
        Assert.That(viewSource, Does.Contain("is-pin-anchor"));
        Assert.That(viewSource, Does.Contain("is-current-selected"));
        Assert.That(uxml, Does.Contain("atlas-preview-action-ribbon"));
        Assert.That(uxml, Does.Contain("atlas-map-overlay"));
        Assert.That(uxml, Does.Contain("atlas-decision-card"));
        Assert.That(uss, Does.Contain(".atlas-preview-action-ribbon"));
        Assert.That(uss, Does.Contain(".atlas-map-node"));
        Assert.That(uss, Does.Contain(".atlas-map-route-segment"));
        Assert.That(uss, Does.Contain(".atlas-decision-card"));
        Assert.That(uss, Does.Contain(".atlas-preview-card--enemy"));
        Assert.That(uss, Does.Contain(".atlas-preview-card--reward"));
        Assert.That(uss, Does.Contain(".atlas-preview-card--recommendations"));
        Assert.That(uss, Does.Contain(".atlas-hex-hit-zone.is-stage-candidate-current"));
        Assert.That(uss, Does.Contain(".atlas-hex-hit-zone.is-stage-candidate-future"));
        Assert.That(uss, Does.Contain(".atlas-hex-hit-zone.is-pin-anchor"));
        Assert.That(uss, Does.Contain(".atlas-stage-candidate-button.is-current"));
    }

    [Test]
    public void AtlasProductionSurface_UsesSceneDrivenCenter_AndDoesNotEmbedReferenceMockup()
    {
        var productionSources = new[]
        {
            File.ReadAllText("Assets/_Game/UI/Screens/Atlas/AtlasScreen.uxml"),
            File.ReadAllText("Assets/_Game/UI/Screens/Atlas/AtlasScreen.uss"),
            File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/AtlasScreenView.cs"),
        };
        var joined = string.Join("\n---\n", productionSources);

        Assert.That(joined, Does.Not.Contain("Screenshots/mockups"));
        Assert.That(joined, Does.Not.Contain("ui_ux_bible"));
        Assert.That(joined, Does.Not.Contain("atlas_overworld_map_central"));
        Assert.That(joined, Does.Not.Contain("atlas_overworld_placeholder"));
        Assert.That(joined, Does.Not.Contain("town_frontier_village_dusk"));
        Assert.That(joined, Does.Not.Contain("atlas-map-matte"));
        Assert.That(joined, Does.Not.Contain("atlas-map-land"));
        Assert.That(joined, Does.Not.Contain("atlas-map-river"));
        Assert.That(joined, Does.Not.Contain("atlas-map-mist"));
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

    [Test]
    public void PreviewPanel_ThreatBandLabel_MatchesComputeThreatBand()
    {
        // task-atlas-modifier-application-v1 acceptance #5: Atlas preview ViewState가 같은
        // ComputeThreatBand 분기를 한국어 라벨로 노출해 RewardScreen settlement summary와 같은 값을 보인다.
        var presenter = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());
        var state = presenter.Build();

        Assert.That(state.Preview.ThreatBandLabel, Is.Not.Null);
        // NUnit 3에는 Is.AnyOf가 없어 Is.EqualTo .Or chain으로 표현.
        Assert.That(
            state.Preview.ThreatBandLabel,
            Is.EqualTo("안정").Or.EqualTo("고조").Or.EqualTo("과중").Or.EqualTo("극단"),
            "ThreatBandLabel은 ComputeThreatBand 4개 band의 한국어 라벨이어야 한다.");

        var allBandLabels = Enum.GetValues(typeof(AtlasThreatBand))
            .Cast<AtlasThreatBand>()
            .Select(AtlasReadabilityFormatter.FormatThreatBand)
            .ToArray();
        Assert.That(allBandLabels, Is.EqualTo(new[] { "안정", "고조", "과중", "극단" }));
        Assert.That(state.Preview.EnemyIntel, Is.Not.Null);
        Assert.That(state.Preview.EnemyIntel!.Header, Is.EqualTo("부분 정보 없음"));
    }

    [Test]
    public void EnemyIntelContract_AtlasAbsorbsEncounterPreview()
    {
        var viewStateSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/AtlasScreenViewState.cs");
        var controllerSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/AtlasScreenController.cs");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Atlas/AtlasScreen.uxml");

        Assert.That(viewStateSource, Does.Contain("AtlasEnemyIntelViewState"));
        Assert.That(viewStateSource, Does.Contain("AtlasPreviewPanelViewState"));
        Assert.That(viewStateSource, Does.Contain("EnemyIntel"));
        Assert.That(controllerSource, Does.Contain("TryGetCombatSnapshot"));
        Assert.That(controllerSource, Does.Contain("ExpeditionEncounterPreviewBuilder.TryBuild"));
        Assert.That(controllerSource, Does.Contain("SessionState.ExpeditionNodes"));
        Assert.That(controllerSource, Does.Contain("SiteNodeIndex"));
        Assert.That(controllerSource, Does.Contain("FormatIntelList(preview.EnemyNames, 3"));
        Assert.That(controllerSource, Does.Contain("FormatIntelList(preview.RewardDropTags, 3"));
        Assert.That(controllerSource, Does.Contain("FormatIntelToken"));
        Assert.That(uxml, Does.Contain("atlas-preview-enemy-intel"));

        var root = CreateAtlasRoot();
        var view = new AtlasScreenView(root);
        var state = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build();
        view.Render(state with
        {
            Preview = state.Preview with
            {
                EnemyIntel = new AtlasEnemyIntelViewState(
                    true,
                    "부분 정보",
                    "예상 정보: sample - 정찰 기준",
                    "적 구성 징후: sample enemy",
                    "위협 예상: 2 skull",
                    "세력 징후: faction_sample",
                    "보상 징후: reward_sample",
                    "boss overlay 징후: sample boss"),
            },
        });

        var intelLabel = root.Q<Label>("atlas-preview-enemy-intel");
        Assert.That(intelLabel, Is.Not.Null);
        Assert.That(intelLabel!.text, Does.Contain("부분 정보"));
        Assert.That(intelLabel.text, Does.Contain("예상 정보"));
        Assert.That(intelLabel.text, Does.Contain("징후"));
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
        boardPane.Add(new VisualElement { name = "atlas-map-overlay" });
        boardPane.Add(new VisualElement { name = "atlas-layer-overlay" });
        boardPane.Add(new VisualElement { name = "atlas-stage-candidate-overlay" });
        content.Add(new VisualElement { name = "atlas-sigil-pool" });
        content.Add(boardPane);
        content.Add(new VisualElement { name = "atlas-stage-candidate-list" });
        root.Add(content);
        root.Add(new VisualElement { name = "atlas-spine-progress-strip" });
        root.Add(new VisualElement { name = "atlas-sitetrack-strip" });
        root.Add(new Label { name = "atlas-region-title" });
        root.Add(new Label { name = "atlas-placement-summary" });
        root.Add(new Label { name = "atlas-preview-title" });
        root.Add(new Label { name = "atlas-preview-judgement" });
        root.Add(new Label { name = "atlas-preview-action-ribbon" });
        root.Add(new Label { name = "atlas-preview-threat-band" });
        root.Add(new Label { name = "atlas-preview-enemy" });
        root.Add(new Label { name = "atlas-preview-enemy-intel" });
        root.Add(new Label { name = "atlas-preview-modifiers" });
        root.Add(new Label { name = "atlas-preview-reward" });
        root.Add(new Label { name = "atlas-preview-recommendations" });
        // wave-25 closure: scout slot label. AtlasScreenView ctor가 Require<Label>로 강제.
        root.Add(new Label { name = "atlas-preview-scout" });
        root.Add(new Label { name = "atlas-boundary-note" });
        root.Add(new Button { name = "atlas-continue-button" });
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
