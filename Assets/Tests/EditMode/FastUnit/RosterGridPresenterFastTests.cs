using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

/// <summary>
/// RosterGridPresenter headless conformance (Phase 2 Stage 2) — 씬·VisualElement 없이 Town 로스터 패널을
/// 순수 GameSessionState + 이름 resolver delegate로 구동한다. 과거 RecruitPresenterFastTests처럼 소스 문자열을
/// grep하는 가짜통과가 아니라, presenter를 실제로 인스턴스화해 BuildState/command를 구동·단언한다.
/// "Unity는 껍데기, 게임플레이 truth는 순수 C#" 비전의 화면 데이터 conformance를 한 화면 추가로 넓힌다.
/// </summary>
[Category("FastUnit")]
public sealed class RosterGridPresenterFastTests
{
    [Test]
    public void BuildState_ReadsBoundRoster_WithDataDrivenFilters()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out _);

        var state = presenter.BuildState();

        Assert.That(state.Heroes, Has.Count.EqualTo(4), "바인딩한 4인이 명부 카드로 보인다.");
        Assert.That(state.RosterCap, Is.GreaterThanOrEqualTo(4), "로스터 cap이 노출된다.");
        Assert.That(state.SelectedFilterKey, Is.EqualTo("all"), "기본 필터는 전체.");
        Assert.That(state.Filters, Is.Not.Null);
        Assert.That(state.Filters!.Any(f => f.Key == "all" && f.Count == 4), Is.True, "전체 필터가 4인을 센다.");
        // 필터 칩은 로스터에 실재하는 race/class만 data-driven 생성(dead 필터 없음).
        Assert.That(state.Filters!.Any(f => f.GroupKey == "race" && f.Key == "human" && f.Count == 4), Is.True,
            "보유 종족(human)만 필터로 노출되고 인원수를 센다.");
        foreach (var classKey in new[] { "vanguard", "ranger", "duelist", "mystic" })
        {
            Assert.That(state.Filters!.Any(f => f.GroupKey == "class" && f.Key == classKey && f.Count == 1), Is.True,
                $"보유 클래스 {classKey}가 필터로 노출된다.");
        }
    }

    [Test]
    public void OnFilterSelected_NarrowsRosterAndDrivesRender_Headlessly()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out var view);

        ((IRosterGridActions)presenter).OnFilterSelected("vanguard");

        Assert.That(view.RenderCount, Is.GreaterThan(0), "필터 선택이 view render를 구동한다(command headless).");
        var filtered = presenter.BuildState();
        Assert.That(filtered.SelectedFilterKey, Is.EqualTo("vanguard"));
        Assert.That(filtered.Heroes, Has.Count.EqualTo(1), "vanguard 필터 → 선봉 1인만 남는다.");
        Assert.That(filtered.Heroes[0].FamilyKey, Is.EqualTo("vanguard"));
    }

    private static RosterGridPresenter CreatePresenter(GameSessionState session, out RecordingRosterGridView view)
    {
        view = new RecordingRosterGridView();
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        return new RosterGridPresenter(
            session,
            lookup,
            view,
            classId => classId,
            raceId => raceId,
            (characterId, archetypeId) => string.IsNullOrEmpty(characterId) ? archetypeId : characterId);
    }

    private static GameSessionState CreateSession()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "roster_grid_presenter_test",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static HeroInstanceRecord CreateHero(string heroId, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = heroId,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }
}
