using System.Collections;
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
    private float _playbackSpeed = 1f;
    private bool _isPaused;
    private bool _playbackFinished;
    public bool IsPlaybackFinished => _playbackFinished;

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

    public void SetSpeed1() => SetSpeed(1f);
    public void SetSpeed2() => SetSpeed(2f);
    public void SetSpeed4() => SetSpeed(4f);

    public void TogglePause()
    {
        if (!EnsureReady()) return;
        _isPaused = !_isPaused;
        presentationController.SetPaused(_isPaused);
        RefreshSpeedText();
        statusText.text = _isPaused ? "재생 일시정지" : "재생 재개";
    }

    public void ContinueToReward()
    {
        if (!EnsureReady()) return;
        if (!_playbackFinished)
        {
            SetResult("전투 재생이 아직 끝나지 않았습니다.");
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

        cam.transform.position = new Vector3(0f, 8f, -8f);
        cam.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
    }

    private void RunBattle()
    {
        if (!EnsureReady()) return;

        titleText.text = "Battle Observer UI";
        resultText.text = "전투 진행 중";
        statusText.text = "Replay track 생성 중";
        logText.text = "전투 시작 준비중";
        progressFill.fillAmount = 0f;
        _playbackFinished = false;
        _isPaused = false;
        _recentLogs.Clear();
        SetSpeed(1f);

        var allyDefinitions = BuildAllyDefinitions();
        if (allyDefinitions.Count == 0)
        {
            SetResult("전투에 투입할 아군이 없습니다.");
            return;
        }

        var enemyDefinitions = BuildEnemyDefinitions();
        var simulationState = BattleFactory.Create(allyDefinitions, enemyDefinitions);
        var replaySeedState = CloneState(simulationState);
        var result = BattleResolver.Run(simulationState, 50);
        var track = BattleReplayBuilder.Build(replaySeedState, result);

        presentationController.Initialize(track);
        presentationController.ApplyOptions(_presentationOptions);
        presentationController.SetPaused(false);
        settingsPanelController.Initialize(_presentationOptions, ApplyPresentationOptions);
        ApplyPresentationOptions(_presentationOptions);
        _root.SessionState.MarkBattleResolved(
            result.Winner == TeamSide.Ally,
            $"{result.Winner} / {result.TickCount} ticks / {result.Events.Count} events");

        StartCoroutine(PlaybackTrack(track));
    }

    private IEnumerator PlaybackTrack(BattleReplayTrack track)
    {
        foreach (var frame in track.Frames)
        {
            ApplyFrame(track, frame);

            var duration = Mathf.Max(0.35f, frame.DurationSeconds);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (!_isPaused)
                {
                    elapsed += Time.deltaTime * _playbackSpeed;
                }

                progressFill.fillAmount = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
        }

        progressFill.fillAmount = 1f;
        resultText.text = track.Winner == TeamSide.Ally ? "승리" : "패배";
        statusText.text = $"전투 종료 | {track.TickCount} ticks | {track.EventCount} events";
        _playbackFinished = true;
    }

    private void ApplyFrame(BattleReplayTrack track, BattleReplayFrame frame)
    {
        presentationController.PresentFrame(frame);
        RefreshHp(frame.ActorStates);
        RefreshStatus(track, frame);

        switch (frame.FrameKind)
        {
            case BattleReplayFrameKind.Intro:
                PushLog("전투 개시");
                resultText.text = "전투 진행 중";
                break;
            case BattleReplayFrameKind.Event:
                PushLog(BuildLogLine(frame));
                break;
            case BattleReplayFrameKind.Result:
                PushLog(track.Winner == TeamSide.Ally ? "결과: 승리" : "결과: 패배");
                resultText.text = track.Winner == TeamSide.Ally ? "승리" : "패배";
                break;
        }
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

    private void RefreshHp(IReadOnlyList<BattleReplayActorSnapshot> actors)
    {
        allyHpText.text = BuildHpText("아군 HP", actors.Where(actor => actor.Side == TeamSide.Ally));
        enemyHpText.text = BuildHpText("적군 HP", actors.Where(actor => actor.Side == TeamSide.Enemy));
    }

    private void RefreshStatus(BattleReplayTrack track, BattleReplayFrame frame)
    {
        RefreshSpeedText();
        var stepText = $"{frame.SequenceIndex + 1}/{track.Frames.Count}";
        if (frame.FrameKind == BattleReplayFrameKind.Intro)
        {
            statusText.text = $"Tick 0 | 전투 개시 | Step {stepText}";
            return;
        }

        if (frame.FrameKind == BattleReplayFrameKind.Result)
        {
            statusText.text = $"Tick {track.TickCount} | 결과 확정 | Step {stepText}";
            return;
        }

        var source = string.IsNullOrWhiteSpace(frame.SourceName) ? "-" : frame.SourceName;
        var target = string.IsNullOrWhiteSpace(frame.TargetName) ? "-" : frame.TargetName;
        var action = frame.ActionType?.ToString() ?? "Unknown";
        var pauseLabel = _isPaused ? " | Paused" : string.Empty;
        statusText.text = $"Tick {frame.Tick} | {source} -> {target} | {action} {frame.Value:0}{pauseLabel} | Step {stepText}";
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
        var heroIds = _root.SessionState.BattleDeployHeroIds.Count > 0
            ? _root.SessionState.BattleDeployHeroIds
            : _root.SessionState.Profile.Heroes.Take(4).Select(hero => hero.HeroId).ToList();

        return heroIds
            .Select(id => _root.SessionState.Profile.Heroes.First(h => h.HeroId == id))
            .Select((hero, index) => BuildDefinitionFromHero(hero, index < 2 ? RowPosition.Front : RowPosition.Back))
            .ToList();
    }

    private static IReadOnlyList<UnitDefinition> BuildEnemyDefinitions()
    {
        return new[]
        {
            BuildEnemy("enemy-1", "Enemy 1", "undead", "vanguard", RowPosition.Front),
            BuildEnemy("enemy-2", "Enemy 2", "beastkin", "duelist", RowPosition.Front),
            BuildEnemy("enemy-3", "Enemy 3", "human", "ranger", RowPosition.Back),
            BuildEnemy("enemy-4", "Enemy 4", "undead", "mystic", RowPosition.Back)
        };
    }

    private static UnitDefinition BuildEnemy(string id, string name, string raceId, string classId, RowPosition row)
    {
        return new UnitDefinition(
            id,
            name,
            raceId,
            classId,
            row,
            BuildEnemyBaseStats(classId),
            BuildTactics(classId),
            BuildSkills(classId));
    }

    private static UnitDefinition BuildDefinitionFromHero(SM.Persistence.Abstractions.Models.HeroInstanceRecord hero, RowPosition row)
    {
        return new UnitDefinition(
            hero.HeroId,
            hero.Name,
            hero.RaceId,
            hero.ClassId,
            row,
            BuildBaseStats(hero.ClassId),
            BuildTactics(hero.ClassId),
            BuildSkills(hero.ClassId));
    }

    private static Dictionary<StatKey, float> BuildBaseStats(string classId)
    {
        return classId switch
        {
            "vanguard" => new Dictionary<StatKey, float> { { StatKey.MaxHealth, 20 }, { StatKey.Attack, 5 }, { StatKey.Defense, 3 }, { StatKey.Speed, 2 }, { StatKey.HealPower, 1 } },
            "duelist" => new Dictionary<StatKey, float> { { StatKey.MaxHealth, 16 }, { StatKey.Attack, 6 }, { StatKey.Defense, 2 }, { StatKey.Speed, 4 }, { StatKey.HealPower, 1 } },
            "ranger" => new Dictionary<StatKey, float> { { StatKey.MaxHealth, 14 }, { StatKey.Attack, 5 }, { StatKey.Defense, 1 }, { StatKey.Speed, 5 }, { StatKey.HealPower, 1 } },
            "mystic" => new Dictionary<StatKey, float> { { StatKey.MaxHealth, 12 }, { StatKey.Attack, 3 }, { StatKey.Defense, 1 }, { StatKey.Speed, 3 }, { StatKey.HealPower, 4 } },
            _ => new Dictionary<StatKey, float> { { StatKey.MaxHealth, 15 }, { StatKey.Attack, 4 }, { StatKey.Defense, 2 }, { StatKey.Speed, 3 }, { StatKey.HealPower, 1 } }
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
                new TacticRule(1, TacticConditionType.AllyHpBelow, 0.5f, BattleActionType.ActiveSkill, TargetSelectorType.LowestHpAlly, "heal"),
                new TacticRule(2, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy)
            };
        }

        return new[]
        {
            new TacticRule(1, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
            new TacticRule(2, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self)
        };
    }

    private static IReadOnlyList<SkillDefinition> BuildSkills(string classId)
    {
        if (classId == "mystic")
        {
            return new[] { new SkillDefinition("heal", "Heal", SkillKind.Heal, 3f, 2) };
        }

        return new[] { new SkillDefinition("strike", "Strike", SkillKind.Strike, 2f, classId == "ranger" ? 2 : 1) };
    }

    private static string BuildHpText(string title, IEnumerable<BattleReplayActorSnapshot> units)
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

    private static string BuildLogLine(BattleReplayFrame frame)
    {
        var source = string.IsNullOrWhiteSpace(frame.SourceName) ? "?" : frame.SourceName;
        var target = string.IsNullOrWhiteSpace(frame.TargetName) ? "?" : frame.TargetName;
        return frame.ActionType switch
        {
            BattleActionType.BasicAttack => $"T{frame.Tick} {source}가 {target}에게 {frame.Value:0} 피해",
            BattleActionType.ActiveSkill when frame.Note == "heal_skill" => $"T{frame.Tick} {source}가 {target}를 {frame.Value:0} 회복",
            BattleActionType.ActiveSkill => $"T{frame.Tick} {source}가 {target}에게 스킬 {frame.Value:0}",
            BattleActionType.WaitDefend => $"T{frame.Tick} {source}가 방어 자세",
            _ => $"T{frame.Tick} {source} {frame.ActionType}"
        };
    }

    private static BattleState CloneState(BattleState state)
    {
        var allies = state.Allies.Select(CloneUnit).ToList();
        var enemies = state.Enemies.Select(CloneUnit).ToList();
        return new BattleState(allies, enemies);
    }

    private static UnitSnapshot CloneUnit(UnitSnapshot unit)
    {
        var definition = new UnitDefinition(
            unit.Definition.Id,
            unit.Definition.Name,
            unit.Definition.RaceId,
            unit.Definition.ClassId,
            unit.Definition.Row,
            new Dictionary<StatKey, float>(unit.Definition.BaseStats),
            unit.Definition.Tactics.ToList(),
            unit.Definition.Skills.ToList(),
            unit.Definition.Packages?.ToList());

        return new UnitSnapshot(unit.Id, unit.Side, definition);
    }
}
