using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
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
    Action<float> HandleScrubberSeek,
    Action<string> SelectRosterUnit,
    Action<string> OpenRosterUnitDetail,
    Action CloseUnitDetail,
    Action<BattleUnitDetailTab> SelectUnitDetailTab);

public sealed class BattleScreenView
{
    private const float AllyRosterPortraitWidth = 128f;
    private const float AllyRosterPortraitHeight = 152f;
    private const float EnemyRosterPortraitWidth = 64f;
    private const float EnemyRosterPortraitHeight = 76f;

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
    private readonly VisualElement _allyRosterList;
    private readonly VisualElement _enemyRosterList;
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
    private readonly Image _selectedUnitPortraitImage;
    private readonly Label _selectedUnitHeaderLabel;
    private readonly Button _unitDetailCloseButton;
    private readonly Button _unitDetailOverviewTab;
    private readonly Button _unitDetailStatsTab;
    private readonly Button _unitDetailSkillsTab;
    private readonly Button _unitDetailEquipmentTab;
    private readonly Button _unitDetailTacticTab;
    private readonly Button _unitDetailStatusTab;
    private readonly Button _unitDetailRecordTab;
    private readonly VisualElement _selectedUnitAilmentTint;
    private readonly VisualElement _selectedUnitHpFill;
    private readonly VisualElement _selectedUnitShieldFill;
    private readonly VisualElement _unitDetailOverviewContent;
    private readonly VisualElement _unitDetailStatsContent;
    private readonly VisualElement _unitDetailSkillsContent;
    private readonly VisualElement _unitDetailEquipmentContent;
    private readonly VisualElement _unitDetailTacticContent;
    private readonly VisualElement _unitDetailStatusContent;
    private readonly VisualElement _unitDetailRecordContent;
    private readonly VisualElement _overviewCoreStats;
    private readonly VisualElement _overviewFormationGrid;
    private readonly VisualElement _overviewTacticDials;
    private readonly VisualElement _statsList;
    private readonly Label _selectedUnitBodyLabel;
    private readonly VisualElement _skillPresentationSlots;
    private readonly VisualElement _equipmentSlots;
    private readonly VisualElement _tacticDials;
    private readonly VisualElement _tacticPriorityList;
    private readonly Label _statusPermanentTitle;
    private readonly VisualElement _statusPermanentGrid;
    private readonly Label _statusBattleScopedTitle;
    private readonly VisualElement _statusBattleGrid;

    private Action<float>? _seekRequested;
    private BattleScreenActions? _actions;
    private bool _isDragging;
    private bool _scrubberInteractable = true;
    private int _blockingPointerDepth;
    private int _rosterPointerDepth;
    private int _pointerBlockFrame = -1;
    private int _unitDetailSwipePointerId = -1;
    private float _unitDetailSwipeStartY;

    public bool IsPointerOverBlockingUi => _blockingPointerDepth > 0
                                           || _rosterPointerDepth > 0
                                           || _pointerBlockFrame == Time.frameCount;

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
        _allyRosterList = Require<VisualElement>(root, "AllyRosterList");
        _enemyRosterList = Require<VisualElement>(root, "EnemyRosterList");
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
        _selectedUnitPortraitImage = Require<Image>(root, "SelectedUnitPortraitImage");
        _selectedUnitHeaderLabel = Require<Label>(root, "SelectedUnitHeaderLabel");
        _unitDetailCloseButton = Require<Button>(root, "UnitDetailCloseButton");
        _unitDetailOverviewTab = Require<Button>(root, "UnitDetailOverviewTab");
        _unitDetailStatsTab = Require<Button>(root, "UnitDetailStatsTab");
        _unitDetailSkillsTab = Require<Button>(root, "UnitDetailSkillsTab");
        _unitDetailEquipmentTab = Require<Button>(root, "UnitDetailEquipmentTab");
        _unitDetailTacticTab = Require<Button>(root, "UnitDetailTacticTab");
        _unitDetailStatusTab = Require<Button>(root, "UnitDetailStatusTab");
        _unitDetailRecordTab = Require<Button>(root, "UnitDetailRecordTab");
        _selectedUnitAilmentTint = Require<VisualElement>(root, "SelectedUnitAilmentTint");
        _selectedUnitHpFill = Require<VisualElement>(root, "SelectedUnitHpFill");
        _selectedUnitShieldFill = Require<VisualElement>(root, "SelectedUnitShieldFill");
        _unitDetailOverviewContent = Require<VisualElement>(root, "UnitDetailOverviewContent");
        _unitDetailStatsContent = Require<VisualElement>(root, "UnitDetailStatsContent");
        _unitDetailSkillsContent = Require<VisualElement>(root, "UnitDetailSkillsContent");
        _unitDetailEquipmentContent = Require<VisualElement>(root, "UnitDetailEquipmentContent");
        _unitDetailTacticContent = Require<VisualElement>(root, "UnitDetailTacticContent");
        _unitDetailStatusContent = Require<VisualElement>(root, "UnitDetailStatusContent");
        _unitDetailRecordContent = Require<VisualElement>(root, "UnitDetailRecordContent");
        _overviewCoreStats = Require<VisualElement>(root, "OverviewCoreStats");
        _overviewFormationGrid = Require<VisualElement>(root, "OverviewFormationGrid");
        _overviewTacticDials = Require<VisualElement>(root, "OverviewTacticDials");
        _statsList = Require<VisualElement>(root, "StatsList");
        _selectedUnitBodyLabel = Require<Label>(root, "SelectedUnitBodyLabel");
        _skillPresentationSlots = Require<VisualElement>(root, "SkillPresentationSlots");
        _equipmentSlots = Require<VisualElement>(root, "EquipmentSlots");
        _tacticDials = Require<VisualElement>(root, "TacticDials");
        _tacticPriorityList = Require<VisualElement>(root, "TacticPriorityList");
        _statusPermanentTitle = Require<Label>(root, "StatusPermanentTitle");
        _statusPermanentGrid = Require<VisualElement>(root, "StatusPermanentGrid");
        _statusBattleScopedTitle = Require<Label>(root, "StatusBattleScopedTitle");
        _statusBattleGrid = Require<VisualElement>(root, "StatusBattleGrid");
        _selectedUnitPortraitImage.scaleMode = ScaleMode.ScaleAndCrop;
        _unitDetailCloseButton.text = "X";

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
            _allyRosterList,
            _enemyRosterList,
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
            _selectedUnitPortraitImage,
            _selectedUnitHeaderLabel,
            _selectedUnitAilmentTint,
            _selectedUnitHpFill,
            _selectedUnitShieldFill,
            _unitDetailOverviewContent,
            _unitDetailStatsContent,
            _unitDetailSkillsContent,
            _unitDetailEquipmentContent,
            _unitDetailTacticContent,
            _unitDetailStatusContent,
            _unitDetailRecordContent,
            _overviewCoreStats,
            _overviewFormationGrid,
            _overviewTacticDials,
            _statsList,
            _selectedUnitBodyLabel,
            _skillPresentationSlots,
            _equipmentSlots,
            _tacticDials,
            _tacticPriorityList,
            _statusPermanentTitle,
            _statusPermanentGrid,
            _statusBattleScopedTitle,
            _statusBattleGrid);

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
            _settingsPanel,
            _selectedUnitPanel,
            _unitDetailCloseButton,
            _unitDetailOverviewTab,
            _unitDetailStatsTab,
            _unitDetailSkillsTab,
            _unitDetailEquipmentTab,
            _unitDetailTacticTab,
            _unitDetailStatusTab,
            _unitDetailRecordTab,
            _toggleOverheadButton,
            _toggleDamageTextButton,
            _toggleTeamSummaryButton,
            _toggleDebugOverlayButton);
    }

    public void Bind(BattleScreenActions actions)
    {
        _actions = actions;
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
        _unitDetailCloseButton.clicked += actions.CloseUnitDetail;
        _unitDetailOverviewTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Overview);
        _unitDetailStatsTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Stats);
        _unitDetailSkillsTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Skills);
        _unitDetailEquipmentTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Equipment);
        _unitDetailTacticTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Tactic);
        _unitDetailStatusTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Status);
        _unitDetailRecordTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Record);
        _seekRequested = actions.HandleScrubberSeek;

        _progressTrack.RegisterCallback<PointerDownEvent>(HandlePointerDown);
        _progressTrack.RegisterCallback<PointerMoveEvent>(HandlePointerMove);
        _progressTrack.RegisterCallback<PointerUpEvent>(HandlePointerUp);
        _selectedUnitPanel.RegisterCallback<PointerDownEvent>(HandleUnitDetailPointerDown);
        _selectedUnitPanel.RegisterCallback<PointerMoveEvent>(HandleUnitDetailPointerMove);
        _selectedUnitPanel.RegisterCallback<PointerUpEvent>(HandleUnitDetailPointerUp);
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
        RenderRoster(_allyRosterList, state.AllyRoster, isEnemy: false);
        RenderRoster(_enemyRosterList, state.EnemyRoster, isEnemy: true);

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
        _selectedUnitPanel.EnableInClassList("sm-bs-unit-detail-backdrop--mobile", IsNarrowDetailViewport());
        var illustration = selectedUnit.FullBodyPortrait != null ? selectedUnit.FullBodyPortrait : selectedUnit.Portrait;
        _selectedUnitPortraitImage.image = illustration;
        _selectedUnitPortraitImage.style.display = illustration != null ? DisplayStyle.Flex : DisplayStyle.None;
        _selectedUnitAilmentTint.style.display = selectedUnit.HasAilmentTint ? DisplayStyle.Flex : DisplayStyle.None;
        _selectedUnitHpFill.style.width = Length.Percent(Mathf.Clamp01(selectedUnit.HealthNormalized) * 100f);
        _selectedUnitShieldFill.style.width = Length.Percent(Mathf.Clamp01(selectedUnit.ShieldNormalized) * 100f);
        _selectedUnitHeaderLabel.text = selectedUnit.Header;
        _unitDetailOverviewTab.text = selectedUnit.OverviewTabLabel;
        _unitDetailStatsTab.text = selectedUnit.StatsTabLabel;
        _unitDetailSkillsTab.text = selectedUnit.SkillsTabLabel;
        _unitDetailEquipmentTab.text = selectedUnit.EquipmentTabLabel;
        _unitDetailTacticTab.text = selectedUnit.TacticTabLabel;
        _unitDetailStatusTab.text = selectedUnit.StatusTabLabel;
        _unitDetailRecordTab.text = selectedUnit.RecordTabLabel;
        UpdateDetailTab(_unitDetailOverviewTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Overview);
        UpdateDetailTab(_unitDetailStatsTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Stats);
        UpdateDetailTab(_unitDetailSkillsTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Skills);
        UpdateDetailTab(_unitDetailEquipmentTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Equipment);
        UpdateDetailTab(_unitDetailTacticTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Tactic);
        UpdateDetailTab(_unitDetailStatusTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Status);
        UpdateDetailTab(_unitDetailRecordTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Record);
        RenderUnitDetail(selectedUnit);

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

    private void RenderRoster(VisualElement container, IReadOnlyList<BattleRosterUnitViewState>? roster, bool isEnemy)
    {
        _rosterPointerDepth = 0;
        container.Clear();
        if (roster == null)
        {
            return;
        }

        foreach (var unit in roster)
        {
            var row = new VisualElement();
            row.AddToClassList("sm-bs-roster-unit");
            row.AddToClassList(isEnemy ? "sm-bs-roster-unit--enemy" : "sm-bs-roster-unit--ally");
            row.EnableInClassList("sm-bs-roster-unit--selected", unit.IsSelected);
            row.EnableInClassList("sm-bs-roster-unit--down", !unit.IsAlive);
            SetRosterBlocking(row);
            row.RegisterCallback<PointerDownEvent>(evt => HandleRosterPointerDown(evt, unit.UnitId));

            if (unit.Portrait != null)
            {
                row.Add(BuildRosterPortrait(unit.Portrait, isEnemy));
            }
            else
            {
                var fallback = new VisualElement();
                fallback.AddToClassList("sm-bs-roster-portrait");
                fallback.AddToClassList("sm-bs-roster-portrait--missing");
                var initial = new Label(BuildInitial(unit.DisplayName));
                initial.AddToClassList("sm-bs-roster-portrait-initial");
                fallback.Add(initial);
                row.Add(fallback);
            }

            var meta = new VisualElement();
            meta.AddToClassList("sm-bs-roster-meta");

            var name = new Label(unit.DisplayName);
            name.AddToClassList("sm-bs-roster-name");
            meta.Add(name);

            var status = new Label(unit.StatusText);
            status.AddToClassList("sm-bs-roster-status");
            meta.Add(status);

            var track = new VisualElement();
            track.AddToClassList("sm-bs-roster-hp-track");
            var fill = new VisualElement();
            fill.AddToClassList("sm-bs-roster-hp-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(unit.HealthNormalized) * 100f);
            track.Add(fill);
            meta.Add(track);

            row.Add(meta);
            container.Add(row);
        }
    }

    private void HandleRosterPointerDown(PointerDownEvent evt, string unitId)
    {
        if (evt.button != 0 || string.IsNullOrWhiteSpace(unitId))
        {
            return;
        }

        _pointerBlockFrame = Time.frameCount;
        if (evt.clickCount >= 2)
        {
            _actions?.OpenRosterUnitDetail(unitId);
        }
        else
        {
            _actions?.SelectRosterUnit(unitId);
        }

        evt.StopPropagation();
    }

    private void RenderUnitDetail(BattleSelectedUnitViewState selectedUnit)
    {
        _unitDetailOverviewContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Overview ? DisplayStyle.Flex : DisplayStyle.None;
        _unitDetailStatsContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Stats ? DisplayStyle.Flex : DisplayStyle.None;
        _unitDetailSkillsContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Skills ? DisplayStyle.Flex : DisplayStyle.None;
        _unitDetailEquipmentContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Equipment ? DisplayStyle.Flex : DisplayStyle.None;
        _unitDetailTacticContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Tactic ? DisplayStyle.Flex : DisplayStyle.None;
        _unitDetailStatusContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Status ? DisplayStyle.Flex : DisplayStyle.None;
        _unitDetailRecordContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Record ? DisplayStyle.Flex : DisplayStyle.None;

        RenderOverview(selectedUnit);
        RenderStats(selectedUnit.StatLines);
        RenderSkillSlots(selectedUnit.SkillSlots);
        RenderEquipment(selectedUnit.EquipmentSlots);
        RenderTactic(selectedUnit.TacticSummary);
        RenderStatusEffects(selectedUnit.StatusEffects);
        _selectedUnitBodyLabel.text = string.IsNullOrWhiteSpace(selectedUnit.CombatRecordBody)
            ? selectedUnit.Body
            : selectedUnit.CombatRecordBody;
    }

    private bool IsNarrowDetailViewport()
    {
        var resolvedWidth = _selectedUnitPanel.resolvedStyle.width;
        return (resolvedWidth > 1f && resolvedWidth < 900f) || Screen.width < 900;
    }

    private void RenderOverview(BattleSelectedUnitViewState selectedUnit)
    {
        var overviewLines = (selectedUnit.StatLines ?? Array.Empty<BattleStatLine>())
            .Where(line => line.Category is BattleStatLineCategory.Vital or BattleStatLineCategory.Combat or BattleStatLineCategory.Movement)
            .Take(8)
            .ToArray();
        RenderStatGrid(_overviewCoreStats, overviewLines, compact: true);
        RenderFormationGrid(_overviewFormationGrid, selectedUnit.PositionSummary);
        RenderDialGrid(_overviewTacticDials, selectedUnit.TacticSummary?.Dials?.Where(IsOverviewDial).Take(4).ToArray());
    }

    private void RenderStats(IReadOnlyList<BattleStatLine>? statLines)
    {
        _statsList.Clear();
        if (statLines == null || statLines.Count == 0)
        {
            _statsList.Add(BuildEmptyLine());
            return;
        }

        foreach (var group in statLines.GroupBy(line => line.Category).OrderBy(group => group.Key))
        {
            var title = new Label(group.Key.ToString());
            title.AddToClassList("sm-bs-stat-category");
            _statsList.Add(title);
            RenderStatGrid(_statsList, group.ToArray(), compact: false);
        }
    }

    private static bool IsOverviewDial(BattleTacticDial dial)
    {
        return dial.Label.IndexOf("Compact", StringComparison.OrdinalIgnoreCase) >= 0
               || dial.Label.IndexOf("밀집", StringComparison.OrdinalIgnoreCase) >= 0
               || dial.Label.IndexOf("Width", StringComparison.OrdinalIgnoreCase) >= 0
               || dial.Label.IndexOf("폭", StringComparison.OrdinalIgnoreCase) >= 0
               || dial.Label.IndexOf("Depth", StringComparison.OrdinalIgnoreCase) >= 0
               || dial.Label.IndexOf("깊", StringComparison.OrdinalIgnoreCase) >= 0
               || dial.Label.IndexOf("Flank", StringComparison.OrdinalIgnoreCase) >= 0
               || dial.Label.IndexOf("측면", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RenderStatGrid(VisualElement container, IReadOnlyList<BattleStatLine>? lines, bool compact)
    {
        container.Clear();
        if (lines == null || lines.Count == 0)
        {
            container.Add(BuildEmptyLine());
            return;
        }

        foreach (var line in lines)
        {
            var row = new VisualElement();
            row.AddToClassList("sm-bs-stat-row");
            row.EnableInClassList("sm-bs-stat-row--compact", compact);
            row.tooltip = line.Tooltip;
            var label = new Label(line.Label);
            label.AddToClassList("sm-bs-stat-label");
            var value = new Label(line.Value);
            value.AddToClassList("sm-bs-stat-value");
            row.Add(label);
            row.Add(value);
            container.Add(row);
        }
    }

    private void RenderFormationGrid(VisualElement container, BattlePositionSummary? position)
    {
        container.Clear();
        var occupied = new HashSet<DeploymentAnchorId>(position?.TeammateAnchors ?? Array.Empty<DeploymentAnchorId>());
        foreach (var anchor in OrderedAnchors())
        {
            var cell = new Label(FormatAnchorShort(anchor));
            cell.AddToClassList("sm-bs-anchor-cell");
            cell.EnableInClassList("sm-bs-anchor-cell--occupied", occupied.Contains(anchor));
            cell.EnableInClassList("sm-bs-anchor-cell--home", position != null && position.HomeAnchor == anchor);
            cell.tooltip = anchor.ToDisplayName();
            container.Add(cell);
        }
    }

    private static IEnumerable<DeploymentAnchorId> OrderedAnchors()
    {
        yield return DeploymentAnchorId.FrontTop;
        yield return DeploymentAnchorId.FrontCenter;
        yield return DeploymentAnchorId.FrontBottom;
        yield return DeploymentAnchorId.BackTop;
        yield return DeploymentAnchorId.BackCenter;
        yield return DeploymentAnchorId.BackBottom;
    }

    private static string FormatAnchorShort(DeploymentAnchorId anchor)
    {
        return anchor switch
        {
            DeploymentAnchorId.FrontTop => "FT",
            DeploymentAnchorId.FrontCenter => "FC",
            DeploymentAnchorId.FrontBottom => "FB",
            DeploymentAnchorId.BackTop => "BT",
            DeploymentAnchorId.BackCenter => "BC",
            DeploymentAnchorId.BackBottom => "BB",
            _ => "?"
        };
    }

    private static void UpdateDetailTab(Button button, bool isActive)
    {
        button.EnableInClassList("sm-bs-unit-detail-tab--active", isActive);
    }

    private static VisualElement BuildRosterPortrait(Texture2D texture, bool isEnemy)
    {
        var frameWidth = isEnemy ? EnemyRosterPortraitWidth : AllyRosterPortraitWidth;
        var frameHeight = isEnemy ? EnemyRosterPortraitHeight : AllyRosterPortraitHeight;
        var frame = new VisualElement();
        frame.AddToClassList("sm-bs-roster-portrait");

        var image = new Image
        {
            image = texture,
            scaleMode = ScaleMode.StretchToFill,
            pickingMode = PickingMode.Ignore
        };
        image.AddToClassList("sm-bs-roster-portrait-image");
        ApplyCoverFit(image, texture, frameWidth, frameHeight);
        frame.Add(image);
        return frame;
    }

    private static void ApplyCoverFit(Image image, Texture texture, float frameWidth, float frameHeight)
    {
        var sourceAspect = texture.width > 0 && texture.height > 0
            ? texture.width / (float)texture.height
            : 1f;
        var frameAspect = frameWidth / frameHeight;
        var renderWidth = frameWidth;
        var renderHeight = frameHeight;
        if (sourceAspect > frameAspect)
        {
            renderWidth = frameHeight * sourceAspect;
        }
        else
        {
            renderHeight = frameWidth / sourceAspect;
        }

        image.style.width = renderWidth;
        image.style.height = renderHeight;
        image.style.left = (frameWidth - renderWidth) * 0.5f;
        image.style.top = (frameHeight - renderHeight) * 0.5f;
    }

    private void RenderSkillSlots(IReadOnlyList<BattleSkillSlotViewState>? slots)
    {
        _skillPresentationSlots.Clear();
        if (slots == null || slots.Count == 0)
        {
            _skillPresentationSlots.Add(BuildEmptyLine());
            return;
        }

        for (var i = 0; i < slots.Count; i++)
        {
            var state = slots[i];
            var slot = new VisualElement();
            slot.AddToClassList("sm-bs-skill-slot");
            slot.EnableInClassList("sm-bs-skill-slot--signature", i is 0 or 2);
            slot.EnableInClassList("sm-bs-skill-slot--flex", i is 1 or 3);
            slot.EnableInClassList("sm-bs-skill-slot--missing", state.Icon == null);
            slot.tooltip = string.IsNullOrWhiteSpace(state.SkillId) ? state.SkillName : state.SkillId;

            if (state.Icon != null)
            {
                var icon = new Image
                {
                    image = state.Icon,
                    scaleMode = ScaleMode.ScaleAndCrop,
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("sm-bs-skill-icon");
                slot.Add(icon);
            }
            else
            {
                var fallback = new Label(BuildInitial(state.SkillName));
                fallback.AddToClassList("sm-bs-skill-icon");
                fallback.AddToClassList("sm-bs-skill-icon--missing");
                slot.Add(fallback);
            }

            var label = new Label($"{state.SlotLabel}\n{state.SkillName}");
            label.AddToClassList("sm-bs-skill-label");
            slot.Add(label);
            _skillPresentationSlots.Add(slot);
        }
    }

    private void RenderEquipment(IReadOnlyList<BattleEquipmentSlotViewState>? slots)
    {
        _equipmentSlots.Clear();
        if (slots == null || slots.Count == 0)
        {
            _equipmentSlots.Add(BuildEmptyLine());
            return;
        }

        foreach (var slotState in slots)
        {
            var slot = new VisualElement();
            slot.AddToClassList("sm-bs-equipment-slot");
            slot.EnableInClassList("sm-bs-equipment-slot--placeholder", slotState.IsPlaceholder);
            var label = new Label(slotState.SlotLabel);
            label.AddToClassList("sm-bs-equipment-label");
            var item = new Label(slotState.ItemName);
            item.AddToClassList("sm-bs-equipment-item");
            slot.Add(label);
            slot.Add(item);
            _equipmentSlots.Add(slot);
        }
    }

    private void RenderTactic(BattleTacticSummary? summary)
    {
        _tacticDials.Clear();
        _tacticPriorityList.Clear();
        if (summary == null)
        {
            _tacticDials.Add(BuildEmptyLine());
            return;
        }

        var header = new Label(summary.PresetName);
        header.AddToClassList("sm-bs-tactic-header");
        _tacticPriorityList.Add(header);
        if (!string.IsNullOrWhiteSpace(summary.RoleInstruction))
        {
            _tacticPriorityList.Add(BuildTacticLine("Role", summary.RoleInstruction));
        }

        if (!string.IsNullOrWhiteSpace(summary.ArchetypeQuirk))
        {
            _tacticPriorityList.Add(BuildTacticLine("Archetype", summary.ArchetypeQuirk));
        }

        foreach (var rule in summary.PriorityRules ?? Array.Empty<string>())
        {
            _tacticPriorityList.Add(BuildTacticLine("*", rule));
        }

        RenderDialGrid(_tacticDials, summary.Dials);
    }

    private static VisualElement BuildTacticLine(string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-bs-tactic-row");
        var key = new Label(label);
        key.AddToClassList("sm-bs-tactic-key");
        var body = new Label(value);
        body.AddToClassList("sm-bs-tactic-value");
        row.Add(key);
        row.Add(body);
        return row;
    }

    private void RenderStatusEffects(IReadOnlyList<BattleStatusEffectChip>? chips)
    {
        _statusPermanentTitle.text = "Permanent";
        _statusBattleScopedTitle.text = "Battle Scoped";
        _statusPermanentGrid.Clear();
        _statusBattleGrid.Clear();
        var permanent = chips?.Where(chip => chip.Section == BattleStatusEffectSection.Permanent).ToArray() ?? Array.Empty<BattleStatusEffectChip>();
        var battleScoped = chips?.Where(chip => chip.Section == BattleStatusEffectSection.BattleScoped).ToArray() ?? Array.Empty<BattleStatusEffectChip>();
        RenderStatusChipGrid(_statusPermanentGrid, permanent);
        RenderStatusChipGrid(_statusBattleGrid, battleScoped);
    }

    private static void RenderStatusChipGrid(VisualElement container, IReadOnlyList<BattleStatusEffectChip> chips)
    {
        if (chips.Count == 0)
        {
            container.Add(BuildEmptyLine());
            return;
        }

        foreach (var chip in chips)
        {
            var element = new VisualElement();
            element.AddToClassList("sm-bs-status-chip");
            element.EnableInClassList("sm-bs-status-chip--permanent", chip.Section == BattleStatusEffectSection.Permanent);
            element.tooltip = $"{chip.Label}\n{chip.SourceActorName}";

            if (chip.Icon != null)
            {
                var icon = new Image
                {
                    image = chip.Icon,
                    scaleMode = ScaleMode.ScaleAndCrop,
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("sm-bs-status-chip-icon");
                element.Add(icon);
            }
            else
            {
                var fallback = new Label(BuildInitial(chip.Label));
                fallback.AddToClassList("sm-bs-status-chip-icon");
                fallback.AddToClassList("sm-bs-status-chip-icon--missing");
                element.Add(fallback);
            }

            var label = new Label(chip.Label);
            label.AddToClassList("sm-bs-status-chip-label");
            element.Add(label);

            if (chip.StackCount > 1)
            {
                var stack = new Label(chip.StackCount.ToString());
                stack.AddToClassList("sm-bs-status-chip-stack");
                element.Add(stack);
            }

            var ring = new VisualElement();
            ring.AddToClassList("sm-bs-status-chip-ring");
            ring.style.width = Length.Percent(chip.MaxDurationSeconds > 0.01f
                ? Mathf.Clamp01(chip.RemainingSeconds / chip.MaxDurationSeconds) * 100f
                : 100f);
            element.Add(ring);
            container.Add(element);
        }
    }

    private static void RenderDialGrid(VisualElement container, IReadOnlyList<BattleTacticDial>? dials)
    {
        container.Clear();
        if (dials == null || dials.Count == 0)
        {
            container.Add(BuildEmptyLine());
            return;
        }

        foreach (var dial in dials)
        {
            var row = new VisualElement();
            row.AddToClassList("sm-bs-dial-row");
            var header = new VisualElement();
            header.AddToClassList("sm-bs-dial-header");
            var label = new Label(dial.Label);
            label.AddToClassList("sm-bs-dial-label");
            var value = new Label(dial.ValueText);
            value.AddToClassList("sm-bs-dial-value");
            header.Add(label);
            header.Add(value);
            var track = new VisualElement();
            track.AddToClassList("sm-bs-dial-track");
            var fill = new VisualElement();
            fill.AddToClassList("sm-bs-dial-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(dial.NormalizedValue) * 100f);
            track.Add(fill);
            row.Add(header);
            row.Add(track);
            container.Add(row);
        }
    }

    private static Label BuildEmptyLine()
    {
        var label = new Label("-");
        label.AddToClassList("sm-bs-empty-line");
        return label;
    }

    private static string BuildInitial(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim()[0].ToString();
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

    private void HandleUnitDetailPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        _unitDetailSwipePointerId = evt.pointerId;
        _unitDetailSwipeStartY = evt.position.y;
    }

    private void HandleUnitDetailPointerMove(PointerMoveEvent evt)
    {
        if (_unitDetailSwipePointerId != evt.pointerId)
        {
            return;
        }

        if (evt.position.y - _unitDetailSwipeStartY >= 120f)
        {
            _unitDetailSwipePointerId = -1;
            _actions?.CloseUnitDetail();
            evt.StopPropagation();
        }
    }

    private void HandleUnitDetailPointerUp(PointerUpEvent evt)
    {
        if (_unitDetailSwipePointerId == evt.pointerId)
        {
            _unitDetailSwipePointerId = -1;
        }
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

    private void SetRosterBlocking(VisualElement element)
    {
        element.pickingMode = PickingMode.Position;
        element.RegisterCallback<PointerEnterEvent>(_ => _rosterPointerDepth++);
        element.RegisterCallback<PointerLeaveEvent>(_ => _rosterPointerDepth = Math.Max(0, _rosterPointerDepth - 1));
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
