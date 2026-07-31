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

    /// <summary>
    /// 결산 화면의 <b>남은</b> 계약 — 시안(ui_ux_bible_reward_v1) 재정렬 후.
    ///
    /// 예전 이 테스트는 "진행" 여섯 행과 "이벤트 타임라인" 네 눈금이 존재하는지를 봤다.
    /// 둘 다 사라졌다 — 시안에 없고, 전투 스텝 수·지갑 총액·인벤토리 개수는 결과가 아니라
    /// 계측이다. 그 자리는 결과 줄 한 줄과 화폐 칩이 가져갔다.
    ///
    /// 대신 <b>일부러 만든 페이오프 표면</b>이 살아 있는지를 계약으로 건다. 진형 페이오프·
    /// 영구 해금 예고·정치 정산 셋은 시안에 없다는 이유로 지우면 안 되는 것들이다.
    /// </summary>
    [Test]
    public void RewardResult_KeepsPayoffLedger_AndDropsTelemetryPanels()
    {
        var viewStateSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenViewState.cs");
        var presenterSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs");
        var viewSource = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenView.cs");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Reward/RewardScreen.uxml");

        // 시안 골격: 결과 줄 + 화폐 칩 + 카드 세 장 + 하단 CTA.
        Assert.That(uxml, Does.Contain("ResultHeadlineLabel"));
        Assert.That(uxml, Does.Contain("RewardCurrencyChips"));
        Assert.That(uxml, Does.Contain("ChoiceCard1TitleLabel"));
        Assert.That(uxml, Does.Contain("ReturnTownButton"));
        Assert.That(viewStateSource, Does.Contain("ResultHeadline"));
        Assert.That(presenterSource, Does.Contain("BuildResultHeadline"));
        Assert.That(presenterSource, Does.Contain("BuildCurrencyChips"));
        Assert.That(presenterSource, Does.Contain("LastAutomaticLootBundle"));

        // 페이오프 표면 — 시안에 없다는 이유로 지우면 안 되는 셋.
        Assert.That(uxml, Does.Contain("RewardPayoffRows"));
        Assert.That(viewSource, Does.Contain("RenderPayoffRows"));
        Assert.That(presenterSource, Does.Contain("BuildFormationPayoffRows"), "진형 페이오프 — 전투의 중심 카타르시스");
        Assert.That(presenterSource, Does.Contain("BuildPermanentUnlockRows"), "영구 해금 예고");
        Assert.That(presenterSource, Does.Contain("BuildPoliticalRows"), "ADR-0028 정치 정산");

        // 걷어낸 계측 표면 — 되돌아오면 여기서 잡는다.
        Assert.That(uxml, Does.Not.Contain("RewardTimelineTicks"), "이벤트 타임라인은 텔레메트리");
        Assert.That(uxml, Does.Not.Contain("SettlementCommitIdValueLabel"), "커밋 id 는 플레이어 정보가 아니다");
        Assert.That(uxml, Does.Not.Contain("RunDeltaLabel"), "요약 패널은 결과 줄과 중복");
        Assert.That(uxml, Does.Not.Contain("BuildContextLabel"), "빌드 맥락은 카드마다 있는 빌드 영향 줄과 중복");
        Assert.That(uxml, Does.Not.Contain("RewardVictoryBanner"), "카드 위 영문 RESULT 배너");
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
