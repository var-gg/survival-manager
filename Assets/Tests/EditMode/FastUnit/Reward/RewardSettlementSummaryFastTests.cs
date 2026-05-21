using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Reward;

namespace SM.Tests.EditMode.FastUnit.Reward;

/// <summary>
/// task-reward-settlement-commit-v1 acceptance #2: RewardScreen에 site/stage/encounter +
/// Atlas modifier 요약 패널이 ActiveRun.Overlay 파생 trace + AtlasExpeditionModifierPayload
/// 파생 modifier로 채워진다. View Render path를 통하지 않고 Presenter level의 ViewState
/// build만 검증 (UXML 로딩 없이 OK).
/// </summary>
[Category("FastUnit")]
public sealed class RewardSettlementSummaryFastTests
{
    [Test]
    public void BuildSettlementSummaryState_NoActiveRun_ReturnsEmptySentinel()
    {
        var session = GameSessionTestFactory.Create();

        var state = RewardScreenPresenter.BuildSettlementSummaryStateForTest(session);

        Assert.That(state, Is.SameAs(RewardSettlementSummaryViewState.Empty));
        Assert.That(state.SiteValueText, Is.EqualTo("-"));
        Assert.That(state.StageValueText, Is.EqualTo("-"));
        Assert.That(state.EncounterValueText, Is.EqualTo("-"));
        Assert.That(state.CommitIdValueText, Is.EqualTo("-"));
        Assert.That(state.HasAnyModifier, Is.False);
    }

    [Test]
    public void BuildSettlementSummaryState_WithOverlayTrace_RendersSiteStageEncounterAndCommitSuffix()
    {
        var session = GameSessionTestFactory.Create();
        var activeRun = CreateStubActiveRun() with
        {
            Overlay = new RunOverlayState(
                CurrentNodeIndex: 0,
                TemporaryAugmentIds: System.Array.Empty<string>(),
                PendingRewardIds: System.Array.Empty<string>(),
                CompileVersion: string.Empty,
                LastCompileHash: string.Empty,
                ChapterId: "chapter_alpha",
                SiteId: "site_wolfpine_trail",
                SiteNodeIndex: 3,
                EncounterId: "site_wolfpine_trail_skirmish_2",
                BattleContextHash: "abc123def456",
                RewardCommitId: "0123456789abcdef0123456789abcdef")
        };
        InjectActiveRun(session, activeRun);

        var state = RewardScreenPresenter.BuildSettlementSummaryStateForTest(session);

        Assert.That(state.SiteValueText, Is.EqualTo("site_wolfpine_trail"));
        Assert.That(state.StageValueText, Does.Contain("chapter_alpha").And.Contains("3"));
        Assert.That(state.EncounterValueText, Is.EqualTo("site_wolfpine_trail_skirmish_2"));
        Assert.That(state.CommitIdValueText, Is.EqualTo("0123456789ab"), "commit id는 12 char로 truncate.");
        Assert.That(state.HasAnyModifier, Is.False, "modifier payload 없으면 chip 표시 안 함.");
        Assert.That(state.RewardBiasChipText, Is.Empty);
        Assert.That(state.ThreatPressureChipText, Is.Empty);
        Assert.That(state.AffinityBoostChipText, Is.Empty);
    }

    [Test]
    public void BuildSettlementSummaryState_WithAtlasModifierPayload_RendersChipsForPositivePercents()
    {
        var session = GameSessionTestFactory.Create();
        var activeRun = CreateStubActiveRun() with
        {
            Overlay = new RunOverlayState(
                CurrentNodeIndex: 0,
                TemporaryAugmentIds: System.Array.Empty<string>(),
                PendingRewardIds: System.Array.Empty<string>(),
                CompileVersion: string.Empty,
                LastCompileHash: string.Empty,
                SiteId: "site_tithe_road",
                SiteNodeIndex: 1,
                EncounterId: "site_tithe_road_skirmish_1",
                RewardCommitId: string.Empty)
        };
        InjectActiveRun(session, activeRun);
        var payload = new AtlasExpeditionModifierPayload(
            RegionId: "region_a",
            AtlasNodeId: "atlas_node_a",
            SiteNodeIndex: 1,
            ExpeditionNodeId: "site_tithe_road:0",
            StageCandidatePathHash: "hash-stage",
            NodeOverlayHash: "hash-overlay",
            BattleContextHash: "hash-ctx",
            RewardBiasPercent: 15,
            ThreatPressurePercent: 0,
            AffinityBoostPercent: 8,
            ResolvedModifiers: System.Array.Empty<SM.Atlas.Model.AtlasResolvedModifier>());
        InjectAtlasModifierPayload(session, payload);

        var state = RewardScreenPresenter.BuildSettlementSummaryStateForTest(session);

        Assert.That(state.HasAnyModifier, Is.True);
        Assert.That(state.RewardBiasChipText, Does.Contain("15"));
        Assert.That(state.ThreatPressureChipText, Is.Empty, "0%면 chip 표시 안 함.");
        Assert.That(state.AffinityBoostChipText, Does.Contain("8"));
        Assert.That(state.ThreatBandLabelText, Is.Empty,
            "ThreatPressurePercent=0은 Normal band → ThreatBandLabelText 표시 안 함.");
    }

    [Test]
    public void BuildSettlementSummaryState_WithElevatedThreatPercent_RendersThreatBandLabel()
    {
        // task-atlas-modifier-application-v1 acceptance #5 evidence: ThreatPressurePercent를
        // AtlasModifierApplicationService.ComputeThreatBand로 매핑해 ThreatBandLabelText에 노출.
        // chip text와 band label이 같은 surface(RewardScreen settlement summary)에 일관 표시.
        var session = GameSessionTestFactory.Create();
        var activeRun = CreateStubActiveRun() with
        {
            Overlay = new RunOverlayState(
                CurrentNodeIndex: 0,
                TemporaryAugmentIds: System.Array.Empty<string>(),
                PendingRewardIds: System.Array.Empty<string>(),
                CompileVersion: string.Empty,
                LastCompileHash: string.Empty,
                SiteId: "site_threat",
                SiteNodeIndex: 0,
                EncounterId: "encounter_threat",
                RewardCommitId: string.Empty)
        };
        InjectActiveRun(session, activeRun);
        var payload = new AtlasExpeditionModifierPayload(
            RegionId: "region_threat",
            AtlasNodeId: "atlas_node_threat",
            SiteNodeIndex: 0,
            ExpeditionNodeId: "site_threat:0",
            StageCandidatePathHash: "hash-spch",
            NodeOverlayHash: "hash-noh",
            BattleContextHash: "hash-ctx",
            RewardBiasPercent: 0,
            ThreatPressurePercent: 30,
            AffinityBoostPercent: 0,
            ResolvedModifiers: System.Array.Empty<SM.Atlas.Model.AtlasResolvedModifier>());
        InjectAtlasModifierPayload(session, payload);

        var state = RewardScreenPresenter.BuildSettlementSummaryStateForTest(session);

        Assert.That(state.ThreatBandLabelText, Is.Not.Empty,
            "Elevated band(threat=30%)는 ThreatBandLabelText에 한국어 label 표시.");
        Assert.That(state.ThreatBandLabelText, Does.Contain("고조").Or.Contain("Elevated"),
            "default fallback label은 '위협 고조'.");
    }

    [Test]
    public void RewardResultProgressionRows_AndTimelineTicks_AreDeclared()
    {
        var session = GameSessionTestFactory.Create();
        var profile = new ProfileView(
            "profile-test",
            "Player",
            SessionRealm.OfflineLocal,
            false,
            Gold: 123,
            Echo: 45,
            PermanentAugmentSlotCount: 0,
            HeroCount: 2,
            InventoryCount: 7,
            Heroes: System.Array.Empty<ProfileHeroView>());

        var rows = RewardScreenPresenter.BuildProgressionRowsForTest(session, profile);
        var ticks = RewardScreenPresenter.BuildTimelineTicksForTest(session, profile);

        Assert.That(rows.Select(row => row.KeyText), Is.SupersetOf(new[] { "Battle", "Auto Loot", "Reward Choice", "Wallet", "Inventory", "Continuation" }));
        Assert.That(rows.Single(row => row.KeyText == "Wallet").ValueText, Does.Contain("123").And.Contain("45"));
        Assert.That(rows.Single(row => row.KeyText == "Inventory").ValueText, Does.Contain("7"));
        Assert.That(ticks.Select(tick => tick.StepText), Is.EqualTo(new[] { "Battle", "Auto Loot", "Reward Choice", "Return" }));
        Assert.That(ticks.Any(tick => tick.IsCurrent), Is.True);

        var viewStateSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenViewState.cs");
        var presenterSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs");
        var viewSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenView.cs");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Reward/RewardScreen.uxml");

        Assert.That(viewStateSource, Does.Contain("RewardProgressionRowViewState"));
        Assert.That(viewStateSource, Does.Contain("RewardTimelineTickViewState"));
        Assert.That(viewStateSource, Does.Contain("ProgressionTitle"));
        Assert.That(viewStateSource, Does.Contain("TimelineTitle"));
        Assert.That(viewStateSource, Does.Contain("ProgressionRows"));
        Assert.That(viewStateSource, Does.Contain("TimelineTicks"));
        Assert.That(presenterSource, Does.Contain("LastBattleSummary"));
        Assert.That(presenterSource, Does.Contain("LastAutomaticLootBundle"));
        Assert.That(presenterSource, Does.Contain("LastRewardApplicationSummary"));
        Assert.That(presenterSource, Does.Contain("BuildProgressionRows"));
        Assert.That(presenterSource, Does.Contain("BuildTimelineTicks"));
        Assert.That(viewSource, Does.Contain("RenderProgressionRows"));
        Assert.That(viewSource, Does.Contain("RenderTimelineTicks"));
        Assert.That(uxml, Does.Contain("ProgressionTitleLabel"));
        Assert.That(uxml, Does.Contain("TimelineTitleLabel"));
        Assert.That(uxml, Does.Contain("RewardProgressionRows"));
        Assert.That(uxml, Does.Contain("RewardTimelineTicks"));
    }

    private static ActiveRunState CreateStubActiveRun()
    {
        var blueprint = new SquadBlueprintState(
            BlueprintId: "blueprint.stub",
            DisplayName: "Reward Settlement Stub",
            TeamPosture: TeamPostureType.StandardAdvance,
            TeamTacticId: string.Empty,
            DeploymentAssignments: new Dictionary<DeploymentAnchorId, string>(),
            ExpeditionSquadHeroIds: System.Array.Empty<string>(),
            HeroRoleIds: new Dictionary<string, string>());
        return RunStateService.StartRun("exp-test-reward-settlement", blueprint, isQuickBattle: false);
    }

    private static void InjectActiveRun(GameSessionState session, ActiveRunState activeRun)
    {
        typeof(GameSessionState)
            .GetProperty("ActiveRun", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(session, activeRun);
    }

    private static void InjectAtlasModifierPayload(GameSessionState session, AtlasExpeditionModifierPayload payload)
    {
        typeof(GameSessionState)
            .GetField("_atlasExpeditionModifierPayload", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(session, payload);
    }
}
