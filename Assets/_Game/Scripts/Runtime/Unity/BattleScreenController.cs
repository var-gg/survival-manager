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
    private readonly BattleHighlightLedger _highlightLedger = new();
    // Phase 4 spectacle director — beat/킬 강조의 단일 정책(컷 예산·콜아웃 밀도). 순수 클래스라
    // 컨트롤러는 배선만 한다(god-file 비대화 방지, BattleHighlightLedger 전례).
    private readonly BattleSpectacleDirector _spectacleDirector = new();
    private readonly List<BeatCalloutEntry> _beatCallouts = new();
    private const int MaxBeatCalloutLines = 1;
    private const double BeatCalloutLifetimeSeconds = 4.0;
    // 디렉터 시계 — 벽시계 진행이되 일시정지 동안 동결한다(TickTransients 의 pause 동결 계약과 동일 축).
    // 배속/step 버스트에서 시청자 기준 컷·콜아웃 밀도를 지키면서, 정지 화면에서 컷/만료/시효가 흐르지 않는다.
    private double _directorClockSeconds;

    private readonly record struct BeatCalloutEntry(string Line, double ExpiresAtSeconds);
    private readonly List<(BattleSimulationStep PreviousStep, BattleSimulationStep CurrentStep)> _consumedTransitions = new();
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
    private BattleSummaryRecord? _lastBattleSummaryRecord;
    private string _battleStartedAtUtc = string.Empty;
    private int _totalEventCount;
    private int _boundRootBuildCount = -1;
    private bool _battleFinishedHandled;
    private bool _settingsVisible;
    private bool _summaryExpanded = true;
    private bool _unitDetailVisible;
    private BattleUnitDetailTab _unitDetailTab = BattleUnitDetailTab.Overview;

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
    private InputAction _closeOverlayAction = null!;
    private GUIStyle? _debugOverlayStyle;
    private GUIStyle? _debugOverlayBackgroundStyle;
    private GUIStyle? _debugOverlaySmallStyle;
    private Texture2D? _debugOverlayBackgroundTexture;

    public bool IsPlaybackFinished => _timeline?.IsFinished ?? false;
    public bool IsBattleFinished => _timeline?.IsFinished ?? false;
    public BattleSimulationStep? LatestStep => _timeline?.CurrentStep;
    public TeamPostureType? ActiveAllyPosture => _simulator?.State.AllyPosture;
    public BattlePlaybackMode PlaybackMode => _policy.Mode;

    private const float DefaultCameraFieldOfView = 54f;
    private static readonly Vector3 DefaultCameraPosition = new(0.4f, 7.7f, -8.9f);
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
            cameraController.Camera.fieldOfView = DefaultCameraFieldOfView;
            cameraController.SetUiBlockPredicate(() => _view?.IsPointerOverBlockingUi ?? false);
        }
        else
        {
            SetupCameraFallback();
        }

        RenderLoadingState();

        // BattleStarted moment fire: boss-engage 대사 등 전투 시작 직전 연출을 재생한 뒤
        // RunBattle로 이어진다. 매칭 event가 없으면 onCompleted가 즉시 호출되어 기존 흐름과 동일.
        // 단 전투테스트(QuickBattle smoke)는 전투 중심 빠른 확인용 레인이라 narrative 연출을
        // 건너뛰고 곧장 전투로 진입한다 (site-intro 대화가 HUD를 덮는 것을 방지).
        if (!_root.SessionState.IsQuickBattleSmokeActive && EnsureStoryBridgeReady())
        {
            _storyBridge.Advance(NarrativeMoment.BattleStarted, BuildStoryMomentContext(), RunBattle);
        }
        else
        {
            RunBattle();
        }
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

        _consumedTransitions.Clear();
        var stepped = _timeline.TryAdvance(
            Time.deltaTime,
            out var previousStep,
            out var currentStep,
            out var alpha,
            _consumedTransitions);

        if (previousStep == null || currentStep == null)
        {
            return;
        }

        if (stepped)
        {
            if (_consumedTransitions.Count == 0)
            {
                ConsumeTimelineTransition(previousStep, currentStep);
            }
            else
            {
                foreach (var transition in _consumedTransitions)
                {
                    ConsumeTimelineTransition(transition.PreviousStep, transition.CurrentStep);
                }
            }
        }

        presentationController.SetBlend(previousStep, currentStep, alpha);
        presentationController.SetFocus(currentStep, _selectedUnitId);
        presentationController.TickTransients(Time.deltaTime, _timeline.PlaybackSpeed, _timeline.IsPaused);
        if (!_timeline.IsPaused)
        {
            _directorClockSeconds += Time.unscaledDeltaTime;
            DrainBeatCallout(currentStep);
        }

        PruneExpiredBeatCallouts(currentStep);
        ApplyDirectedCameraFrame(currentStep);
        _view?.SetProgress(_timeline.NormalizedProgress);

        HandlePointerSelection(currentStep);

        if (_presentationOptions.ShowDebugOverlay)
        {
            DrawDebugTargetLines(currentStep);
        }
    }

    private void ConsumeTimelineTransition(BattleSimulationStep previousStep, BattleSimulationStep currentStep)
    {
        _totalEventCount += currentStep.Events.Count;
        TrackDecisiveEvents(currentStep);
        // P2 하이라이트 원장 — typed 채널 집계 + 종료 시 MVP/하이라이트를 decisive timeline에 1회 첨부.
        _highlightLedger.Record(currentStep);
        // Phase 4 — beat/킬 강조 후보 적재(pause 동결 디렉터 시계 기준).
        _spectacleDirector.IngestStep(currentStep, _directorClockSeconds);
        _highlightLedger.TryAppendBattleEndLines(
            currentStep,
            _decisiveTimeline,
            actorId => ResolveBattleEventUnitName(currentStep, actorId, null));
        presentationController.AdvanceStep(previousStep, currentStep);
        RefreshHud(currentStep);

        if (currentStep.IsFinished && !_battleFinishedHandled)
        {
            FinishBattle();
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

    public void SetSpeed05() => SetSpeed(0.5f);
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
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.rebattle_smoke_only", "재전투는 빠른 전투에서만 사용할 수 있습니다."));
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
            RenderErrorState(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.return_town_smoke_only", "마을 바로 복귀는 빠른 전투에서만 사용할 수 있습니다."));
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
            // 발화는 세션(MarkBattleResolved가 BattleResolved 발화) — 씬은 큐를 present하고 완료 후 보상으로.
            _storyBridge.PresentPending(_root.SceneFlow.GoToReward);
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

    public void SelectRosterUnit(string unitId)
    {
        SelectUnit(unitId, openDetail: false, snapCamera: true);
    }

    public void OpenRosterUnitDetail(string unitId)
    {
        SelectUnit(unitId, openDetail: true, snapCamera: true);
    }

    public void CloseUnitDetail()
    {
        _unitDetailVisible = false;
        RenderCurrentState();
    }

    public void SelectUnitDetailTab(BattleUnitDetailTab tab)
    {
        _unitDetailTab = tab;
        _unitDetailVisible = true;
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

    private void SelectUnit(string unitId, bool openDetail, bool snapCamera)
    {
        var currentStep = _timeline?.CurrentStep;
        if (currentStep == null || currentStep.Units.All(unit => unit.Id != unitId))
        {
            return;
        }

        _selectedUnitId = unitId;
        if (openDetail)
        {
            _unitDetailVisible = true;
            _unitDetailTab = BattleUnitDetailTab.Overview;
        }

        presentationController.SetFocus(currentStep, _selectedUnitId);
        if (snapCamera && cameraController != null)
        {
            cameraController.SnapToSuggestedFrame(_cameraFramingPolicy.BuildUnitFocusFrame(currentStep, _selectedUnitId));
        }

        RenderCurrentState(currentStep);
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
        // Phase 4 — 강조 상태도 transient 청산 계약을 따른다: 되감기/시크 후 stale 컷·콜아웃 금지.
        // 시크 후 재생은 의도적 재발화다(재시청 = 하이라이트를 다시 본다) — dedup 까지 함께 청산.
        _spectacleDirector.Reset();
        _beatCallouts.Clear();
        if (resetReason == BattlePresentationCueType.PlaybackReset)
        {
            // 처음부터 재시청 — step 0 의 개전 beat 을 청산된 디렉터에 다시 공급한다
            // (원장은 리셋되지 않으므로 여기서는 디렉터만).
            _spectacleDirector.IngestStep(currentStep, _directorClockSeconds);
        }

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
        if (_compiledSnapshot == null || _resolvedEncounterContext == null || _root == null)
        {
            return;
        }

        var encounter = _resolvedEncounterContext;
        // 재합성은 세션 단일 소스(TryComposeBattleState) — 첫 전투(RunBattle)와 동일 합성이라 같은 시드
        // 재시작이 byte-identical 전투를 보장한다. 씬이 별도 BattleFactory를 직접 호출하면 보스 overlay
        // bootstrap·status rule fallback이 빠지는 2nd battle-truth가 된다(2026-07 준비도 감사로 차단).
        if (!_root.SessionState.TryComposeBattleState(_compiledSnapshot, encounter, out var newState, out var composeError))
        {
            RenderBattleSetupFailure(composeError);
            return;
        }

        _simulator = new BattleSimulator(newState, MaxBattleSteps);

        _timeline!.Reset(_simulator, _simulator.CurrentStep, MaxBattleSteps);
        _timeline.ConfigureStartupHold(BattlePresentationController.StartupHoldSeconds);
        _battleFinishedHandled = false;
        _totalEventCount = 0;
        _lastBattleSummaryRecord = null;
        _recentLogs.Clear();
        _decisiveTimeline.Clear();
        _highlightLedger.Reset();
        _spectacleDirector.Reset();
        _beatCallouts.Clear();
        _selectedUnitId = string.Empty;
        _unitDetailVisible = false;
        _unitDetailTab = BattleUnitDetailTab.Overview;
        _settingsStatusText = string.Empty;
        presentationController.Initialize(_simulator.CurrentStep, BuildBattleMapSelectionContext(encounter.Context));
        presentationController.ConfigureSkillPresentations(CollectBattleSkillSpecs());
        presentationController.ApplyOptions(_presentationOptions);
        EnsureSelectedUnit(_simulator.CurrentStep);
        presentationController.SetFocus(_simulator.CurrentStep, _selectedUnitId);
        IngestOpeningStep();
        RenderCurrentState(_simulator.CurrentStep);
        ApplyBootstrapCameraFrame(_simulator.CurrentStep);
    }

    /// <summary>스킬 계열별 VFX 해상 룩업 소스 — 현재 sim의 전 유닛 컴파일 스킬 spec.</summary>
    private IEnumerable<BattleSkillSpec> CollectBattleSkillSpecs()
    {
        return _simulator == null
            ? System.Linq.Enumerable.Empty<BattleSkillSpec>()
            : _simulator.State.AllUnits
                .SelectMany(unit => unit.Definition.Skills ?? System.Linq.Enumerable.Empty<BattleSkillSpec>());
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
            SetSpeed05,
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
            HandleScrubberSeek,
            SelectRosterUnit,
            OpenRosterUnitDetail,
            CloseUnitDetail,
            SelectUnitDetailTab));
        _presenter = new BattleScreenPresenter(_localization, _root.SessionState, _presentationOptions, _contentText);
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
        _view.RenderDebugFoldout(_presenter!.BuildDebugFoldoutState());
        _view.SetScrubberInteractable(false);
    }

    private void RenderErrorState(string message)
    {
        if (EnsureViewReady())
        {
            _view!.Render(_presenter!.BuildErrorState(message, _helpState.IsVisible, _summaryExpanded));
            _view.RenderDebugFoldout(_presenter!.BuildDebugFoldoutState());
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
            _view.RenderDebugFoldout(_presenter!.BuildDebugFoldoutState());
            _view.SetScrubberInteractable(false);
            return;
        }

        var isFinished = _timeline?.IsFinished ?? currentStep.IsFinished;
        EnsureSelectedUnit(currentStep);
        var selectedUnit = currentStep.Units.FirstOrDefault(unit => unit.Id == _selectedUnitId);
        var selectedTeamUnits = selectedUnit == null
            ? Array.Empty<BattleUnitReadModel>()
            : currentStep.Units.Where(unit => unit.Side == selectedUnit.Side).ToArray();
        var selectedUnitState = _metadataFormatter.BuildSelectedUnitPanel(
            selectedUnit,
            _unitDetailVisible,
            _unitDetailTab,
            selectedUnit != null ? BuildSelectedUnitRecord(selectedUnit.Id) : string.Empty,
            selectedUnit != null ? ResolveTeamTactic(selectedUnit.Side) : null,
            selectedTeamUnits);
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
            selectedUnit: selectedUnitState,
            beatCallouts: _beatCallouts.Select(entry => entry.Line).ToList());
        _view!.Render(state);
        _view.RenderDebugFoldout(_presenter!.BuildDebugFoldoutState());
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
        _closeOverlayAction = new InputAction("CloseOverlay", InputActionType.Button, "<Keyboard>/escape");

        _toggleDebugAction.Enable();
        _stepOnceAction.Enable();
        _restartAction.Enable();
        _cycleUnitAction.Enable();
        _togglePauseAction.Enable();
        _closeOverlayAction.Enable();
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
        _closeOverlayAction?.Dispose();
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

        if (_closeOverlayAction.WasPressedThisFrame())
        {
            if (_unitDetailVisible)
            {
                CloseUnitDetail();
            }
            else if (_settingsVisible)
            {
                ToggleSettingsPanel();
            }
        }
    }

    private void RenderBattleSetupFailure(string diagnostic)
    {
        Debug.LogError($"[BattleScreenController] Battle setup failed. {diagnostic}");
        RenderErrorState(Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.error.setup_failed",
            "The battle could not be prepared."));
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
        cam.fieldOfView = DefaultCameraFieldOfView;
    }

    private void ApplyBootstrapCameraFrame(BattleSimulationStep step)
    {
        if (cameraController == null)
        {
            return;
        }

        cameraController.SetSuggestedFrame(_cameraFramingPolicy.BuildBootstrapFrame(step, _selectedUnitId));
    }

    /// <summary>
    /// Phase 4 — 디렉터가 강조 샷을 쥐고 있으면 사건 당사자 프레임, 아니면 종전 passive 보드 프레임.
    /// 어느 쪽이든 비-bootstrap 제안(블렌드)이라 하드 스냅이 없고 수동 카메라 우선권이 유지된다.
    /// </summary>
    private void ApplyDirectedCameraFrame(BattleSimulationStep step)
    {
        if (cameraController == null)
        {
            return;
        }

        if (_spectacleDirector.TryGetCameraEmphasis(_directorClockSeconds, out var emphasis))
        {
            cameraController.SetSuggestedFrame(_cameraFramingPolicy.BuildEmphasisFrame(step, emphasis));
            return;
        }

        cameraController.SetSuggestedFrame(_cameraFramingPolicy.BuildPassiveFrame(step, _selectedUnitId));
    }

    private void DrainBeatCallout(BattleSimulationStep step)
    {
        if (!_spectacleDirector.TryDrainCallout(_directorClockSeconds, out var beat))
        {
            return;
        }

        var line = BuildBeatCalloutLine(step, beat);
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        _beatCallouts.Add(new BeatCalloutEntry(line, _directorClockSeconds + BeatCalloutLifetimeSeconds));
        while (_beatCallouts.Count > MaxBeatCalloutLines)
        {
            _beatCallouts.RemoveAt(0);
        }

        RenderCurrentState(step);
    }

    private void PruneExpiredBeatCallouts(BattleSimulationStep step)
    {
        if (_beatCallouts.RemoveAll(entry => entry.ExpiresAtSeconds <= _directorClockSeconds) > 0)
        {
            RenderCurrentState(step);
        }
    }

    private string BuildBeatCalloutLine(BattleSimulationStep step, CombatBeat beat)
    {
        var label = BattleReadabilityFormatter.BuildBeatLabel(beat.Type, LocaleCode);
        switch (beat.Type)
        {
            case CombatBeatType.SynergyActivated:
            {
                var sidePrefix = beat.Side == TeamSide.Ally ? string.Empty : "적 ";
                return $"{sidePrefix}{label} | {ResolveSynergyCalloutName(beat.Tag)}";
            }

            case CombatBeatType.ComboConsumed:
            case CombatBeatType.ComboPrimerApplied:
            {
                var source = ResolveBattleEventUnitName(step, beat.SourceId?.Value, null);
                var target = ResolveBattleEventUnitName(step, beat.TargetId?.Value, null);
                return $"{label} | {source} → {target}";
            }

            default:
            {
                var subject = ResolveBattleEventUnitName(step, beat.TargetId?.Value ?? beat.SourceId?.Value, null);
                return $"{label} | {subject}";
            }
        }
    }

    // 시너지 식별자 형식: "synergy:{id}:{threshold}"(authored) 또는 "race:/class:{id}:{count}"(V1 폴백).
    // authored 는 콘텐츠 표시명으로 해석하고, 폴백은 식별자를 사람이 읽게만 다듬는다.
    private string ResolveSynergyCalloutName(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "-";
        }

        var parts = tag.Split(':');
        if (parts.Length >= 2 && string.Equals(parts[0], "synergy", StringComparison.Ordinal))
        {
            var resolved = _contentText?.GetSynergyName(parts[1]);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return BattleReadabilityFormatter.HumanizeToken(parts.Length >= 2 ? parts[1] : tag);
    }

    private void RunBattle()
    {
        if (!EnsureReady() || !EnsureViewReady())
        {
            return;
        }

        _battleFinishedHandled = false;
        _totalEventCount = 0;
        _lastBattleSummaryRecord = null;
        _selectedUnitId = string.Empty;
        _settingsVisible = false;
        _unitDetailVisible = false;
        _unitDetailTab = BattleUnitDetailTab.Overview;
        _settingsStatusText = string.Empty;
        _recentLogs.Clear();
        _decisiveTimeline.Clear();
        _highlightLedger.Reset();
        _spectacleDirector.Reset();
        _beatCallouts.Clear();

        // 전투 구성은 세션 단일 소스(TryBuildSelectedBattleState)에 위임 — 헤드리스 sim
        // (TryResolveSelectedBattleNodeViaSimulation)과 동일 합성(BattleFactory + 인카운터 bootstrap)을 공유한다.
        // 씬은 그 결과(state/encounter/allySnapshot)를 재생·HUD·replay 조립에 소비한다("구성은 세션, 재생은 소비자").
        BattleState simulationState;
        ResolvedEncounterContext encounter;
        BattleLoadoutSnapshot allySnapshot;
        string buildError;
        try
        {
            if (!_root.SessionState.TryBuildSelectedBattleState(out simulationState, out encounter, out allySnapshot, out buildError))
            {
                RenderBattleSetupFailure(buildError);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BattleScreenController] Battle setup threw an exception. {ex}");
            RenderErrorState(Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.error.setup_failed",
                "The battle could not be prepared."));
            return;
        }

        _compiledSnapshot = allySnapshot;
        _resolvedEncounterContext = encounter;
        _enemyLoadouts = encounter.Enemies;
        _battleStartedAtUtc = DateTime.UtcNow.ToString("O");

        _simulator = new BattleSimulator(simulationState, MaxBattleSteps);
        _policy = new BattlePlaybackPolicy(
            _root.SessionState.IsQuickBattleSmokeActive
                ? BattlePlaybackMode.QuickBattle
                : BattlePlaybackMode.InGame);
        _timeline = new BattleTimelineController();
        _timeline.Initialize(_simulator, _simulator.CurrentStep, MaxBattleSteps);
        _timeline.ConfigureStartupHold(BattlePresentationController.StartupHoldSeconds);

        presentationController.Initialize(_simulator.CurrentStep, BuildBattleMapSelectionContext(encounter.Context));
        presentationController.ConfigureSkillPresentations(CollectBattleSkillSpecs());
        presentationController.ApplyOptions(_presentationOptions);
        EnsureSelectedUnit(_simulator.CurrentStep);
        presentationController.SetFocus(_simulator.CurrentStep, _selectedUnitId);
        IngestOpeningStep();
        RenderCurrentState(_simulator.CurrentStep);
        ApplyBootstrapCameraFrame(_simulator.CurrentStep);

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(true);
        }
    }

    /// <summary>
    /// step 0 은 타임라인 전환의 currentStep 이 될 수 없다(전환은 step 1 부터) — 개전 beat
    /// (시너지 발동/개전 효과)을 디렉터와 원장에 1회 직접 공급한다. 이 호출이 없으면 개전 발현이
    /// 실전에서 영구 침묵한다(리뷰 확정 결함).
    /// </summary>
    private void IngestOpeningStep()
    {
        if (_simulator == null)
        {
            return;
        }

        _spectacleDirector.IngestStep(_simulator.CurrentStep, _directorClockSeconds);
        _highlightLedger.Record(_simulator.CurrentStep);
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
        if (IsSmokeLane && BattleActivityMetricsLog.TryFormatPositionalSummary(result.TelemetryEvents, out var positionalSummary))
        {
            // 전투테스트 검증 surface — detector 발동 횟수가 0인지 아닌지를 콘솔에서 즉시 판정한다.
            Debug.Log($"[CombatSandbox] {positionalSummary}");
        }

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

        // wave-33-progression: result.FinalUnits를 함께 전달 → ally hero unit별 surviving HP가
        // HeroInstanceRecord에, victory XP가 HeroProgressionRecord에 반영된다.
        var victory = winner == TeamSide.Ally;
        _lastBattleSummaryRecord = BuildBattleSummaryRecord(victory, result.StepCount, _totalEventCount);
        // 게임의 중심 카타르시스 — 원장이 집계한 "내 진형이 만든 그림"을 보상 화면으로 운반(전투 피드와 동일 소스).
        var formationPayoff = _highlightLedger.BuildFormationPayoff(
            actorId => ResolveBattleEventUnitName(currentStep, actorId, null));
        _root.SessionState.MarkBattleResolved(
            victory,
            result.StepCount,
            _totalEventCount,
            result.FinalUnits,
            formationPayoff);
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

    private string BuildSelectedUnitRecord(string unitId)
    {
        var lines = _recentLogs
            .Where(eventData => string.Equals(eventData.ActorId.Value, unitId, StringComparison.Ordinal)
                                || string.Equals(eventData.TargetId?.Value, unitId, StringComparison.Ordinal))
            .TakeLast(8)
            .Select(eventData =>
            {
                var isActor = string.Equals(eventData.ActorId.Value, unitId, StringComparison.Ordinal);
                var subject = isActor
                    ? Localize(GameLocalizationTables.UIBattle, "ui.battle.record.actor", "Acted")
                    : Localize(GameLocalizationTables.UIBattle, "ui.battle.record.target", "Received");
                var target = string.IsNullOrWhiteSpace(eventData.TargetName)
                    ? string.Empty
                    : $" -> {ResolveBattleEventUnitName(_timeline?.CurrentStep, eventData.TargetId?.Value, eventData.TargetName)}";
                var value = Mathf.Abs(eventData.Value) > 0.01f ? $" {eventData.Value:0.#}" : string.Empty;
                return $"{eventData.TimeSeconds:0.0}s  {subject}: {BattleReadabilityFormatter.BuildShortEventVerb(eventData, LocaleCode)}{value}{target}";
            })
            .ToList();

        return lines.Count == 0
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.detail.record.empty", "No notable personal events yet.")
            : string.Join("\n", lines);
    }

    private TeamTacticProfile? ResolveTeamTactic(TeamSide side)
    {
        if (_simulator != null)
        {
            return _simulator.State.GetTeamTactic(side);
        }

        if (side == TeamSide.Ally)
        {
            return _compiledSnapshot?.TeamTactic;
        }

        return _enemyLoadouts.FirstOrDefault(loadout => loadout.TeamTactic != null)?.TeamTactic;
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
                var target = ResolveBattleEventUnitName(step, evt.TargetId?.Value, evt.TargetName);
                _decisiveTimeline.Add(IsKoreanLocale
                    ? $"{step.TimeSeconds:0.0}s | {target} 전투불능"
                    : $"{step.TimeSeconds:0.0}s | {target} went down");
            }
            else if (evt.LogCode == BattleLogCode.ActiveSkillHeal)
            {
                var actor = ResolveBattleEventUnitName(step, evt.ActorId.Value, evt.ActorName);
                var target = ResolveBattleEventUnitName(step, evt.TargetId?.Value, evt.TargetName);
                _decisiveTimeline.Add(IsKoreanLocale
                    ? $"{step.TimeSeconds:0.0}s | {actor} -> {target} 회복 {evt.Value:0}"
                    : $"{step.TimeSeconds:0.0}s | {actor} restored {target} for {evt.Value:0}");
            }
            else if (evt.ActionType == BattleActionType.ActiveSkill && evt.Value > 0)
            {
                var actor = ResolveBattleEventUnitName(step, evt.ActorId.Value, evt.ActorName);
                var target = ResolveBattleEventUnitName(step, evt.TargetId?.Value, evt.TargetName);
                _decisiveTimeline.Add(IsKoreanLocale
                    ? $"{step.TimeSeconds:0.0}s | {actor} -> {target} 스킬 {evt.Value:0}"
                    : $"{step.TimeSeconds:0.0}s | {actor} used a skill on {target} for {evt.Value:0}");
            }
        }
    }

    private string ResolveBattleEventUnitName(BattleSimulationStep? step, string? unitId, string? fallbackName)
    {
        var unit = step?.Units.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(unitId)
            && string.Equals(candidate.Id, unitId, StringComparison.Ordinal));
        if (unit == null && step != null && !string.IsNullOrWhiteSpace(fallbackName))
        {
            unit = step.Units.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, fallbackName, StringComparison.Ordinal));
        }

        if (unit != null)
        {
            return _metadataFormatter.BuildOverhead(unit).Header;
        }

        if (string.IsNullOrWhiteSpace(fallbackName))
        {
            return "-";
        }

        var token = BattleReadabilityFormatter.HumanizeToken(fallbackName, fallbackName);
        return token.Any(ch => ch > 127) ? token : ToTitleCase(token);
    }

    private bool IsKoreanLocale => string.Equals(LocaleCode, "ko", StringComparison.OrdinalIgnoreCase);

    private string LocaleCode => _localization?.CurrentLocale?.Identifier.Code ?? string.Empty;

    private static string ToTitleCase(string value)
    {
        var words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            words[i] = word.Length == 1
                ? word.ToUpperInvariant()
                : char.ToUpperInvariant(word[0]) + word[1..];
        }

        return words.Length == 0 ? value : string.Join(" ", words);
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
            BattleSummary = _lastBattleSummaryRecord,
        };
    }

    private BattleSummaryRecord BuildBattleSummaryRecord(bool victory, int stepCount, int eventCount)
    {
        var session = _root.SessionState;
        var node = session.GetSelectedExpeditionNode() ?? session.GetCurrentExpeditionNode();
        return new BattleSummaryRecord(
            session.SelectedCampaignChapterId,
            session.SelectedCampaignSiteId,
            node?.Id ?? string.Empty,
            node?.Index ?? session.CurrentExpeditionNodeIndex,
            victory,
            stepCount,
            eventCount,
            session.ActiveRun?.Overlay.RewardSourceId ?? node?.RewardSourceId ?? string.Empty,
            node?.LabelKey ?? string.Empty);
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
