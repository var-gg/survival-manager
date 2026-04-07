using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity.UI;
using SM.Unity.UI.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity;

public sealed class BattleScreenController : MonoBehaviour
{
    private const int MaxRecentLogLines = 8;
    private const int MaxBattleSteps = BattleSimulator.DefaultMaxSteps;
    private const string QuickBattleRequestedEditorPrefKey = "SM.QuickBattleRequested";

    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private BattlePresentationController presentationController = null!;
    [SerializeField] private BattleCameraController cameraController = null!;

    private readonly List<BattleEvent> _recentLogs = new();
    private readonly List<string> _decisiveTimeline = new();
    private readonly BattlePresentationOptions _presentationOptions = BattlePresentationOptions.CreateDefault();
    private string _selectedUnitId = string.Empty;
    private string _settingsStatusText = string.Empty;
    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private BattleUnitMetadataFormatter _metadataFormatter = null!;
    private BattleSimulator? _simulator;
    private BattleLoadoutSnapshot? _compiledSnapshot;
    private IReadOnlyList<BattleUnitLoadout> _enemyLoadouts = Array.Empty<BattleUnitLoadout>();
    private ResolvedEncounterContext? _resolvedEncounterContext;
    private string _battleStartedAtUtc = string.Empty;
    private int _totalEventCount;
    private int _boundRootBuildCount = -1;
    private bool _battleFinishedHandled;
    private bool _settingsVisible;

    private BattleTimelineController? _timeline;
    private BattlePlaybackPolicy _policy = new(BattlePlaybackMode.QuickBattle);
    private BattleScreenPresenter? _presenter;
    private BattleScreenView? _view;
    private bool _inputActionsInitialized;

    private InputAction _toggleDebugAction = null!;
    private InputAction _stepOnceAction = null!;
    private InputAction _restartAction = null!;
    private InputAction _cycleUnitAction = null!;
    private InputAction _togglePauseAction = null!;
    private GUIStyle? _debugOverlayStyle;
    private GUIStyle? _debugOverlayBackgroundStyle;
    private GUIStyle? _debugOverlaySmallStyle;
    private Texture2D? _debugOverlayBackgroundTexture;

    public bool IsPlaybackFinished => _timeline?.IsFinished ?? false;
    public bool IsBattleFinished => _timeline?.IsFinished ?? false;
    public BattleSimulationStep? LatestStep => _timeline?.CurrentStep;
    public TeamPostureType? ActiveAllyPosture => _simulator?.State.AllyPosture;

    private static readonly Vector3 DefaultCameraPosition = new(0.4f, 7.8f, -9.1f);
    private static readonly Quaternion DefaultCameraRotation = Quaternion.Euler(33f, -12f, 0f);

    private void Start()
    {
        if (!EnsureReady())
        {
            return;
        }

        BootstrapQuickBattleDirectEntryIfRequested();

        if (!EnsureViewReady())
        {
            return;
        }

        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Battle);

        CreateInputActions();

        if (cameraController != null)
        {
            cameraController.Initialize(DefaultCameraPosition, DefaultCameraRotation);
            cameraController.SetUiBlockPredicate(() => _view?.IsPointerOverBlockingUi ?? false);
        }
        else
        {
            SetupCameraFallback();
        }

        RenderLoadingState();
        RunBattle();
    }

    private void OnDestroy()
    {
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }

        ReleaseDebugOverlayResources();
        DisposeInputActions();
    }

    private void Update()
    {
        HandleKeyboardShortcuts();

        if (_timeline == null)
        {
            return;
        }

        var stepped = _timeline.TryAdvance(
            Time.deltaTime,
            out var previousStep,
            out var currentStep,
            out var alpha);

        if (previousStep == null || currentStep == null)
        {
            return;
        }

        if (stepped)
        {
            _totalEventCount += currentStep.Events.Count;
            TrackDecisiveEvents(currentStep);
            presentationController.PushStep(previousStep, currentStep);
            RefreshHud(currentStep);

            if (currentStep.IsFinished && !_battleFinishedHandled)
            {
                FinishBattle();
            }
        }

        presentationController.SetBlend(previousStep, currentStep, alpha);
        _view?.SetProgress(_timeline.NormalizedProgress);

        if (_presentationOptions.ShowDebugOverlay)
        {
            DrawDebugTargetLines(currentStep);
        }
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void SetSpeed1() => SetSpeed(1f);
    public void SetSpeed2() => SetSpeed(2f);
    public void SetSpeed4() => SetSpeed(4f);

    public void HandleScrubberSeek(float normalized)
    {
        if (_timeline == null || !_policy.CanSeek(_timeline.IsFinished))
        {
            return;
        }

        var targetStep = Mathf.RoundToInt(normalized * MaxBattleSteps);
        _timeline.SeekToStep(targetStep);
        RefreshAfterSeek();
    }

    public void RebattleNewSeed()
    {
        if (!EnsureReady())
        {
            return;
        }

        _root.SessionState.ReloadQuickBattleConfig();
        _root.SessionState.PrepareQuickBattleSmoke();
        _root.SaveProfile();
        RenderLoadingState();
        RunBattle();

        if (cameraController != null)
        {
            cameraController.ResetToDefault();
        }
    }

    public void ReturnToTownDirect()
    {
        if (!EnsureReady())
        {
            return;
        }

        _root.SessionState.ReturnToTownAfterReward();
        _root.SaveProfile();

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(false);
        }

        _root.SceneFlow.ReturnToTown();
    }

    public void TogglePause()
    {
        if (!EnsureReady() || _timeline == null || !_policy.CanPause(_timeline.IsFinished))
        {
            return;
        }

        _timeline.TogglePause();
        RenderCurrentState();
    }

    public void ContinueToReward()
    {
        if (!EnsureReady())
        {
            return;
        }

        if (!IsBattleFinished)
        {
            RenderErrorState("전투가 아직 끝나지 않았습니다.");
            return;
        }

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(false);
        }

        _root.SceneFlow.GoToReward();
    }

    public void ToggleSettingsPanel()
    {
        _settingsVisible = !_settingsVisible;
        _settingsStatusText = _settingsVisible
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.title", "Battle View Settings")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.settings.closed", "Settings panel closed");
        RenderCurrentState();
    }

    public void ToggleOverheadUi()
    {
        _presentationOptions.ToggleOverheadUi();
        ApplyPresentationOptions(
            GameLocalizationTables.UIBattle,
            "ui.battle.settings.overhead_ui_label",
            "Overhead UI",
            _presentationOptions.ShowOverheadUi);
    }

    public void ToggleDamageText()
    {
        _presentationOptions.ToggleDamageText();
        ApplyPresentationOptions(
            GameLocalizationTables.UIBattle,
            "ui.battle.settings.damage_text_label",
            "Damage Text",
            _presentationOptions.ShowDamageText);
    }

    public void ToggleTeamSummary()
    {
        _presentationOptions.ToggleTeamHpSummary();
        ApplyPresentationOptions(
            GameLocalizationTables.UIBattle,
            "ui.battle.settings.team_summary_label",
            "Team Summary",
            _presentationOptions.ShowTeamHpSummary);
    }

    public void ToggleDebugOverlay()
    {
        _presentationOptions.ToggleDebugOverlay();
        ApplyPresentationOptions(
            GameLocalizationTables.UIBattle,
            "ui.battle.settings.debug_overlay_label",
            "Debug Overlay",
            _presentationOptions.ShowDebugOverlay);
    }

    private static void DrawDebugTargetLines(BattleSimulationStep step)
    {
        foreach (var unit in step.Units)
        {
            if (!unit.IsAlive || string.IsNullOrEmpty(unit.TargetId))
            {
                continue;
            }

            var target = step.Units.FirstOrDefault(u => u.Id == unit.TargetId);
            if (target == null)
            {
                continue;
            }

            var from = new Vector3(unit.Position.X, 0.15f, unit.Position.Y);
            var to = new Vector3(target.Position.X, 0.15f, target.Position.Y);
            var color = unit.Side == TeamSide.Ally ? Color.cyan : new Color(1f, 0.5f, 0.2f);
            Debug.DrawLine(from, to, color);
        }
    }

    private void RefreshAfterSeek()
    {
        if (_timeline == null)
        {
            return;
        }

        var previousStep = _timeline.PreviousStep;
        var currentStep = _timeline.CurrentStep;
        if (previousStep == null || currentStep == null)
        {
            return;
        }

        presentationController.PushStep(previousStep, currentStep);
        presentationController.SetBlend(previousStep, currentStep, 1f);
        _view?.SetProgress(_timeline.NormalizedProgress);
        RefreshHud(currentStep);
    }

    private void RestartSameSeed()
    {
        if (_compiledSnapshot == null || _resolvedEncounterContext == null)
        {
            return;
        }

        var encounter = _resolvedEncounterContext;
        var newState = BattleFactory.Create(
            _compiledSnapshot.Allies,
            encounter.Enemies,
            _compiledSnapshot.TeamTactic.Posture,
            encounter.EnemyPosture,
            BattleSimulator.DefaultFixedStepSeconds,
            seed: encounter.Context.BattleSeed);
        _simulator = new BattleSimulator(newState, MaxBattleSteps);

        _timeline!.Reset(_simulator, _simulator.CurrentStep, MaxBattleSteps);
        _battleFinishedHandled = false;
        _totalEventCount = 0;
        _recentLogs.Clear();
        _decisiveTimeline.Clear();
        _selectedUnitId = string.Empty;
        _settingsStatusText = string.Empty;
        presentationController.Initialize(_simulator.CurrentStep);
        presentationController.ApplyOptions(_presentationOptions);
        RenderCurrentState(_simulator.CurrentStep);

        if (cameraController != null)
        {
            cameraController.ResetToDefault();
        }
    }

    private void CycleSelectedUnit()
    {
        var currentStep = _timeline?.CurrentStep;
        if (currentStep == null)
        {
            return;
        }

        var alive = currentStep.Units
            .Where(u => u.IsAlive)
            .OrderBy(u => u.Side)
            .ThenBy(u => u.Id)
            .ToList();
        if (alive.Count == 0)
        {
            return;
        }

        var currentIndex = alive.FindIndex(u => u.Id == _selectedUnitId);
        _selectedUnitId = alive[(currentIndex + 1) % alive.Count].Id;
    }

    private bool EnsureReady()
    {
        ValidateReferences();
        if (_root != null)
        {
            return true;
        }

        _root = GameSessionRoot.EnsureInstance();
        if (_root == null)
        {
            Debug.LogError("[BattleScreenController] GameSessionRoot가 없습니다.");
            return false;
        }

        _localization = _root.Localization;
        _metadataFormatter ??= new BattleUnitMetadataFormatter(_localization, _root.CombatContentLookup);
        return true;
    }

    private void BootstrapQuickBattleDirectEntryIfRequested()
    {
#if UNITY_EDITOR
        if (!EditorPrefs.GetBool(QuickBattleRequestedEditorPrefKey, false))
        {
            return;
        }

        EditorPrefs.DeleteKey(QuickBattleRequestedEditorPrefKey);
        _root.EnsureOfflineLocalSession();
        _root.SessionState.ReloadQuickBattleConfig();
        _root.SessionState.PrepareQuickBattleSmoke();
        _root.SaveProfile();
        Debug.Log("[BattleScreenController] Quick Battle bootstrap 요청을 소비하고 Battle smoke를 초기화했습니다.");
#endif
    }

    private bool EnsureViewReady()
    {
        if (!EnsureReady() || panelHost == null)
        {
            return false;
        }

        panelHost.EnsureReady();
        if (_view != null && _presenter != null && _boundRootBuildCount == panelHost.RootBuildCount)
        {
            return true;
        }

        _view = new BattleScreenView(panelHost.Root);
        _view.Bind(new BattleScreenActions(
            SelectKorean,
            SelectEnglish,
            SetSpeed1,
            SetSpeed2,
            SetSpeed4,
            TogglePause,
            ContinueToReward,
            RebattleNewSeed,
            ReturnToTownDirect,
            ToggleSettingsPanel,
            ToggleOverheadUi,
            ToggleDamageText,
            ToggleTeamSummary,
            ToggleDebugOverlay,
            HandleScrubberSeek));
        _presenter = new BattleScreenPresenter(_localization, _root.SessionState, _presentationOptions);
        presentationController.ConfigureMetadataFormatter(_metadataFormatter);
        _boundRootBuildCount = panelHost.RootBuildCount;

        if (cameraController != null)
        {
            cameraController.SetUiBlockPredicate(() => _view.IsPointerOverBlockingUi);
        }

        return true;
    }

    private void ValidateReferences()
    {
        if (panelHost == null)
        {
            Debug.LogError("[BattleScreenController] Missing RuntimePanelHost reference: panelHost");
        }

        if (presentationController == null)
        {
            Debug.LogError("[BattleScreenController] Missing BattlePresentationController reference: presentationController");
        }
    }

    private void RenderLoadingState()
    {
        if (!EnsureViewReady())
        {
            return;
        }

        _view!.Render(_presenter!.BuildLoadingState());
        _view.SetScrubberInteractable(false);
    }

    private void RenderErrorState(string message)
    {
        if (EnsureViewReady())
        {
            _view!.Render(_presenter!.BuildErrorState(message));
            _view.SetScrubberInteractable(false);
        }

        Debug.LogError($"[BattleScreenController] {message}");
    }

    private void RenderCurrentState(BattleSimulationStep? step = null)
    {
        if (!EnsureViewReady())
        {
            return;
        }

        var currentStep = step ?? _timeline?.CurrentStep;
        if (currentStep == null)
        {
            _view!.Render(_presenter!.BuildLoadingState());
            _view.SetScrubberInteractable(false);
            return;
        }

        var isFinished = _timeline?.IsFinished ?? currentStep.IsFinished;
        EnsureSelectedUnit(currentStep);
        var selectedUnit = currentStep.Units.FirstOrDefault(unit => unit.Id == _selectedUnitId);
        var state = _presenter!.BuildState(
            currentStep,
            _recentLogs,
            _totalEventCount,
            _timeline?.IsPaused ?? false,
            _timeline?.PlaybackSpeed ?? 1f,
            isFinished,
            _settingsVisible,
            _timeline?.NormalizedProgress ?? 0f,
            _settingsStatusText,
            _metadataFormatter.BuildSelectedUnitPanel(selectedUnit));
        _view!.Render(state);
        _view.SetScrubberInteractable(_timeline != null && _policy.CanSeek(_timeline.IsFinished));
    }

    private void SetSpeed(float speed)
    {
        if (_timeline == null || !_policy.CanControlSpeed(_timeline.IsFinished))
        {
            return;
        }

        _timeline.SetSpeed(speed);
        RenderCurrentState();
    }

    private void CreateInputActions()
    {
        _toggleDebugAction = new InputAction("ToggleDebug", InputActionType.Button, "<Keyboard>/f3");
        _stepOnceAction = new InputAction("StepOnce", InputActionType.Button, "<Keyboard>/f4");
        _restartAction = new InputAction("Restart", InputActionType.Button, "<Keyboard>/f5");
        _cycleUnitAction = new InputAction("CycleUnit", InputActionType.Button, "<Keyboard>/tab");
        _togglePauseAction = new InputAction("TogglePause", InputActionType.Button, "<Keyboard>/space");

        _toggleDebugAction.Enable();
        _stepOnceAction.Enable();
        _restartAction.Enable();
        _cycleUnitAction.Enable();
        _togglePauseAction.Enable();
        _inputActionsInitialized = true;
    }

    private void DisposeInputActions()
    {
        _inputActionsInitialized = false;
        _toggleDebugAction?.Dispose();
        _stepOnceAction?.Dispose();
        _restartAction?.Dispose();
        _cycleUnitAction?.Dispose();
        _togglePauseAction?.Dispose();
    }

    private void HandleKeyboardShortcuts()
    {
        if (!_inputActionsInitialized)
        {
            return;
        }

        if (_toggleDebugAction.WasPressedThisFrame())
        {
            _presentationOptions.ToggleDebugOverlay();
            presentationController.ApplyOptions(_presentationOptions);
            RenderCurrentState();
        }

        if (_stepOnceAction.WasPressedThisFrame() && _timeline is { IsPaused: true } && !IsBattleFinished)
        {
            _timeline.StepOnce();
            RefreshAfterSeek();
        }

        if (_restartAction.WasPressedThisFrame() && _simulator != null)
        {
            RestartSameSeed();
        }

        if (_cycleUnitAction.WasPressedThisFrame() && _timeline?.CurrentStep != null)
        {
            CycleSelectedUnit();
        }

        if (_togglePauseAction.WasPressedThisFrame())
        {
            TogglePause();
        }
    }

    private static void SetupCameraFallback()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.transform.position = DefaultCameraPosition;
        cam.transform.rotation = DefaultCameraRotation;
    }

    private void RunBattle()
    {
        if (!EnsureReady() || !EnsureViewReady())
        {
            return;
        }

        _battleFinishedHandled = false;
        _totalEventCount = 0;
        _selectedUnitId = string.Empty;
        _settingsVisible = false;
        _settingsStatusText = string.Empty;
        _recentLogs.Clear();
        _decisiveTimeline.Clear();

        BattleLoadoutSnapshot allySnapshot;
        try
        {
            allySnapshot = _root.SessionState.BuildBattleLoadoutSnapshot();
        }
        catch (Exception ex)
        {
            RenderErrorState(ex.Message);
            return;
        }

        if (allySnapshot.Allies.Count == 0)
        {
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.no_allies", "No allied unit is ready for battle."));
            return;
        }

        if (!_root.CombatContentLookup.TryGetCombatSnapshot(out var snapshot, out var lookupError))
        {
            RenderErrorState(lookupError);
            return;
        }

        if (!_root.SessionState.TryResolveCurrentEncounter(out var encounter, out var encounterError))
        {
            RenderErrorState(encounterError);
            return;
        }

        _compiledSnapshot = allySnapshot;
        _resolvedEncounterContext = encounter;
        _enemyLoadouts = encounter.Enemies;
        _battleStartedAtUtc = DateTime.UtcNow.ToString("O");

        var simulationState = BattleFactory.Create(
            allySnapshot.Allies,
            encounter.Enemies,
            allySnapshot.TeamTactic.Posture,
            encounter.EnemyPosture,
            BattleSimulator.DefaultFixedStepSeconds,
            seed: encounter.Context.BattleSeed);
        new EncounterResolutionService(snapshot).ApplyBattleBootstrap(simulationState, encounter);

        _simulator = new BattleSimulator(simulationState, MaxBattleSteps);
        _policy = new BattlePlaybackPolicy(BattlePlaybackMode.QuickBattle);
        _timeline = new BattleTimelineController();
        _timeline.Initialize(_simulator, _simulator.CurrentStep, MaxBattleSteps);

        presentationController.Initialize(_simulator.CurrentStep);
        presentationController.ApplyOptions(_presentationOptions);
        RenderCurrentState(_simulator.CurrentStep);

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(true);
        }
    }

    private void FinishBattle()
    {
        if (_simulator == null || _timeline == null || _battleFinishedHandled || _compiledSnapshot == null)
        {
            return;
        }

        _battleFinishedHandled = true;
        _timeline.MarkFinished();

        var currentStep = _timeline.CurrentStep!;
        var winner = currentStep.Winner ?? TeamSide.Ally;
        var result = _simulator.RunToEnd();
        var replay = ReplayAssembler.Assemble(
            _compiledSnapshot,
            _enemyLoadouts,
            result,
            _resolvedEncounterContext?.Context.BattleSeed ?? 0,
            _battleStartedAtUtc,
            DateTime.UtcNow.ToString("O"));
        _root.SessionState.RecordBattleAudit(replay);
        BattleDebugLogWriter.Write(replay, currentStep.Units);
        _root.SessionState.MarkBattleResolved(
            winner == TeamSide.Ally,
            currentStep.StepIndex,
            _totalEventCount);
        RenderCurrentState(currentStep);
        _view?.SetProgress(1f);
    }

    private void ApplyPresentationOptions(string table, string key, string fallback, bool isOn)
    {
        presentationController.ApplyOptions(_presentationOptions);
        _settingsVisible = true;
        _settingsStatusText = Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.settings.state_changed",
            "{0}: {1}",
            Localize(table, key, fallback),
            isOn
                ? Localize(GameLocalizationTables.UICommon, "ui.common.on", "ON")
                : Localize(GameLocalizationTables.UICommon, "ui.common.off", "OFF"));
        RenderCurrentState();
    }

    private void RefreshHud(BattleSimulationStep step)
    {
        foreach (var eventData in step.Events)
        {
            PushLog(eventData);
        }

        RenderCurrentState(step);
    }

    private void PushLog(BattleEvent eventData)
    {
        _recentLogs.Add(eventData);
        while (_recentLogs.Count > MaxRecentLogLines)
        {
            _recentLogs.RemoveAt(0);
        }
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization != null
            ? _localization.LocalizeOrFallback(table, key, fallback, args)
            : args.Length == 0
                ? fallback
                : string.Format(fallback, args);
    }

    private void OnGUI()
    {
        if (!_presentationOptions.ShowDebugOverlay || _timeline?.CurrentStep == null)
        {
            return;
        }

        EnsureDebugOverlayStyles();
        var style = _debugOverlayStyle!;
        var bgStyle = _debugOverlayBackgroundStyle!;

        var step = _timeline.CurrentStep;
        var allyCount = step.Units.Count(u => u.Side == TeamSide.Ally);
        var allyAlive = step.Units.Count(u => u.Side == TeamSide.Ally && u.IsAlive);
        var enemyCount = step.Units.Count(u => u.Side == TeamSide.Enemy);
        var enemyAlive = step.Units.Count(u => u.Side == TeamSide.Enemy && u.IsAlive);
        var isPaused = _timeline.IsPaused;
        var speedLabel = isPaused ? "PAUSED" : $"x{_timeline.PlaybackSpeed:0}";

        var pauseHint = isPaused ? " | <color=#ff6>F4=Step  F5=Restart</color>" : " | <color=#aaa>F5=Restart</color>";
        var headerRect = new Rect(4, 4, 780, 20);
        GUI.Box(headerRect, GUIContent.none, bgStyle);
        GUI.Label(headerRect, $"  Step: {step.StepIndex}/{MaxBattleSteps} | Time: {step.TimeSeconds:0.0}s | {speedLabel} | Allies: {allyAlive}/{allyCount} | Enemies: {enemyAlive}/{enemyCount}{pauseHint}", style);

        var y = 28f;
        foreach (var unit in step.Units.OrderBy(u => u.Side).ThenBy(u => u.Id))
        {
            var isSelected = unit.Id == _selectedUnitId;
            var marker = isSelected ? "<color=#ff0>></color> " : "  ";
            var sideTag = unit.Side == TeamSide.Ally ? "<color=#6cc>ally</color>" : "<color=#f93>enemy</color>";
            var hpPct = unit.MaxHealth > 0 ? unit.CurrentHealth / unit.MaxHealth * 100f : 0f;
            var targetLabel = !string.IsNullOrEmpty(unit.TargetName) ? $"-> {unit.TargetName}" : string.Empty;
            var actionLabel = FormatActionState(unit);
            var lockLabel = unit.RetargetLockRemaining > 0.01f ? $" lock:{unit.RetargetLockRemaining:0.0}s" : string.Empty;
            var cdLabel = unit.CooldownRemaining > 0.01f ? $" cd:{unit.CooldownRemaining:0.0}s" : string.Empty;
            var selectorLabel = !string.IsNullOrEmpty(unit.CurrentSelector) ? $" sel:{unit.CurrentSelector}" : string.Empty;
            var guardLabel = unit.FrontlineGuardRadius > 0.01f ? $" guard:{unit.FrontlineGuardRadius:0.#}" : string.Empty;

            var line = $"{marker}[{sideTag}] {unit.Name} HP:{unit.CurrentHealth:0}/{unit.MaxHealth:0}({hpPct:0}%) {targetLabel} [{actionLabel}]{cdLabel}{lockLabel}{selectorLabel}{guardLabel}";
            var lineRect = new Rect(4, y, 780, 16);
            GUI.Box(lineRect, GUIContent.none, bgStyle);
            GUI.Label(lineRect, line, style);
            y += 16f;
        }

        DrawSelectedUnitPanel(step, bgStyle, style, y);
        DrawDecisiveTimeline(bgStyle, style);
    }

    private void DrawSelectedUnitPanel(BattleSimulationStep step, GUIStyle bgStyle, GUIStyle style, float startY)
    {
        if (string.IsNullOrEmpty(_selectedUnitId))
        {
            return;
        }

        var unit = step.Units.FirstOrDefault(u => u.Id == _selectedUnitId);
        if (unit == null)
        {
            return;
        }

        var panelY = startY + 8f;
        var panelRect = new Rect(4, panelY, 400, 96);
        GUI.Box(panelRect, GUIContent.none, bgStyle);

        var lines = new[]
        {
            $"  <color=#ff0>Selected: {unit.Name}</color> ({unit.Side} {unit.EntityKind})",
            $"  HP: {unit.CurrentHealth:0}/{unit.MaxHealth:0} | Energy: {unit.CurrentEnergy:0}/{unit.MaxEnergy:0} | Barrier: {unit.Barrier:0}",
            $"  Pos: ({unit.Position.X:0.0}, {unit.Position.Y:0.0}) | Target: {unit.TargetName ?? "none"}",
            $"  Selector: {unit.CurrentSelector} | Fallback: {unit.CurrentFallback}",
            $"  Lock: {unit.RetargetLockRemaining:0.0}s | Guard: {unit.FrontlineGuardRadius:0.#} | Cluster: {unit.ClusterRadius:0.#}",
            $"  Class: {unit.ClassId} | Race: {unit.RaceId} | Anchor: {unit.Anchor}"
        };

        for (var i = 0; i < lines.Length; i++)
        {
            GUI.Label(new Rect(4, panelY + i * 16f, 400, 16), lines[i], style);
        }
    }

    private void DrawDecisiveTimeline(GUIStyle bgStyle, GUIStyle style)
    {
        if (_decisiveTimeline.Count == 0)
        {
            return;
        }

        var startX = Screen.width - 320f;
        var visible = _decisiveTimeline.Count > 8
            ? _decisiveTimeline.Skip(_decisiveTimeline.Count - 8).ToList()
            : _decisiveTimeline;
        var panelRect = new Rect(startX, 4, 316, 16 + visible.Count * 14f);
        GUI.Box(panelRect, GUIContent.none, bgStyle);
        GUI.Label(new Rect(startX, 4, 316, 16), "  <color=#ff6>Decisive Timeline</color>", style);
        var smallStyle = _debugOverlaySmallStyle!;
        for (var i = 0; i < visible.Count; i++)
        {
            GUI.Label(new Rect(startX, 20 + i * 14f, 316, 14), $"  {visible[i]}", smallStyle);
        }
    }

    private void TrackDecisiveEvents(BattleSimulationStep step)
    {
        foreach (var evt in step.Events)
        {
            if (evt.EventKind == BattleEventKind.Kill)
            {
                _decisiveTimeline.Add($"<color=#f66>{step.TimeSeconds:0.0}s</color> Kill: {evt.ActorName} -> {evt.TargetName}");
            }
            else if (evt.ActionType == BattleActionType.ActiveSkill && evt.Value > 0)
            {
                _decisiveTimeline.Add($"<color=#6cf>{step.TimeSeconds:0.0}s</color> Skill: {evt.ActorName} ({evt.Value:0})");
            }
        }
    }

    private static string FormatActionState(BattleUnitReadModel unit)
    {
        if (!unit.IsAlive)
        {
            return "Dead";
        }

        return unit.WindupProgress > 0.01f
            ? $"{unit.ActionState} {Mathf.RoundToInt(unit.WindupProgress * 100f)}%"
            : unit.ActionState.ToString();
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        _settingsStatusText = string.Empty;
        if (_timeline?.CurrentStep is { } currentStep)
        {
            presentationController.SetBlend(currentStep, currentStep, 1f);
        }

        RenderCurrentState();
    }

    private void EnsureSelectedUnit(BattleSimulationStep step)
    {
        if (step.Units.Any(unit => unit.Id == _selectedUnitId))
        {
            return;
        }

        _selectedUnitId = step.Units
            .OrderBy(unit => unit.Side)
            .ThenBy(unit => unit.Id)
            .Select(unit => unit.Id)
            .FirstOrDefault() ?? string.Empty;
    }

    private void EnsureDebugOverlayStyles()
    {
        if (_debugOverlayStyle != null && _debugOverlayBackgroundStyle != null && _debugOverlaySmallStyle != null)
        {
            return;
        }

        _debugOverlayStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            richText = true
        };

        _debugOverlayBackgroundTexture ??= new Texture2D(1, 1);
        _debugOverlayBackgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
        _debugOverlayBackgroundTexture.Apply();

        _debugOverlayBackgroundStyle = new GUIStyle
        {
            normal = { background = _debugOverlayBackgroundTexture }
        };
        _debugOverlaySmallStyle = new GUIStyle(_debugOverlayStyle) { fontSize = 10 };
    }

    private void ReleaseDebugOverlayResources()
    {
        _debugOverlayStyle = null;
        _debugOverlayBackgroundStyle = null;
        _debugOverlaySmallStyle = null;

        if (_debugOverlayBackgroundTexture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_debugOverlayBackgroundTexture);
        }
        else
        {
            DestroyImmediate(_debugOverlayBackgroundTexture);
        }

        _debugOverlayBackgroundTexture = null;
    }
}
