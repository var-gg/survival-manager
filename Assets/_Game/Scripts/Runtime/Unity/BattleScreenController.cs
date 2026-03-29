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
    [SerializeField] private Text titleText = null!;
    [SerializeField] private Text allyHpText = null!;
    [SerializeField] private Text enemyHpText = null!;
    [SerializeField] private Text logText = null!;
    [SerializeField] private Text resultText = null!;
    [SerializeField] private Text speedText = null!;

    private readonly List<GameObject> _spawnedActors = new();
    private GameSessionRoot _root = null!;
    private float _playbackSpeed = 1f;
    private bool _playbackFinished;

    private void Awake()
    {
        ValidateReferences();
    }

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
    }

    private static void AssertText(Text text, string fieldName)
    {
        if (text == null)
        {
            Debug.LogError($"[BattleScreenController] Missing Text reference: {fieldName}");
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
        if (speedText != null)
        {
            speedText.text = $"Speed x{speed:0}";
        }
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

        titleText.text = "Battle Debug UI";
        logText.text = "전투 시작 준비중";
        resultText.text = "전투 진행 중";
        _playbackFinished = false;
        SetSpeed(1f);

        foreach (var actor in _spawnedActors)
        {
            if (actor != null)
            {
                Destroy(actor);
            }
        }
        _spawnedActors.Clear();

        var allyDefinitions = BuildAllyDefinitions();
        var enemyDefinitions = BuildEnemyDefinitions();
        var state = BattleFactory.Create(allyDefinitions, enemyDefinitions);

        SpawnTeam(state.Allies.ToList(), true);
        SpawnTeam(state.Enemies.ToList(), false);
        RefreshHp(state);

        var result = BattleResolver.Run(state, 50);
        StartCoroutine(PlaybackResult(state, result));
    }

    private IEnumerator PlaybackResult(BattleState state, BattleResult result)
    {
        var lines = new List<string>();
        foreach (var battleEvent in result.Events)
        {
            lines.Add($"T{battleEvent.Tick} {battleEvent.ActionType} {battleEvent.Note} {battleEvent.Value:0}");
            logText.text = string.Join("\n", lines.TakeLast(16));
            RefreshHp(state);
            yield return new WaitForSeconds(0.35f / _playbackSpeed);
        }

        var victory = result.Winner == TeamSide.Ally;
        resultText.text = victory ? "승리" : "패배";
        _root.SessionState.SetLastBattleResult(victory, $"{result.Winner} / {result.TickCount} ticks / {result.Events.Count} events");
        _playbackFinished = true;
    }

    private void RefreshHp(BattleState state)
    {
        allyHpText.text = BuildHpText("아군 HP", state.Allies);
        enemyHpText.text = BuildHpText("적군 HP", state.Enemies);
    }

    private void SpawnTeam(IReadOnlyList<UnitSnapshot> units, bool ally)
    {
        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            primitive.name = unit.Definition.Name;
            primitive.transform.position = ally
                ? new Vector3(-3f + (i % 2) * 1.5f, 0f, i < 2 ? -1f : 1f)
                : new Vector3(3f - (i % 2) * 1.5f, 0f, i < 2 ? -1f : 1f);

            var renderer = primitive.GetComponent<Renderer>();
            renderer.material.color = ResolveColor(unit.Definition.RaceId, unit.Definition.ClassId, ally);
            _spawnedActors.Add(primitive);
        }
    }

    private static Color ResolveColor(string raceId, string classId, bool ally)
    {
        if (raceId == "human") return ally ? Color.blue : new Color(0.3f, 0.3f, 1f);
        if (raceId == "beastkin") return ally ? Color.green : new Color(0.2f, 0.7f, 0.2f);
        if (raceId == "undead") return ally ? Color.gray : new Color(0.5f, 0.5f, 0.5f);
        if (classId == "mystic") return Color.magenta;
        return ally ? Color.cyan : Color.red;
    }

    private IReadOnlyList<UnitDefinition> BuildAllyDefinitions()
    {
        return _root.SessionState.BattleDeployHeroIds
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
            BuildBaseStats(classId),
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

    private static string BuildHpText(string title, IReadOnlyList<UnitSnapshot> units)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        foreach (var unit in units)
        {
            sb.AppendLine($"- {unit.Definition.Name}: {unit.CurrentHealth:0}/{unit.MaxHealth:0}");
        }
        return sb.ToString();
    }
}
