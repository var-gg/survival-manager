using System.Collections.Generic;
using NUnit.Framework;
using SM.Unity.UI.Town;
using UnityEditor;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode;

/// <summary>
/// TownCharacterSheet.uxml ↔ TownCharacterSheetView 계약 회귀: panel body가 단일 Label에서
/// row 컨테이너(.tcs-row)로 전환된 후에도 named element 바인딩이 유지되고, label:value row와
/// note row가 정확한 자식 구조로 렌더되는지 AssetDatabase 로드로 검증(PlayMode 불필요).
/// </summary>
[Category("BatchOnly")]
public sealed class TownCharacterSheetViewBindingTests
{
    private const string UxmlPath = "Assets/_Game/UI/Panels/TownCharacterSheet/TownCharacterSheet.uxml";

    [Test]
    public void Uxml_IsImportableAsVisualTreeAsset()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.IsNotNull(uxml, $"TownCharacterSheet.uxml가 VisualTreeAsset으로 import되지 않음: {UxmlPath}");
    }

    [Test]
    public void View_BindsAllNamedElements_AndRendersLabelValueRows()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.IsNotNull(uxml, UxmlPath);
        var root = uxml.Instantiate();

        // 생성자가 모든 Require named element(body 5개 포함, 이제 VisualElement)를 못 찾으면 throw → 바인딩 계약 검증.
        var view = new TownCharacterSheetView(root);
        view.Render(BuildState());

        var overview = root.Q<VisualElement>("TcsOverviewBody");
        Assert.AreEqual(3, overview.childCount, "overview row count (1 note + 2 label rows)");

        // 첫 행 = note row: label 없이 value 1개 + note modifier.
        var note = overview[0];
        Assert.IsTrue(note.ClassListContains("tcs-row--note"), "first row is note");
        Assert.AreEqual(1, note.childCount, "note row has only value");
        Assert.IsTrue(note[0].ClassListContains("tcs-row__value--note"), "note value modifier");

        // 둘째 행 = label:value row: label + value 2개.
        var labelRow = overview[1];
        Assert.IsFalse(labelRow.ClassListContains("tcs-row--note"), "second row is label row");
        Assert.AreEqual(2, labelRow.childCount, "label row has label + value");
        Assert.IsTrue(labelRow[0].ClassListContains("tcs-row__label"), "label class");
        Assert.IsTrue(labelRow[1].ClassListContains("tcs-row__value"), "value class");
        Assert.AreEqual("전술", ((Label)labelRow[0]).text);
        Assert.AreEqual("표준 전선", ((Label)labelRow[1]).text);
    }

    [Test]
    public void Render_ClearsPreviousRows_OnRebind()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.IsNotNull(uxml, UxmlPath);
        var root = uxml.Instantiate();
        var view = new TownCharacterSheetView(root);

        view.Render(BuildState());
        view.Render(BuildState());

        // 재렌더가 누적되지 않고 Clear됨.
        Assert.AreEqual(3, root.Q<VisualElement>("TcsOverviewBody").childCount, "rebind does not accumulate rows");
    }

    private static TownCharacterSheetViewState BuildState()
    {
        var overviewRows = new List<TownCharacterSheetPanelRowViewState>
        {
            new(string.Empty, "약탈자 (약탈자)"),
            new("전술", "표준 전선"),
            new("태세", "표준 전선"),
        };
        var emptyPanel = new TownCharacterSheetPanelViewState(
            "패널",
            new List<TownCharacterSheetPanelRowViewState>());
        return new TownCharacterSheetViewState(
            HeroId: "hero_test",
            DisplayName: "약탈자",
            ArchetypeLabel: "Lv. 1 / 약탈자",
            RoleLabel: "파격",
            FamilyKey: "duelist",
            PortraitSprite: null,
            HeroRail: new List<TownCharacterSheetHeroRailEntryViewState>(),
            Stats: new List<TownCharacterSheetStatViewState>(),
            Skills: new List<TownCharacterSheetSkillCardViewState>(),
            Equipment: new List<TownCharacterSheetEquipmentSlotViewState>(),
            ProgressionNodes: new List<TownCharacterSheetProgressionNodeViewState>(),
            Overview: new TownCharacterSheetPanelViewState("개요", overviewRows),
            Loadout: emptyPanel,
            Passives: emptyPanel,
            Synergy: emptyPanel,
            Progression: emptyPanel);
    }
}
