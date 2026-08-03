using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Passive Board V1 Presenter — selected hero의 board + active node 매핑 → ViewState.
///
/// Sprint 3 wire: CombatContentLookup.TryGetPassiveBoardDefinition으로 board.Nodes read.
/// per-hero `HeroLoadoutRecord.SelectedPassiveNodeIds`로 active state. node 위치는 BoardDepth ring layout.
/// SelectPassiveBoard / TogglePassiveNode edit wire.
///
/// 워크플로우: hero 선택 → 클래스 보드 고정 표시 → node 클릭 toggle → BattleTest stat 즉시 반영.
/// 보드는 hero 클래스로 고정 — 자유 탭 전환 없음 (PassiveBoardDefinition.ClassId = 클래스 단위 트리).
/// </summary>
public sealed class PassiveBoardPresenter : IPassiveBoardActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    private readonly GameSessionState _session;
    private readonly ICombatContentLookup _lookup;
    private readonly IPassiveBoardView _view;
    private readonly ContentTextResolver? _contentText;
    private readonly SpriteLoader _classSprite;
    private readonly SpriteLoader _affixSprite;
    private string _selectedNodeId = string.Empty;
    private string _selectedHeroId = string.Empty;
    private OperationFailure? _toggleFailure;

    public PassiveBoardPresenter(
        GameSessionRoot root,
        IPassiveBoardView view,
        ContentTextResolver contentText,
        SpriteLoader? classSprite = null,
        SpriteLoader? affixSprite = null)
        : this(
            (root ?? throw new ArgumentNullException(nameof(root))).SessionState,
            root.CombatContentLookup,
            view,
            contentText,
            classSprite,
            affixSprite)
    {
    }

    public PassiveBoardPresenter(
        GameSessionState session,
        ICombatContentLookup lookup,
        IPassiveBoardView view,
        ContentTextResolver? contentText = null,
        SpriteLoader? classSprite = null,
        SpriteLoader? affixSprite = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText;
        _classSprite = classSprite ?? (_ => null);
        _affixSprite = affixSprite ?? (_ => null);
    }

    /// <summary>Town hub controller가 selected hero 컨텍스트를 전달 (Sprint 3+ navigation).</summary>
    public void SetSelectedHero(string heroId)
    {
        _selectedHeroId = heroId ?? string.Empty;
        _toggleFailure = null;
        Refresh();
    }

    public void Initialize()
    {
        _view.Bind(this);
        _view.BindClose(Close);
        Refresh();
    }

    public void Open()
    {
        _view.Open();
        Refresh();
    }

    public void Close()
    {
        _view.Close();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    void IPassiveBoardActions.OnNodeSelected(string nodeId)
    {
        _selectedNodeId = nodeId;
        _toggleFailure = null;
        Refresh();
    }

    void IPassiveBoardActions.OnToggleActivateClicked()
    {
        var heroId = ResolveSelectedHeroId();
        if (!string.IsNullOrEmpty(heroId) && !string.IsNullOrEmpty(_selectedNodeId))
        {
            var result = _session.TogglePassiveNode(heroId, _selectedNodeId);
            _toggleFailure = result.IsSuccess ? null : result.Failure;
        }
        Refresh();
    }

    private string ResolveSelectedHeroId()
    {
        if (!string.IsNullOrEmpty(_selectedHeroId)) return _selectedHeroId;
        var heroes = _session.Profile.Heroes;
        return heroes.Count > 0 ? heroes[0].HeroId : string.Empty;
    }

    private PassiveBoardViewState BuildState()
    {
        var session = _session;
        var heroId = ResolveSelectedHeroId();
        var hero = session.Profile.Heroes
            .FirstOrDefault(h => string.Equals(h.HeroId, heroId, StringComparison.Ordinal));
        var heroLoadout = session.Profile.HeroLoadouts
            .FirstOrDefault(record => string.Equals(record.HeroId, heroId, StringComparison.Ordinal));

        // 보드는 hero의 클래스로 고정 — 자유 전환 없음. loadout에 board가 박혀 있으면 그것을 우선.
        var classKey = !string.IsNullOrWhiteSpace(hero?.ClassId) ? hero!.ClassId : "duelist";
        var boardEntry = BoardCatalog.FirstOrDefault(b => string.Equals(b.ClassKey, classKey, StringComparison.Ordinal));
        var boardId = !string.IsNullOrWhiteSpace(heroLoadout?.PassiveBoardId)
            ? heroLoadout!.PassiveBoardId
            : (string.IsNullOrEmpty(boardEntry.BoardId) ? $"board_{classKey}" : boardEntry.BoardId);

        var activeNodeIds = new HashSet<string>(
            heroLoadout?.SelectedPassiveNodeIds ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        var heroDisplayName = hero != null
            ? HeroDisplayLabelFormatter.ResolvePersonAndJob(
                hero,
                GetCharacterName,
                GetArchetypeName)
            : "—";
        var header = new PassiveBoardHeaderViewState(
            HeroId: heroId,
            HeroDisplayName: heroDisplayName,
            ClassKey: classKey,
            ClassLabel: GetPassiveBoardName(boardId),
            BoardId: boardId,
            HeroPortrait: null,   // runtime portrait wiring은 별도 (HeroPortraitCard 경로)
            ClassIconSprite: _classSprite(classKey));

        var nodes = BuildNodes(boardId, activeNodeIds);
        var detail = BuildDetail(nodes);
        var footer = BuildFooter(boardId, nodes);

        return new PassiveBoardViewState(header, nodes, detail, footer);
    }

    private IReadOnlyList<PassiveBoardNodeViewState> BuildNodes(string boardId, HashSet<string> activeNodeIds)
    {
        if (!_lookup.TryGetPassiveBoardDefinition(boardId, out var board) || board?.Nodes == null)
        {
            return Array.Empty<PassiveBoardNodeViewState>();
        }

        // BoardDepth로 ring 그룹핑 — depth 0 = keystone(center), 1 = notable(inner), 2 = small(outer).
        var validNodes = board.Nodes
            .Where(n => n != null && !string.IsNullOrWhiteSpace(n.Id))
            .ToList();
        var byDepth = validNodes
            .GroupBy(n => n.BoardDepth)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Id, StringComparer.Ordinal).ToList());

        var result = new List<PassiveBoardNodeViewState>(validNodes.Count);
        foreach (var depthGroup in byDepth)
        {
            var depth = depthGroup.Key;
            var ringNodes = depthGroup.Value;
            for (var i = 0; i < ringNodes.Count; i++)
            {
                var node = ringNodes[i];
                var (left, top) = ComputeRingPosition(depth, i, ringNodes.Count);
                var iconKey = ResolveNodeIconKey(node);
                result.Add(new PassiveBoardNodeViewState(
                    NodeId: node.Id,
                    KindKey: node.NodeKind.ToString().ToLowerInvariant(),
                    Left: left,
                    Top: top,
                    IconKey: iconKey,
                    IconSprite: _affixSprite(iconKey),
                    RuleSummary: GetPassiveNodeDescription(node.Id),
                    Tags: string.Join(" · ", node.CompileTags.Select(t => t?.ToString() ?? string.Empty).Where(s => s.Length > 0)),
                    IsActive: activeNodeIds.Contains(node.Id)));
            }
        }
        return result;
    }

    /// <summary>
    /// 노드가 무엇을 하는지 아이콘 하나로 말하게 한다.
    ///
    /// 2026-07-31 까지 모든 노드가 <b>무지 파란 사각형/원</b>이었다 — 프리젠터가
    /// <c>IconSprite: null</c> 을 내려보내고 있었고, 주입된 affix 스프라이트 로더는
    /// 한 번도 호출되지 않았다("TODO: node icon mapping"). 클릭해서 상세를 열기 전에는
    /// 어느 노드가 공격이고 어느 노드가 방어인지 화면이 전혀 말하지 않았다.
    ///
    /// 노드의 첫 스탯 수정자를 축으로 잡는다 — 노드가 실제로 바꾸는 값이고, affix 아이콘
    /// 세트가 이미 같은 축(공격·체력·치명·방어…)으로 그려져 있다.
    /// </summary>
    private static string ResolveNodeIconKey(PassiveNodeDefinition node)
    {
        foreach (var modifier in node.Modifiers)
        {
            if (StatIconTokens.TryGetValue(modifier.StatId ?? string.Empty, out var token))
            {
                return token;
            }
        }

        // 스탯을 안 바꾸고 스킬만 주는 노드(키스톤에 많다)는 등급 표식으로 둔다.
        return node.NodeKind == SM.Core.Content.PassiveNodeKindValue.Keystone ? "charge" : "link";
    }

    /// <summary>스탯 축 → affix 아이콘 토큰. 아이콘 세트에 있는 24종 안에서만 고른다.</summary>
    private static readonly IReadOnlyDictionary<string, string> StatIconTokens =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phys_power"] = "atk",
            ["mag_power"] = "magic_atk",
            ["max_health"] = "hp",
            ["crit_chance"] = "crit",
            ["crit_multiplier"] = "crit",
            ["armor"] = "armor",
            ["resist"] = "resist_magic",
            ["tenacity"] = "block",
            ["protect_radius"] = "aura",
            ["aggro_radius"] = "taunt",
            ["move_speed"] = "speed",
            ["attack_speed"] = "cast_speed",
            ["cooldown_recovery"] = "cooldown",
            ["attack_windup"] = "charge",
            ["projectile_speed"] = "charge",
            ["attack_range"] = "pierce",
            ["preferred_distance"] = "pierce",
            ["phys_pen"] = "pierce",
            ["mag_pen"] = "amplify",
            ["heal_power"] = "heal",
            ["lifesteal"] = "lifesteal",
            ["mana_max"] = "mana",
            ["mana_gain_on_hit"] = "mana",
            ["target_switch_delay"] = "link",
        };

    /// <summary>BoardDepth ring layout — depth 0=center, 1=inner ring(r 0.18), 2+=outer ring(r 0.36).</summary>
    private static (float Left, float Top) ComputeRingPosition(int depth, int index, int ringCount)
    {
        const float centerX = 0.46f;
        const float centerY = 0.40f;
        if (depth <= 0 || ringCount <= 1)
        {
            return (centerX, centerY);
        }

        var radius = depth == 1 ? 0.18f : 0.36f;
        var angle = (Mathf.PI * 2f * index) / ringCount;
        var x = centerX + radius * Mathf.Sin(angle);
        var y = centerY - radius * Mathf.Cos(angle);
        return (x, y);
    }

    private PassiveBoardDetailViewState BuildDetail(IReadOnlyList<PassiveBoardNodeViewState> nodes)
    {
        var toggleFailureLabel = LocalizeToggleFailure(_toggleFailure);
        var selected = nodes.FirstOrDefault(n => string.Equals(n.NodeId, _selectedNodeId, StringComparison.Ordinal));
        if (selected == null)
        {
            return new PassiveBoardDetailViewState(
                SelectedNodeId: string.Empty,
                KindLabel: "—",
                TitleText: "노드를 선택하세요",
                RuleSummary: "보드의 노드를 클릭하면 효과와 태그가 표시됩니다.",
                Tags: string.Empty,
                AvailableLabel: string.IsNullOrEmpty(toggleFailureLabel) ? "—" : toggleFailureLabel,
                // uxqa1: 미선택 상태의 ACTIVATE는 눌러도 no-op인 죽은 버튼 — 빈 라벨로 내려
                // View가 버튼을 숨기게 한다 (노드 선택 시에만 CTA 노출).
                ButtonLabel: string.Empty,
                IconSprite: null);
        }

        return new PassiveBoardDetailViewState(
            SelectedNodeId: selected.NodeId,
            KindLabel: FormatNodeKind(selected.KindKey),
            TitleText: GetPassiveNodeName(selected.NodeId),
            RuleSummary: selected.RuleSummary,
            Tags: selected.Tags,
            AvailableLabel: string.IsNullOrEmpty(toggleFailureLabel)
                ? (selected.IsActive ? "활성" : "비활성")
                : toggleFailureLabel,
            ButtonLabel: selected.IsActive ? "해제" : "활성화",
            // 보드에서 고른 노드와 같은 아이콘을 상세에도 띄운다 — 눈이 둘을 잇는다.
            IconSprite: selected.IconSprite);
    }

    private PassiveBoardFooterViewState BuildFooter(string boardId, IReadOnlyList<PassiveBoardNodeViewState> nodes)
    {
        int aSmall = 0, tSmall = 0, aNotable = 0, tNotable = 0, aKeystone = 0, tKeystone = 0;
        foreach (var n in nodes)
        {
            switch (n.KindKey)
            {
                case "small":    tSmall++;    if (n.IsActive) aSmall++;    break;
                case "notable":  tNotable++;  if (n.IsActive) aNotable++;  break;
                case "keystone": tKeystone++; if (n.IsActive) aKeystone++; break;
            }
        }
        // ToUpperInvariant 는 시안의 영문 캡스 흉내였는데, 한국어 이름에는 아무 효과가 없고
        // 라틴 폴백(vanguard)만 "VANGUARD" 로 키워 화면에 영어를 더 크게 띄웠다.
        var boardName = GetPassiveBoardName(boardId);
        return new PassiveBoardFooterViewState(
            $"{boardName} · 소형 {aSmall}/{tSmall} · 주요 {aNotable}/{tNotable} · 핵심 {aKeystone}/{tKeystone}");
    }

    private string LocalizeToggleFailure(OperationFailure? failure)
    {
        if (failure == null)
        {
            return string.Empty;
        }

        if (failure.IsInvariantViolation)
        {
            return Ui(
                "ui.town.passive.toggle.failed",
                "The passive node could not be changed. Please try again.");
        }

        return failure.Code switch
        {
            SessionOperationFailureCodes.PassiveTownOnly => Ui(
                "ui.town.passive.toggle.town_only",
                "Passive nodes can be changed only in Town."),
            SessionOperationFailureCodes.HeroNotFound => Ui(
                "ui.town.passive.toggle.hero_missing",
                "The selected hero is no longer available."),
            SessionOperationFailureCodes.PassiveLoadoutMissing => Ui(
                "ui.town.passive.toggle.loadout_missing",
                "The selected hero has no passive board."),
            MetaOperationFailureCodes.PassivePrerequisiteRequired => Ui(
                "ui.town.passive.toggle.prerequisite_required",
                "Activate the prerequisite node first."),
            MetaOperationFailureCodes.PassiveActiveNodeLimitReached => Ui(
                "ui.town.passive.toggle.active_node_limit",
                "You can activate up to {0} passive nodes.",
                FailureArgument(failure, 0, "0")),
            MetaOperationFailureCodes.PassiveKeystoneLimitReached => Ui(
                "ui.town.passive.toggle.keystone_limit",
                "You can activate up to {0} keystone nodes.",
                FailureArgument(failure, 0, "1")),
            MetaOperationFailureCodes.PassiveMutualExclusion => Ui(
                "ui.town.passive.toggle.mutual_exclusion",
                "A conflicting passive node is already active."),
            _ => Ui(
                "ui.town.passive.toggle.failed",
                "The passive node could not be changed. Please try again."),
        };
    }

    private string GetCharacterName(string id, string fallbackArchetypeId)
        => _contentText?.GetCharacterName(id, fallbackArchetypeId) ?? "—";

    private string GetArchetypeName(string id) => _contentText?.GetArchetypeName(id) ?? "—";

    /// <summary>노드 등급 표시명. 이전에는 raw id 를 대문자로 올려 "KEYSTONE" 이 그대로 떴다.</summary>
    private static string FormatNodeKind(string kindKey) => kindKey switch
    {
        "small" => "소형",
        "notable" => "주요",
        "keystone" => "핵심",
        _ => "노드",
    };

    /// <summary>
    /// 보드 이름. 콘텐츠에 한국어 이름이 저작돼 있지 않으면 해석기가 <c>LegacyDisplayName</c>
    /// (= 역할군 raw id, "vanguard")을 그대로 낸다. 보드 id 는 역할군 태그와 같으므로
    /// 이미 있는 한국어 역할군 사전으로 떨어뜨린다 — 화면에 영어 id 를 띄우지 않는다.
    /// </summary>
    private string GetPassiveBoardName(string id)
    {
        var name = _contentText?.GetPassiveBoardName(id);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "—";
        }

        // 저작된 이름이 "vanguard 보드" 처럼 <b>영문 태그 + 한국어 접미사</b> 형태다.
        // 앞 토큰만 사전으로 바꾼다(사전에 없으면 손대지 않는다).
        var head = name.Split(' ')[0];
        var family = SM.Content.Definitions.RoleGlossary.GetLocalizedRoleFamilyFallback(head, "ko");
        return string.Equals(family, head, StringComparison.Ordinal)
            ? name
            : family + name[head.Length..];
    }

    private string GetPassiveNodeName(string id) => _contentText?.GetPassiveNodeName(id) ?? "—";

    private string GetPassiveNodeDescription(string id) => _contentText?.GetPassiveNodeDescription(id) ?? "—";

    private string Ui(string key, string fallback, params object[] arguments)
        => _contentText?.LocalizeUi(
               GameLocalizationTables.UITown,
               key,
               fallback,
               arguments)
           ?? (arguments.Length == 0
               ? fallback
               : string.Format(fallback, arguments));

    private static string FailureArgument(OperationFailure failure, int index, string fallback)
        => failure.Arguments.Count > index && !string.IsNullOrWhiteSpace(failure.Arguments[index])
            ? failure.Arguments[index]
            : fallback;

    private readonly record struct BoardCatalogEntry(string BoardId, string ClassKey, string Label);

    // 클래스 → 보드 매핑. 보드 트리는 클래스 단위(PassiveBoardDefinition.ClassId), hero는 자기 클래스 보드로 고정.
    private static readonly BoardCatalogEntry[] BoardCatalog =
    {
        new("board_vanguard", "vanguard", "VANGUARD"),
        new("board_duelist",  "duelist",  "DUELIST"),
        new("board_ranger",   "ranger",   "RANGER"),
        new("board_mystic",   "mystic",   "MYSTIC"),
    };

    public static IReadOnlyList<(string BoardId, string ClassKey, string Label)> Boards
        => BoardCatalog.Select(b => (b.BoardId, b.ClassKey, b.Label)).ToList();
}
