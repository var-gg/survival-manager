using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Atlas;

namespace SM.Tests.EditMode;

/// <summary>
/// 출격 편성 확인 Presenter 계약 — BuildState가 6 anchor 배치와 활성 시너지를 view-state로 옮기는 규칙.
/// synergy 집계는 LaunchCoreRosterBaselineCatalog baseline(race 2/4 · class 2/3)을 read-only로 재사용하고,
/// breakpoint 미달 family는 칩에서 제외하며, 빈 배치는 출격을 막는다(빈 파티 dead-end 차단).
/// 이름 해석은 identity 델리게이트로 주입해 localization 없이 FastUnit으로 검증한다.
/// </summary>
[Category("FastUnit")]
public sealed class SortieConfirmPresenterContractFastTests
{
    private static readonly DeploymentAnchorId[] DeployedAnchors =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
    };

    [Test]
    public void BuildState_AggregatesDeployedSynergies_ExcludesBelowBreakpoint_MarksMajor()
    {
        var (session, catalog) = CreateSessionAndCatalog(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"),
            CreateHero("hero-2", "선봉 둘", race: "human", classId: "vanguard"),
            CreateHero("hero-3", "선봉 셋", race: "human", classId: "vanguard"),
            CreateHero("hero-4", "사수 하나", race: "human", classId: "ranger"));

        // 결정론적 배치: 3 vanguard + 1 ranger(모두 human)를 4 anchor에 명시 배치.
        DeployExactly(session, ("hero-1", DeploymentAnchorId.FrontTop), ("hero-2", DeploymentAnchorId.FrontCenter),
            ("hero-3", DeploymentAnchorId.FrontBottom), ("hero-4", DeploymentAnchorId.BackTop));

        var state = BuildState(session, catalog);

        // 배치 슬롯은 항상 6 anchor 전부를 노출하고, 배치 인원만 비어있지 않다.
        Assert.That(state.DeploySlots.Count, Is.EqualTo(6), "배치 슬롯은 6 anchor 전부를 노출한다.");
        Assert.That(state.DeploySlots.Count(slot => !slot.IsEmpty), Is.EqualTo(4), "4명 배치 → 4 슬롯만 채워진다.");
        Assert.That(state.DeploySlots.Count(slot => slot.IsEmpty), Is.EqualTo(2), "나머지 2 anchor는 빈 슬롯이다.");
        Assert.That(state.DeployCount, Is.EqualTo(4));
        Assert.That(state.DeployCap, Is.EqualTo(MetaBalanceDefaults.BattleDeployCap));
        Assert.That(state.CanLaunch, Is.True, "1명 이상 배치면 출격 가능.");

        var chips = state.SynergyChips;
        Assert.That(chips.All(chip => chip.Count >= chip.Minor), Is.True,
            "활성 칩은 모두 minor breakpoint 이상이다(미달 family는 노출되지 않는다).");

        var human = chips.SingleOrDefault(chip => chip.FamilyName == SynergyName("human"));
        Assert.That(human, Is.Not.Null, "human 4명 → race 시너지 활성.");
        Assert.That(human!.Count, Is.EqualTo(4));
        Assert.That(human.MajorReached, Is.True, "human 4명은 race major(4)를 채운다.");

        var vanguard = chips.SingleOrDefault(chip => chip.FamilyName == SynergyName("vanguard"));
        Assert.That(vanguard, Is.Not.Null, "vanguard 3명 → class 시너지 활성.");
        Assert.That(vanguard!.Count, Is.EqualTo(3));
        Assert.That(vanguard.MajorReached, Is.True, "vanguard 3명은 class major(3)를 채운다.");

        Assert.That(chips.Any(chip => chip.FamilyName == SynergyName("ranger")), Is.False,
            "ranger 1명은 class minor(2) 미달 → 칩으로 노출되지 않는다.");
    }

    [Test]
    public void BuildState_EmptyDeploy_BlocksLaunch_AndHasNoChips()
    {
        var (session, catalog) = CreateSessionAndCatalog(
            CreateHero("hero-1", "선봉 하나", race: "human", classId: "vanguard"),
            CreateHero("hero-2", "선봉 둘", race: "human", classId: "vanguard"));

        // 유저가 모든 anchor를 비운다(비우기도 1급 편성 의도) — fallback이 다시 채우지 않는다.
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }
        session.EnsureBattleDeployReady();

        var state = BuildState(session, catalog);

        Assert.That(state.DeployCount, Is.EqualTo(0));
        Assert.That(state.CanLaunch, Is.False, "빈 배치는 출격을 막는다(빈 파티 dead-end 차단).");
        Assert.That(state.DeploySlots.Count, Is.EqualTo(6));
        Assert.That(state.DeploySlots.All(slot => slot.IsEmpty), Is.True, "배치가 비면 6 슬롯 모두 빈 상태다.");
        Assert.That(state.SynergyChips.Count, Is.EqualTo(0), "배치 인원이 없으면 활성 시너지도 없다.");
    }

    private static void DeployExactly(GameSessionState session, params (string HeroId, DeploymentAnchorId Anchor)[] placements)
    {
        // 자동배치 잔여를 먼저 비우고 명시 배치만 남긴다 → BattleDeployHeroIds가 placements와 정확히 일치.
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        foreach (var (heroId, anchor) in placements)
        {
            Assert.That(session.AssignHeroToAnchor(anchor, heroId), Is.True, $"{heroId} → {anchor} 배치 성공.");
        }
    }

    private static SortieConfirmViewState BuildState(GameSessionState session, LaunchCoreRosterBaselineCatalog catalog)
    {
        var presenter = new SortieConfirmPresenter(
            session,
            catalog,
            archetypeName: id => id,
            synergyName: id => id,
            anchorName: anchor => anchor.ToString());
        return presenter.BuildState();
    }

    private static string SynergyName(string familyId) => LaunchCoreRosterBaselineCatalog.BuildSynergyId(familyId);

    private static (GameSessionState Session, LaunchCoreRosterBaselineCatalog Catalog) CreateSessionAndCatalog(
        params HeroInstanceRecord[] heroes)
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "sortie_confirm_presenter_test",
            Heroes = heroes.ToList(),
        });
        session.SetCurrentScene(SceneNames.Town);
        return (session, new LaunchCoreRosterBaselineCatalog(lookup));
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
