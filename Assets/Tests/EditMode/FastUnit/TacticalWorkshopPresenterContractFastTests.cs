using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

/// <summary>
/// 전술 공방(TacticalWorkshop) Presenter 계약 — BuildState가 세션 truth(배치/태세/지시)와
/// content SoT(SynergyCatalog / CounterCoverage 8차원)를 view-state로 옮기는 규칙.
/// 편집 표면은 태세 + per-unit 타겟 지시 + 초기화뿐이고, anchor는 read-only(SquadBuilder 소유)다.
/// 이름 해석은 identity 델리게이트 주입 — localization/scene 없이 FastUnit으로 검증한다.
/// </summary>
[Category("FastUnit")]
public sealed class TacticalWorkshopPresenterContractFastTests
{
    [Test]
    public void BuildState_Postures_Are_Five_And_Selection_Follows_Session()
    {
        var (session, presenter, _) = CreatePresenter(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"));

        var state = presenter.BuildState();

        Assert.That(state.Postures.Count, Is.EqualTo(5), "팀 태세 카드는 wiki SoT 5종 전부를 노출한다.");
        Assert.That(state.Postures.Select(p => p.PostureId),
            Is.EquivalentTo(Enum.GetNames(typeof(TeamPostureType))),
            "카드 id는 TeamPostureType enum 이름과 1:1이다.");
        Assert.That(state.SelectedPostureId, Is.EqualTo(session.SelectedTeamPosture.ToString()));
        Assert.That(state.Postures.Single(p => p.IsSelected).PostureId,
            Is.EqualTo(session.SelectedTeamPosture.ToString()),
            "선택 카드는 세션 SelectedTeamPosture 하나뿐이다.");
    }

    [Test]
    public void OnPostureSelected_Writes_Session_And_Rerenders()
    {
        var (session, presenter, view) = CreatePresenter(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"));
        presenter.Initialize();
        var renderedBefore = view.RenderCount;

        ((ITacticalWorkshopActions)presenter).OnPostureSelected("HoldLine");

        Assert.That(session.SelectedTeamPosture, Is.EqualTo(TeamPostureType.HoldLine),
            "카드 클릭은 SetTeamPosture로 세션 truth를 바꾼다.");
        Assert.That(view.RenderCount, Is.GreaterThan(renderedBefore), "mutate 후 재렌더된다.");
        Assert.That(view.LastState!.Postures.Single(p => p.IsSelected).PostureId, Is.EqualTo("HoldLine"));
        Assert.That(view.LastState.PostureChipLabel, Does.Contain("전열 사수"),
            "command chip은 태세 표시명(TacticsLexicon)을 따라간다.");
    }

    [Test]
    public void OnPostureSelected_InvalidId_Keeps_Session_Posture()
    {
        var (session, presenter, _) = CreatePresenter(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"));
        var before = session.SelectedTeamPosture;

        ((ITacticalWorkshopActions)presenter).OnPostureSelected("NotAPosture");

        Assert.That(session.SelectedTeamPosture, Is.EqualTo(before), "미지 id는 태세를 바꾸지 않는다.");
    }

    [Test]
    public void BuildState_Tactics_Cover_Deployed_Heroes_And_Directive_Cycles()
    {
        var (session, presenter, view) = CreatePresenter(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"),
            CreateHero("hero-2", "사수 하나", race: "human", classId: "ranger"));
        DeployExactly(session, ("hero-1", DeploymentAnchorId.FrontCenter), ("hero-2", DeploymentAnchorId.BackCenter));
        presenter.Initialize();

        var state = presenter.BuildState();
        Assert.That(state.Tactics.Count, Is.EqualTo(2), "유닛 전술 행은 배치된 유닛만큼 나온다.");
        Assert.That(state.Tactics.Select(t => t.HeroId), Is.EqualTo(new[] { "hero-1", "hero-2" }),
            "행 순서는 anchor 순서(전열→후열)를 따른다.");
        Assert.That(state.Tactics.All(t => t.DirectiveLabel == "기본(자동)"), Is.True,
            "지시 미설정 유닛은 기본(자동)으로 표시된다.");
        Assert.That(state.Tactics.All(t => t.Biases.Count == 3), Is.True,
            "bias는 RoleInstruction 3축 요약이다.");
        Assert.That(state.DeployChipLabel, Is.EqualTo("배치 2/6"));

        ((ITacticalWorkshopActions)presenter).OnTacticDirectiveCycled("hero-1");

        Assert.That(session.GetHeroTargetDirective("hero-1"), Is.EqualTo(PlayerTargetDirective.NearestEnemy),
            "지시 버튼 클릭은 세션 directive를 순환시킨다(P1 배선 재사용).");
        Assert.That(view.LastState!.Tactics.Single(t => t.HeroId == "hero-1").DirectiveLabel,
            Is.EqualTo("최근접 교전"), "재렌더된 행이 새 지시 표시명을 반영한다.");
    }

    [Test]
    public void BuildState_SynergyChips_Follow_Content_Catalog()
    {
        var catalog = new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal)
        {
            ["human:2"] = new SynergyTierTemplate("human:2",
                new TeamSynergyTierRule("human_synergy", "human", 2, Array.Empty<StatModifier>())),
            ["human:4"] = new SynergyTierTemplate("human:4",
                new TeamSynergyTierRule("human_synergy", "human", 4, Array.Empty<StatModifier>())),
        };
        var lookup = new FakeCombatContentLookup(
            snapshot: EditorFreeCombatContentFixture.CreateSnapshot() with { SynergyCatalog = catalog });
        var (session, presenter, _) = CreatePresenter(lookup,
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"),
            CreateHero("hero-2", "선봉 둘", race: "human", classId: "vanguard"));
        DeployExactly(session, ("hero-1", DeploymentAnchorId.FrontTop), ("hero-2", DeploymentAnchorId.FrontCenter));

        var state = presenter.BuildState();

        var human = state.SynergyChips.SingleOrDefault(chip => chip.SynergyId == "human_synergy");
        Assert.That(human, Is.Not.Null, "human 2명 → content SoT 시너지 칩이 노출된다.");
        Assert.That(human!.IsActive, Is.True, "2명은 2-경계 이상 → 활성.");
        Assert.That(human.CountLabel, Is.EqualTo("2/2"));
        Assert.That(state.SynergyEmptyText, Is.Empty, "칩이 있으면 빈 안내문은 없다.");
    }

    [Test]
    public void BuildState_EmptyDeploy_Yields_Synergy_Hint_And_Unevaluated_Threats()
    {
        var (session, presenter, _) = CreatePresenter(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"));
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        var state = presenter.BuildState();

        Assert.That(state.SynergyChips, Is.Empty);
        Assert.That(state.SynergyEmptyText, Is.Not.Empty, "빈 배치는 이유 안내문을 노출한다.");
        Assert.That(state.Threats.All(t => t.AnswerState == string.Empty), Is.True,
            "배치가 없으면 답수는 미평가 상태다(가짜 판정 금지 — 허구 위생).");
        Assert.That(state.AnswerChipLabel, Is.EqualTo("위협 답수 —"));
        Assert.That(state.DeployChipLabel, Is.EqualTo("배치 0/6"));
        Assert.That(state.Tactics, Is.Empty);
    }

    [Test]
    public void BuildState_Threat_Lanes_Match_CounterCoverage_Dimensions()
    {
        var (_, presenter, _) = CreatePresenter(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"));

        var state = presenter.BuildState();

        Assert.That(state.Threats.Select(t => t.LaneId),
            Is.EqualTo(SquadCounterCoveragePreview.Dimensions),
            "위협 lane id는 counter-coverage 8차원과 순서까지 일치한다 — 정적 사본 어휘 금지.");
        Assert.That(state.Threats.All(t => t.KoLabel != t.LaneId), Is.True,
            "8차원 전부 한국어 표시명이 등록돼 있다(raw id 노출 없음).");
    }

    [Test]
    public void OnTacticsReset_Resets_Posture_And_Deployed_Directives()
    {
        var (session, presenter, _) = CreatePresenter(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"));
        DeployExactly(session, ("hero-1", DeploymentAnchorId.FrontCenter));
        session.SetTeamPosture(TeamPostureType.AllInBackline);
        session.SetHeroTargetDirective("hero-1", PlayerTargetDirective.HuntExposedBackline);

        ((ITacticalWorkshopActions)presenter).OnTacticsReset();

        Assert.That(session.SelectedTeamPosture, Is.EqualTo(TeamPostureType.StandardAdvance));
        Assert.That(session.GetHeroTargetDirective("hero-1"), Is.EqualTo(PlayerTargetDirective.Default));
        Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.FrontCenter), Is.EqualTo("hero-1"),
            "초기화는 전술만 되돌린다 — 배치(anchor)는 SquadBuilder 소유라 보존.");
    }

    private static void DeployExactly(GameSessionState session, params (string HeroId, DeploymentAnchorId Anchor)[] placements)
    {
        // 자동배치 잔여를 먼저 비우고 명시 배치만 남긴다.
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        foreach (var (heroId, anchor) in placements)
        {
            Assert.That(session.AssignHeroToAnchor(anchor, heroId), Is.True, $"{heroId} → {anchor} 배치 성공.");
        }
    }

    private static (GameSessionState Session, TacticalWorkshopPresenter Presenter, RecordingTacticalWorkshopView View)
        CreatePresenter(params HeroInstanceRecord[] heroes)
        => CreatePresenter(EditorFreeCombatContentFixture.CreateRunLoopLookup(), heroes);

    private static (GameSessionState Session, TacticalWorkshopPresenter Presenter, RecordingTacticalWorkshopView View)
        CreatePresenter(FakeCombatContentLookup lookup, params HeroInstanceRecord[] heroes)
    {
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "tactical_workshop_presenter_test",
            Heroes = heroes.ToList(),
        });
        session.SetCurrentScene(SceneNames.Town);

        var view = new RecordingTacticalWorkshopView();
        var presenter = new TacticalWorkshopPresenter(
            session,
            lookup,
            view,
            characterName: (characterId, fallback) => string.IsNullOrEmpty(characterId) ? fallback : characterId,
            roleName: (roleId, fallback) => string.IsNullOrEmpty(roleId) ? fallback : roleId,
            synergyName: id => id);
        return (session, presenter, view);
    }

    private static HeroInstanceRecord CreateHero(string heroId, string name, string race, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = name,
            ArchetypeId = $"{classId}_archetype",
            RaceId = race,
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }
}
