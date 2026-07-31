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
    Action SetSpeed05,
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
    // USS .sm-bs-roster-portrait(pass-2 override)와 일치해야 한다. 실제 크기는 레이아웃 후
    // GeometryChangedEvent로 재적용하므로 이 값은 첫 프레임 추정치 역할만 한다.
    // (과거 128x152 stale 값 때문에 1:1 얼굴이 82x70 창에 152px로 렌더돼 정수리만 보이던 버그)
    private const float AllyRosterPortraitWidth = 86f;
    private const float AllyRosterPortraitHeight = 160f;
    private const float EnemyRosterPortraitWidth = 86f;
    private const float EnemyRosterPortraitHeight = 128f;

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
    private readonly Label _battleTitleLabel;
    private readonly Label _allyTitleLabel;
    private readonly Label _logTitleLabel;
    private readonly VisualElement _allySummaryPanel;
    private readonly VisualElement _allyRosterList;
    private readonly Label _allyHpLabel;
    private readonly Label _enemyTitleLabel;
    private readonly VisualElement _enemySummaryPanel;
    private readonly VisualElement _enemyRosterList;
    private readonly Label _enemyHpLabel;
    private readonly Label _logLabel;
    private readonly Label _resultLabel;
    private readonly VisualElement _playbackActionsGroup;
    private readonly Label _playbackGroupTitleLabel;
    private readonly Button _speed05Button;
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
    private readonly VisualElement _observerStatusGroup;
    private readonly Label _observerStatusTitleLabel;
    private readonly VisualElement _observerStatusChips;
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
    private readonly Button _unitDetailStatusTab;
    private readonly VisualElement _selectedUnitAilmentTint;
    private readonly VisualElement _selectedUnitHpFill;
    private readonly VisualElement _selectedUnitShieldFill;
    private readonly VisualElement _unitDetailOverviewContent;
    private readonly VisualElement _unitDetailStatsContent;
    private readonly VisualElement _unitDetailSkillsContent;
    private readonly VisualElement _unitDetailEquipmentContent;
    private readonly VisualElement _unitDetailStatusContent;
    private readonly VisualElement _overviewCoreStats;
    private readonly VisualElement _overviewFormationGrid;
    private readonly VisualElement _statsList;
    private readonly VisualElement _skillPresentationSlots;
    private readonly VisualElement _equipmentSlots;
    private readonly Label _statusPermanentTitle;
    private readonly VisualElement _statusPermanentGrid;
    private readonly Label _statusBattleScopedTitle;
    private readonly VisualElement _statusBattleGrid;
    private readonly Foldout _battleDebugFoldout;
    private readonly Label _battleDebugEncounterIdValue;
    private readonly Label _battleDebugSiteNodeIndexValue;
    private readonly Label _battleDebugBattleContextHashValue;

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
        _battleTitleLabel = Require<Label>(root, "BattleTitleLabel");
        _allyTitleLabel = Require<Label>(root, "AllyTitleLabel");
        _logTitleLabel = Require<Label>(root, "LogTitleLabel");
        _allySummaryPanel = Require<VisualElement>(root, "AllySummaryPanel");
        _allyRosterList = Require<VisualElement>(root, "AllyRosterList");
        _allyHpLabel = Require<Label>(root, "AllyHpLabel");
        _enemyTitleLabel = Require<Label>(root, "EnemyTitleLabel");
        _enemySummaryPanel = Require<VisualElement>(root, "EnemySummaryPanel");
        _enemyRosterList = Require<VisualElement>(root, "EnemyRosterList");
        _enemyHpLabel = Require<Label>(root, "EnemyHpLabel");
        _logLabel = Require<Label>(root, "LogLabel");
        _resultLabel = Require<Label>(root, "ResultLabel");
        _playbackActionsGroup = Require<VisualElement>(root, "PlaybackActionsGroup");
        _playbackGroupTitleLabel = Require<Label>(root, "PlaybackGroupTitleLabel");
        _speed05Button = Require<Button>(root, "Speed05Button");
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
        _observerStatusGroup = Require<VisualElement>(root, "ObserverStatusGroup");
        _observerStatusTitleLabel = Require<Label>(root, "ObserverStatusTitleLabel");
        _observerStatusChips = Require<VisualElement>(root, "ObserverStatusChips");
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
        _unitDetailStatusTab = Require<Button>(root, "UnitDetailStatusTab");
        _selectedUnitAilmentTint = Require<VisualElement>(root, "SelectedUnitAilmentTint");
        _selectedUnitHpFill = Require<VisualElement>(root, "SelectedUnitHpFill");
        _selectedUnitShieldFill = Require<VisualElement>(root, "SelectedUnitShieldFill");
        _unitDetailOverviewContent = Require<VisualElement>(root, "UnitDetailOverviewContent");
        _unitDetailStatsContent = Require<VisualElement>(root, "UnitDetailStatsContent");
        _unitDetailSkillsContent = Require<VisualElement>(root, "UnitDetailSkillsContent");
        _unitDetailEquipmentContent = Require<VisualElement>(root, "UnitDetailEquipmentContent");
        _unitDetailStatusContent = Require<VisualElement>(root, "UnitDetailStatusContent");
        _overviewCoreStats = Require<VisualElement>(root, "OverviewCoreStats");
        _overviewFormationGrid = Require<VisualElement>(root, "OverviewFormationGrid");
        _statsList = Require<VisualElement>(root, "StatsList");
        _skillPresentationSlots = Require<VisualElement>(root, "SkillPresentationSlots");
        _equipmentSlots = Require<VisualElement>(root, "EquipmentSlots");
        _statusPermanentTitle = Require<Label>(root, "StatusPermanentTitle");
        _statusPermanentGrid = Require<VisualElement>(root, "StatusPermanentGrid");
        _statusBattleScopedTitle = Require<Label>(root, "StatusBattleScopedTitle");
        _statusBattleGrid = Require<VisualElement>(root, "StatusBattleGrid");
        _battleDebugFoldout = Require<Foldout>(root, "BattleDebugFoldout");
        _battleDebugFoldout.style.display = DisplayStyle.None;
        _battleDebugEncounterIdValue = Require<Label>(root, "BattleDebugEncounterIdValue");
        _battleDebugSiteNodeIndexValue = Require<Label>(root, "BattleDebugSiteNodeIndexValue");
        _battleDebugBattleContextHashValue = Require<Label>(root, "BattleDebugBattleContextHashValue");
        _selectedUnitPortraitImage.scaleMode = ScaleMode.ScaleAndCrop;
        _unitDetailCloseButton.text = "X";

        SetNonBlocking(
            _localeStatusLabel,
            _helpStrip,
            _helpBodyLabel,
            _summaryPanel,
            _summaryTitleLabel,
            _summaryBody,
            _battleTitleLabel,
            _allyTitleLabel,
            _logTitleLabel,
            _allySummaryPanel,
            _allyRosterList,
            _allyHpLabel,
            _enemyTitleLabel,
            _enemySummaryPanel,
            _enemyRosterList,
            _enemyHpLabel,
            _logLabel,
            _resultLabel,
            _playbackGroupTitleLabel,
            _continueGroupTitleLabel,
            _smokeGroupTitleLabel,
            _utilityGroupTitleLabel,
            _observerStatusGroup,
            _observerStatusTitleLabel,
            _observerStatusChips,
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
            _unitDetailStatusContent,
            _overviewCoreStats,
            _overviewFormationGrid,
            _statsList,
            _skillPresentationSlots,
            _equipmentSlots,
            _statusPermanentTitle,
            _statusPermanentGrid,
            _statusBattleScopedTitle,
            _statusBattleGrid,
            _battleDebugEncounterIdValue,
            _battleDebugSiteNodeIndexValue,
            _battleDebugBattleContextHashValue);

        SetBlocking(
            _localeKoButton,
            _localeEnButton,
            _helpButton,
            _helpDismissButton,
            _summaryToggleButton,
            _speed05Button,
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
            _unitDetailStatusTab,
            _toggleOverheadButton,
            _toggleDamageTextButton,
            _toggleTeamSummaryButton,
            _toggleDebugOverlayButton,
            _battleDebugFoldout);
    }

    public void RenderDebugFoldout(BattleDebugFoldoutViewState state)
    {
        var snapshot = state ?? BattleDebugFoldoutViewState.Empty;
        _battleDebugFoldout.style.display = DisplayStyle.None;
        _battleDebugEncounterIdValue.text = snapshot.EncounterId;
        _battleDebugSiteNodeIndexValue.text = snapshot.SiteNodeIndexText;
        _battleDebugBattleContextHashValue.text = snapshot.BattleContextHash;
    }

    public void Bind(BattleScreenActions actions)
    {
        _actions = actions;
        _localeKoButton.clicked += actions.SelectKorean;
        _localeEnButton.clicked += actions.SelectEnglish;
        _helpButton.clicked += actions.ToggleHelp;
        _helpDismissButton.clicked += actions.DismissHelp;
        _speed05Button.clicked += actions.SetSpeed05;
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
        _unitDetailStatusTab.clicked += () => actions.SelectUnitDetailTab(BattleUnitDetailTab.Status);
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
        _battleTitleLabel.text = state.Title;
        _allyTitleLabel.text = state.AllyTitle;
        _logTitleLabel.text = state.LogTitle;
        _allyHpLabel.text = state.AllyHpText;
        _enemyTitleLabel.text = state.EnemyTitle;
        _enemyHpLabel.text = state.EnemyHpText;
        _logLabel.text = state.LogText;
        _resultLabel.text = state.ResultText;

        _playbackActionsGroup.style.display = state.ShowPlaybackControls ? DisplayStyle.Flex : DisplayStyle.None;
        _playbackGroupTitleLabel.text = state.PlaybackGroupTitle;
        _speed05Button.text = state.Speed05Label;
        _speed05Button.tooltip = state.PauseTooltip;
        _speed05Button.SetEnabled(state.CanChangeSpeed);
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
        RenderObserverStatusDock(state);

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
        _unitDetailStatusTab.text = selectedUnit.StatusTabLabel;
        UpdateDetailTab(_unitDetailOverviewTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Overview);
        UpdateDetailTab(_unitDetailStatsTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Stats);
        UpdateDetailTab(_unitDetailSkillsTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Skills);
        UpdateDetailTab(_unitDetailEquipmentTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Equipment);
        UpdateDetailTab(_unitDetailStatusTab, selectedUnit.ActiveTab == BattleUnitDetailTab.Status);
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

            // 역할 칩 — 시안이 이름 밑에 둔 자리. 값이 없으면 칩 자체를 만들지 않는다.
            // 빈 상자가 뜨느니 없는 편이 낫고, 그래야 미해결 id 가 화면에 새지 않는다.
            if (!string.IsNullOrEmpty(unit.RoleText))
            {
                var role = new Label(unit.RoleText);
                role.AddToClassList("sm-bs-roster-role");
                meta.Add(role);
            }

            var status = new Label(unit.StatusText);
            status.AddToClassList("sm-bs-roster-status");
            meta.Add(status);

            // HP 바는 인라인 치수·색으로 박는다.
            //
            // 클래스만 걸었을 때 이 바는 <b>양 팀 모두 화면에 한 번도 나온 적이 없었다</b>.
            // 렌더 코드는 있고 USS 규칙도 하나뿐인데(8px, 알파 10% 트랙) 실제 캡쳐를 픽셀 단위로
            // 훑어도 초록 채널 우세 픽셀이 카드 안에 0 이었다. 원인을 더 파는 대신, 파티 카드는
            // 어차피 다시 짓는 자리라 <b>치수와 색을 인라인으로 확정</b>했다. 인라인은 USS 로 덮이지 않는다.
            // 즉 이 바가 안 보이면 그건 레이아웃이 아니라 데이터 문제다 — 원인이 한 곳으로 좁혀진다.
            var track = new VisualElement();
            track.AddToClassList("sm-bs-roster-hp-track");
            // fill 을 가로로 눕히려면 트랙이 row 여야 한다. 기본값 column 에서는 퍼센트 높이가
            // 교차축 stretch 와 얽혀 실제로 칠해지지 않았다 — 트랙 선만 나오고 초록이 안 나왔다.
            track.style.flexDirection = FlexDirection.Row;
            track.style.height = 14f;
            track.style.marginTop = 5f;
            track.style.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 0.92f);
            track.style.borderTopWidth = 1f;
            track.style.borderBottomWidth = 1f;
            track.style.borderLeftWidth = 1f;
            track.style.borderRightWidth = 1f;
            var edge = new Color(0f, 0f, 0f, 0.55f);
            track.style.borderTopColor = edge;
            track.style.borderBottomColor = edge;
            track.style.borderLeftColor = edge;
            track.style.borderRightColor = edge;

            var health = Mathf.Clamp01(unit.HealthNormalized);
            var fill = new VisualElement();
            fill.AddToClassList("sm-bs-roster-hp-fill");
            fill.style.height = 12f;
            fill.style.width = Length.Percent(health * 100f);
            fill.style.backgroundColor = ResolveHealthColor(health, unit.IsAlive, isEnemy);
            track.Add(fill);

            // 수치는 바 <b>위에</b> 얹는다 — 시안이 그렇게 했고, 카드 한 장이 세로로
            // 이름·역할·바·숫자 네 줄이 되면 4장이 기둥 높이를 넘긴다.
            if (!string.IsNullOrEmpty(unit.HealthText))
            {
                var healthLabel = new Label(unit.HealthText);
                healthLabel.AddToClassList("sm-bs-roster-hp-text");
                healthLabel.pickingMode = PickingMode.Ignore;
                track.Add(healthLabel);
            }

            meta.Add(track);

            row.Add(meta);
            container.Add(row);
        }
    }

    private void RenderObserverStatusDock(BattleShellViewState state)
    {
        var showObserverDock = !state.ShowPlaybackControls && !state.ShowContinueAction && !state.ShowSmokeActions;
        _observerStatusGroup.style.display = showObserverDock ? DisplayStyle.Flex : DisplayStyle.None;
        _observerStatusTitleLabel.text = state.UtilityGroupTitle;
        _observerStatusChips.Clear();
        if (!showObserverDock)
        {
            return;
        }

        // 2026-07-31: 칩 넷 중 둘이 문제였다. 세 번째는 시뮬레이터 스텝 문자열을 그대로 냈고
        // ("스텝 000 | 묘직 준비 -> - | 압박 균형"), 첫째와 넷째는 이름표로 "요약"을 함께 써서
        // 같은 글자가 두 번 떴다. 스텝 줄은 전투 기록이 이미 서술하므로 뺀다.
        AddObserverStatusChip(_observerStatusChips, state.ObserverStateTitle, state.ResultText, "primary");
        AddObserverStatusChip(_observerStatusChips, state.PlaybackGroupTitle, state.SpeedText, "speed");
        AddObserverStatusChip(_observerStatusChips, state.ObserverProgressTitle, BuildProgressText(state.ProgressNormalized), "progress");
    }

    private static void AddObserverStatusChip(VisualElement container, string labelText, string valueText, string tone)
    {
        var chip = new VisualElement();
        chip.AddToClassList("sm-bs-observer-chip");
        chip.AddToClassList($"sm-bs-observer-chip--{tone}");

        var label = new Label(labelText);
        label.AddToClassList("sm-bs-observer-chip__label");
        chip.Add(label);

        var value = new Label(BuildCompactText(valueText, 38));
        value.AddToClassList("sm-bs-observer-chip__value");
        chip.Add(value);
        container.Add(chip);
    }

    /// <summary>
    /// 체력 구간에 따라 바 색을 바꾼다 — 한 눈에 "누가 위험한가"가 읽혀야 파티 바가 제 일을 한다.
    /// 숫자를 읽게 만들면 이미 늦다.
    /// </summary>
    /// <summary>
    /// HP 바 색.
    ///
    /// 아군은 초록 → 호박 → 빨강으로 <b>남은 양</b>을 색으로도 말한다.
    /// 적군은 시안(<c>ui_ux_bible_battle_hud_v1</c>)대로 항상 적색이다 — 색은 <b>소속</b>을,
    /// 길이는 남은 양을 말하는 업계 관습이다. 적 카드에까지 초록이 뜨면 화면에서
    /// 어느 쪽이 우리 편인지가 한눈에 안 잡힌다.
    /// </summary>
    private static Color ResolveHealthColor(float normalized, bool isAlive, bool isEnemy = false)
    {
        if (!isAlive)
        {
            return new Color(0.34f, 0.36f, 0.40f, 0.85f);
        }

        if (isEnemy)
        {
            return new Color(0.80f, 0.26f, 0.24f, 0.95f);
        }

        if (normalized <= 0.3f)
        {
            return new Color(0.85f, 0.28f, 0.30f, 0.95f);
        }

        return normalized <= 0.6f
            ? new Color(0.92f, 0.72f, 0.32f, 0.95f)
            : new Color(0.44f, 0.78f, 0.42f, 0.95f);
    }

    private static string BuildProgressText(float normalized)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f)}%";
    }

    private static string BuildCompactText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= maxLength ? text : text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
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
        _unitDetailStatusContent.style.display = selectedUnit.ActiveTab == BattleUnitDetailTab.Status ? DisplayStyle.Flex : DisplayStyle.None;

        RenderOverview(selectedUnit);
        RenderStats(selectedUnit.StatLines);
        RenderSkillSlots(selectedUnit.SkillSlots);
        RenderEquipment(selectedUnit.EquipmentSlots);
        RenderStatusEffects(selectedUnit.StatusEffects);
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
            var title = new Label(CategoryName(group.Key));
            title.AddToClassList("sm-bs-stat-category");
            _statsList.Add(title);
            RenderStatGrid(_statsList, group.ToArray(), compact: false);
        }
    }

    // 스탯 카테고리 헤더 한국어 표시 — enum.ToString() 영문(Vital/Combat...)이 한국어 UI에 새던 것을 교정.
    private static string CategoryName(BattleStatLineCategory category) => category switch
    {
        BattleStatLineCategory.Vital => "생존",
        BattleStatLineCategory.Combat => "전투",
        BattleStatLineCategory.Defense => "방어",
        BattleStatLineCategory.Resource => "자원",
        BattleStatLineCategory.Movement => "기동",
        BattleStatLineCategory.Targeting => "표적",
        _ => "알 수 없음",
    };

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

    /// <summary>
    /// 진형 칸 표시명.
    ///
    /// 2026-07-31 까지 여기는 <c>"FT" / "FC" / "FB" / "BT" / "BC" / "BB"</c> 를 냈다.
    /// 내부 enum 의 머리글자 약어가 <b>플레이어 화면에 그대로 떠 있었다.</b> 유닛 상세창은
    /// 클릭해야 나오는 화면이라 캡쳐 경로에 한 번도 안 걸렸고, 그래서 아무도 이걸 못 봤다.
    ///
    /// 같은 enum 을 <see cref="SM.Unity.UI.Atlas.AtlasScreenController"/> 는 이미
    /// "전열 상 / 전열 중 ..." 으로 한글화해 쓰고 있었다 — 즉 전투 화면만 예외였다.
    /// 표기를 그쪽에 맞춘다. 화면마다 같은 칸을 다른 말로 부르면 그것도 결함이다.
    /// </summary>
    private static string FormatAnchorShort(DeploymentAnchorId anchor)
    {
        return anchor switch
        {
            DeploymentAnchorId.FrontTop => "전열 상",
            DeploymentAnchorId.FrontCenter => "전열 중",
            DeploymentAnchorId.FrontBottom => "전열 하",
            DeploymentAnchorId.BackTop => "후열 상",
            DeploymentAnchorId.BackCenter => "후열 중",
            DeploymentAnchorId.BackBottom => "후열 하",
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
        // USS가 frame 크기를 바꿔도(과거 82x70 override ↔ C# 128x152 상수 drift로 정수리만
        // 보이던 버그) 실제 레이아웃 크기로 cover-fit을 재적용해 자가 교정한다.
        frame.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            if (evt.newRect.width > 1f && evt.newRect.height > 1f)
            {
                ApplyCoverFit(image, texture, evt.newRect.width, evt.newRect.height);
            }
        });
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
            slot.AddToClassList("sm-cd-card");
            slot.AddToClassList($"sm-bs-skill-slot--{state.PresentationStyle}");
            slot.EnableInClassList("sm-bs-skill-slot--signature", state.IsSignatureSlot);
            slot.EnableInClassList("sm-bs-skill-slot--flex", state.IsFlexSlot);
            slot.EnableInClassList("sm-bs-skill-slot--active", state.IsActiveSlot);
            slot.EnableInClassList("sm-bs-skill-slot--missing", state.Icon == null);
            slot.tooltip = BuildSkillTooltip(state);

            if (state.Icon != null)
            {
                var icon = new Image
                {
                    image = state.Icon,
                    scaleMode = ScaleMode.ScaleAndCrop,
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("sm-bs-skill-icon");
                icon.AddToClassList("sm-cd-icon");
                slot.Add(icon);
            }
            else
            {
                var fallback = new Label(BuildInitial(state.SkillName));
                fallback.AddToClassList("sm-bs-skill-icon");
                fallback.AddToClassList("sm-bs-skill-icon--missing");
                fallback.AddToClassList("sm-cd-icon");
                slot.Add(fallback);
            }

            var copy = new VisualElement();
            copy.AddToClassList("sm-cd-copy");
            var slotLabel = new Label(state.SlotLabel);
            slotLabel.AddToClassList("sm-cd-kicker");
            copy.Add(slotLabel);
            var label = new Label(state.SkillName);
            label.AddToClassList("sm-bs-skill-label");
            label.AddToClassList("sm-cd-title");
            copy.Add(label);
            if (!string.IsNullOrWhiteSpace(state.TimingText))
            {
                var timing = new Label(state.TimingText);
                timing.AddToClassList("sm-cd-meta");
                copy.Add(timing);
            }

            if (!string.IsNullOrWhiteSpace(state.EffectSummary))
            {
                var summary = new Label(state.EffectSummary);
                summary.AddToClassList("sm-cd-body-text");
                copy.Add(summary);
            }

            if (state.Tags.Count > 0)
            {
                copy.Add(BuildTagRow(state.Tags));
            }

            slot.Add(copy);
            _skillPresentationSlots.Add(slot);
        }
    }

    private static string BuildSkillTooltip(BattleSkillSlotViewState state)
    {
        var lines = new List<string> { state.SkillName };
        if (!string.IsNullOrWhiteSpace(state.Description))
        {
            lines.Add(state.Description);
        }

        if (!string.IsNullOrWhiteSpace(state.TimingText))
        {
            lines.Add(state.TimingText);
        }

        if (!string.IsNullOrWhiteSpace(state.EffectSummary))
        {
            lines.Add(state.EffectSummary);
        }

        if (!string.IsNullOrWhiteSpace(state.ScalingSummary))
        {
            lines.Add(state.ScalingSummary);
        }

        return string.Join("\n", lines);
    }

    private static VisualElement BuildTagRow(IReadOnlyList<string> tags)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-cd-tag-row");
        foreach (var tag in tags)
        {
            var chip = new Label(tag);
            chip.AddToClassList("sm-cd-tag");
            row.Add(chip);
        }

        return row;
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
            element.AddToClassList("sm-cd-chip");
            element.EnableInClassList("sm-bs-status-chip--permanent", chip.Section == BattleStatusEffectSection.Permanent);
            element.tooltip = BuildStatusTooltip(chip);

            if (chip.Icon != null)
            {
                var icon = new Image
                {
                    image = chip.Icon,
                    scaleMode = ScaleMode.ScaleAndCrop,
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("sm-bs-status-chip-icon");
                icon.AddToClassList("sm-cd-icon");
                element.Add(icon);
            }
            else
            {
                var fallback = new Label(BuildInitial(chip.Label));
                fallback.AddToClassList("sm-bs-status-chip-icon");
                fallback.AddToClassList("sm-bs-status-chip-icon--missing");
                fallback.AddToClassList("sm-cd-icon");
                element.Add(fallback);
            }

            var label = new Label(chip.Label);
            label.AddToClassList("sm-bs-status-chip-label");
            label.AddToClassList("sm-cd-title");
            element.Add(label);

            var meta = new Label(chip.DurationText);
            meta.AddToClassList("sm-cd-meta");
            element.Add(meta);

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

    private static string BuildStatusTooltip(BattleStatusEffectChip chip)
    {
        var lines = new List<string>
        {
            chip.Label,
            chip.SourceActorName,
            chip.DurationText,
            chip.PersistenceText,
            chip.CleanseText
        };
        if (!string.IsNullOrWhiteSpace(chip.Description))
        {
            lines.Insert(1, chip.Description);
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
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
