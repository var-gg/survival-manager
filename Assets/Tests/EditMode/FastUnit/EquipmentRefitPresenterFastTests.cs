using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

/// <summary>
/// EquipmentRefitPresenter headless conformance (Phase 2 Stage 2) — 씬·VisualElement·아이템 콘텐츠 없이
/// 장비 재가공 화면을 순수 GameSessionState로 구동한다. profile.Inventory에 아이템+affix를 직접 시드하고
/// presenter를 실제로 돌려 화면 데이터(pool/affix list)·선택 command를 단언한다.
///
/// item/affix definition lookup이 비어도 BuildState가 id 기반 fallback으로 graceful degrade하므로
/// (run-loop fixture는 ItemDefinition/AffixDefinition 0) 콘텐츠 확장 없이 conformance를 검증할 수 있다.
/// </summary>
[Category("FastUnit")]
public sealed class EquipmentRefitPresenterFastTests
{
    [Test]
    public void BuildState_ReadsInventoryPoolAndAffixes_WithGracefulLookupFallback()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out _);

        var state = presenter.BuildState();

        Assert.That(state.Pool, Has.Count.GreaterThanOrEqualTo(2), "인벤토리 전체가 재가공 pool로 보인다.");
        Assert.That(state.Pool.Any(row => row.Name == "demo_blade"), Is.True,
            "item def가 없어도 ItemBaseId 기반 fallback 이름으로 노출(graceful degrade).");
        // ResolveSelectedItem은 affix가 가장 많은 아이템을 default 선택 → demo_blade(2 affix).
        Assert.That(state.SelectedItemName, Is.EqualTo("demo_blade"), "affix가 가장 많은 아이템이 기본 선택된다.");
        Assert.That(state.Affixes, Has.Count.EqualTo(2), "선택 아이템의 affix 2줄이 표시된다.");
        Assert.That(state.Affixes.Select(affix => affix.AffixId),
            Is.EquivalentTo(new[] { "affix_demo_a", "affix_demo_b" }), "affix id가 그대로 노출(이름 resolver identity).");
        Assert.That(state.RefitCost, Is.EqualTo(EquipmentRefitPresenter.RefitEchoCost), "재가공 비용이 표면화된다.");
    }

    [Test]
    public void OnPoolItemSelected_SwitchesSelection_AndDrivesRenderHeadlessly()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out var view);

        ((IEquipmentRefitActions)presenter).OnPoolItemSelected("inv-2");

        Assert.That(view.RenderCount, Is.GreaterThan(0), "pool 선택이 view render를 구동(command headless).");
        var state = presenter.BuildState();
        Assert.That(state.SelectedItemName, Is.EqualTo("demo_guard"), "선택을 affix 없는 아이템으로 전환.");
        Assert.That(state.Affixes, Is.Empty, "affix 없는 아이템은 affix 목록이 비어 있다.");
        Assert.That(state.Pool.Single(row => row.ItemInstanceId == "inv-2").IsSelected, Is.True, "선택 상태가 pool row에 반영.");
    }

    [Test]
    public void OnAffixSelected_MarksAffixForReroll_Headlessly()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out _);
        ((IEquipmentRefitActions)presenter).OnPoolItemSelected("inv-1");

        ((IEquipmentRefitActions)presenter).OnAffixSelected("affix_demo_b");

        var state = presenter.BuildState();
        var marked = state.Affixes.Single(affix => affix.AffixId == "affix_demo_b");
        Assert.That(marked.IsSelectedForReroll, Is.True, "선택한 affix가 reroll 대상으로 표시된다.");
        Assert.That(state.Affixes.Single(affix => affix.AffixId == "affix_demo_a").IsSelectedForReroll, Is.False,
            "선택하지 않은 affix는 표시되지 않는다.");
    }

    private static EquipmentRefitPresenter CreatePresenter(GameSessionState session, out RecordingEquipmentRefitView view)
    {
        view = new RecordingEquipmentRefitView();
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        return new EquipmentRefitPresenter(
            session,
            lookup,
            view,
            itemId => itemId,
            affixId => affixId,
            (characterId, archetypeId) => string.IsNullOrEmpty(characterId) ? archetypeId : characterId);
    }

    private static GameSessionState CreateSession()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "equipment_refit_presenter_test",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
            },
            Inventory = new List<InventoryItemRecord>
            {
                new() { ItemInstanceId = "inv-1", ItemBaseId = "demo_blade", AffixIds = new List<string> { "affix_demo_a", "affix_demo_b" }, EquippedHeroId = string.Empty },
                new() { ItemInstanceId = "inv-2", ItemBaseId = "demo_guard", AffixIds = new List<string>(), EquippedHeroId = string.Empty },
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
