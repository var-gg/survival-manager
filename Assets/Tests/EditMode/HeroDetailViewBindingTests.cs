using System.Collections.Generic;
using NUnit.Framework;
using SM.Unity.UI.HeroDetail;
using UnityEditor;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode;

/// <summary>
/// HeroDetailPanel.uxml ↔ HeroDetailView 계약 회귀: named element 전수 바인딩 + 모든 상태 렌더 +
/// 4-slot 위계 modifier 부착을 AssetDatabase 로드로 검증(PlayMode 불필요).
/// </summary>
[Category("BatchOnly")]
public sealed class HeroDetailViewBindingTests
{
    private const string UxmlPath = "Assets/_Game/UI/Panels/HeroDetail/HeroDetailPanel.uxml";

    [Test]
    public void Uxml_IsImportableAsVisualTreeAsset()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.IsNotNull(uxml, $"HeroDetailPanel.uxml가 VisualTreeAsset으로 import되지 않음: {UxmlPath}");
    }

    [Test]
    public void View_BindsAllNamedElements_AndRendersComprehensiveState()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.IsNotNull(uxml, UxmlPath);
        var root = uxml.Instantiate();

        // 생성자가 모든 Require named element를 찾지 못하면 throw → 바인딩 계약 검증
        var view = new HeroDetailView(root);
        view.Render(BuildComprehensiveState());

        Assert.AreEqual(8, root.Q<VisualElement>("HeroDetailStats").childCount, "stat rows");
        Assert.AreEqual(4, root.Q<VisualElement>("HeroDetailSkills").childCount, "skill slots");
        Assert.AreEqual(3, root.Q<VisualElement>("HeroDetailEquip").childCount, "equip slots");
        Assert.AreEqual(2, root.Q<VisualElement>("HeroDetailTraits").childCount, "trait rows");
    }

    [Test]
    public void SkillSlots_CarryAllFourHierarchyModifiers()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.IsNotNull(uxml, UxmlPath);
        var root = uxml.Instantiate();
        new HeroDetailView(root).Render(BuildComprehensiveState());

        var skills = root.Q<VisualElement>("HeroDetailSkills");
        Assert.IsTrue(HasChildWithClass(skills, "sm-hd-slot--signature"), "signature");
        Assert.IsTrue(HasChildWithClass(skills, "sm-hd-slot--flex-active"), "flex-active");
        Assert.IsTrue(HasChildWithClass(skills, "sm-hd-slot--flex-retrain"), "flex-retrain");
        Assert.IsTrue(HasChildWithClass(skills, "sm-hd-slot--late-unlock"), "late-unlock");
    }

    [Test]
    public void EmptyState_RendersWithoutError()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.IsNotNull(uxml, UxmlPath);
        var root = uxml.Instantiate();
        var view = new HeroDetailView(root);

        Assert.DoesNotThrow(() => view.Render(HeroDetailViewState.Empty));
        Assert.AreEqual(0, root.Q<VisualElement>("HeroDetailSkills").childCount);
    }

    private static bool HasChildWithClass(VisualElement container, string className)
    {
        foreach (var child in container.Children())
        {
            if (child.ClassListContains(className))
            {
                return true;
            }
        }

        return false;
    }

    private static HeroDetailViewState BuildComprehensiveState()
    {
        return new HeroDetailViewState(
            HeroId: "hero_test",
            DisplayName: "단린",
            ArchetypeLabel: "여명의 사제",
            RoleLabel: "전열",
            FamilyKey: "vanguard",
            TierLabel: "Rare",
            LevelLabel: "Lv. 7",
            PortraitSprite: null,
            Stats: new List<HeroDetailStatViewState>
            {
                new("hp", "HP", "1200", "vital"),
                new("armor", "ARM", "40", "guard"),
                new("resist", "RES", "30", "guard"),
                new("phys", "PWR", "85", "attack"),
                new("magic", "MAG", "20", "magic"),
                new("speed", "SPD", "5.5", "tempo"),
                new("range", "RNG", "2", "tempo"),
                new("haste", "HST", "10", "magic"),
            },
            SkillSlots: new List<HeroDetailSkillSlotViewState>
            {
                new(HeroDetailSlotKind.SignatureLock, "sig_a", "수호 강타", "SIG · ACTIVE", "전열 보호 일격.", "icon_sig", HeroDetailIconState.Present, "8s", false),
                new(HeroDetailSlotKind.FlexActive, "flex_a", "신성 정화", "FLEX · ACTIVE", "상태이상 정화.", "icon_flex", HeroDetailIconState.Fallback, "12s", false),
                new(HeroDetailSlotKind.FlexRetrain, "sig_p", "불굴", "SIG · PASSIVE", "받는 피해 감소.", "", HeroDetailIconState.Missing, "Passive", true),
                new(HeroDetailSlotKind.LateUnlock, "late", "여명의 가호", "FLEX · PASSIVE", "후반 해금 패시브.", "icon_late", HeroDetailIconState.Present, "Passive", true),
            },
            Equipment: new List<HeroDetailEquipSlotViewState>
            {
                new("weapon", "Weapon", "여명의 창", "Rare / 2 affixes", "icon_weapon", true),
                new("armor", "Armor", "수호 갑주", "Common / 1 affixes", "icon_armor", true),
                new("accessory", "Accessory", "—", "Empty", "accessory", false),
            },
            Traits: new List<HeroDetailTraitViewState>
            {
                new("boon", "굳건함", string.Empty),
                new("bane", "느린 손", string.Empty),
            });
    }
}
