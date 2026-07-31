using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Reward;

public sealed class RewardScreenView
{
    private readonly Label _titleLabel;
    private readonly Label _localeStatusLabel;
    private readonly Button _localeKoButton;
    private readonly Button _localeEnButton;
    private readonly Button _helpButton;
    private readonly VisualElement _helpStrip;
    private readonly Label _helpBodyLabel;
    private readonly Button _helpDismissButton;
    private readonly Label _resultHeadlineLabel;
    private readonly VisualElement _currencyChips;
    private readonly VisualElement _payoffPanel;
    private readonly VisualElement _payoffRows;
    private readonly VisualElement _survivorPanel;
    private readonly VisualElement _survivorRows;
    private readonly Label _statusLabel;
    private readonly Button _returnTownButton;
    private readonly IReadOnlyList<(VisualElement card, Label title, Label body, Label kind, Label context, Button button)> _choiceCards;

    public RewardScreenView(VisualElement root)
    {
        _titleLabel = Require<Label>(root, "TitleLabel");
        _localeStatusLabel = Require<Label>(root, "LocaleStatusLabel");
        _localeKoButton = Require<Button>(root, "LocaleKoButton");
        _localeEnButton = Require<Button>(root, "LocaleEnButton");
        _helpButton = Require<Button>(root, "HelpButton");
        _helpStrip = Require<VisualElement>(root, "HelpStrip");
        _helpBodyLabel = Require<Label>(root, "HelpBodyLabel");
        _helpDismissButton = Require<Button>(root, "HelpDismissButton");
        _resultHeadlineLabel = Require<Label>(root, "ResultHeadlineLabel");
        _currencyChips = Require<VisualElement>(root, "RewardCurrencyChips");
        _payoffPanel = Require<VisualElement>(root, "RewardPayoffPanel");
        _payoffRows = Require<VisualElement>(root, "RewardPayoffRows");
        // wave-28-survivor GPT Pro patch: squad 4명 survivor list.
        _survivorPanel = Require<VisualElement>(root, "RewardSurvivorPanel");
        _survivorRows = Require<VisualElement>(root, "RewardSurvivorRows");
        _statusLabel = Require<Label>(root, "StatusLabel");
        _returnTownButton = Require<Button>(root, "ReturnTownButton");
        _choiceCards = Enumerable.Range(1, 3)
            .Select(index => (
                Require<VisualElement>(root, $"ChoiceCard{index}"),
                Require<Label>(root, $"ChoiceCard{index}TitleLabel"),
                Require<Label>(root, $"ChoiceCard{index}BodyLabel"),
                Require<Label>(root, $"ChoiceCard{index}KindLabel"),
                Require<Label>(root, $"ChoiceCard{index}ContextLabel"),
                Require<Button>(root, $"ChoiceCard{index}Button")))
            .ToArray();
    }

    public void Bind(RewardScreenPresenter presenter)
    {
        _localeKoButton.clicked += presenter.SelectKorean;
        _localeEnButton.clicked += presenter.SelectEnglish;
        _helpButton.clicked += presenter.ToggleHelp;
        _helpDismissButton.clicked += presenter.DismissHelp;
        _returnTownButton.clicked += presenter.ReturnToTown;
        _choiceCards[0].button.clicked += presenter.Choose0;
        _choiceCards[1].button.clicked += presenter.Choose1;
        _choiceCards[2].button.clicked += presenter.Choose2;
    }

    public void Render(RewardScreenViewState state)
    {
        _titleLabel.text = state.Title;
        _localeStatusLabel.text = state.LocaleStatus;
        _localeKoButton.text = state.LocaleKoLabel;
        _localeEnButton.text = state.LocaleEnLabel;
        _helpButton.text = state.HelpButtonLabel;
        _helpStrip.style.display = DisplayStyle.None;
        _helpBodyLabel.text = state.Help.Body;
        _helpDismissButton.text = state.Help.DismissLabel;
        _resultHeadlineLabel.text = state.ResultHeadline;
        RenderCurrencyChips(state.CurrencyChips);
        RenderPayoffRows(state.PayoffRows);
        RenderSurvivorRows(state.SurvivorRows);
        _statusLabel.text = state.StatusText;
        _returnTownButton.text = state.ReturnTownLabel;
        _returnTownButton.tooltip = state.ReturnTownTooltip;
        _returnTownButton.SetEnabled(state.CanReturnToTown);
        SetPrimaryClass(_returnTownButton, state.ReturnTownIsPrimary);

        for (var i = 0; i < _choiceCards.Count; i++)
        {
            var hasCard = i < state.Choices.Count;
            var cardState = hasCard
                ? state.Choices[i]
                : new RewardChoiceCardViewState(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false);

            // 카드가 세 장 미만이면 빈 액자를 남기지 않는다. 시안은 항상 세 장이지만
            // 회수 보상 레인은 두 장일 수 있고, 그때 빈 금테 상자가 서면 "로딩 실패"로 읽힌다.
            _choiceCards[i].card.style.display = hasCard ? DisplayStyle.Flex : DisplayStyle.None;

            _choiceCards[i].title.text = cardState.Title;
            _choiceCards[i].title.tooltip = cardState.Tooltip;
            _choiceCards[i].body.text = cardState.Body;
            _choiceCards[i].body.tooltip = cardState.Tooltip;
            _choiceCards[i].kind.text = cardState.KindText;
            _choiceCards[i].kind.tooltip = cardState.Tooltip;
            ApplyOptionalText(_choiceCards[i].kind, cardState.KindText);
            _choiceCards[i].context.text = cardState.ContextText;
            _choiceCards[i].context.tooltip = cardState.Tooltip;
            ApplyOptionalText(_choiceCards[i].context, cardState.ContextText);
            _choiceCards[i].button.text = cardState.ActionLabel;
            _choiceCards[i].button.tooltip = cardState.Tooltip;
            _choiceCards[i].button.SetEnabled(cardState.IsEnabled);
            SetPrimaryClass(_choiceCards[i].button, cardState.IsEnabled && !state.ReturnTownIsPrimary);
        }
    }

    /// <summary>
    /// 결과 줄 옆의 화폐 칩. 시안의 <c>XP +84 / 골드 +25 / 잔향 +8</c> 자리다.
    /// 전리품이 없으면 칩 줄 자체를 비운다 — "없음"이라고 적힌 칩은 정보가 아니라 잡음이다.
    /// </summary>
    private void RenderCurrencyChips(IReadOnlyList<RewardCurrencyChipViewState>? chips)
    {
        _currencyChips.Clear();
        foreach (var chip in chips ?? Array.Empty<RewardCurrencyChipViewState>())
        {
            if (string.IsNullOrWhiteSpace(chip.Label))
            {
                continue;
            }

            var label = new Label(chip.Label);
            label.AddToClassList("reward-currency-chip");
            label.AddToClassList($"reward-currency-chip--{chip.ToneKey}");
            _currencyChips.Add(label);
        }
    }

    /// <summary>
    /// 전과 원장. 행이 하나도 없으면 패널째 감춘다 — 평시엔 이 화면에 존재하지 않는 게 맞다.
    /// </summary>
    private void RenderPayoffRows(IReadOnlyList<RewardProgressionRowViewState>? rows)
    {
        _payoffRows.Clear();
        if (rows == null || rows.Count == 0)
        {
            _payoffPanel.style.display = DisplayStyle.None;
            return;
        }

        _payoffPanel.style.display = DisplayStyle.Flex;
        foreach (var row in rows)
        {
            var container = new VisualElement();
            container.AddToClassList("reward-payoff-row");
            container.AddToClassList($"reward-payoff-row--{row.ToneKey}");

            var key = new Label(row.KeyText);
            key.AddToClassList("reward-payoff-row__key");
            container.Add(key);

            var value = new Label(row.ValueText);
            value.AddToClassList("reward-payoff-row__value");
            container.Add(value);

            _payoffRows.Add(container);
        }
    }

    // wave-28-survivor GPT Pro patch: squad 4명 survivor row (portrait glyph + name + HP bar + EXP + status).
    private void RenderSurvivorRows(IReadOnlyList<RewardSurvivorRowViewState>? rows)
    {
        _survivorRows.Clear();
        if (rows == null || rows.Count == 0)
        {
            _survivorPanel.style.display = DisplayStyle.None;
            return;
        }

        _survivorPanel.style.display = DisplayStyle.Flex;
        foreach (var row in rows)
        {
            var container = new VisualElement();
            container.AddToClassList("reward-survivor-row");
            container.AddToClassList($"reward-survivor-row--{row.StatusChipKind}");

            var portrait = new Label(row.PortraitGlyph);
            portrait.AddToClassList("reward-survivor-row__portrait");
            container.Add(portrait);

            var copy = new VisualElement();
            copy.AddToClassList("reward-survivor-row__copy");
            var nameLabel = new Label(row.DisplayName);
            nameLabel.AddToClassList("reward-survivor-row__name");
            copy.Add(nameLabel);

            var stats = new VisualElement();
            stats.AddToClassList("reward-survivor-row__stats");
            var hpBar = new VisualElement();
            hpBar.AddToClassList("reward-survivor-row__hp-bar");
            var hpFill = new VisualElement();
            hpFill.AddToClassList("reward-survivor-row__hp-fill");
            hpFill.style.width = new StyleLength(new Length(Math.Clamp(row.HpPercent * 100f, 0f, 100f), LengthUnit.Percent));
            hpBar.Add(hpFill);
            var hpLabel = new Label(row.HpText);
            hpLabel.AddToClassList("reward-survivor-row__hp");
            hpLabel.pickingMode = PickingMode.Ignore;
            hpBar.Add(hpLabel);
            stats.Add(hpBar);
            var expLabel = new Label(row.ExpText);
            expLabel.AddToClassList("reward-survivor-row__exp");
            stats.Add(expLabel);
            copy.Add(stats);
            container.Add(copy);

            _survivorRows.Add(container);
        }
    }

    private static void ApplyOptionalText(Label label, string text)
    {
        label.style.display = string.IsNullOrWhiteSpace(text)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

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
