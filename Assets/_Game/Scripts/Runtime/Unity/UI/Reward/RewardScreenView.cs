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
    private readonly Label _summaryTitleLabel;
    private readonly Label _runDeltaLabel;
    private readonly Label _buildContextTitleLabel;
    private readonly Label _buildContextLabel;
    private readonly Label _choicesHeaderLabel;
    private readonly Label _statusLabel;
    private readonly Button _returnTownButton;
    private readonly IReadOnlyList<(Label title, Label body, Label kind, Label context, Button button)> _choiceCards;

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
        _summaryTitleLabel = Require<Label>(root, "SummaryTitleLabel");
        _runDeltaLabel = Require<Label>(root, "RunDeltaLabel");
        _buildContextTitleLabel = Require<Label>(root, "BuildContextTitleLabel");
        _buildContextLabel = Require<Label>(root, "BuildContextLabel");
        _choicesHeaderLabel = Require<Label>(root, "ChoicesHeaderLabel");
        _statusLabel = Require<Label>(root, "StatusLabel");
        _returnTownButton = Require<Button>(root, "ReturnTownButton");
        _choiceCards = Enumerable.Range(1, 3)
            .Select(index => (
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
        _helpStrip.style.display = state.Help.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _helpBodyLabel.text = state.Help.Body;
        _helpDismissButton.text = state.Help.DismissLabel;
        _summaryTitleLabel.text = state.SummaryTitle;
        _runDeltaLabel.text = state.RunDeltaText;
        _buildContextTitleLabel.text = state.BuildContextTitle;
        _buildContextLabel.text = state.BuildContextText;
        _choicesHeaderLabel.text = state.ChoicesHeaderText;
        _statusLabel.text = state.StatusText;
        _returnTownButton.text = state.ReturnTownLabel;
        _returnTownButton.tooltip = state.ReturnTownTooltip;
        _returnTownButton.SetEnabled(state.CanReturnToTown);
        SetPrimaryClass(_returnTownButton, state.ReturnTownIsPrimary);

        for (var i = 0; i < _choiceCards.Count; i++)
        {
            var cardState = i < state.Choices.Count
                ? state.Choices[i]
                : new RewardChoiceCardViewState(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false);
            _choiceCards[i].title.text = cardState.Title;
            _choiceCards[i].title.tooltip = cardState.Tooltip;
            _choiceCards[i].body.text = cardState.Body;
            _choiceCards[i].body.tooltip = cardState.Tooltip;
            _choiceCards[i].kind.text = cardState.KindText;
            _choiceCards[i].kind.tooltip = cardState.Tooltip;
            _choiceCards[i].context.text = cardState.ContextText;
            _choiceCards[i].context.tooltip = cardState.Tooltip;
            _choiceCards[i].button.text = cardState.ActionLabel;
            _choiceCards[i].button.tooltip = cardState.Tooltip;
            _choiceCards[i].button.SetEnabled(cardState.IsEnabled);
            SetPrimaryClass(_choiceCards[i].button, cardState.IsEnabled && !state.ReturnTownIsPrimary);
        }
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
