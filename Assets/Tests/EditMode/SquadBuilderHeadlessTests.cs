using System;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Editor.SeedData;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using SM.Unity.UI.Town;

namespace SM.Tests.EditMode;

/// <summary>
/// 전술 설정(SquadBuilder) 헤드리스 구동 증명 — FM 전술 셋업 pillar의 마지막 미순수화 코어 presenter가
/// 씬·VisualElement 없이 실 콘텐츠(RuntimeCombatContentLookup) + 실 세션(GameSessionState) 위에서
/// BuildState를 완주하고, 사용자 액션(posture 클릭)이 세션 truth와 ViewState에 반영됨을 확인한다.
/// 셋업은 HeadlessBattleSimulationTests와 동일: BindProfile(new SaveProfile())가 기본 분대(배치 포함)를 시드.
/// </summary>
[Category("BatchOnly")]
public sealed class SquadBuilderHeadlessTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(SquadBuilderHeadlessTests));
    }

    /// <summary>ISquadBuilderView 기록 스텁 — 마지막 ViewState와 렌더 횟수만 남긴다.</summary>
    private sealed class RecordingSquadBuilderView : ISquadBuilderView
    {
        public SquadBuilderViewState? LastState { get; private set; }
        public int RenderCount { get; private set; }
        public ISquadBuilderActions? Actions { get; private set; }

        public void Bind(ISquadBuilderActions actions) => Actions = actions;

        public void Render(SquadBuilderViewState state)
        {
            LastState = state;
            RenderCount++;
        }

        public void FocusModal()
        {
        }
    }

    private static (SquadBuilderPresenter Presenter, RecordingSquadBuilderView View, GameSessionState Session) CreateHeadless()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile()); // 기본 분대(헤더 배치 포함) 시드

        var view = new RecordingSquadBuilderView();
        var presenter = new SquadBuilderPresenter(
            session,
            lookup,
            view,
            loadoutView: () => BuildLoadoutView(session),
            saveProfile: () => { },
            className: id => id,
            raceName: id => id,
            synergyName: id => id,
            roleName: (roleId, fallbackTag) => string.IsNullOrWhiteSpace(roleId) ? fallbackTag : roleId,
            archetypeName: id => id);
        presenter.Initialize();
        return (presenter, view, session);
    }

    // 헤드리스에는 GameSessionRoot.ProfileQueries가 없으므로 OfflineLocalSessionAdapter.GetLoadoutView와
    // 동등한 read model을 세션 truth에서 직접 구성한다 (같은 필드, 같은 필터).
    private static LoadoutView BuildLoadoutView(GameSessionState session)
    {
        var deployments = session.EnumerateDeploymentAssignments()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.HeroId))
            .Select(entry => new LoadoutDeploymentView(entry.Anchor, entry.HeroId!))
            .ToArray();
        return new LoadoutView(
            session.Profile.ActiveBlueprintId,
            session.SelectedTeamPosture,
            session.ExpeditionSquadHeroIds.Count,
            session.BattleDeployHeroIds.Count,
            session.ExpeditionSquadHeroIds.ToArray(),
            deployments);
    }

    [Test]
    public void BuildState_ProducesCoherentTacticalSetup_Headless()
    {
        var (presenter, view, session) = CreateHeadless();
        presenter.Open();

        var state = view.LastState;
        Assert.That(state, Is.Not.Null, "Open 후 ViewState가 렌더됨.");
        Assert.That(state!.IsOpen, Is.True);

        // formation board 6슬롯 + 기본 분대 배치.
        Assert.That(state.AnchorSlots.Count, Is.EqualTo(6), "anchor 슬롯은 항상 6개.");
        var deployedSlots = state.AnchorSlots.Count(slot => slot.HeroRow != null);
        Assert.That(deployedSlots, Is.GreaterThan(0), "기본 분대가 배치되어 있어야 함(BindProfile 시드).");
        Assert.That(state.AnchorSlots.Count(slot => slot.IsSelected), Is.EqualTo(1), "선택 anchor는 항상 1개.");

        // posture 5옵션 중 정확히 1개 선택 — 기본은 StandardAdvance.
        Assert.That(state.Postures.Count, Is.EqualTo(5));
        Assert.That(state.Postures.Count(p => p.IsSelected), Is.EqualTo(1));
        Assert.That(state.Postures.Single(p => p.IsSelected).Posture, Is.EqualTo(TeamPostureType.StandardAdvance));

        // roster read model — 전 영웅이 노출되고, 배치 영웅의 pip 규칙(배치=5)이 지켜진다.
        Assert.That(state.RosterCount, Is.EqualTo(session.Profile.Heroes.Count));
        Assert.That(state.RosterRows.Count, Is.EqualTo(state.RosterCount));
        Assert.That(state.RosterRows.Where(r => r.IsDeployed).Select(r => r.PipCount), Is.All.EqualTo(5),
            "배치 영웅 pip=5 규칙.");

        // 기본 분대는 시너지가 성립 — 실 SynergyCatalog 평가 결과 칩이 존재해야 한다.
        Assert.That(state.SynergyChips, Is.Not.Empty, "기본 분대에서 시너지 칩이 비면 SoT 평가가 끊긴 것.");
        Assert.That(state.SynergyEmptyText, Is.Empty, "칩이 있으면 안내 문구는 비어야 함.");

        // 위험 점수 heuristic: max(0, 6-배치)*3 + posture 가중(StandardAdvance=8).
        var expectedRisk = Math.Max(0, 6 - deployedSlots) * 3 + 8;
        Assert.That(state.RiskChipLabel, Is.EqualTo($"위험 점수 {expectedRisk}"));
        Assert.That(state.DeploymentChipLabel, Is.EqualTo($"배치 {deployedSlots}/6"));

        // 대응 요약은 posture + disclaimer를 항상 포함.
        Assert.That(state.ResponseSummary, Does.Contain("표준 전진"));
        Assert.That(state.StatusText, Does.Contain("현재 팀 태세: 표준 전진"));
    }

    [Test]
    public void OnPostureClicked_UpdatesSessionTruthAndViewState()
    {
        var (presenter, view, session) = CreateHeadless();
        presenter.Open();
        Assert.That(view.Actions, Is.Not.Null, "Initialize에서 액션이 바인딩됨.");

        view.Actions!.OnPostureClicked(TeamPostureType.HoldLine);

        // 세션 truth 반영.
        Assert.That(session.SelectedTeamPosture, Is.EqualTo(TeamPostureType.HoldLine));

        // ViewState 반영 — 선택 posture, 상태 문구, 위험 점수 가중(HoldLine=4)까지 일관.
        var state = view.LastState!;
        Assert.That(state.Postures.Single(p => p.IsSelected).Posture, Is.EqualTo(TeamPostureType.HoldLine));
        Assert.That(state.StatusText, Does.Contain("팀 태세 갱신"));
        Assert.That(state.StatusText, Does.Contain("전열 사수"));

        var deployedSlots = state.AnchorSlots.Count(slot => slot.HeroRow != null);
        var expectedRisk = Math.Max(0, 6 - deployedSlots) * 3 + 4;
        Assert.That(state.RiskChipLabel, Is.EqualTo(deployedSlots == 0 ? "위험 점수 —" : $"위험 점수 {expectedRisk}"));
    }
}
