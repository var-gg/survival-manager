using NUnit.Framework;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode;

/// <summary>
/// TownScreen.uxml 템플릿 인스턴스화 회귀 — modal Template이 실제로 clone되어
/// 각 panel View가 요구하는 필수 element가 트리에 존재하는지 asset 기반으로 검증.
/// (2026-07 전술 공방 wire에서 template 참조가 비어 TryWireTacticalWorkshop이
/// 조용히 실패한 사고의 headless 재현 게이트. FastUnit이 아님 — AssetDatabase 필요.)
/// </summary>
[Category("BatchOnly")]
public sealed class TownScreenTemplateInstanceTests
{
    private const string TownUxmlPath = "Assets/_Game/UI/Screens/Town/TownScreen.uxml";

    [Test]
    public void TownScreen_Instantiates_TacticalWorkshop_Template_With_Required_Elements()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TownUxmlPath);
        Assert.That(tree, Is.Not.Null, TownUxmlPath);

        var root = tree.Instantiate();
        Assert.That(root.Q<VisualElement>("TwpRoot"), Is.Not.Null,
            "TacticalWorkshopTemplate 인스턴스가 Town 트리에 있어야 한다 — 없으면 wire가 조용히 실패한다.");
        Assert.That(root.Q<VisualElement>(className: "twp-anchor-pad"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("PostureCardRow"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("ThreatGrid"), Is.Not.Null);
        Assert.That(root.Q<VisualElement>("TacticPresetRows"), Is.Not.Null);
        Assert.That(root.Q<Button>("TwpCloseButton"), Is.Not.Null);

        // View ctor는 필수 element 결손 시 throw — 생성 자체가 계약 검증.
        Assert.DoesNotThrow(() => _ = new TacticalWorkshopView(root));
    }
}
