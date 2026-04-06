using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Battle;

public readonly record struct BattleScreenActions(
    Action SelectKorean,
    Action SelectEnglish,
    Action SetSpeed1,
    Action SetSpeed2,
    Action SetSpeed4,
    Action TogglePause,
    Action ContinueToReward,
    Action RebattleNewSeed,
    Action ReturnToTownDirect,
    Action ToggleSettingsPanel,
    Action ToggleOverheadUi,
    Action ToggleDamageText,
    Action ToggleTeamSummary,
    Action ToggleDebugOverlay,
    Action<float> HandleScrubberSeek);

public sealed class BattleScreenView
{
    private readonly Label _titleLabel;
    private readonly Label _localeStatusLabel;
    private readonly Button _localeKoButton;
    private readonly Button _localeEnButton;
    private readonly VisualElement _allySummaryPanel;
    private readonly VisualElement _enemySummaryPanel;
    private readonly Label _allyHpLabel;
    private readonly Label _enemyHpLabel;
    private readonly Label _logLabel;
    private readonly Label _resultLabel;
    private readonly Label _speedLabel;
    private readonly Label _statusLabel;
    private readonly Button _speed1Button;
    private readonly Button _speed2Button;
    private readonly Button _speed4Button;
    private readonly Button _pauseButton;
    private readonly Button _continueButton;
    private readonly Button _rebattleButton;
    private readonly Button _returnTownButton;
    private readonly Button _settingsButton;
    private readonly VisualElement _progressTrack;
    private readonly VisualElement _progressFill;
    private readonly VisualElement _settingsPanel;
    private readonly Button _toggleOverheadButton;
    private readonly Button _toggleDamageTextButton;
    private readonly Button _toggleTeamSummaryButton;
    private readonly Button _toggleDebugOverlayButton;
    private readonly Label _settingsStatusLabel;

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
        _allySummaryPanel = Require<VisualElement>(root, "AllySummaryPanel");
        _enemySummaryPanel = Require<VisualElement>(root, "EnemySummaryPanel");
        _allyHpLabel = Require<Label>(root, "AllyHpLabel");
        _enemyHpLabel = Require<Label>(root, "EnemyHpLabel");
        _logLabel = Require<Label>(root, "LogLabel");
        _resultLabel = Require<Label>(root, "ResultLabel");
        _speedLabel = Require<Label>(root, "SpeedLabel");
        _statusLabel = Require<Label>(root, "StatusLabel");
        _speed1Button = Require<Button>(root, "Speed1Button");
        _speed2Button = Require<Button>(root, "Speed2Button");
        _speed4Button = Require<Button>(root, "Speed4Button");
        _pauseButton = Require<Button>(root, "PauseButton");
        _continueButton = Require<Button>(root, "ContinueButton");
        _rebattleButton = Require<Button>(root, "RebattleButton");
        _returnTownButton = Require<Button>(root, "ReturnTownButton");
        _settingsButton = Require<Button>(root, "SettingsButton");
        _progressTrack = Require<VisualElement>(root, "ProgressTrack");
        _progressFill = Require<VisualElement>(root, "ProgressFill");
        _settingsPanel = Require<VisualElement>(root, "SettingsPanel");
        _toggleOverheadButton = Require<Button>(root, "ToggleOverheadButton");
        _toggleDamageTextButton = Require<Button>(root, "ToggleDamageTextButton");
        _toggleTeamSummaryButton = Require<Button>(root, "ToggleTeamSummaryButton");
        _toggleDebugOverlayButton = Require<Button>(root, "ToggleDebugOverlayButton");
        _settingsStatusLabel = Require<Label>(root, "SettingsStatusLabel");

        SetNonBlocking(_titleLabel, _localeStatusLabel, _allySummaryPanel, _enemySummaryPanel, _allyHpLabel, _enemyHpLabel, _logLabel, _resultLabel, _speedLabel, _statusLabel, _settingsPanel, _settingsStatusLabel);
        SetBlocking(_localeKoButton, _localeEnButton, _speed1Button, _speed2Button, _speed4Button, _pauseButton, _continueButton, _rebattleButton, _returnTownButton, _settingsButton, _progressTrack, _toggleOverheadButton, _toggleDamageTextButton, _toggleTeamSummaryButton, _toggleDebugOverlayButton);
    }

    public void Bind(BattleScreenActions actions)
    {
        _localeKoButton.clicked += actions.SelectKorean;
        _localeEnButton.clicked += actions.SelectEnglish;
        _speed1Button.clicked += actions.SetSpeed1;
        _speed2Button.clicked += actions.SetSpeed2;
        _speed4Button.clicked += actions.SetSpeed4;
        _pauseButton.clicked += actions.TogglePause;
        _continueButton.clicked += actions.ContinueToReward;
        _rebattleButton.clicked += actions.RebattleNewSeed;
        _returnTownButton.clicked += actions.ReturnToTownDirect;
        _settingsButton.clicked += actions.ToggleSettingsPanel;
        _toggleOverheadButton.clicked += actions.ToggleOverheadUi;
        _toggleDamageTextButton.clicked += actions.ToggleDamageText;
        _toggleTeamSummaryButton.clicked += actions.ToggleTeamSummary;
        _toggleDebugOverlayButton.clicked += actions.ToggleDebugOverlay;
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
        _allyHpLabel.text = state.AllyHpText;
        _enemyHpLabel.text = state.EnemyHpText;
        _logLabel.text = state.LogText;
        _resultLabel.text = state.ResultText;
        _speedLabel.text = state.SpeedText;
        _statusLabel.text = state.StatusText;
        _pauseButton.text = state.PauseLabel;
        _continueButton.text = state.ContinueLabel;
        _rebattleButton.text = state.RebattleLabel;
        _returnTownButton.text = state.ReturnTownLabel;
        _settingsButton.text = state.SettingsLabel;
        _continueButton.SetEnabled(state.CanContinue);
        _allySummaryPanel.style.display = state.ShowTeamSummary ? DisplayStyle.Flex : DisplayStyle.None;
        _enemySummaryPanel.style.display = state.ShowTeamSummary ? DisplayStyle.Flex : DisplayStyle.None;
        _settingsPanel.style.display = state.Settings.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _toggleOverheadButton.text = state.Settings.OverheadLabel;
        _toggleDamageTextButton.text = state.Settings.DamageTextLabel;
        _toggleTeamSummaryButton.text = state.Settings.TeamSummaryLabel;
        _toggleDebugOverlayButton.text = state.Settings.DebugOverlayLabel;
        _settingsStatusLabel.text = state.Settings.StatusText;

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
