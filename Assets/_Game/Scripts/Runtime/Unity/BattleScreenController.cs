using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;
using SM.Meta.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleScreenController : MonoBehaviour
{
    private const int MaxRecentLogLines = 8;
    private const int MaxBattleSteps = BattleSimulator.DefaultMaxSteps;

    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text allyHpText = null!;
    [SerializeField] private Text enemyHpText = null!;
    [SerializeField] private Text logText = null!;
    [SerializeField] private Text resultText = null!;
    [SerializeField] private Text speedText = null!;
    [SerializeField] private Text statusText = null!;
    [SerializeField] private Text pauseButtonLabel = null!;
    [SerializeField] private Image progressFill = null!;
    [SerializeField] private Image allySummaryPanel = null!;
    [SerializeField] private Image enemySummaryPanel = null!;
    [SerializeField] private BattlePresentationController presentationController = null!;
    [SerializeField] private BattleSettingsPanelController settingsPanelController = null!;
    [SerializeField] private BattleCameraController cameraController = null!;

    private BattleTimelineScrubberView? _scrubberView;

    private readonly List<BattleEvent> _recentLogs = new();
    private readonly List<string> _decisiveTimeline = new();
    private readonly BattlePresentationOptions _presentationOptions = BattlePresentationOptions.CreateDefault();
    private string _selectedUnitId = string.Empty;
    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private BattleSimulator? _simulator;
    private BattleLoadoutSnapshot? _compiledSnapshot;
    private IReadOnlyList<BattleUnitLoadout> _enemyLoadouts = Array.Empty<BattleUnitLoadout>();
    private ResolvedEncounterContext? _resolvedEncounterContext;
    private string _battleStartedAtUtc = string.Empty;
    private int _totalEventCount;

    private BattleTimelineController? _timeline;
    private BattlePlaybackPolicy _policy = new(BattlePlaybackMode.QuickBattle);

    // --- New Input System actions for keyboard shortcuts ---
    private InputAction _toggleDebugAction = null!;
    private InputAction _stepOnceAction = null!;
    private InputAction _restartAction = null!;
    private InputAction _cycleUnitAction = null!;
    private InputAction _togglePauseAction = null!;

    public bool IsPlaybackFinished => _timeline?.IsFinished ?? false;
    public bool IsBattleFinished => _timeline?.IsFinished ?? false;
    public BattleSimulationStep? LatestStep => _timeline?.CurrentStep;
    public TeamPostureType? ActiveAllyPosture => _simulator?.State.AllyPosture;

    private static readonly Vector3 DefaultCameraPosition = new(0.4f, 7.8f, -9.1f);
    private static readonly Quaternion DefaultCameraRotation = Quaternion.Euler(33f, -12f, 0f);

    private void Start()
    {
        _root = GameSessionRoot.EnsureInstance();
        if (_root == null)
        {
            SetResult("GameSessionRoot가 없습니다.");
            return;
        }

        _localization = _root.Localization;
        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Battle);

        CreateInputActions();

        if (cameraController != null)
        {
            cameraController.Initialize(DefaultCameraPosition, DefaultCameraRotation);
        }
        else
        {
            SetupCameraFallback();
        }

        RunBattle();
    }

    private void OnDestroy()
    {
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }

        DisposeInputActions();
    }

    private void Update()
    {
        HandleKeyboardShortcuts();

        if (_timeline == null) return;

        var stepped = _timeline.TryAdvance(Time.deltaTime,
            out var previousStep, out var currentStep, out var alpha);

        if (previousStep == null || currentStep == null) return;

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
        UpdateProgressBar(_timeline.NormalizedProgress);

        if (_presentationOptions.ShowDebugOverlay)
        {
            DrawDebugTargetLines(currentStep);
        }
    }

    private bool _battleFinishedHandled;

    private static void DrawDebugTargetLines(BattleSimulationStep step)
    {
        foreach (var unit in step.Units)
        {
            if (!unit.IsAlive || string.IsNullOrEmpty(unit.TargetId)) continue;
            var target = step.Units.FirstOrDefault(u => u.Id == unit.TargetId);
            if (target == null) continue;

            var from = new Vector3(unit.Position.X, 0.15f, unit.Position.Y);
            var to = new Vector3(target.Position.X, 0.15f, target.Position.Y);
            var color = unit.Side == TeamSide.Ally ? Color.cyan : new Color(1f, 0.5f, 0.2f);
            Debug.DrawLine(from, to, color);
        }
    }

    public void SetSpeed1() => SetSpeed(1f);
    public void SetSpeed2() => SetSpeed(2f);
    public void SetSpeed4() => SetSpeed(4f);

    public void SetScrubberView(BattleTimelineScrubberView scrubber)
    {
        _scrubberView = scrubber;
    }

    public void HandleScrubberSeek(float normalized)
    {
        if (_timeline == null || !_policy.CanSeek(_timeline.IsFinished)) return;
        var targetStep = Mathf.RoundToInt(normalized * MaxBattleSteps);
        _timeline.SeekToStep(targetStep);
        RefreshAfterSeek();
    }

    private void RefreshAfterSeek()
    {
        if (_timeline == null) return;
        var prev = _timeline.PreviousStep;
        var curr = _timeline.CurrentStep;
        if (prev == null || curr == null) return;

        presentationController.PushStep(prev, curr);
        presentationController.SetBlend(prev, curr, 1f);
        UpdateProgressBar(_timeline.NormalizedProgress);
        RefreshHud(curr);
    }

    private void RestartSameSeed()
    {
        if (_compiledSnapshot == null || _resolvedEncounterContext == null) return;
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
        _decisiveTimeline.Clear();
        _selectedUnitId = string.Empty;
        presentationController.Initialize(_simulator.CurrentStep);
        RefreshHud(_simulator.CurrentStep);

        if (cameraController != null)
        {
            cameraController.ResetToDefault();
        }
    }

    public void RebattleNewSeed()
    {
        if (!EnsureReady()) return;
        _root.SessionState.ReloadQuickBattleConfig();
        _root.SessionState.PrepareQuickBattleSmoke();
        _root.SaveProfile();
        RunBattle();

        if (cameraController != null)
        {
            cameraController.ResetToDefault();
        }
    }

    public void ReturnToTownDirect()
    {
        if (!EnsureReady()) return;
        _root.SessionState.ReturnToTownAfterReward();
        _root.SaveProfile();

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(false);
        }

        _root.SceneFlow.ReturnToTown();
    }

    private void CycleSelectedUnit()
    {
        var currentStep = _timeline?.CurrentStep;
        if (currentStep == null) return;
        var alive = currentStep.Units.Where(u => u.IsAlive).OrderBy(u => u.Side).ThenBy(u => u.Id).ToList();
        if (alive.Count == 0) return;
        var currentIndex = alive.FindIndex(u => u.Id == _selectedUnitId);
        _selectedUnitId = alive[(currentIndex + 1) % alive.Count].Id;
    }

    public void TogglePause()
    {
        if (!EnsureReady() || _timeline == null) return;
        if (!_policy.CanPause(_timeline.IsFinished)) return;

        _timeline.TogglePause();
        RefreshPlaybackUi();
        statusText.text = _timeline.IsPaused
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.status.paused", "Battle paused")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.status.resumed", "Battle resumed");
    }

    public void ContinueToReward()
    {
        if (!EnsureReady()) return;
        if (!IsBattleFinished)
        {
            SetResult("전투가 아직 끝나지 않았습니다.");
            return;
        }

        if (cameraController != null)
        {
            cameraController.SetInputEnabled(false);
        }

        _root.SceneFlow.GoToReward();
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
            SetResult("GameSessionRoot가 없습니다.");
            return false;
        }

        return true;
    }

    private void ValidateReferences()
    {
        AssertText(titleText, nameof(titleText));
        AssertText(allyHpText, nameof(allyHpText));
        AssertText(enemyHpText, nameof(enemyHpText));
        AssertText(logText, nameof(logText));
        AssertText(resultText, nameof(resultText));
        AssertText(speedText, nameof(speedText));
        AssertText(statusText, nameof(statusText));
        AssertText(pauseButtonLabel, nameof(pauseButtonLabel));
        AssertImage(progressFill, nameof(progressFill));
        AssertImage(allySummaryPanel, nameof(allySummaryPanel));
        AssertImage(enemySummaryPanel, nameof(enemySummaryPanel));
        if (presentationController == null)
        {
            Debug.LogError("[BattleScreenController] Missing BattlePresentationController reference: presentationController");
        }

        if (settingsPanelController == null)
        {
            Debug.LogError("[BattleScreenController] Missing BattleSettingsPanelController reference: settingsPanelController");
        }
    }

    private static void AssertText(Text text, string fieldName)
    {
        if (text == null)
        {
            Debug.LogError($"[BattleScreenController] Missing Text reference: {fieldName}");
        }
    }

    private static void AssertImage(Image image, string fieldName)
    {
        if (image == null)
        {
            Debug.LogError($"[BattleScreenController] Missing Image reference: {fieldName}");
        }
    }

    private void SetResult(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }

        Debug.LogError($"[BattleScreenController] {message}");
    }

    private void SetSpeed(float speed)
    {
        if (_timeline == null || !_policy.CanControlSpeed(_timeline.IsFinished)) return;
        _timeline.SetSpeed(speed);
        RefreshPlaybackUi();
    }

    private void RefreshSpeedText()
    {
        var isPaused = _timeline?.IsPaused ?? false;
        var playbackSpeed = _timeline?.PlaybackSpeed ?? 1f;
        speedText.text = isPaused
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.paused", "Speed x{0:0} | Paused", playbackSpeed)
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.active", "Speed x{0:0}", playbackSpeed);
    }

    private void RefreshPauseButtonLabel()
    {
        if (pauseButtonLabel == null)
        {
            return;
        }

        var isPaused = _timeline?.IsPaused ?? false;
        pauseButtonLabel.text = isPaused
            ? Localize(GameLocalizationTables.UICommon, "ui.common.resume", "Resume")
            : Localize(GameLocalizationTables.UICommon, "ui.common.pause", "Pause");
    }

    private void RefreshPlaybackUi()
    {
        presentationController.SetPaused(_timeline?.IsPaused ?? false);
        RefreshSpeedText();
        RefreshPauseButtonLabel();
    }

    private void UpdateProgressBar(float normalized)
    {
        if (_scrubberView != null)
        {
            _scrubberView.SetFillAmount(normalized);
        }
        else
        {
            progressFill.fillAmount = normalized;
        }
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
    }

    private void DisposeInputActions()
    {
        _toggleDebugAction?.Dispose();
        _stepOnceAction?.Dispose();
        _restartAction?.Dispose();
        _cycleUnitAction?.Dispose();
        _togglePauseAction?.Dispose();
    }

    private void HandleKeyboardShortcuts()
    {
        if (_toggleDebugAction.WasPressedThisFrame())
        {
            _presentationOptions.ToggleDebugOverlay();
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
        if (cam == null) return;
        cam.transform.position = DefaultCameraPosition;
        cam.transform.rotation = DefaultCameraRotation;
    }

    private void RunBattle()
    {
        if (!EnsureReady()) return;

        titleText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.title", "Battle Observer UI");
        resultText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress");
        statusText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.status.initializing", "Initializing live simulation");
        logText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.log.preparing", "Preparing battle log");
        UpdateProgressBar(0f);
        _battleFinishedHandled = false;
        _totalEventCount = 0;
        _recentLogs.Clear();

        BattleLoadoutSnapshot allySnapshot;
        try
        {
            allySnapshot = _root.SessionState.BuildBattleLoadoutSnapshot();
        }
        catch (System.Exception ex)
        {
            SetResult(ex.Message);
            return;
        }

        if (allySnapshot.Allies.Count == 0)
        {
            SetResult(Localize(GameLocalizationTables.UIBattle, "ui.battle.error.no_allies", "No allied unit is ready for battle."));
            return;
        }

        if (!_root.CombatContentLookup.TryGetCombatSnapshot(out var snapshot, out var lookupError))
        {
            SetResult(lookupError);
            return;
        }

        if (!_root.SessionState.TryResolveCurrentEncounter(out var encounter, out var encounterError))
        {
            SetResult(encounterError);
            return;
        }

        _compiledSnapshot = allySnapshot;
        _resolvedEncounterContext = encounter;
        _enemyLoadouts = encounter.Enemies;
        _battleStartedAtUtc = System.DateTime.UtcNow.ToString("O");
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
        settingsPanelController.Initialize(_presentationOptions, ApplyPresentationOptions);
        ApplyPresentationOptions(_presentationOptions);
        RefreshHud(_simulator.CurrentStep);

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
        UpdateProgressBar(1f);

        var currentStep = _timeline.CurrentStep!;
        resultText.text = currentStep.Winner == TeamSide.Ally
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
        statusText.text = Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.status.finished",
            "Battle finished | {0} steps | {1} events",
            currentStep.StepIndex,
            _totalEventCount);
        var winner = currentStep.Winner ?? TeamSide.Ally;
        var result = _simulator.RunToEnd();
        var replay = ReplayAssembler.Assemble(
            _compiledSnapshot,
            _enemyLoadouts,
            result,
            _resolvedEncounterContext?.Context.BattleSeed ?? 0,
            _battleStartedAtUtc,
            System.DateTime.UtcNow.ToString("O"));
        _root.SessionState.RecordBattleAudit(replay);
        BattleDebugLogWriter.Write(replay, currentStep.Units);
        _root.SessionState.MarkBattleResolved(
            winner == TeamSide.Ally,
            currentStep.StepIndex,
            _totalEventCount);
    }

    private void ApplyPresentationOptions(BattlePresentationOptions options)
    {
        presentationController.ApplyOptions(options);

        var showTeamSummary = options.ShowTeamHpSummary;
        if (allySummaryPanel != null)
        {
            allySummaryPanel.gameObject.SetActive(showTeamSummary);
        }

        if (enemySummaryPanel != null)
        {
            enemySummaryPanel.gameObject.SetActive(showTeamSummary);
        }

        if (allyHpText != null)
        {
            allyHpText.gameObject.SetActive(showTeamSummary);
        }

        if (enemyHpText != null)
        {
            enemyHpText.gameObject.SetActive(showTeamSummary);
        }
    }

    private void RefreshHud(BattleSimulationStep step)
    {
        RefreshHp(step.Units);
        RefreshStatus(step);
        foreach (var eventData in step.Events)
        {
            PushLog(eventData);
        }
    }

    private void RefreshHp(IReadOnlyList<BattleUnitReadModel> actors)
    {
        allyHpText.text = BuildHpText(
            Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.allies", "Allied HP"),
            actors.Where(actor => actor.Side == TeamSide.Ally));
        enemyHpText.text = BuildHpText(
            Localize(GameLocalizationTables.UIBattle, "ui.battle.hp.enemies", "Enemy HP"),
            actors.Where(actor => actor.Side == TeamSide.Enemy));
    }

    private void RefreshStatus(BattleSimulationStep step)
    {
        RefreshPlaybackUi();
        var isPaused = _timeline?.IsPaused ?? false;
        var pauseLabel = isPaused
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.status.pause_suffix", " | Paused")
            : string.Empty;
        if (step.IsFinished)
        {
            statusText.text = Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.resolved",
                "Step {0} | Result resolved{1}",
                step.StepIndex,
                pauseLabel);
            return;
        }

        var lastEvent = step.Events.LastOrDefault();
        if (lastEvent != null)
        {
            statusText.text = Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.last_event",
                "Step {0} | {1} -> {2} | {3} {4:0}{5}",
                step.StepIndex,
                lastEvent.ActorName,
                lastEvent.TargetName ?? "-",
                lastEvent.ActionType,
                lastEvent.Value,
                pauseLabel);
            return;
        }

        var windingUp = step.Units.FirstOrDefault(unit => unit.ActionState == CombatActionState.Windup);
        if (windingUp != null)
        {
            statusText.text = Localize(
                GameLocalizationTables.UIBattle,
                "ui.battle.status.windup",
                "Step {0} | {1} windup {2}% -> {3}{4}",
                step.StepIndex,
                windingUp.Name,
                Mathf.RoundToInt(windingUp.WindupProgress * 100f),
                windingUp.TargetName ?? "-",
                pauseLabel);
            return;
        }

        statusText.text = Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.status.posture",
            "Step {0} | posture {1}{2}",
            step.StepIndex,
            _root.SessionState.SelectedTeamPosture,
            pauseLabel);
    }

    private void PushLog(BattleEvent eventData)
    {
        _recentLogs.Add(eventData);
        while (_recentLogs.Count > MaxRecentLogLines)
        {
            _recentLogs.RemoveAt(0);
        }

        RefreshLogText();
    }

    private string BuildHpText(string title, IEnumerable<BattleUnitReadModel> units)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        foreach (var unit in units)
        {
            var marker = unit.IsAlive ? "-" : "x";
            var displayName = ResolveUnitDisplayName(unit.Name);
            sb.AppendLine($"{marker} {displayName}: {unit.CurrentHealth:0}/{unit.MaxHealth:0}");
        }

        return sb.ToString();
    }

    private string ResolveUnitDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith("content."))
        {
            return name;
        }

        var resolved = Localize(GameLocalizationTables.ContentArchetype, name, name);
        return resolved != name ? resolved : name.Replace("content.archetype.", "").Replace(".name", "");
    }

    private void RefreshLogText()
    {
        logText.text = string.Join("\n", _recentLogs.Select(BuildLogLine));
    }

    private string BuildLogLine(BattleEvent eventData)
    {
        var source = string.IsNullOrWhiteSpace(eventData.ActorName) ? "?" : eventData.ActorName;
        var target = string.IsNullOrWhiteSpace(eventData.TargetName) ? "?" : eventData.TargetName;
        return eventData.LogCode switch
        {
            BattleLogCode.BasicAttackDamage => Localize(GameLocalizationTables.CombatLog, "combat.log.damage", "S{0} {1} dealt {3:0} damage to {2}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.ActiveSkillHeal => Localize(GameLocalizationTables.CombatLog, "combat.log.heal", "S{0} {1} healed {2} for {3:0}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.ActiveSkillDamage => Localize(GameLocalizationTables.CombatLog, "combat.log.skill", "S{0} {1} used a skill on {2} for {3:0}", eventData.StepIndex, source, target, eventData.Value),
            BattleLogCode.WaitDefend => Localize(GameLocalizationTables.CombatLog, "combat.log.guard", "S{0} {1} took a guard stance", eventData.StepIndex, source),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.StatusApplied => Localize(GameLocalizationTables.CombatLog, "combat.log.status_applied", "S{0} {1} applied {2} to {3}", eventData.StepIndex, source, eventData.PayloadId, target),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.StatusRemoved => Localize(GameLocalizationTables.CombatLog, "combat.log.status_removed", "S{0} {1} removed {2}", eventData.StepIndex, target, eventData.PayloadId),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.CleanseTriggered => Localize(GameLocalizationTables.CombatLog, "combat.log.cleanse", "S{0} {1} cleansed {2} on {3}", eventData.StepIndex, source, eventData.PayloadId, target),
            BattleLogCode.Generic when eventData.EventKind == BattleEventKind.ControlResistApplied => Localize(GameLocalizationTables.CombatLog, "combat.log.control_resist", "S{0} {1} gained control resist", eventData.StepIndex, target),
            _ => Localize(GameLocalizationTables.CombatLog, "combat.log.generic", "S{0} {1} {2}", eventData.StepIndex, source, eventData.ActionType)
        };
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

        var style = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
        var bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
        bgTex.Apply();
        var bgStyle = new GUIStyle { normal = { background = bgTex } };

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
            var targetLabel = !string.IsNullOrEmpty(unit.TargetName) ? $"-> {unit.TargetName}" : "";
            var actionLabel = FormatActionState(unit);
            var lockLabel = unit.RetargetLockRemaining > 0.01f ? $" lock:{unit.RetargetLockRemaining:0.0}s" : "";
            var cdLabel = unit.CooldownRemaining > 0.01f ? $" cd:{unit.CooldownRemaining:0.0}s" : "";
            var selectorLabel = !string.IsNullOrEmpty(unit.CurrentSelector) ? $" sel:{unit.CurrentSelector}" : "";
            var guardLabel = unit.FrontlineGuardRadius > 0.01f ? $" guard:{unit.FrontlineGuardRadius:0.#}" : "";

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
        if (string.IsNullOrEmpty(_selectedUnitId)) return;
        var unit = step.Units.FirstOrDefault(u => u.Id == _selectedUnitId);
        if (unit == null) return;

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
        if (_decisiveTimeline.Count == 0) return;
        var startX = Screen.width - 320f;
        var visible = _decisiveTimeline.Count > 8 ? _decisiveTimeline.Skip(_decisiveTimeline.Count - 8).ToList() : _decisiveTimeline;
        var panelRect = new Rect(startX, 4, 316, 16 + visible.Count * 14f);
        GUI.Box(panelRect, GUIContent.none, bgStyle);
        GUI.Label(new Rect(startX, 4, 316, 16), "  <color=#ff6>Decisive Timeline</color>", style);
        var smallStyle = new GUIStyle(style) { fontSize = 10 };
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
        if (!unit.IsAlive) return "Dead";
        if (unit.WindupProgress > 0.01f)
            return $"{unit.ActionState} {Mathf.RoundToInt(unit.WindupProgress * 100f)}%";
        return unit.ActionState.ToString();
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        var currentStep = _timeline?.CurrentStep;
        if (currentStep == null)
        {
            return;
        }

        titleText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.title", "Battle Observer UI");
        RefreshHp(currentStep.Units);
        RefreshStatus(currentStep);
        RefreshLogText();

        if (IsBattleFinished)
        {
            resultText.text = currentStep.Winner == TeamSide.Ally
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
        }
        else
        {
            resultText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress");
        }
    }
}
