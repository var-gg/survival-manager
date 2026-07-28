using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode;

/// <summary>
/// InventoryPresenter headless conformance (Phase 2 Stage 2) — 씬·VisualElement·아이템 콘텐츠 없이 인벤토리
/// 화면을 순수 GameSessionState로 구동한다. 과거 InventoryPresenterFastTests는 presenter 소스를 grep하는
/// 가짜통과였다(presenter를 인스턴스화하지도, BuildState를 부르지도 않음). 이제 profile.Inventory에 아이템을
/// 직접 시드하고 presenter를 실제로 돌려 화면 데이터·command를 단언한다.
///
/// item definition lookup이 비어 있어도 BuildState가 honest placeholder로 graceful degrade하므로
/// (run-loop fixture는 ItemDefinition 0) 콘텐츠 확장 없이 화면 conformance를 검증할 수 있다.
/// </summary>
[Category("FastUnit")]
public sealed class InventoryPresenterFastTests
{
    [Test]
    public void BuildState_ReadsSeededInventory_WithGracefulLookupFallback()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out _);

        var state = presenter.BuildState();

        Assert.That(state.Items, Has.Count.GreaterThanOrEqualTo(2), "시드한 아이템이 그리드에 보인다.");
        Assert.That(state.Items.All(item => item.Name == "Unknown item"), Is.True,
            "item def가 없어도 raw ItemBaseId 대신 honest placeholder로 노출(graceful degrade).");
        Assert.That(state.Categories.Any(category => category.Key == "all"), Is.True, "카테고리 사이드바가 채워진다.");
        Assert.That(state.Detail, Is.Not.Null, "선택 아이템 상세가 채워진다.");
        Assert.That(state.Compare, Is.Not.Null);
        Assert.That(state.Compare!.TargetHeroLabel, Is.EqualTo("선택 영웅 없음"),
            "대상 영웅 미선택 상태가 비교 레인에 표면화된다.");
    }

    [Test]
    public void SetTargetHero_ReflectsInCompareLane_Headlessly()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out var view);

        presenter.SetTargetHero("hero-1");

        Assert.That(view.RenderCount, Is.GreaterThan(0), "대상 영웅 설정이 view render를 구동(headless).");
        var state = presenter.BuildState();
        Assert.That(state.Compare!.TargetHeroId, Is.EqualTo("hero-1"));
        Assert.That(state.Compare!.TargetHeroLabel, Is.EqualTo("—"),
            "contentText 미주입 상태에서도 save instance id는 player-facing 이름으로 노출하지 않는다.");
    }

    [Test]
    public void OnEquipItem_WithoutTargetHero_GuidesAndRendersHeadlessly()
    {
        var session = CreateSession();
        var presenter = CreatePresenter(session, out var view);

        ((IInventoryActions)presenter).OnEquipItem("inv-1");

        Assert.That(view.RenderCount, Is.GreaterThan(0), "장착 시도가 view render를 구동(command headless).");
        Assert.That(view.LastState, Is.Not.Null);
        Assert.That(view.LastState!.Compare!.EquipCta.StatusText, Does.Contain("hero"),
            "대상 영웅 없이 장착하면 영웅 선택을 안내한다.");
    }

    [Test]
    public void BuildState_SameBaseInstancesExposeDifferentTrustworthyTileFacts()
    {
        var session = CreateSession();
        session.Profile.Inventory = new List<InventoryItemRecord>
        {
            new()
            {
                ItemInstanceId = "inv-plain",
                ItemBaseId = "demo_blade",
                AffixIds = new List<string> { "affix-a" },
                RefitLevel = 0,
            },
            new()
            {
                ItemInstanceId = "inv-crafted",
                ItemBaseId = "demo_blade",
                AffixIds = new List<string> { "affix-a", "affix-b", "affix-c" },
                RefitLevel = 2,
            },
        };
        session.Profile.ItemCraftOperations = new List<ItemCraftOperationRecord>
        {
            new()
            {
                OperationId = "craft-1",
                ItemInstanceId = "inv-crafted",
                ItemBaseId = "demo_blade",
                OperationKind = CraftOperationKindValue.Reforge,
                TargetRefitLevel = 1,
            },
            new()
            {
                OperationId = "craft-2",
                ItemInstanceId = "inv-crafted",
                ItemBaseId = "demo_blade",
                OperationKind = CraftOperationKindValue.Seal,
                TargetRefitLevel = 2,
                SealedAffixIds = new List<string> { "affix-a" },
            },
        };
        var presenter = CreatePresenter(session, out _);

        var state = presenter.BuildState();
        var plain = state.Items.Single(item => item.ItemInstanceId == "inv-plain");
        var crafted = state.Items.Single(item => item.ItemInstanceId == "inv-crafted");

        Assert.That(plain.Name, Is.EqualTo(crafted.Name), "동일 base item의 표시명은 같아야 한다.");
        Assert.That(plain.MetaLabel, Is.EqualTo("A1 · R0 · C0/S0"));
        Assert.That(crafted.MetaLabel, Is.EqualTo("A3 · R2 · C2/S1"));
        Assert.That(crafted.MetaLabel, Is.Not.EqualTo(plain.MetaLabel));
        Assert.That(crafted.MetaTooltip, Does.Contain("Affixes: 3"));
        Assert.That(crafted.MetaTooltip, Does.Contain("Refit level: 2"));
        Assert.That(crafted.MetaTooltip, Does.Contain("Crafts: 2 (Seals: 1)"));
    }

    // UXML/USS 자산 구조 계약 — view가 Q<>로 찾는 named element가 .uxml에 존재하는지 검증한다.
    // 이건 자산(asset) 계약이라 headless로 대체 불가 → presenter 실구동 테스트와 별개 layer로 유지.
    [Test]
    public void InventoryView_Declares_Compare_Lane_And_Disabled_Equip_Cta_Rendering()
    {
        var uxml = File.ReadAllText("Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uxml");
        Assert.That(uxml, Does.Contain("CompareLane"));
        Assert.That(uxml, Does.Contain("CompareTargetHeroLabel"));
        Assert.That(uxml, Does.Contain("CompareSelectedItemLabel"));
        Assert.That(uxml, Does.Contain("CompareSelectedItemIcon"));
        Assert.That(uxml, Does.Contain("CompareEquippedItemLabel"));
        Assert.That(uxml, Does.Contain("CompareEquippedItemIcon"));
        Assert.That(uxml, Does.Contain("CompareRows"));
        Assert.That(uxml, Does.Contain("CompareEquipStatusLabel"));
        Assert.That(uxml, Does.Contain("InventoryTitleLabel"));
        Assert.That(uxml, Does.Contain("ItemGridScroll"));

        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryView.cs");
        Assert.That(view, Does.Contain("RenderCompare"));
        Assert.That(view, Does.Contain("ApplyEquipCta"));
        Assert.That(view, Does.Contain("_equipButton.SetEnabled(cta?.CanEquip == true)"));
        Assert.That(view, Does.Contain("inv-grid__cell-name"));
        Assert.That(view, Does.Contain("RenderDetailProfileRows"));
        Assert.That(view, Does.Contain("inv-detail__icon-image"));
        Assert.That(view, Does.Contain("var scope = _modalRoot ?? root"));
        Assert.That(view, Does.Contain("_equipButton.text = cta?.Label ?? \"장착\""));
        Assert.That(view, Does.Contain("FormatAffixGroupLabel"));
        Assert.That(view, Does.Contain("장비 정보"));

        var uss = File.ReadAllText("Assets/_Game/UI/Panels/InventoryTab/InventoryTab.uss");
        Assert.That(uss, Does.Contain("width: 540px"));
        Assert.That(uss, Does.Contain("width: 420px"));
        Assert.That(uss, Does.Contain("flex-direction: row"));
    }

    private static InventoryPresenter CreatePresenter(GameSessionState session, out RecordingInventoryView view)
    {
        view = new RecordingInventoryView();
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        // contentText·sprite loader 미주입(이미 nullable) — headless 구동에 불필요.
        return new InventoryPresenter(session, lookup, view);
    }

    private static GameSessionState CreateSession()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "inventory_presenter_test",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
            },
            Inventory = new List<InventoryItemRecord>
            {
                new() { ItemInstanceId = "inv-1", ItemBaseId = "demo_blade", AffixIds = new List<string>(), EquippedHeroId = string.Empty },
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
