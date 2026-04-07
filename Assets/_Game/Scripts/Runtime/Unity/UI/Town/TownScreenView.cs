using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

public sealed class TownScreenView
{
    private readonly Label _titleLabel;
    private readonly Label _localeStatusLabel;
    private readonly Button _localeKoButton;
    private readonly Button _localeEnButton;
    private readonly Label _campaignSummaryLabel;
    private readonly Button _previousChapterButton;
    private readonly Button _nextChapterButton;
    private readonly Button _previousSiteButton;
    private readonly Button _nextSiteButton;
    private readonly Label _currencyLabel;
    private readonly Label _rosterLabel;
    private readonly Label _recruitSummaryLabel;
    private readonly Label _squadLabel;
    private readonly Label _deployPreviewLabel;
    private readonly Label _statusLabel;
    private readonly Button _rerollButton;
    private readonly Button _saveButton;
    private readonly Button _loadButton;
    private readonly Button _returnToStartButton;
    private readonly Button _expeditionButton;
    private readonly Button _quickBattleButton;
    private readonly Button _teamPostureButton;
    private readonly IReadOnlyList<(Label title, Label body, Button button)> _recruitCards;
    private readonly Dictionary<DeploymentAnchorId, Button> _deployButtons;

    public TownScreenView(VisualElement root)
    {
        _titleLabel = Require<Label>(root, "TitleLabel");
        _localeStatusLabel = Require<Label>(root, "LocaleStatusLabel");
        _localeKoButton = Require<Button>(root, "LocaleKoButton");
        _localeEnButton = Require<Button>(root, "LocaleEnButton");
        _campaignSummaryLabel = Require<Label>(root, "CampaignSummaryLabel");
        _previousChapterButton = Require<Button>(root, "PrevChapterButton");
        _nextChapterButton = Require<Button>(root, "NextChapterButton");
        _previousSiteButton = Require<Button>(root, "PrevSiteButton");
        _nextSiteButton = Require<Button>(root, "NextSiteButton");
        _currencyLabel = Require<Label>(root, "CurrencyLabel");
        _rosterLabel = Require<Label>(root, "RosterLabel");
        _recruitSummaryLabel = Require<Label>(root, "RecruitSummaryLabel");
        _squadLabel = Require<Label>(root, "SquadLabel");
        _deployPreviewLabel = Require<Label>(root, "DeployPreviewLabel");
        _statusLabel = Require<Label>(root, "StatusLabel");
        _rerollButton = Require<Button>(root, "RerollButton");
        _saveButton = Require<Button>(root, "SaveButton");
        _loadButton = Require<Button>(root, "LoadButton");
        _returnToStartButton = Require<Button>(root, "ReturnToStartButton");
        _expeditionButton = Require<Button>(root, "ExpeditionButton");
        _quickBattleButton = Require<Button>(root, "QuickBattleButton");
        _teamPostureButton = Require<Button>(root, "TeamPostureButton");

        _recruitCards = Enumerable.Range(1, 4)
            .Select(index => (
                Require<Label>(root, $"RecruitCard{index}TitleLabel"),
                Require<Label>(root, $"RecruitCard{index}BodyLabel"),
                Require<Button>(root, $"RecruitCard{index}Button")))
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

    public void Bind(TownScreenPresenter presenter)
    {
        _localeKoButton.clicked += presenter.SelectKorean;
        _localeEnButton.clicked += presenter.SelectEnglish;
        _previousChapterButton.clicked += presenter.PreviousChapter;
        _nextChapterButton.clicked += presenter.NextChapter;
        _previousSiteButton.clicked += presenter.PreviousSite;
        _nextSiteButton.clicked += presenter.NextSite;
        _rerollButton.clicked += presenter.RerollOffers;
        _saveButton.clicked += presenter.SaveProfile;
        _loadButton.clicked += presenter.LoadProfile;
        _returnToStartButton.clicked += presenter.ReturnToStart;
        _expeditionButton.clicked += presenter.OpenExpedition;
        _quickBattleButton.clicked += presenter.QuickBattle;
        _teamPostureButton.clicked += presenter.CycleTeamPosture;

        _recruitCards[0].button.clicked += presenter.RecruitOffer0;
        _recruitCards[1].button.clicked += presenter.RecruitOffer1;
        _recruitCards[2].button.clicked += presenter.RecruitOffer2;
        _recruitCards[3].button.clicked += presenter.RecruitOffer3;

        _deployButtons[DeploymentAnchorId.FrontTop].clicked += presenter.CycleFrontTop;
        _deployButtons[DeploymentAnchorId.FrontCenter].clicked += presenter.CycleFrontCenter;
        _deployButtons[DeploymentAnchorId.FrontBottom].clicked += presenter.CycleFrontBottom;
        _deployButtons[DeploymentAnchorId.BackTop].clicked += presenter.CycleBackTop;
        _deployButtons[DeploymentAnchorId.BackCenter].clicked += presenter.CycleBackCenter;
        _deployButtons[DeploymentAnchorId.BackBottom].clicked += presenter.CycleBackBottom;
    }

    public void Render(TownScreenViewState state)
    {
        _titleLabel.text = state.Title;
        _localeStatusLabel.text = state.LocaleStatus;
        _localeKoButton.text = state.LocaleKoLabel;
        _localeEnButton.text = state.LocaleEnLabel;
        _campaignSummaryLabel.text = state.CampaignSummaryText;
        _previousChapterButton.text = state.PreviousChapterLabel;
        _previousChapterButton.SetEnabled(state.CanSelectPreviousChapter);
        _nextChapterButton.text = state.NextChapterLabel;
        _nextChapterButton.SetEnabled(state.CanSelectNextChapter);
        _previousSiteButton.text = state.PreviousSiteLabel;
        _previousSiteButton.SetEnabled(state.CanSelectPreviousSite);
        _nextSiteButton.text = state.NextSiteLabel;
        _nextSiteButton.SetEnabled(state.CanSelectNextSite);
        _currencyLabel.text = state.CurrencySummary;
        _rosterLabel.text = state.RosterText;
        _recruitSummaryLabel.text = state.RecruitSummaryText;
        _squadLabel.text = state.SquadText;
        _deployPreviewLabel.text = state.DeployPreviewText;
        _statusLabel.text = state.StatusText;
        _rerollButton.text = state.RerollLabel;
        _saveButton.text = state.SaveLabel;
        _loadButton.text = state.LoadLabel;
        _returnToStartButton.text = state.ReturnToStartLabel;
        _returnToStartButton.SetEnabled(state.CanReturnToStart);
        _expeditionButton.text = state.ExpeditionActionLabel;
        _quickBattleButton.text = state.QuickBattleLabel;
        _quickBattleButton.SetEnabled(state.CanQuickBattle);
        _teamPostureButton.text = state.TeamPostureButtonLabel;

        for (var i = 0; i < _recruitCards.Count; i++)
        {
            var cardState = i < state.RecruitCards.Count ? state.RecruitCards[i] : new TownRecruitCardViewState(string.Empty, string.Empty, string.Empty, false);
            _recruitCards[i].title.text = cardState.Title;
            _recruitCards[i].body.text = cardState.Body;
            _recruitCards[i].button.text = cardState.ActionLabel;
            _recruitCards[i].button.SetEnabled(cardState.IsEnabled);
        }

        foreach (var entry in state.DeployButtons)
        {
            if (_deployButtons.TryGetValue(entry.Anchor, out var button))
            {
                button.text = entry.Label;
            }
        }
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
