using System;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Atlas;

/// <summary>
/// 출격 서약 선택 화면 View — RewardScreenView 패턴(Require/Bind/Render + 동적 행 빌드)을 그대로 따른다.
/// chrome 라벨(Title/Header/Proceed)은 UXML named element에 바인딩하고, 서약 카드는 WarrantCardRow
/// 컨테이너에 Render에서 동적으로 생성한다(카드 4개라 고정 슬롯보다 동적 빌드가 깔끔 — RenderProgressionRows 패턴).
/// View는 presentation만 — battle/save truth를 만들지 않고 Presenter가 노출한 콜백만 호출한다.
/// </summary>
public sealed class WarrantSelectionView
{
    private readonly Label _titleLabel;
    private readonly Label _headerLabel;
    private readonly VisualElement _cardRow;
    private readonly Button _proceedButton;
    private readonly Label _statusLabel;

    public WarrantSelectionView(VisualElement root)
    {
        _titleLabel = Require<Label>(root, "TitleLabel");
        _headerLabel = Require<Label>(root, "HeaderLabel");
        _cardRow = Require<VisualElement>(root, "WarrantCardRow");
        _proceedButton = Require<Button>(root, "ProceedButton");
        _statusLabel = Require<Label>(root, "StatusLabel");
    }

    public void Bind(WarrantSelectionPresenter presenter)
    {
        _proceedButton.clicked += presenter.SelectNone;
    }

    public void Render(WarrantSelectionViewState state)
    {
        _titleLabel.text = state.Title;
        _headerLabel.text = state.HeaderText;
        _statusLabel.text = state.StatusText;
        _proceedButton.text = state.ProceedLabel;
        SetPrimaryClass(_proceedButton, state.ProceedIsSelected);
        RenderCards(state);
    }

    private void RenderCards(WarrantSelectionViewState state)
    {
        _cardRow.Clear();
        foreach (var card in state.Cards ?? Array.Empty<WarrantSelectionCardViewState>())
        {
            _cardRow.Add(BuildCard(card));
        }
    }

    private VisualElement BuildCard(WarrantSelectionCardViewState card)
    {
        var container = new VisualElement();
        container.AddToClassList("card");
        container.AddToClassList("warrant-card");
        container.EnableInClassList("is-selected", card.IsSelected);

        var title = new Label(card.TitleText);
        title.AddToClassList("card__title");
        container.Add(title);

        var issuer = new Label(card.IssuerText);
        issuer.AddToClassList("panel__text");
        issuer.AddToClassList("panel__text--muted");
        container.Add(issuer);

        var opposed = new Label(card.OpposedText);
        opposed.AddToClassList("panel__text");
        opposed.AddToClassList("panel__text--muted");
        container.Add(opposed);

        var kind = new Label(card.KindText);
        kind.AddToClassList("panel__text");
        kind.AddToClassList("panel__text--muted");
        container.Add(kind);

        var preview = new Label(card.PreviewText);
        preview.AddToClassList("card__body");
        preview.AddToClassList("warrant-card__preview");
        container.Add(preview);

        var warrantId = card.WarrantId;
        var button = new Button(() => Selected?.Invoke(warrantId))
        {
            text = card.ActionLabel,
        };
        button.AddToClassList("ui-button");
        SetPrimaryClass(button, card.IsSelected);
        container.Add(button);

        return container;
    }

    public event Action<string>? Selected;

    private static void SetPrimaryClass(VisualElement element, bool enabled)
    {
        if (enabled)
        {
            element.AddToClassList("ui-button--accent");
        }
        else
        {
            element.RemoveFromClassList("ui-button--accent");
        }
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
