using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Battle;

public readonly record struct BattleScreenActions(
    Action SelectKorean,
    Action SelectEnglish,
    Action ToggleHelp,
    Action DismissHelp,
    Action SetSpeed1,
    Action SetSpeed2,
    Action SetSpeed4,
    Action TogglePause,
    Action ContinueToReward,
    Action ReplayRecordedTimeline,
    Action RebattleNewSeed,
    Action ReturnToTownDirect,
    Action ToggleSettingsPanel,
    Action ToggleOverheadUi,
    Action ToggleDamageText,
    Action ToggleTeamSummary,
    Action ToggleDebugOverlay,
    Action ToggleSummaryPanel,
    Action<float> HandleScrubberSeek);

public sealed class BattleScreenView
{
    private readonly Label _titleLabel;
    private readonly Label _localeStatusLabel;
    private readonly Button _localeKoButton;
    private readonly Button _localeEnButton;
    private readonly Button _helpButton;
    private readonly VisualElement _helpStrip;
    private readonly Label _helpBodyLabel;
    private readonly Button _helpDismissButton;
    private readonly VisualElement _summaryPanel;
    private readonly Label _summaryTitleLabel;
    private readonly Button _summaryToggleButton;
    private readonly VisualElement _summaryBody;
    private readonly Label _allyTitleLabel;
    private readonly Label _enemyTitleLabel;
    private readonly Label _logTitleLabel;
    private readonly VisualElement _allySummaryPanel;
    private readonly VisualElement _enemySummaryPanel;
    private readonly Label _allyHpLabel;
    private readonly Label _enemyHpLabel;
    private readonly Label _logLabel;
    private readonly Label _resultLabel;
    private readonly Label _speedLabel;
    private readonly Label _statusLabel;
    private readonly VisualElement _playbackActionsGroup;
    private readonly Label _playbackGroupTitleLabel;
    private readonly Button _speed1Button;
    private readonly Button _speed2Button;
    private readonly Button _speed4Button;
    private readonly Button _pauseButton;
    private readonly Button _replayButton;
    private readonly VisualElement _continueActionsGroup;
    private readonly Label _continueGroupTitleLabel;
    private readonly Button _continueButton;
    private readonly VisualElement _smokeActionsGroup;
    private readonly Label _smokeGroupTitleLabel;
    private readonly Button _rebattleButton;
    private readonly Button _returnTownButton;
    private readonly Label _utilityGroupTitleLabel;
    private readonly Button _settingsButton;
    private readonly VisualElement _progressTrack;
    private readonly VisualElement _progressFill;
    private readonly VisualElement _settingsPanel;
    private readonly Label _settingsTitleLabel;
    private readonly Label _settingsDisplayTitleLabel;
    private readonly Button _toggleOverheadButton;
    private readonly Button _toggleDamageTextButton;
    private readonly Button _toggleTeamSummaryButton;
    private readonly VisualElement _debugSettingsSection;
    private readonly Label _settingsDebugTitleLabel;
    private readonly Button _toggleDebugOverlayButton;
    private readonly Label _settingsStatusLabel;
    private readonly VisualElement _selectedUnitPanel;
    private readonly Label _selectedUnitHeaderLabel;
    private readonly Label _selectedUnitBodyLabel;

    private Action<float>? _seekRequested;
    private bool _isDragging;
    private bool _scrubberInteractable = true;
    private int _blockingPointerDepth;

    public bool IsPointerOverBlockingUi => _blockingPointerDepth > 0;

    public BattleScreenView(VisualElement root)
    {
        root.pickingMode = PickingMode.Ignore;
        _titleLabel = Require<Label>(root, "TitleLabel");
        _localeStatusLabel = Require<Label>(root, "LocaleStatusLabel");
        _localeKoButton = Require<Button>(root, "LocaleKoButton");
        _localeEnButton = Require<Button>(root, "LocaleEnButton");
        _helpButton = Require<Button>(root, "HelpButton");
        _helpStrip = Require<VisualElement>(root, "HelpStrip");
        _helpBodyLabel = Require<Label>(root, "HelpBodyLabel");
        _helpDismissButton = Require<Button>(root, "HelpDismissButton");
        _summaryPanel = Require<VisualElement>(root, "SummaryPanel");
        _summaryTitleLabel = Require<Label>(root, "SummaryTitleLabel");
        _summaryToggleButton = Require<Button>(root, "SummaryToggleButton");
        _summaryBody = Require<VisualElement>(root, "SummaryBody");
        _allyTitleLabel = Require<Label>(root, "AllyTitleLabel");
        _enemyTitleLabel = Require<Label>(root, "EnemyTitleLabel");
        _logTitleLabel = Require<Label>(root, "LogTitleLabel");
        _allySummaryPanel = Require<VisualElement>(root, "AllySummaryPanel");
        _enemySummaryPanel = Require<VisualElement>(root, "EnemySummaryPanel");
        _allyHpLabel = Require<Label>(root, "AllyHpLabel");
        _enemyHpLabel = Require<Label>(root, "EnemyHpLabel");
        _logLabel = Require<Label>(root, "LogLabel");
        _resultLabel = Require<Label>(root, "ResultLabel");
        _speedLabel = Require<Label>(root, "SpeedLabel");
        _statusLabel = Require<Label>(root, "StatusLabel");
        _playbackActionsGroup = Require<VisualElement>(root, "PlaybackActionsGroup");
        _playbackGroupTitleLabel = Require<Label>(root, "PlaybackGroupTitleLabel");
        _speed1Button = Require<Button>(root, "Speed1Button");
        _speed2Button = Require<Button>(root, "Speed2Button");
        _speed4Button = Require<Button>(root, "Speed4Button");
        _pauseButton = Require<Button>(root, "PauseButton");
        _replayButton = Require<Button>(root, "ReplayButton");
        _continueActionsGroup = Require<VisualElement>(root, "ContinueActionsGroup");
        _continueGroupTitleLabel = Require<Label>(root, "ContinueGroupTitleLabel");
        _continueButton = Require<Button>(root, "ContinueButton");
        _smokeActionsGroup = Require<VisualElement>(root, "SmokeActionsGroup");
        _smokeGroupTitleLabel = Require<Label>(root, "SmokeGroupTitleLabel");
        _rebattleButton = Require<Button>(root, "RebattleButton");
        _returnTownButton = Require<Button>(root, "ReturnTownButton");
        _utilityGroupTitleLabel = Require<Label>(root, "UtilityGroupTitleLabel");
        _settingsButton = Require<Button>(root, "SettingsButton");
        _progressTrack = Require<VisualElement>(root, "ProgressTrack");
        _progressFill = Require<VisualElement>(root, "ProgressFill");
        _settingsPanel = Require<VisualElement>(root, "SettingsPanel");
        _settingsTitleLabel = Require<Label>(root, "SettingsTitleLabel");
        _settingsDisplayTitleLabel = Require<Label>(root, "SettingsDisplayTitleLabel");
        _toggleOverheadButton = Require<Button>(root, "ToggleOverheadButton");
        _toggleDamageTextButton = Require<Button>(root, "ToggleDamageTextButton");
        _toggleTeamSummaryButton = Require<Button>(root, "ToggleTeamSummaryButton");
        _debugSettingsSection = Require<VisualElement>(root, "DebugSettingsSection");
        _settingsDebugTitleLabel = Require<Label>(root, "SettingsDebugTitleLabel");
        _toggleDebugOverlayButton = Require<Button>(root, "ToggleDebugOverlayButton");
        _settingsStatusLabel = Require<Label>(root, "SettingsStatusLabel");
        _selectedUnitPanel = Require<VisualElement>(root, "SelectedUnitPanel");
        _selectedUnitHeaderLabel = Require<Label>(root, "SelectedUnitHeaderLabel");
        _selectedUnitBodyLabel = Require<Label>(root, "SelectedUnitBodyLabel");

        SetNonBlocking(
            _titleLabel,
            _localeStatusLabel,
            _helpStrip,
            _helpBodyLabel,
            _summaryPanel,
            _summaryTitleLabel,
            _summaryBody,
            _allyTitleLabel,
            _enemyTitleLabel,
            _logTitleLabel,
            _allySummaryPanel,
            _enemySummaryPanel,
            _allyHpLabel,
            _enemyHpLabel,
            _logLabel,
            _resultLabel,
            _speedLabel,
            _statusLabel,
            _playbackGroupTitleLabel,
            _continueGroupTitleLabel,
            _smokeGroupTitleLabel,
            _utilityGroupTitleLabel,
            _settingsPanel,
            _settingsTitleLabel,
            _settingsDisplayTitleLabel,
            _debugSettingsSection,
            _settingsDebugTitleLabel,
            _settingsStatusLabel,
            _selectedUnitPanel,
            _selectedUnitHeaderLabel,
            _selectedUnitBodyLabel);

        SetBlocking(
            _localeKoButton,
            _localeEnButton,
            _helpButton,
            _helpDismissButton,
            _summaryToggleButton,
            _speed1Button,
            _speed2Button,
            _speed4Button,
            _pauseButton,
            _replayButton,
            _continueButton,
            _rebattleButton,
            _returnTownButton,
            _settingsButton,
            _progressTrack,
            _toggleOverheadButton,
            _toggleDamageTextButton,
            _toggleTeamSummaryButton,
            _toggleDebugOverlayButton);
    }

    public void Bind(BattleScreenActions actions)
    {
        _localeKoButton.clicked += actions.SelectKorean;
        _localeEnButton.clicked += actions.SelectEnglish;
        _helpButton.clicked += actions.ToggleHelp;
        _helpDismissButton.clicked += actions.DismissHelp;
        _speed1Button.clicked += actions.SetSpeed1;
        _speed2Button.clicked += actions.SetSpeed2;
        _speed4Button.clicked += actions.SetSpeed4;
        _pauseButton.clicked += actions.TogglePause;
        _continueButton.clicked += actions.ContinueToReward;
        _replayButton.clicked += actions.ReplayRecordedTimeline;
        _rebattleButton.clicked += actions.RebattleNewSeed;
        _returnTownButton.clicked += actions.ReturnToTownDirect;
        _settingsButton.clicked += actions.ToggleSettingsPanel;
        _toggleOverheadButton.clicked += actions.ToggleOverheadUi;
        _toggleDamageTextButton.clicked += actions.ToggleDamageText;
        _toggleTeamSummaryButton.clicked += actions.ToggleTeamSummary;
        _toggleDebugOverlayButton.clicked += actions.ToggleDebugOverlay;
        _summaryToggleButton.clicked += actions.ToggleSummaryPanel;
        _seekRequested = actions.HandleScrubberSeek;

        _progressTrack.RegisterCallback<PointerDownEvent>(HandlePointerDown);
        _progressTrack.RegisterCallback<PointerMoveEvent>(HandlePointerMove);
        _progressTrack.RegisterCallback<PointerUpEvent>(HandlePointerUp);
    }

    public void Render(BattleShellViewState state)
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
        _summaryToggleButton.text = state.SummaryToggleLabel;
        _summaryToggleButton.tooltip = state.SummaryToggleTooltip;
        _summaryBody.style.display = state.IsSummaryExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        _allyTitleLabel.text = state.AllyTitle;
        _enemyTitleLabel.text = state.EnemyTitle;
        _logTitleLabel.text = state.LogTitle;
        _allyHpLabel.text = state.AllyHpText;
        _enemyHpLabel.text = state.EnemyHpText;
        _logLabel.text = state.LogText;
        _resultLabel.text = state.ResultText;
        _speedLabel.text = state.SpeedText;
        _statusLabel.text = state.StatusText;

        _playbackActionsGroup.style.display = state.ShowPlaybackControls ? DisplayStyle.Flex : DisplayStyle.None;
        _playbackGroupTitleLabel.text = state.PlaybackGroupTitle;
        _speed1Button.text = state.Speed1Label;
        _speed1Button.tooltip = state.PauseTooltip;
        _speed1Button.SetEnabled(state.CanChangeSpeed);
        _speed2Button.text = state.Speed2Label;
        _speed2Button.tooltip = state.PauseTooltip;
        _speed2Button.SetEnabled(state.CanChangeSpeed);
        _speed4Button.text = state.Speed4Label;
        _speed4Button.tooltip = state.PauseTooltip;
        _speed4Button.SetEnabled(state.CanChangeSpeed);
        _pauseButton.text = state.PauseLabel;
        _pauseButton.tooltip = state.PauseTooltip;
        _pauseButton.SetEnabled(state.CanPause);
        _replayButton.text = state.ReplayLabel;
        _replayButton.tooltip = state.ReplayTooltip;
        _replayButton.SetEnabled(state.CanReplay);

        _continueActionsGroup.style.display = state.ShowContinueAction ? DisplayStyle.Flex : DisplayStyle.None;
        _continueGroupTitleLabel.text = state.ContinueGroupTitle;
        _continueButton.text = state.ContinueLabel;
        _continueButton.tooltip = state.ContinueTooltip;
        _continueButton.SetEnabled(state.CanContinue);

        _smokeActionsGroup.style.display = state.ShowSmokeActions ? DisplayStyle.Flex : DisplayStyle.None;
        _smokeGroupTitleLabel.text = state.SmokeGroupTitle;
        _rebattleButton.text = state.RebattleLabel;
        _rebattleButton.tooltip = state.RebattleTooltip;
        _rebattleButton.SetEnabled(state.CanRebattle);
        _returnTownButton.text = state.ReturnTownLabel;
        _returnTownButton.tooltip = state.ReturnTownTooltip;
        _returnTownButton.SetEnabled(state.CanReturnTownDirect);

        _utilityGroupTitleLabel.text = state.UtilityGroupTitle;
        _settingsButton.text = state.SettingsLabel;
        _settingsButton.tooltip = state.SettingsTooltip;

        _allySummaryPanel.style.display = state.ShowTeamSummary ? DisplayStyle.Flex : DisplayStyle.None;
        _enemySummaryPanel.style.display = state.ShowTeamSummary ? DisplayStyle.Flex : DisplayStyle.None;

        _settingsPanel.style.display = state.Settings.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _settingsTitleLabel.text = state.Settings.Title;
        _settingsDisplayTitleLabel.text = state.Settings.DisplaySectionTitle;
        _toggleOverheadButton.text = state.Settings.OverheadLabel;
        _toggleOverheadButton.tooltip = state.Settings.OverheadTooltip;
        _toggleDamageTextButton.text = state.Settings.DamageTextLabel;
        _toggleDamageTextButton.tooltip = state.Settings.DamageTextTooltip;
        _toggleTeamSummaryButton.text = state.Settings.TeamSummaryLabel;
        _toggleTeamSummaryButton.tooltip = state.Settings.TeamSummaryTooltip;
        _debugSettingsSection.style.display = state.Settings.ShowDebugSection ? DisplayStyle.Flex : DisplayStyle.None;
        _settingsDebugTitleLabel.text = state.Settings.DebugSectionTitle;
        _toggleDebugOverlayButton.text = state.Settings.DebugOverlayLabel;
        _toggleDebugOverlayButton.tooltip = state.Settings.DebugOverlayTooltip;
        _settingsStatusLabel.text = state.Settings.StatusText;

        var selectedUnit = state.SelectedUnit ?? BattleSelectedUnitViewState.Hidden;
        _selectedUnitPanel.style.display = selectedUnit.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _selectedUnitHeaderLabel.text = selectedUnit.Header;
        _selectedUnitBodyLabel.text = selectedUnit.Body;

        if (!_isDragging)
        {
            SetProgress(state.ProgressNormalized);
        }
    }

    public void SetScrubberInteractable(bool interactable)
    {
        _scrubberInteractable = interactable;
        _progressTrack.pickingMode = interactable ? PickingMode.Position : PickingMode.Ignore;
    }

    public void SetProgress(float normalized)
    {
        if (_isDragging)
        {
            return;
        }

        _progressFill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
    }

    private void HandlePointerDown(PointerDownEvent evt)
    {
        if (!_scrubberInteractable)
        {
            return;
        }

        _isDragging = true;
        _progressTrack.CapturePointer(evt.pointerId);
        UpdateSeek(evt.position);
    }

    private void HandlePointerMove(PointerMoveEvent evt)
    {
        if (!_scrubberInteractable || !_isDragging)
        {
            return;
        }

        UpdateSeek(evt.position);
    }

    private void HandlePointerUp(PointerUpEvent evt)
    {
        if (_progressTrack.HasPointerCapture(evt.pointerId))
        {
            _progressTrack.ReleasePointer(evt.pointerId);
        }

        _isDragging = false;
    }

    private void UpdateSeek(Vector2 pointerPosition)
    {
        var rect = _progressTrack.worldBound;
        if (rect.width <= 0f)
        {
            return;
        }

        var normalized = Mathf.Clamp01((pointerPosition.x - rect.xMin) / rect.width);
        _progressFill.style.width = Length.Percent(normalized * 100f);
        _seekRequested?.Invoke(normalized);
    }

    private void SetNonBlocking(params VisualElement[] elements)
    {
        foreach (var element in elements)
        {
            element.pickingMode = PickingMode.Ignore;
        }
    }

    private void SetBlocking(params VisualElement[] elements)
    {
        foreach (var element in elements)
        {
            element.pickingMode = PickingMode.Position;
            element.RegisterCallback<PointerEnterEvent>(_ => _blockingPointerDepth++);
            element.RegisterCallback<PointerLeaveEvent>(_ => _blockingPointerDepth = Math.Max(0, _blockingPointerDepth - 1));
        }
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
