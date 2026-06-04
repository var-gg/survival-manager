using System;
using System.Linq;
using NUnit.Framework;
using SM.Meta;
using SM.Unity;
using SM.Unity.UI.Atlas;

namespace SM.Tests.EditMode;

/// <summary>
/// WarrantSelectionPresenter / ViewState 빌드 로직의 headless 검증. Presenter가 이름 해석을
/// Func 델리게이트로 주입받게 설계돼 있어 ContentTextResolver(무거운 localization/lookup) 없이
/// 단순 fake로 view-model 빌드만 단위 테스트한다(Unity 렌더 불필요).
/// </summary>
[Category("FastUnit")]
public sealed class WarrantSelectionPresenterTests
{
    private static WarrantSelectionPresenter CreatePresenter(Func<string, int>? standingLookup = null)
    {
        return new WarrantSelectionPresenter(
            standingLookup ?? (_ => 0),
            factionName: id => $"faction:{id}",
            warrantName: id => $"warrant:{id}");
    }

    [Test]
    public void BuildState_ReturnsFourPoliticalCards()
    {
        var presenter = CreatePresenter();

        var state = presenter.BuildState();

        Assert.That(state.Cards.Count, Is.EqualTo(4));
        // 카드 수는 정치 warrant 카탈로그와 일치(placeholder offer 소스 = PoliticalWarrantIds).
        Assert.That(state.Cards.Count, Is.EqualTo(WarrantCatalog.PoliticalWarrantIds.Count));
        Assert.That(state.Cards.All(card => !string.IsNullOrWhiteSpace(card.WarrantId)), Is.True);
    }

    [Test]
    public void BuildState_DefaultSelection_IsProceedNone()
    {
        var presenter = CreatePresenter();

        var state = presenter.BuildState();

        Assert.That(presenter.SelectedWarrantId, Is.Empty);
        Assert.That(state.ProceedIsSelected, Is.True);
        Assert.That(state.Cards.All(card => !card.IsSelected), Is.True);
    }

    [Test]
    public void BuildState_ResolvesNamesViaInjectedDelegates()
    {
        var presenter = CreatePresenter();

        var card = presenter.BuildState().Cards.Single(c => c.WarrantId == WarrantCatalog.SolarumOrderId);

        Assert.That(card.TitleText, Is.EqualTo($"warrant:{WarrantCatalog.SolarumOrderId}"));
        Assert.That(card.IssuerText, Does.Contain($"faction:{WarrantCatalog.SolarumId}"));
        Assert.That(card.OpposedText, Does.Contain($"faction:{WarrantCatalog.PaleConclaveId}"));
        Assert.That(card.KindText, Does.Contain(WarrantDisplayDefaults.KindName(WarrantKind.Intact)));
    }

    [Test]
    public void Select_MarksMatchingCard_AndSetsSelectedWarrantId()
    {
        var presenter = CreatePresenter();

        presenter.Select(WarrantCatalog.SolarumOrderId);
        var state = presenter.BuildState();

        Assert.That(presenter.SelectedWarrantId, Is.EqualTo(WarrantCatalog.SolarumOrderId));
        Assert.That(state.ProceedIsSelected, Is.False);
        var selectedCards = state.Cards.Where(card => card.IsSelected).ToArray();
        Assert.That(selectedCards.Length, Is.EqualTo(1));
        Assert.That(selectedCards[0].WarrantId, Is.EqualTo(WarrantCatalog.SolarumOrderId));
    }

    [Test]
    public void SelectNone_ClearsSelection()
    {
        var presenter = CreatePresenter();
        presenter.Select(WarrantCatalog.SolarumOrderId);

        presenter.SelectNone();
        var state = presenter.BuildState();

        Assert.That(presenter.SelectedWarrantId, Is.Empty);
        Assert.That(state.ProceedIsSelected, Is.True);
        Assert.That(state.Cards.All(card => !card.IsSelected), Is.True);
    }

    [Test]
    public void Select_RaisesStateChanged()
    {
        var presenter = CreatePresenter();
        var raised = 0;
        presenter.StateChanged += () => raised++;

        presenter.Select(WarrantCatalog.SolarumOrderId);
        presenter.Select(WarrantCatalog.SolarumOrderId); // no-op, same id
        presenter.SelectNone();

        Assert.That(raised, Is.EqualTo(2));
    }

    [Test]
    public void BuildState_PreviewText_ShowsSupport_WhenIssuerTrusted()
    {
        // Solarum 신뢰가 지원 임계 이상이면 발행 세력 지원 조건이 카드 미리보기에 보인다(ADR-0028).
        var presenter = CreatePresenter(factionId =>
            factionId == WarrantCatalog.SolarumId ? PoliticalCombatConditionService.SupportTrustThreshold : 0);

        var card = presenter.BuildState().Cards.Single(c => c.WarrantId == WarrantCatalog.SolarumOrderId);

        Assert.That(string.IsNullOrWhiteSpace(card.PreviewText), Is.False);
        Assert.That(card.PreviewText, Does.Contain("후원"));
        Assert.That(card.PreviewText, Does.Contain($"faction:{WarrantCatalog.SolarumId}"));
    }

    [Test]
    public void BuildState_PreviewText_ShowsAlert_WhenOpposedHostile()
    {
        // 거스른 세력 적대가 임계 이하면 적 경계 조건이 미리보기에 보인다(opposed 축이 다음 전장에 닿는 지점).
        var presenter = CreatePresenter(factionId =>
            factionId == WarrantCatalog.PaleConclaveId ? PoliticalCombatConditionService.AlertStandingThreshold : 0);

        var card = presenter.BuildState().Cards.Single(c => c.WarrantId == WarrantCatalog.SolarumOrderId);

        Assert.That(card.PreviewText, Does.Contain("경계"));
        Assert.That(card.PreviewText, Does.Contain($"faction:{WarrantCatalog.PaleConclaveId}"));
    }

    [Test]
    public void BuildState_PreviewText_FallsBack_WhenNoConditions()
    {
        var presenter = CreatePresenter(_ => 0);

        var card = presenter.BuildState().Cards.First();

        Assert.That(string.IsNullOrWhiteSpace(card.PreviewText), Is.False);
    }
}
