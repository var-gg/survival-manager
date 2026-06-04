using NUnit.Framework;
using SM.Unity.UI.Atlas;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode;

/// <summary>
/// ADR-0028 P2b — 출격 서약 선택 화면이 *실제 UXML에서* 구성되고 렌더되는지 검증(에디터 의존, BatchOnly).
/// Resources.Load 활성화 + UXML↔View named-element 계약 + 카드 렌더를 한 번에 잡는다 — full PlayMode 네비게이션
/// 없이도 "화면이 깨지지 않고 뜬다"의 핵심 회귀를 막는다(이름 불일치면 View 생성자 Require에서 throw).
/// </summary>
[Category("BatchOnly")]
public sealed class WarrantSelectionScreenSmokeTests
{
    [Test]
    public void Uxml_LoadsFromResources_ViewConstructs_AndRendersCards()
    {
        var uxml = Resources.Load<VisualTreeAsset>("WarrantSelection");
        Assert.That(uxml, Is.Not.Null, "WarrantSelection.uxml이 Resources에서 로드돼야 한다(통합 활성화).");

        var root = uxml.Instantiate();

        // View 생성자가 named element(TitleLabel/HeaderLabel/WarrantCardRow/ProceedButton/StatusLabel)를 Require.
        // UXML↔View 이름 불일치면 여기서 throw — 즉 화면 계약 검증.
        var view = new WarrantSelectionView(root);

        // 실제 Presenter로 옵션을 빌드해 Render → 4 세력 위임 카드가 채워지는지.
        var presenter = new WarrantSelectionPresenter(_ => 0, id => $"faction:{id}", id => $"warrant:{id}");
        view.Render(presenter.BuildState());

        var cardRow = root.Q<VisualElement>("WarrantCardRow");
        Assert.That(cardRow, Is.Not.Null);
        Assert.That(cardRow.childCount, Is.GreaterThan(0), "4 세력 위임 카드가 렌더돼야 한다.");
    }
}
