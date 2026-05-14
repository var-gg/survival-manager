using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
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

    private readonly GameSessionRoot _root;
    private readonly PassiveBoardView _view;
    private readonly ContentTextResolver _contentText;
    private readonly SpriteLoader _classSprite;
    private readonly SpriteLoader _affixSprite;
    private string _selectedNodeId = string.Empty;
    private string _selectedHeroId = string.Empty;

    public PassiveBoardPresenter(
        GameSessionRoot root,
        PassiveBoardView view,
        ContentTextResolver contentText,
        SpriteLoader classSprite,
        SpriteLoader affixSprite)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _classSprite = classSprite ?? throw new ArgumentNullException(nameof(classSprite));
        _affixSprite = affixSprite ?? throw new ArgumentNullException(nameof(affixSprite));
    }

    /// <summary>Town hub controller가 selected hero 컨텍스트를 전달 (Sprint 3+ navigation).</summary>
    public void SetSelectedHero(string heroId)
    {
        _selectedHeroId = heroId ?? string.Empty;
        Refresh();
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    void IPassiveBoardActions.OnNodeSelected(string nodeId)
    {
        _selectedNodeId = nodeId;
        Refresh();
    }

    void IPassiveBoardActions.OnToggleActivateClicked()
    {
        var heroId = ResolveSelectedHeroId();
        if (!string.IsNullOrEmpty(heroId) && !string.IsNullOrEmpty(_selectedNodeId))
        {
            _root.SessionState.TogglePassiveNode(heroId, _selectedNodeId);
        }
        Refresh();
    }

    private string ResolveSelectedHeroId()
    {
        if (!string.IsNullOrEmpty(_selectedHeroId)) return _selectedHeroId;
        var heroes = _root.SessionState.Profile.Heroes;
        return heroes.Count > 0 ? heroes[0].HeroId : string.Empty;
    }

    private PassiveBoardViewState BuildState()
    {
        var session = _root.SessionState;
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

        var header = new PassiveBoardHeaderViewState(
            HeroId: heroId,
            HeroDisplayName: !string.IsNullOrEmpty(hero?.Name) ? hero!.Name : heroId,
            ClassKey: classKey,
            ClassLabel: _contentText.GetPassiveBoardName(boardId),
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
        if (!_root.CombatContentLookup.TryGetPassiveBoardDefinition(boardId, out var board) || board?.Nodes == null)
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
                result.Add(new PassiveBoardNodeViewState(
                    NodeId: node.Id,
                    KindKey: node.NodeKind.ToString().ToLowerInvariant(),
                    Left: left,
                    Top: top,
                    IconKey: string.Empty,  // TODO: node icon mapping (CompileTags 기반)
                    IconSprite: null,       // sprite는 Bootstrap mock에서만 — runtime은 affix 매핑 후
                    RuleSummary: _contentText.GetPassiveNodeDescription(node.Id),
                    Tags: string.Join(" · ", node.CompileTags.Select(t => t?.ToString() ?? string.Empty).Where(s => s.Length > 0)),
                    IsActive: activeNodeIds.Contains(node.Id)));
            }
        }
        return result;
    }

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
        var selected = nodes.FirstOrDefault(n => string.Equals(n.NodeId, _selectedNodeId, StringComparison.Ordinal));
        if (selected == null)
        {
            return new PassiveBoardDetailViewState(
                SelectedNodeId: string.Empty,
                KindLabel: "—",
                TitleText: "노드를 선택하세요",
                RuleSummary: "보드의 노드를 클릭하면 효과와 태그가 표시됩니다.",
                Tags: string.Empty,
                AvailableLabel: "—",
                ButtonLabel: "ACTIVATE",
                IconSprite: null);
        }

        return new PassiveBoardDetailViewState(
            SelectedNodeId: selected.NodeId,
            KindLabel: selected.KindKey.ToUpperInvariant(),
            TitleText: _contentText.GetPassiveNodeName(selected.NodeId),
            RuleSummary: selected.RuleSummary,
            Tags: selected.Tags,
            AvailableLabel: selected.IsActive ? "ACTIVE" : "INACTIVE",
            ButtonLabel: selected.IsActive ? "DEACTIVATE" : "ACTIVATE",
            IconSprite: null);
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
        var boardName = _contentText.GetPassiveBoardName(boardId).ToUpperInvariant();
        return new PassiveBoardFooterViewState(
            $"{boardName} · SMALL {aSmall}/{tSmall} · NOTABLE {aNotable}/{tNotable} · KEYSTONE {aKeystone}/{tKeystone}");
    }

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
