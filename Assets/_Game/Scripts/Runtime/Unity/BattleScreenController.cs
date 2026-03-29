using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleScreenController : MonoBehaviour
{
    private const int MaxRecentLogLines = 8;
    private const int MaxBattleSteps = 320;

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

    private readonly List<string> _recentLogs = new();
    private readonly BattlePresentationOptions _presentationOptions = BattlePresentationOptions.CreateDefault();
    private GameSessionRoot _root = null!;
    private BattleSimulator? _simulator;
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

        _root.SessionState.SetCurrentScene(SceneNames.Battle);
        SetupCamera();
        RunBattle();
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
        statusText.text = _isPaused ? "전투 일시정지" : "전투 재개";
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
        speedText.text = _isPaused ? $"Speed x{_playbackSpeed:0} | Paused" : $"Speed x{_playbackSpeed:0}";
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

        titleText.text = "Battle Observer UI";
        resultText.text = "전투 진행 중";
        statusText.text = "live simulation 초기화";
        logText.text = "전투 시작 준비중";
        progressFill.fillAmount = 0f;
        _battleFinished = false;
        _isPaused = false;
        _stepAccumulator = 0f;
        _totalEventCount = 0;
        _recentLogs.Clear();
        SetSpeed(1f);

        var allyDefinitions = BuildAllyDefinitions();
        if (allyDefinitions.Count == 0)
        {
            SetResult("전투에 투입할 아군이 없습니다.");
            return;
        }

        var enemyDefinitions = BuildEnemyDefinitions();
        var simulationState = BattleFactory.Create(
            allyDefinitions,
            enemyDefinitions,
            _root.SessionState.SelectedTeamPosture,
            TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds,
            seed: 17);

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
        if (_simulator == null || _currentStep == null || _battleFinished)
        {
            return;
        }

        _battleFinished = true;
        progressFill.fillAmount = 1f;
        resultText.text = _currentStep.Winner == TeamSide.Ally ? "승리" : "패배";
        statusText.text = $"전투 종료 | {_currentStep.StepIndex} steps | {_totalEventCount} events";
        var winner = _currentStep.Winner ?? TeamSide.Ally;
        _root.SessionState.MarkBattleResolved(
            winner == TeamSide.Ally,
            $"{winner} / {_currentStep.StepIndex} steps / {_totalEventCount} events");
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
            PushLog(BuildLogLine(eventData));
        }
    }

    private void RefreshHp(IReadOnlyList<BattleUnitReadModel> actors)
    {
        allyHpText.text = BuildHpText("아군 HP", actors.Where(actor => actor.Side == TeamSide.Ally));
        enemyHpText.text = BuildHpText("적군 HP", actors.Where(actor => actor.Side == TeamSide.Enemy));
    }

    private void RefreshStatus(BattleSimulationStep step)
    {
        RefreshSpeedText();
        var pauseLabel = _isPaused ? " | Paused" : string.Empty;
        if (step.IsFinished)
        {
            statusText.text = $"Step {step.StepIndex} | 결과 확정 {pauseLabel}";
            return;
        }

        var lastEvent = step.Events.LastOrDefault();
        if (lastEvent != null)
        {
            statusText.text = $"Step {step.StepIndex} | {lastEvent.ActorName} -> {lastEvent.TargetName ?? "-"} | {lastEvent.ActionType} {lastEvent.Value:0}{pauseLabel}";
            return;
        }

        var windingUp = step.Units.FirstOrDefault(unit => unit.ActionState == CombatActionState.Windup);
        if (windingUp != null)
        {
            statusText.text = $"Step {step.StepIndex} | {windingUp.Name} windup {Mathf.RoundToInt(windingUp.WindupProgress * 100f)}% -> {windingUp.TargetName ?? "-"}{pauseLabel}";
            return;
        }

        statusText.text = $"Step {step.StepIndex} | posture {_root.SessionState.SelectedTeamPosture}{pauseLabel}";
    }

    private void PushLog(string line)
    {
        _recentLogs.Add(line);
        while (_recentLogs.Count > MaxRecentLogLines)
        {
            _recentLogs.RemoveAt(0);
        }

        logText.text = string.Join("\n", _recentLogs);
    }

    private IReadOnlyList<UnitDefinition> BuildAllyDefinitions()
    {
        _root.SessionState.EnsureBattleDeployReady();
        return _root.SessionState
            .EnumerateDeploymentAssignments()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.HeroId))
            .Select(entry =>
            {
                var hero = _root.SessionState.Profile.Heroes.First(h => h.HeroId == entry.HeroId);
                return BuildDefinitionFromHero(hero, entry.Anchor);
            })
            .ToList();
    }

    private static IReadOnlyList<UnitDefinition> BuildEnemyDefinitions()
    {
        return new[]
        {
            BuildEnemy("enemy-1", "Enemy 1", "undead", "vanguard", DeploymentAnchorId.FrontTop),
            BuildEnemy("enemy-2", "Enemy 2", "beastkin", "duelist", DeploymentAnchorId.FrontBottom),
            BuildEnemy("enemy-3", "Enemy 3", "human", "ranger", DeploymentAnchorId.BackTop),
            BuildEnemy("enemy-4", "Enemy 4", "undead", "mystic", DeploymentAnchorId.BackBottom)
        };
    }

    private static UnitDefinition BuildEnemy(string id, string name, string raceId, string classId, DeploymentAnchorId anchor)
    {
        return new UnitDefinition(
            id,
            name,
            raceId,
            classId,
            anchor,
            BuildEnemyBaseStats(classId),
            BuildTactics(classId),
            BuildSkills(classId));
    }

    private static UnitDefinition BuildDefinitionFromHero(SM.Persistence.Abstractions.Models.HeroInstanceRecord hero, DeploymentAnchorId anchor)
    {
        return new UnitDefinition(
            hero.HeroId,
            hero.Name,
            hero.RaceId,
            hero.ClassId,
            anchor,
            BuildBaseStats(hero.ClassId),
            BuildTactics(hero.ClassId),
            BuildSkills(hero.ClassId));
    }

    private static Dictionary<StatKey, float> BuildBaseStats(string classId)
    {
        return classId switch
        {
            "vanguard" => new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20f,
                [StatKey.Attack] = 5f,
                [StatKey.Defense] = 3f,
                [StatKey.Speed] = 2f,
                [StatKey.HealPower] = 1f,
                [StatKey.MoveSpeed] = 1.65f,
                [StatKey.AttackRange] = 1.2f,
                [StatKey.AggroRadius] = 7f,
                [StatKey.AttackWindup] = 0.25f,
                [StatKey.AttackCooldown] = 1.0f,
                [StatKey.LeashDistance] = 5.2f,
                [StatKey.TargetSwitchDelay] = 0.45f
            },
            "duelist" => new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 16f,
                [StatKey.Attack] = 6f,
                [StatKey.Defense] = 2f,
                [StatKey.Speed] = 4f,
                [StatKey.HealPower] = 1f,
                [StatKey.MoveSpeed] = 2.05f,
                [StatKey.AttackRange] = 1.3f,
                [StatKey.AggroRadius] = 7.5f,
                [StatKey.AttackWindup] = 0.20f,
                [StatKey.AttackCooldown] = 0.85f,
                [StatKey.LeashDistance] = 5.8f,
                [StatKey.TargetSwitchDelay] = 0.35f
            },
            "ranger" => new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 14f,
                [StatKey.Attack] = 5f,
                [StatKey.Defense] = 1f,
                [StatKey.Speed] = 5f,
                [StatKey.HealPower] = 1f,
                [StatKey.MoveSpeed] = 1.8f,
                [StatKey.AttackRange] = 3.2f,
                [StatKey.AggroRadius] = 8f,
                [StatKey.AttackWindup] = 0.18f,
                [StatKey.AttackCooldown] = 1.05f,
                [StatKey.LeashDistance] = 4.6f,
                [StatKey.TargetSwitchDelay] = 0.30f
            },
            "mystic" => new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 12f,
                [StatKey.Attack] = 3f,
                [StatKey.Defense] = 1f,
                [StatKey.Speed] = 3f,
                [StatKey.HealPower] = 4f,
                [StatKey.MoveSpeed] = 1.7f,
                [StatKey.AttackRange] = 2.8f,
                [StatKey.AggroRadius] = 7.2f,
                [StatKey.AttackWindup] = 0.24f,
                [StatKey.AttackCooldown] = 1.10f,
                [StatKey.LeashDistance] = 4.8f,
                [StatKey.TargetSwitchDelay] = 0.35f
            },
            _ => new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 15f,
                [StatKey.Attack] = 4f,
                [StatKey.Defense] = 2f,
                [StatKey.Speed] = 3f,
                [StatKey.HealPower] = 1f,
                [StatKey.MoveSpeed] = 1.75f,
                [StatKey.AttackRange] = 1.5f,
                [StatKey.AggroRadius] = 7f,
                [StatKey.AttackWindup] = 0.22f,
                [StatKey.AttackCooldown] = 0.95f,
                [StatKey.LeashDistance] = 5f,
                [StatKey.TargetSwitchDelay] = 0.35f
            }
        };
    }

    private static Dictionary<StatKey, float> BuildEnemyBaseStats(string classId)
    {
        var stats = BuildBaseStats(classId);
        stats[StatKey.MaxHealth] = Mathf.Max(8f, stats[StatKey.MaxHealth] - 3f);
        stats[StatKey.Attack] = Mathf.Max(2f, stats[StatKey.Attack] - 1f);
        stats[StatKey.Defense] = Mathf.Max(0f, stats[StatKey.Defense] - 1f);
        return stats;
    }

    private static IReadOnlyList<TacticRule> BuildTactics(string classId)
    {
        if (classId == "mystic")
        {
            return new[]
            {
                new TacticRule(1, TacticConditionType.AllyHpBelow, 0.55f, BattleActionType.ActiveSkill, TargetSelectorType.LowestHpAlly, "heal"),
                new TacticRule(2, TacticConditionType.EnemyExposed, 1.5f, BattleActionType.BasicAttack, TargetSelectorType.MostExposedEnemy),
                new TacticRule(3, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
                new TacticRule(4, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self)
            };
        }

        return new[]
        {
            new TacticRule(1, TacticConditionType.EnemyExposed, 1.5f, BattleActionType.ActiveSkill, TargetSelectorType.MostExposedEnemy, "strike"),
            new TacticRule(2, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
            new TacticRule(3, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self)
        };
    }

    private static IReadOnlyList<SkillDefinition> BuildSkills(string classId)
    {
        if (classId == "mystic")
        {
            return new[] { new SkillDefinition("heal", "Heal", SkillKind.Heal, 3f, 3.0f) };
        }

        return new[] { new SkillDefinition("strike", "Strike", SkillKind.Strike, 2f, classId == "ranger" ? 3.6f : 1.5f) };
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

    private static string BuildLogLine(BattleEvent eventData)
    {
        var source = string.IsNullOrWhiteSpace(eventData.ActorName) ? "?" : eventData.ActorName;
        var target = string.IsNullOrWhiteSpace(eventData.TargetName) ? "?" : eventData.TargetName;
        return eventData.ActionType switch
        {
            BattleActionType.BasicAttack => $"S{eventData.StepIndex} {source}가 {target}에게 {eventData.Value:0} 피해",
            BattleActionType.ActiveSkill when eventData.Note == "heal_skill" => $"S{eventData.StepIndex} {source}가 {target}를 {eventData.Value:0} 회복",
            BattleActionType.ActiveSkill => $"S{eventData.StepIndex} {source}가 {target}에게 스킬 {eventData.Value:0}",
            BattleActionType.WaitDefend => $"S{eventData.StepIndex} {source}가 방어 자세",
            _ => $"S{eventData.StepIndex} {source} {eventData.ActionType}"
        };
    }
}
