using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity.Narrative;
using SM.Unity.UI;
using SM.Unity.UI.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity
{

public sealed class BattleScreenController : MonoBehaviour
{
    private const int MaxRecentLogLines = 8;
    private const int MaxBattleSteps = BattleSimulator.DefaultMaxSteps;
    private const string HelpPrefsKey = "SM.Help.Battle";

    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private BattlePresentationController presentationController = null!;
    [SerializeField] private BattleCameraController cameraController = null!;
    [SerializeField] private StorySceneFlowBridge _storyBridge = null!;

    private readonly List<BattleEvent> _recentLogs = new();
    private readonly List<string> _decisiveTimeline = new();
    private readonly BattlePresentationOptions _presentationOptions = BattlePresentationOptions.CreateDefault();
    private readonly BattleCameraFramingPolicy _cameraFramingPolicy = new();
    private readonly ScreenHelpState _helpState = new(HelpPrefsKey);
    private string _selectedUnitId = string.Empty;
    private string _settingsStatusText = string.Empty;
    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
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
    private bool _summaryExpanded = true;

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
    public BattlePlaybackMode PlaybackMode => _policy.Mode;

    private static readonly Vector3 DefaultCameraPosition = new(0.4f, 7.8f, -9.1f);
    private static readonly Quaternion DefaultCameraRotation = Quaternion.Euler(33f, -12f, 0f);
    private bool IsSmokeLane => _policy.Mode == BattlePlaybackMode.QuickBattle;

    private void Start()
    {
        if (!EnsureReady())
        {
            return;
        }

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

        _storyBridge?.ClearPending();
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
            presentationController.AdvanceStep(previousStep, currentStep);
            RefreshHud(currentStep);

            if (currentStep.IsFinished && !_battleFinishedHandled)
            {
                FinishBattle();
            }
        }

        presentationController.SetBlend(previousStep, currentStep, alpha);
        presentationController.SetFocus(currentStep, _selectedUnitId);
        presentationController.TickTransients(Time.deltaTime, _timeline.PlaybackSpeed, _timeline.IsPaused);
        ApplyPassiveCameraFrame(currentStep);
        _view?.SetProgress(_timeline.NormalizedProgress);

        HandlePointerSelection(currentStep);

        if (_presentationOptions.ShowDebugOverlay)
        {
            DrawDebugTargetLines(currentStep);
        }
    }

    public void SelectKorean() => _localization.TrySetLocale("ko");
    public void SelectEnglish() => _localization.TrySetLocale("en");
    public void ToggleHelp()
    {
        _helpState.Toggle();
        RenderCurrentState();
    }

    public void DismissHelp()
    {
        _helpState.Dismiss();
        RenderCurrentState();
    }

    public void SetSpeed1() => SetSpeed(1f);
    public void SetSpeed2() => SetSpeed(2f);
    public void SetSpeed4() => SetSpeed(4f);

    public void HandleScrubberSeek(float normalized)
    {
        if (!IsSmokeLane || _timeline == null || !_policy.CanSeek(_timeline.IsFinished))
        {
            return;
        }

        var targetStep = Mathf.RoundToInt(normalized * MaxBattleSteps);
        _timeline.SeekToStep(targetStep);
        RefreshAfterSeek();
    }

    public void ReplayRecordedTimeline()
    {
        if (!IsSmokeLane || _timeline == null || !_policy.CanReplay(_timeline.IsFinished))
        {
            return;
        }

        _timeline.SeekToStep(0);
        RefreshAfterSeek(BattlePresentationCueType.PlaybackReset, bootstrapCamera: true);
    }

    public void RebattleNewSeed()
    {
        if (!EnsureReady())
        {
            return;
        }

        if (!_root.SessionState.IsQuickBattleSmokeActive)
        {
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.rebattle_smoke_only", "Re-battle is only available in Quick Battle (Smoke)."));
            return;
        }

        _root.SessionState.RestartQuickBattle(advanceSeed: true);
        if (!_root.SessionState.IsDirectCombatSandboxLane)
        {
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.QuickBattleBootstrap);
            if (checkpoint.Status == SessionCheckpointStatus.Failed)
            {
                RenderErrorState(checkpoint.Message);
                return;
            }
        }

        RenderLoadingState();
        RunBattle();
    }

    public void ReturnToTownDirect()
    {
        if (!EnsureReady())
        {
            return;
        }

        if (!_root.SessionState.IsQuickBattleSmokeActive)
        {
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.return_town_smoke_only", "Direct return to Town is only available in Quick Battle (Smoke)."));
            return;
        }

        if (!IsBattleFinished)
        {
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.return_town_before_finish", "Finish the battle before returning directly to Town."));
            return;
        }

        if (_root.SessionState.IsDirectCombatSandboxLane)
        {
            _root.SessionState.ExitCombatSandbox();
            if (cameraController != null)
            {
                cameraController.SetInputEnabled(false);
            }

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            _root.SceneFlow.GoToBoot();
#endif
            return;
        }

        if (_root.IsTransientTownSmokeActive)
        {
            var restored = _root.RestoreCanonicalProfileAfterTransientSmoke();
            if (!restored.IsSuccessful)
            {
                RenderErrorState(restored.Message);
                return;
            }
        }
        else
        {
            _root.SessionState.ReturnToTownAfterReward();
            var checkpoint = _root.SaveProfile(SessionCheckpointKind.RewardSettled);
            if (!checkpoint.IsSuccessful)
            {
                RenderErrorState(checkpoint.Message);
                return;
            }
        }

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(false);
        }

        _root.SceneFlow.ReturnToTown();
    }

    public void TogglePause()
    {
        if (!EnsureReady() || !IsSmokeLane || _timeline == null || !_policy.CanPause(_timeline.IsFinished))
        {
            return;
        }

        _timeline.TogglePause();
        presentationController.TickTransients(0f, _timeline.PlaybackSpeed, _timeline.IsPaused);
        RenderCurrentState();
    }

    public void ContinueToReward()
    {
        if (!EnsureReady())
        {
            return;
        }

        if (_root.SessionState.IsDirectCombatSandboxLane)
        {
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.direct_sandbox_reward_hidden", "Combat Sandbox does not continue into Reward. Use Exit Sandbox or replay controls instead."));
            return;
        }

        if (!IsBattleFinished)
        {
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.continue_before_finish", "Continue activates after the battle is fully resolved."));
            return;
        }

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(false);
        }

        var checkpoint = _root.SaveProfile(SessionCheckpointKind.BattleResolved);
        if (checkpoint.Status == SessionCheckpointStatus.Failed)
        {
            RenderErrorState(checkpoint.Message);
            return;
        }

        if (EnsureStoryBridgeReady())
        {
            _storyBridge.Advance(NarrativeMoment.BattleResolved, BuildStoryMomentContext(), _root.SceneFlow.GoToReward);
            return;
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

    public void ToggleSummaryPanel()
    {
        _summaryExpanded = !_summaryExpanded;
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
        if (!IsSmokeLane)
        {
            return;
        }

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

    private void RefreshAfterSeek(BattlePresentationCueType resetReason = BattlePresentationCueType.SeekSnapshotApplied, bool bootstrapCamera = false)
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

        presentationController.ClearTransients(resetReason);
        presentationController.RenderSnapshot(currentStep);
        presentationController.SetFocus(currentStep, _selectedUnitId);
        _view?.SetProgress(_timeline.NormalizedProgress);
        RefreshHud(currentStep);

        if (bootstrapCamera)
        {
            ApplyBootstrapCameraFrame(currentStep);
        }
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
        presentationController.Initialize(_simulator.CurrentStep, BuildBattleMapSelectionContext(encounter.Context));
        presentationController.ApplyOptions(_presentationOptions);
        EnsureSelectedUnit(_simulator.CurrentStep);
        presentationController.SetFocus(_simulator.CurrentStep, _selectedUnitId);
        RenderCurrentState(_simulator.CurrentStep);
        ApplyBootstrapCameraFrame(_simulator.CurrentStep);
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
        _contentText ??= new ContentTextResolver(_localization, _root.CombatContentLookup);
        _metadataFormatter ??= new BattleUnitMetadataFormatter(_localization, _root.CombatContentLookup);
        return true;
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
            ToggleHelp,
            DismissHelp,
            SetSpeed1,
            SetSpeed2,
            SetSpeed4,
            TogglePause,
            ContinueToReward,
            ReplayRecordedTimeline,
            RebattleNewSeed,
            ReturnToTownDirect,
            ToggleSettingsPanel,
            ToggleOverheadUi,
            ToggleDamageText,
            ToggleTeamSummary,
            ToggleDebugOverlay,
            ToggleSummaryPanel,
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

        _view!.Render(_presenter!.BuildLoadingState(_helpState.IsVisible, _summaryExpanded));
        _view.SetScrubberInteractable(false);
    }

    private void RenderErrorState(string message)
    {
        if (EnsureViewReady())
        {
            _view!.Render(_presenter!.BuildErrorState(message, _helpState.IsVisible, _summaryExpanded));
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
            _view!.Render(_presenter!.BuildLoadingState(_helpState.IsVisible, _summaryExpanded));
            _view.SetScrubberInteractable(false);
            return;
        }

        var isFinished = _timeline?.IsFinished ?? currentStep.IsFinished;
        EnsureSelectedUnit(currentStep);
        var selectedUnit = currentStep.Units.FirstOrDefault(unit => unit.Id == _selectedUnitId);
        var state = _presenter!.BuildState(
            currentStep,
            _recentLogs,
            _decisiveTimeline,
            _totalEventCount,
            _timeline?.IsPaused ?? false,
            _timeline?.PlaybackSpeed ?? 1f,
            isFinished,
            _settingsVisible,
            _timeline?.NormalizedProgress ?? 0f,
            _settingsStatusText,
            canReplay: IsSmokeLane && _timeline != null && _policy.CanReplay(_timeline.IsFinished),
            canRebattle: IsSmokeLane,
            canPause: IsSmokeLane && _timeline != null && _policy.CanPause(_timeline.IsFinished),
            canChangeSpeed: IsSmokeLane && _timeline != null && _policy.CanControlSpeed(_timeline.IsFinished),
            showHelp: _helpState.IsVisible,
            isSummaryExpanded: _summaryExpanded,
            selectedUnit: _metadataFormatter.BuildSelectedUnitPanel(selectedUnit));
        _view!.Render(state);
        _view.SetScrubberInteractable(IsSmokeLane && _timeline != null && _policy.CanSeek(_timeline.IsFinished));
    }

    private void SetSpeed(float speed)
    {
        if (!IsSmokeLane || _timeline == null || !_policy.CanControlSpeed(_timeline.IsFinished))
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

        if (IsSmokeLane && _toggleDebugAction.WasPressedThisFrame())
        {
            ToggleDebugOverlay();
        }

        if (IsSmokeLane && _stepOnceAction.WasPressedThisFrame() && _timeline is { IsPaused: true } && !IsBattleFinished)
        {
            _timeline.StepOnce();
            RefreshAfterSeek();
        }

        if (IsSmokeLane && _restartAction.WasPressedThisFrame() && _simulator != null)
        {
            RestartSameSeed();
        }

        if (_cycleUnitAction.WasPressedThisFrame() && _timeline?.CurrentStep != null)
        {
            CycleSelectedUnit();
            presentationController.SetFocus(_timeline.CurrentStep, _selectedUnitId);
            RenderCurrentState();
        }

        if (IsSmokeLane && _togglePauseAction.WasPressedThisFrame())
        {
            TogglePause();
        }
    }

    private void HandlePointerSelection(BattleSimulationStep currentStep)
    {
        if (Mouse.current?.leftButton.wasPressedThisFrame != true || _view?.IsPointerOverBlockingUi != false)
        {
            return;
        }

        var pointerPosition = Mouse.current.position.ReadValue();
        if (!presentationController.TryPickActor(pointerPosition, out var actorId))
        {
            return;
        }

        _selectedUnitId = actorId;
        presentationController.SetFocus(currentStep, _selectedUnitId);
        RenderCurrentState(currentStep);
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

    private void ApplyBootstrapCameraFrame(BattleSimulationStep step)
    {
        if (cameraController == null)
        {
            return;
        }

        cameraController.SetSuggestedFrame(_cameraFramingPolicy.BuildBootstrapFrame(step, _selectedUnitId));
    }

    private void ApplyPassiveCameraFrame(BattleSimulationStep step)
    {
        if (cameraController == null)
        {
            return;
        }

        cameraController.SetSuggestedFrame(_cameraFramingPolicy.BuildPassiveFrame(step, _selectedUnitId));
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
        _policy = new BattlePlaybackPolicy(
            _root.SessionState.IsQuickBattleSmokeActive
                ? BattlePlaybackMode.QuickBattle
                : BattlePlaybackMode.InGame);
        _timeline = new BattleTimelineController();
        _timeline.Initialize(_simulator, _simulator.CurrentStep, MaxBattleSteps);

        presentationController.Initialize(_simulator.CurrentStep, BuildBattleMapSelectionContext(encounter.Context));
        presentationController.ApplyOptions(_presentationOptions);
        EnsureSelectedUnit(_simulator.CurrentStep);
        presentationController.SetFocus(_simulator.CurrentStep, _selectedUnitId);
        RenderCurrentState(_simulator.CurrentStep);
        ApplyBootstrapCameraFrame(_simulator.CurrentStep);

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
        var result = _simulator.RunToEnd();
        var winner = result.Winner;
        var replay = ReplayAssembler.Assemble(
            _compiledSnapshot,
            _enemyLoadouts,
            result,
            _resolvedEncounterContext?.Context.BattleSeed ?? 0,
            _battleStartedAtUtc,
            DateTime.UtcNow.ToString("O"));
        _root.SessionState.RecordBattleAudit(replay);
        if (RuntimeInstrumentation.ShouldEmitVerboseArtifacts)
        {
            BattleDebugLogWriter.Write(replay, result.FinalUnits);
        }

        _root.SessionState.MarkBattleResolved(
            winner == TeamSide.Ally,
            result.StepCount,
            _totalEventCount);
        var checkpoint = _root.SaveProfile(SessionCheckpointKind.BattleResolved);
        if (checkpoint.Status == SessionCheckpointStatus.Failed)
        {
            RenderErrorState(checkpoint.Message);
            return;
        }

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
        if (!IsSmokeLane || !_presentationOptions.ShowDebugOverlay || _timeline?.CurrentStep == null)
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
                _decisiveTimeline.Add($"{step.TimeSeconds:0.0}s | {evt.TargetName} down");
            }
            else if (evt.LogCode == BattleLogCode.ActiveSkillHeal)
            {
                _decisiveTimeline.Add($"{step.TimeSeconds:0.0}s | {evt.ActorName} heal {evt.TargetName} +{evt.Value:0}");
            }
            else if (evt.ActionType == BattleActionType.ActiveSkill && evt.Value > 0)
            {
                _decisiveTimeline.Add($"{step.TimeSeconds:0.0}s | {evt.ActorName} skill {evt.TargetName} -{evt.Value:0}");
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
            presentationController.RenderSnapshot(currentStep);
            presentationController.SetFocus(currentStep, _selectedUnitId);
        }

        RenderCurrentState();
    }

    private bool EnsureStoryBridgeReady()
    {
        if (_storyBridge != null)
        {
            return true;
        }

        _storyBridge = GetComponent<StorySceneFlowBridge>();
        if (_storyBridge == null)
        {
            _storyBridge = gameObject.AddComponent<StorySceneFlowBridge>();
        }

        return _storyBridge != null;
    }

    private StoryMomentContext BuildStoryMomentContext()
    {
        var session = _root.SessionState;
        return new StoryMomentContext
        {
            ChapterId = session.SelectedCampaignChapterId,
            SiteId = session.SelectedCampaignSiteId,
            NodeIndex = session.GetSelectedExpeditionNode()?.Index ?? session.CurrentExpeditionNodeIndex,
        };
    }

    private BattleMapSelectionContext BuildBattleMapSelectionContext(BattleContextState context)
    {
        return new BattleMapSelectionContext(
            context.ChapterId,
            context.SiteId,
            context.EncounterId,
            context.BattleSeed);
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
}
