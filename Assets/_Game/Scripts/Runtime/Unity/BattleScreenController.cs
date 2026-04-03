using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Meta.Model;
using SM.Meta.Services;
using UnityEngine;
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
    [SerializeField] private Image progressFill = null!;
    [SerializeField] private Image allySummaryPanel = null!;
    [SerializeField] private Image enemySummaryPanel = null!;
    [SerializeField] private BattlePresentationController presentationController = null!;
    [SerializeField] private BattleSettingsPanelController settingsPanelController = null!;

    private readonly List<BattleEvent> _recentLogs = new();
    private readonly BattlePresentationOptions _presentationOptions = BattlePresentationOptions.CreateDefault();
    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private BattleSimulator? _simulator;
    private BattleLoadoutSnapshot? _compiledSnapshot;
    private IReadOnlyList<BattleUnitLoadout> _enemyLoadouts = Array.Empty<BattleUnitLoadout>();
    private ResolvedEncounterContext? _resolvedEncounterContext;
    private string _battleStartedAtUtc = string.Empty;
    private BattleSimulationStep? _previousStep;
    private BattleSimulationStep? _currentStep;
    private float _playbackSpeed = 1f;
    private float _stepAccumulator;
    private bool _isPaused;
    private bool _battleFinished;
    private int _totalEventCount;

    public bool IsPlaybackFinished => _battleFinished;
    public bool IsBattleFinished => _battleFinished;
    public BattleSimulationStep? LatestStep => _currentStep;
    public TeamPostureType? ActiveAllyPosture => _simulator?.State.AllyPosture;

    private void Start()
    {
        _root = GameSessionRoot.Instance!;
        if (_root == null)
        {
            SetResult("GameSessionRoot가 없습니다.");
            return;
        }

        _localization = _root.Localization;
        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Battle);
        SetupCamera();
        RunBattle();
    }

    private void OnDestroy()
    {
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
    }

    private void Update()
    {
        if (_simulator == null || _currentStep == null || _previousStep == null)
        {
            return;
        }

        if (!_battleFinished && !_isPaused)
        {
            _stepAccumulator += Time.deltaTime * _playbackSpeed;
            while (_stepAccumulator >= _simulator.State.FixedStepSeconds && !_battleFinished)
            {
                _stepAccumulator -= _simulator.State.FixedStepSeconds;
                AdvanceSimulation();
            }
        }

        var fixedStep = Mathf.Max(0.0001f, _simulator.State.FixedStepSeconds);
        var alpha = _battleFinished ? 1f : Mathf.Clamp01(_stepAccumulator / fixedStep);
        presentationController.SetBlend(_previousStep, _currentStep, alpha);
        progressFill.fillAmount = Mathf.Clamp01((float)_currentStep.StepIndex / MaxBattleSteps);
    }

    public void SetSpeed1() => SetSpeed(1f);
    public void SetSpeed2() => SetSpeed(2f);
    public void SetSpeed4() => SetSpeed(4f);

    public void TogglePause()
    {
        if (!EnsureReady()) return;
        _isPaused = !_isPaused;
        presentationController.SetPaused(_isPaused);
        RefreshSpeedText();
        statusText.text = _isPaused
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.status.paused", "Battle paused")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.status.resumed", "Battle resumed");
    }

    public void ContinueToReward()
    {
        if (!EnsureReady()) return;
        if (!_battleFinished)
        {
            SetResult("전투가 아직 끝나지 않았습니다.");
            return;
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

        _root = GameSessionRoot.Instance!;
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
        _playbackSpeed = speed;
        RefreshSpeedText();
    }

    private void RefreshSpeedText()
    {
        speedText.text = _isPaused
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.paused", "Speed x{0:0} | Paused", _playbackSpeed)
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.speed.active", "Speed x{0:0}", _playbackSpeed);
    }

    private void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.transform.position = new Vector3(0.4f, 7.8f, -9.1f);
        cam.transform.rotation = Quaternion.Euler(33f, -12f, 0f);
    }

    private void RunBattle()
    {
        if (!EnsureReady()) return;

        titleText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.title", "Battle Observer UI");
        resultText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress");
        statusText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.status.initializing", "Initializing live simulation");
        logText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.log.preparing", "Preparing battle log");
        progressFill.fillAmount = 0f;
        _battleFinished = false;
        _isPaused = false;
        _stepAccumulator = 0f;
        _totalEventCount = 0;
        _recentLogs.Clear();
        SetSpeed(1f);

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
        _previousStep = _simulator.CurrentStep;
        _currentStep = _simulator.CurrentStep;

        presentationController.Initialize(_currentStep);
        presentationController.ApplyOptions(_presentationOptions);
        presentationController.SetPaused(false);
        settingsPanelController.Initialize(_presentationOptions, ApplyPresentationOptions);
        ApplyPresentationOptions(_presentationOptions);
        RefreshHud(_currentStep);
    }

    private void AdvanceSimulation()
    {
        if (_simulator == null || _currentStep == null)
        {
            return;
        }

        _previousStep = _currentStep;
        _currentStep = _simulator.Step();
        _totalEventCount += _currentStep.Events.Count;
        presentationController.PushStep(_previousStep, _currentStep);
        RefreshHud(_currentStep);

        if (_currentStep.IsFinished)
        {
            FinishBattle();
        }
    }

    private void FinishBattle()
    {
        if (_simulator == null || _currentStep == null || _battleFinished || _compiledSnapshot == null)
        {
            return;
        }

        _battleFinished = true;
        progressFill.fillAmount = 1f;
        resultText.text = _currentStep.Winner == TeamSide.Ally
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
            : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
        statusText.text = Localize(
            GameLocalizationTables.UIBattle,
            "ui.battle.status.finished",
            "Battle finished | {0} steps | {1} events",
            _currentStep.StepIndex,
            _totalEventCount);
        var winner = _currentStep.Winner ?? TeamSide.Ally;
        var result = _simulator.RunToEnd();
        var replay = ReplayAssembler.Assemble(
            _compiledSnapshot,
            _enemyLoadouts,
            result,
            _resolvedEncounterContext?.Context.BattleSeed ?? 0,
            _battleStartedAtUtc,
            System.DateTime.UtcNow.ToString("O"));
        _root.SessionState.RecordBattleAudit(replay);
        _root.SessionState.MarkBattleResolved(
            winner == TeamSide.Ally,
            _currentStep.StepIndex,
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
        RefreshSpeedText();
        var pauseLabel = _isPaused
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

    private static string BuildHpText(string title, IEnumerable<BattleUnitReadModel> units)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        foreach (var unit in units)
        {
            var marker = unit.IsAlive ? "-" : "x";
            sb.AppendLine($"{marker} {unit.Name}: {unit.CurrentHealth:0}/{unit.MaxHealth:0}");
        }

        return sb.ToString();
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

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        if (_currentStep == null)
        {
            return;
        }

        titleText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.title", "Battle Observer UI");
        RefreshHp(_currentStep.Units);
        RefreshStatus(_currentStep);
        RefreshLogText();
        RefreshSpeedText();

        if (_battleFinished)
        {
            resultText.text = _currentStep.Winner == TeamSide.Ally
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.result.victory", "Victory")
                : Localize(GameLocalizationTables.UIBattle, "ui.battle.result.defeat", "Defeat");
        }
        else
        {
            resultText.text = Localize(GameLocalizationTables.UIBattle, "ui.battle.result.in_progress", "Battle in progress");
        }
    }
}
