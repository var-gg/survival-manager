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
    private readonly Button _helpButton;
    private readonly VisualElement _helpStrip;
    private readonly Label _helpBodyLabel;
    private readonly Button _helpDismissButton;
    private readonly Label _campaignTitleLabel;
    private readonly Label _campaignSummaryLabel;
    private readonly Button _previousChapterButton;
    private readonly Button _nextChapterButton;
    private readonly Button _previousSiteButton;
    private readonly Button _nextSiteButton;
    private readonly Label _economyTitleLabel;
    private readonly Label _currencyLabel;
    private readonly Label _rosterTitleLabel;
    private readonly Label _rosterLabel;
    private readonly Label _blueprintTitleLabel;
    private readonly Label _blueprintSummaryLabel;
    private readonly Label _recruitTitleLabel;
    private readonly Label _recruitSummaryLabel;
    private readonly IReadOnlyList<(Label title, Label body)> _characterSheetPanels;
    private readonly Label _deployTitleLabel;
    private readonly Label _deployPreviewLabel;
    private readonly Label _statusLabel;
    private readonly Label _primaryActionsTitleLabel;
    private readonly Label _gameplayActionsTitleLabel;
    private readonly Label _utilityActionsTitleLabel;
    private readonly Label _debugActionsTitleLabel;
    private readonly VisualElement _debugActionsGroup;
    private readonly Button _rerollButton;
    private readonly Button _saveButton;
    private readonly Button _loadButton;
    private readonly Button _returnToStartButton;
    private readonly Button _expeditionButton;
    private readonly Button _quickBattleButton;
    private readonly Button _tacticalWorkshopButton;
    private readonly Button _teamPostureButton;
    private readonly Button _cycleHeroButton;
    private readonly Button _cycleItemButton;
    private readonly Button _scoutButton;
    private readonly Button _retrainActiveButton;
    private readonly Button _retrainPassiveButton;
    private readonly Button _fullRetrainButton;
    private readonly Button _dismissButton;
    private readonly Button _refitButton;
    private readonly Button _cycleBoardButton;
    private readonly Button _cycleNodeButton;
    private readonly Button _toggleNodeButton;
    private readonly Button _cyclePermanentButton;
    private readonly Button _equipPermanentButton;
    private readonly IReadOnlyList<(Label title, Label body, Button button)> _recruitCards;
    private readonly Dictionary<DeploymentAnchorId, Button> _deployButtons;

    public TownScreenView(VisualElement root)
    {
        _titleLabel = Require<Label>(root, "TitleLabel");
        _localeStatusLabel = Require<Label>(root, "LocaleStatusLabel");
        _localeKoButton = Require<Button>(root, "LocaleKoButton");
        _localeEnButton = Require<Button>(root, "LocaleEnButton");
        _helpButton = Require<Button>(root, "HelpButton");
        _helpStrip = Require<VisualElement>(root, "HelpStrip");
        _helpBodyLabel = Require<Label>(root, "HelpBodyLabel");
        _helpDismissButton = Require<Button>(root, "HelpDismissButton");
        _campaignTitleLabel = Require<Label>(root, "CampaignTitleLabel");
        _campaignSummaryLabel = Require<Label>(root, "CampaignSummaryLabel");
        _previousChapterButton = Require<Button>(root, "PrevChapterButton");
        _nextChapterButton = Require<Button>(root, "NextChapterButton");
        _previousSiteButton = Require<Button>(root, "PrevSiteButton");
        _nextSiteButton = Require<Button>(root, "NextSiteButton");
        _economyTitleLabel = Require<Label>(root, "EconomyTitleLabel");
        _currencyLabel = Require<Label>(root, "CurrencyLabel");
        _rosterTitleLabel = Require<Label>(root, "RosterTitleLabel");
        _rosterLabel = Require<Label>(root, "RosterLabel");
        _blueprintTitleLabel = Require<Label>(root, "BlueprintTitleLabel");
        _blueprintSummaryLabel = Require<Label>(root, "BlueprintSummaryLabel");
        _recruitTitleLabel = Require<Label>(root, "RecruitTitleLabel");
        _recruitSummaryLabel = Require<Label>(root, "RecruitSummaryLabel");
        _characterSheetPanels = new[]
        {
            (
                Require<Label>(root, "CharacterSheetOverviewTitleLabel"),
                Require<Label>(root, "CharacterSheetOverviewBodyLabel")),
            (
                Require<Label>(root, "CharacterSheetLoadoutTitleLabel"),
                Require<Label>(root, "CharacterSheetLoadoutBodyLabel")),
            (
                Require<Label>(root, "CharacterSheetPassivesTitleLabel"),
                Require<Label>(root, "CharacterSheetPassivesBodyLabel")),
            (
                Require<Label>(root, "CharacterSheetSynergyTitleLabel"),
                Require<Label>(root, "CharacterSheetSynergyBodyLabel")),
            (
                Require<Label>(root, "CharacterSheetProgressionTitleLabel"),
                Require<Label>(root, "CharacterSheetProgressionBodyLabel")),
        };
        _deployTitleLabel = Require<Label>(root, "DeployTitleLabel");
        _deployPreviewLabel = Require<Label>(root, "DeployPreviewLabel");
        _statusLabel = Require<Label>(root, "StatusLabel");
        _primaryActionsTitleLabel = Require<Label>(root, "PrimaryActionsTitleLabel");
        _gameplayActionsTitleLabel = Require<Label>(root, "GameplayActionsTitleLabel");
        _utilityActionsTitleLabel = Require<Label>(root, "UtilityActionsTitleLabel");
        _debugActionsTitleLabel = Require<Label>(root, "DebugActionsTitleLabel");
        _debugActionsGroup = Require<VisualElement>(root, "DebugActionsGroup");
        _rerollButton = Require<Button>(root, "RerollButton");
        _saveButton = Require<Button>(root, "SaveButton");
        _loadButton = Require<Button>(root, "LoadButton");
        _returnToStartButton = Require<Button>(root, "ReturnToStartButton");
        _expeditionButton = Require<Button>(root, "ExpeditionButton");
        _quickBattleButton = Require<Button>(root, "QuickBattleButton");
        _tacticalWorkshopButton = Require<Button>(root, "TacticalWorkshopButton");
        _teamPostureButton = Require<Button>(root, "TeamPostureButton");
        _cycleHeroButton = Require<Button>(root, "CycleHeroButton");
        _cycleItemButton = Require<Button>(root, "CycleItemButton");
        _scoutButton = Require<Button>(root, "ScoutButton");
        _retrainActiveButton = Require<Button>(root, "RetrainActiveButton");
        _retrainPassiveButton = Require<Button>(root, "RetrainPassiveButton");
        _fullRetrainButton = Require<Button>(root, "FullRetrainButton");
        _dismissButton = Require<Button>(root, "DismissButton");
        _refitButton = Require<Button>(root, "RefitButton");
        _cycleBoardButton = Require<Button>(root, "CycleBoardButton");
        _cycleNodeButton = Require<Button>(root, "CycleNodeButton");
        _toggleNodeButton = Require<Button>(root, "ToggleNodeButton");
        _cyclePermanentButton = Require<Button>(root, "CyclePermanentButton");
        _equipPermanentButton = Require<Button>(root, "EquipPermanentButton");

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
        _helpButton.clicked += presenter.ToggleHelp;
        _helpDismissButton.clicked += presenter.DismissHelp;
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
        _cycleHeroButton.clicked += presenter.CycleHero;
        _cycleItemButton.clicked += presenter.CycleItem;
        _scoutButton.clicked += presenter.UseScout;
        _retrainActiveButton.clicked += presenter.RetrainFlexActive;
        _retrainPassiveButton.clicked += presenter.RetrainFlexPassive;
        _fullRetrainButton.clicked += presenter.FullRetrain;
        _dismissButton.clicked += presenter.DismissSelectedHero;
        _refitButton.clicked += presenter.RefitSelectedItem;
        _cycleBoardButton.clicked += presenter.CycleBoard;
        _cycleNodeButton.clicked += presenter.CyclePassiveNode;
        _toggleNodeButton.clicked += presenter.TogglePassiveNode;
        _cyclePermanentButton.clicked += presenter.CyclePermanentCandidate;
        _equipPermanentButton.clicked += presenter.EquipSelectedPermanentAugment;

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

    public void BindTacticalWorkshopOpen(Action open)
    {
        _tacticalWorkshopButton.clicked += open;
        _tacticalWorkshopButton.text = "Tactical Workshop";
        _tacticalWorkshopButton.tooltip = "Open the Tactical Workshop modal.";
    }

    public void Render(TownScreenViewState state)
    {
        _titleLabel.text = state.Title;
        _localeStatusLabel.text = state.LocaleStatus;
        _localeKoButton.text = state.LocaleKoLabel;
        _localeEnButton.text = state.LocaleEnLabel;
        _helpButton.text = state.HelpButtonLabel;
        _helpStrip.style.display = state.Help.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _helpBodyLabel.text = state.Help.Body;
        _helpDismissButton.text = state.Help.DismissLabel;

        _campaignTitleLabel.text = state.CampaignTitle;
        _campaignSummaryLabel.text = state.CampaignSummaryText;
        _previousChapterButton.text = state.PreviousChapterLabel;
        _previousChapterButton.SetEnabled(state.CanSelectPreviousChapter);
        _nextChapterButton.text = state.NextChapterLabel;
        _nextChapterButton.SetEnabled(state.CanSelectNextChapter);
        _previousSiteButton.text = state.PreviousSiteLabel;
        _previousSiteButton.SetEnabled(state.CanSelectPreviousSite);
        _nextSiteButton.text = state.NextSiteLabel;
        _nextSiteButton.SetEnabled(state.CanSelectNextSite);

        _economyTitleLabel.text = state.EconomyTitle;
        _currencyLabel.text = state.CurrencySummary;
        _rosterTitleLabel.text = state.RosterTitle;
        _rosterLabel.text = state.RosterText;
        _blueprintTitleLabel.text = state.BlueprintTitle;
        _blueprintSummaryLabel.text = state.BlueprintSummaryText;
        _recruitTitleLabel.text = state.RecruitTitle;
        _recruitSummaryLabel.text = state.RecruitSummaryText;
        var sheetPanels = new[]
        {
            state.CharacterSheet.Overview,
            state.CharacterSheet.Loadout,
            state.CharacterSheet.Passives,
            state.CharacterSheet.Synergy,
            state.CharacterSheet.Progression,
        };
        for (var i = 0; i < _characterSheetPanels.Count; i++)
        {
            var panel = sheetPanels[i];
            _characterSheetPanels[i].title.text = panel.Title;
            _characterSheetPanels[i].body.text = panel.Body;
        }
        _deployTitleLabel.text = state.DeployTitle;
        _deployPreviewLabel.text = state.DeployPreviewText;
        _statusLabel.text = state.StatusText;

        _teamPostureButton.text = state.TeamPostureButtonLabel;
        _teamPostureButton.tooltip = state.TeamPostureTooltip;
        _cycleHeroButton.text = state.CycleHeroLabel;
        _cycleHeroButton.SetEnabled(state.CanCycleHero);
        _cycleItemButton.text = state.CycleItemLabel;
        _cycleItemButton.SetEnabled(state.CanCycleItem);
        _scoutButton.text = state.ScoutLabel;
        _scoutButton.SetEnabled(state.CanScout);
        _retrainActiveButton.text = state.RetrainActiveLabel;
        _retrainActiveButton.SetEnabled(state.CanRetrainActive);
        _retrainPassiveButton.text = state.RetrainPassiveLabel;
        _retrainPassiveButton.SetEnabled(state.CanRetrainPassive);
        _fullRetrainButton.text = state.FullRetrainLabel;
        _fullRetrainButton.SetEnabled(state.CanFullRetrain);
        _dismissButton.text = state.DismissLabel;
        _dismissButton.SetEnabled(state.CanDismiss);
        _refitButton.text = state.RefitLabel;
        _refitButton.SetEnabled(state.CanRefit);
        _cycleBoardButton.text = state.CycleBoardLabel;
        _cycleBoardButton.SetEnabled(state.CanCycleBoard);
        _cycleNodeButton.text = state.CycleNodeLabel;
        _cycleNodeButton.SetEnabled(state.CanCycleNode);
        _toggleNodeButton.text = state.ToggleNodeLabel;
        _toggleNodeButton.SetEnabled(state.CanToggleNode);
        _cyclePermanentButton.text = state.CyclePermanentLabel;
        _cyclePermanentButton.SetEnabled(state.CanCyclePermanent);
        _equipPermanentButton.text = state.EquipPermanentLabel;
        _equipPermanentButton.SetEnabled(state.CanEquipPermanent);

        _primaryActionsTitleLabel.text = state.PrimaryActionsTitle;
        _gameplayActionsTitleLabel.text = state.GameplayActionsTitle;
        _utilityActionsTitleLabel.text = state.UtilityActionsTitle;
        _debugActionsTitleLabel.text = state.DebugActionsTitle;
        _debugActionsGroup.style.display = state.ShowDebugActions ? DisplayStyle.Flex : DisplayStyle.None;

        _rerollButton.text = state.RerollLabel;
        _saveButton.text = state.SaveLabel;
        _loadButton.text = state.LoadLabel;
        _returnToStartButton.text = state.ReturnToStartLabel;
        _returnToStartButton.tooltip = state.ReturnToStartTooltip;
        _returnToStartButton.SetEnabled(state.CanReturnToStart);
        _expeditionButton.text = state.ExpeditionActionLabel;
        _expeditionButton.tooltip = state.ExpeditionTooltip;
        _quickBattleButton.text = state.QuickBattleLabel;
        _quickBattleButton.tooltip = state.QuickBattleTooltip;
        _quickBattleButton.SetEnabled(state.CanQuickBattle);

        for (var i = 0; i < _recruitCards.Count; i++)
        {
            var cardState = i < state.RecruitCards.Count
                ? state.RecruitCards[i]
                : new TownRecruitCardViewState(string.Empty, string.Empty, string.Empty, string.Empty, false);
            _recruitCards[i].title.text = cardState.Title;
            _recruitCards[i].body.text = cardState.Body;
            _recruitCards[i].title.tooltip = cardState.Tooltip;
            _recruitCards[i].body.tooltip = cardState.Tooltip;
            _recruitCards[i].button.text = cardState.ActionLabel;
            _recruitCards[i].button.tooltip = cardState.Tooltip;
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
