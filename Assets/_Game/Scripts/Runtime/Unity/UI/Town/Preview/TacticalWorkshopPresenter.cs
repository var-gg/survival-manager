using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Tactical Workshop V1 Presenter — `GameSessionRoot.SessionState` (Profile) → TacticalWorkshopViewState 변환.
///
/// Sprint 1 (현재): 패턴 scaffold. posture + anchor 기본 binding은 wire. 나머지 column
/// (synergy active, threat coverage, per-unit RuleSet runtime fetch)은 Sprint 2에서 wire.
///
/// 본 Presenter는 sprite 로드를 직접 안 함 — caller가 `SpriteLoader` delegate를 ctor로 inject.
/// Runtime: Resources/Addressables 기반. Editor Bootstrap: AssetDatabase 기반.
///
/// 워크플로우: posture 카드 클릭 → CycleTeamPosture / SetTeamPosture → profile mutation → BattleTest 즉시 반영.
///
/// Codex legacy `SM.Unity.UI.TacticalWorkshop.TacticalWorkshopPresenter`와 별개 — V1 redesign 자리.
/// </summary>
public sealed class TacticalWorkshopPresenter : ITacticalWorkshopActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    private readonly GameSessionRoot _root;
    private readonly TacticalWorkshopView _view;
    private readonly SpriteLoader _postureSprite;
    private readonly SpriteLoader _threatSprite;
    private readonly SpriteLoader _classSprite;

    public TacticalWorkshopPresenter(
        GameSessionRoot root,
        TacticalWorkshopView view,
        SpriteLoader postureSprite,
        SpriteLoader threatSprite,
        SpriteLoader classSprite)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _postureSprite = postureSprite ?? throw new ArgumentNullException(nameof(postureSprite));
        _threatSprite = threatSprite ?? throw new ArgumentNullException(nameof(threatSprite));
        _classSprite = classSprite ?? throw new ArgumentNullException(nameof(classSprite));
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

    // === ITacticalWorkshopActions ===

    void ITacticalWorkshopActions.OnPostureSelected(string postureId)
    {
        // Sprint 2: GameSessionState.SetTeamPosture(TeamPostureType) named-set 사용.
        // 워크플로우: 카드 클릭 → enum parse → SetTeamPosture → profile mutation → BattleTest 즉시 반영.
        if (Enum.TryParse<TeamPostureType>(postureId, out var posture))
        {
            _root.SessionState.SetTeamPosture(posture);
        }
        Refresh();
    }

    void ITacticalWorkshopActions.OnAnchorClicked(string anchorId)
    {
        if (!Enum.TryParse<DeploymentAnchorId>(anchorId, out var anchor))
        {
            return;
        }
        _root.SessionState.CycleDeploymentAssignment(anchor);
        Refresh();
    }

    // === ViewState builder ===

    private TacticalWorkshopViewState BuildState()
    {
        var session = _root.SessionState;
        return new TacticalWorkshopViewState(
            BuildAnchors(session),
            BuildPostures(session),
            session.SelectedTeamPosture.ToString(),
            BuildSynergyChips(session),
            BuildThreats(session),
            BuildTactics(session)
        );
    }

    private IReadOnlyList<TacticalWorkshopAnchorViewState> BuildAnchors(GameSessionState session)
    {
        // Sprint 2: ProfileQueries.GetLoadoutView → anchor → heroId 매핑 + Profile.Heroes → ClassId → class sprite.
        // 워크플로우: SquadBuilder가 anchor 편집 → BattleTest에 즉시 반영. TW는 read-only 시각화.
        var loadout = _root.ProfileQueries.GetLoadoutView(_root.ActiveProfileId);
        var heroById = session.Profile.Heroes.ToDictionary(h => h.HeroId, StringComparer.Ordinal);

        var anchors = new List<TacticalWorkshopAnchorViewState>(6);
        foreach (DeploymentAnchorId anchor in Enum.GetValues(typeof(DeploymentAnchorId)))
        {
            var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == anchor);
            var heroId = deployment?.HeroId ?? string.Empty;
            var hero = !string.IsNullOrEmpty(heroId) && heroById.TryGetValue(heroId, out var h) ? h : null;
            var classKey = hero?.ClassId ?? string.Empty;
            anchors.Add(new TacticalWorkshopAnchorViewState(
                AnchorId: anchor.ToString(),
                AssignedHeroId: heroId,
                AssignedFigure: string.IsNullOrEmpty(classKey) ? null : _classSprite(classKey)));
        }
        return anchors;
    }

    private IReadOnlyList<TacticalWorkshopPostureViewState> BuildPostures(GameSessionState session)
    {
        var selected = session.SelectedTeamPosture;
        return PostureCatalog
            .Select(p => new TacticalWorkshopPostureViewState(
                PostureId: p.Id,
                Sprite: _postureSprite(p.SpriteKey),
                KoLabel: p.KoLabel,
                IsSelected: string.Equals(p.Id, selected.ToString(), StringComparison.Ordinal)))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopSynergyChipViewState> BuildSynergyChips(GameSessionState session)
    {
        // Sprint 1: 7 family 정적 list. active state는 Sprint 2.
        // TODO Sprint 2: SynergyService.BuildForTeam(deployedHeroes)로 breakpoint + active count.
        return SynergyCatalog
            .Select(s => new TacticalWorkshopSynergyChipViewState(
                SynergyId: s.Id,
                Group: s.Group,
                Sprite: s.Group == "class" ? _classSprite(s.SpriteKey) : null,
                KoLabel: s.KoLabel))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopThreatViewState> BuildThreats(GameSessionState session)
    {
        // Sprint 1: 8 lane 정적 list. AnswerState는 Sprint 2 (TeamCounterCoverage).
        return ThreatCatalog
            .Select(t => new TacticalWorkshopThreatViewState(
                LaneId: t.Id,
                Sprite: _threatSprite(t.SpriteKey),
                KoLabel: t.KoLabel,
                AnswerState: string.Empty))
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopHeroTacticViewState> BuildTactics(GameSessionState session)
    {
        // Sprint 2: deployed hero × RoleInstructionDefinition.RuleSet 매핑.
        return Array.Empty<TacticalWorkshopHeroTacticViewState>();
    }

    // === Static catalog (pindoc V1 wiki SoT 한국어 표시명) ===

    private readonly record struct PostureCatalogEntry(string Id, string SpriteKey, string KoLabel);

    private static readonly PostureCatalogEntry[] PostureCatalog =
    {
        new("HoldLine",         "hold_line",          "전열 사수"),
        new("StandardAdvance",  "standard_advance",   "표준 전진"),
        new("ProtectCarry",     "protect_carry",      "캐리 보호"),
        new("CollapseWeakSide", "collapse_weak_side", "약측 무너뜨리기"),
        new("AllInBackline",    "all_in_backline",    "후열 깊이 침투"),
    };

    private readonly record struct SynergyCatalogEntry(string Id, string Group, string SpriteKey, string KoLabel);

    private static readonly SynergyCatalogEntry[] SynergyCatalog =
    {
        new("synergy_human",    "race",  "",          "솔라룸"),
        new("synergy_beastkin", "race",  "",          "이리솔 부족"),
        new("synergy_undead",   "race",  "",          "회상 결사"),
        new("synergy_vanguard", "class", "vanguard",  "전위"),
        new("synergy_duelist",  "class", "duelist",   "결투가"),
        new("synergy_ranger",   "class", "ranger",    "궁수"),
        new("synergy_mystic",   "class", "mystic",    "신비"),
    };

    private readonly record struct ThreatCatalogEntry(string Id, string SpriteKey, string KoLabel);

    private static readonly ThreatCatalogEntry[] ThreatCatalog =
    {
        new("ArmorFrontline",  "pierce",  "방어 전열"),
        new("ResistanceShell", "sustain", "저항 외피"),
        new("GuardBulwark",    "burst",   "가드 보루"),
        new("EvasiveSkirmish", "dive",    "회피형 산개"),
        new("ControlChain",    "control", "제어 사슬"),
        new("SustainBall",     "heal",    "지속력 덩어리"),
        new("DiveBackline",    "swarm",   "후열 침투"),
        new("SwarmFlood",      "summon",  "군중 범람"),
    };

    public static IReadOnlyList<(string Id, string SpriteKey, string KoLabel)> Postures
        => PostureCatalog.Select(p => (p.Id, p.SpriteKey, p.KoLabel)).ToList();
    public static IReadOnlyList<(string Id, string Group, string SpriteKey, string KoLabel)> Synergies
        => SynergyCatalog.Select(s => (s.Id, s.Group, s.SpriteKey, s.KoLabel)).ToList();
    public static IReadOnlyList<(string Id, string SpriteKey, string KoLabel)> Threats
        => ThreatCatalog.Select(t => (t.Id, t.SpriteKey, t.KoLabel)).ToList();
}
