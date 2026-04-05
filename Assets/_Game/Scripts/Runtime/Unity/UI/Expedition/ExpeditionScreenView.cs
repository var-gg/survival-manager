using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Expedition;

public sealed class ExpeditionScreenView
{
    private readonly Label _titleLabel;
    private readonly Label _localeStatusLabel;
    private readonly Button _localeKoButton;
    private readonly Button _localeEnButton;
    private readonly Label _positionLabel;
    private readonly Label _mapLabel;
    private readonly Label _rewardLabel;
    private readonly Label _squadLabel;
    private readonly Label _statusLabel;
    private readonly Button _nextBattleButton;
    private readonly Button _returnTownButton;
    private readonly Button _teamPostureButton;
    private readonly IReadOnlyList<(VisualElement card, Label title, Label reward, Button button)> _nodeCards;
    private readonly Dictionary<DeploymentAnchorId, Button> _deployButtons;

    public ExpeditionScreenView(VisualElement root)
    {
        _titleLabel = Require<Label>(root, "TitleLabel");
        _localeStatusLabel = Require<Label>(root, "LocaleStatusLabel");
        _localeKoButton = Require<Button>(root, "LocaleKoButton");
        _localeEnButton = Require<Button>(root, "LocaleEnButton");
        _positionLabel = Require<Label>(root, "PositionLabel");
        _mapLabel = Require<Label>(root, "MapLabel");
        _rewardLabel = Require<Label>(root, "RewardLabel");
        _squadLabel = Require<Label>(root, "SquadLabel");
        _statusLabel = Require<Label>(root, "StatusLabel");
        _nextBattleButton = Require<Button>(root, "NextBattleButton");
        _returnTownButton = Require<Button>(root, "ReturnTownButton");
        _teamPostureButton = Require<Button>(root, "TeamPostureButton");

        _nodeCards = Enumerable.Range(1, 5)
            .Select(index => (
                Require<VisualElement>(root, $"NodeCard{index}"),
                Require<Label>(root, $"NodeCard{index}TitleLabel"),
                Require<Label>(root, $"NodeCard{index}RewardLabel"),
                Require<Button>(root, $"NodeCard{index}Button")))
            .ToArray();

        _deployButtons = new Dictionary<DeploymentAnchorId, Button>
        {
            [DeploymentAnchorId.FrontTop] = Require<Button>(root, "DeployButton_FrontTop"),
            [DeploymentAnchorId.FrontCenter] = Require<Button>(root, "DeployButton_FrontCenter"),
            [DeploymentAnchorId.FrontBottom] = Require<Button>(root, "DeployButton_FrontBottom"),
            [DeploymentAnchorId.BackTop] = Require<Button>(root, "DeployButton_BackTop"),
            [DeploymentAnchorId.BackCenter] = Require<Button>(root, "DeployButton_BackCenter"),
            [DeploymentAnchorId.BackBottom] = Require<Button>(root, "DeployButton_BackBottom"),
        };
    }

    public void Bind(ExpeditionScreenPresenter presenter)
    {
        _localeKoButton.clicked += presenter.SelectKorean;
        _localeEnButton.clicked += presenter.SelectEnglish;
        _nextBattleButton.clicked += presenter.NextBattleOrAdvance;
        _returnTownButton.clicked += presenter.ReturnToTown;
        _teamPostureButton.clicked += presenter.CycleTeamPosture;

        _nodeCards[0].button.clicked += presenter.SelectNode1;
        _nodeCards[1].button.clicked += presenter.SelectNode2;
        _nodeCards[2].button.clicked += presenter.SelectNode3;
        _nodeCards[3].button.clicked += presenter.SelectNode4;
        _nodeCards[4].button.clicked += presenter.SelectNode5;

        _deployButtons[DeploymentAnchorId.FrontTop].clicked += presenter.CycleFrontTop;
        _deployButtons[DeploymentAnchorId.FrontCenter].clicked += presenter.CycleFrontCenter;
        _deployButtons[DeploymentAnchorId.FrontBottom].clicked += presenter.CycleFrontBottom;
        _deployButtons[DeploymentAnchorId.BackTop].clicked += presenter.CycleBackTop;
        _deployButtons[DeploymentAnchorId.BackCenter].clicked += presenter.CycleBackCenter;
        _deployButtons[DeploymentAnchorId.BackBottom].clicked += presenter.CycleBackBottom;
    }

    public void Render(ExpeditionScreenViewState state)
    {
        _titleLabel.text = state.Title;
        _localeStatusLabel.text = state.LocaleStatus;
        _localeKoButton.text = state.LocaleKoLabel;
        _localeEnButton.text = state.LocaleEnLabel;
        _positionLabel.text = state.PositionText;
        _mapLabel.text = state.MapText;
        _rewardLabel.text = state.RewardText;
        _squadLabel.text = state.SquadText;
        _statusLabel.text = state.StatusText;
        _nextBattleButton.text = state.NextBattleLabel;
        _returnTownButton.text = state.ReturnTownLabel;
        _teamPostureButton.text = state.TeamPostureButtonLabel;

        for (var i = 0; i < _nodeCards.Count; i++)
        {
            var cardState = i < state.NodeCards.Count ? state.NodeCards[i] : new ExpeditionNodeCardViewState(string.Empty, string.Empty, string.Empty, false, false, false, false, false);
            var card = _nodeCards[i];
            card.card.style.display = cardState.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            card.title.text = cardState.Title;
            card.reward.text = cardState.RewardSummary;
            card.button.text = cardState.ActionLabel;
            card.button.SetEnabled(cardState.IsSelectable);
            SetCardClass(card.card, "node-card--current", cardState.IsCurrent);
            SetCardClass(card.card, "node-card--selected", cardState.IsSelected);
            SetCardClass(card.card, "node-card--completed", cardState.IsCompleted);
        }

        foreach (var entry in state.DeployButtons)
        {
            if (_deployButtons.TryGetValue(entry.Anchor, out var button))
            {
                button.text = entry.Label;
            }
        }
    }

    private static void SetCardClass(VisualElement element, string className, bool enabled)
    {
        if (enabled)
        {
            element.AddToClassList(className);
        }
        else
        {
            element.RemoveFromClassList(className);
        }
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
