using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
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
    private readonly ContentTextResolver _contentText;

    public TacticalWorkshopPresenter(
        GameSessionRoot root,
        TacticalWorkshopView view,
        SpriteLoader postureSprite,
        SpriteLoader threatSprite,
        SpriteLoader classSprite,
        ContentTextResolver? contentText = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _postureSprite = postureSprite ?? throw new ArgumentNullException(nameof(postureSprite));
        _threatSprite = threatSprite ?? throw new ArgumentNullException(nameof(threatSprite));
        _classSprite = classSprite ?? throw new ArgumentNullException(nameof(classSprite));
        _contentText = contentText ?? new ContentTextResolver(root.Localization, root.CombatContentLookup);
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

    // anchor pad는 read-only reference — anchor 편집은 SquadBuilder 책임 (audit §2.2).
    // OnAnchorClicked 액션 제거: CycleDeploymentAssignment edit는 SquadBuilder surface로 이관.

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
        foreach (var anchor in AnchorOrder)
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
        // deployed hero × role instruction + behavior profile 요약.
        // condition→action→target rule chain은 runtime 모델에 없으므로 가짜 RuleSet을 만들지 않는다.
        var loadout = _root.ProfileQueries.GetLoadoutView(_root.ActiveProfileId);
        var heroById = session.Profile.Heroes.ToDictionary(h => h.HeroId, StringComparer.Ordinal);
        var activeBlueprint = ResolveActiveBlueprint(session);
        var rows = new List<TacticalWorkshopHeroTacticViewState>();

        foreach (var anchor in AnchorOrder)
        {
            var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == anchor);
            var heroId = deployment?.HeroId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(heroId) || !heroById.TryGetValue(heroId, out var hero))
            {
                continue;
            }

            var roleInstructionId = ResolveRoleInstructionId(hero, anchor, activeBlueprint);
            var fallbackRoleTag = ResolveDefaultRoleTag(hero.ClassId, anchor);
            RoleInstructionDefinition? roleInstruction = null;
            if (!string.IsNullOrWhiteSpace(roleInstructionId)
                && _root.CombatContentLookup.TryGetRoleInstructionDefinition(roleInstructionId, out var resolvedRole))
            {
                roleInstruction = resolvedRole;
            }

            var behaviorProfile = ResolveBehaviorProfile(hero);
            rows.Add(new TacticalWorkshopHeroTacticViewState(
                HeroId: hero.HeroId,
                DisplayName: ResolveHeroDisplayName(hero),
                AnchorLabel: LocalizeAnchor(anchor),
                RoleLabel: _contentText.GetRoleName(roleInstructionId, roleInstruction?.RoleTag ?? fallbackRoleTag),
                FormationLabel: LocalizeFormation(behaviorProfile?.FormationLine),
                RangeLabel: LocalizeRange(behaviorProfile?.RangeDiscipline),
                Biases: BuildBiases(roleInstruction)));
        }

        return rows;
    }

    private SquadBlueprintRecord? ResolveActiveBlueprint(GameSessionState session)
    {
        return session.Profile.SquadBlueprints.FirstOrDefault(record =>
                   string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
               ?? session.Profile.SquadBlueprints.FirstOrDefault();
    }

    private string ResolveHeroDisplayName(HeroInstanceRecord hero)
    {
        if (!string.IsNullOrWhiteSpace(hero.Name))
        {
            return hero.Name;
        }

        if (!string.IsNullOrWhiteSpace(hero.CharacterId))
        {
            return _contentText.GetCharacterName(hero.CharacterId, hero.ArchetypeId);
        }

        return !string.IsNullOrWhiteSpace(hero.ArchetypeId)
            ? _contentText.GetArchetypeName(hero.ArchetypeId)
            : hero.HeroId;
    }

    private string ResolveRoleInstructionId(HeroInstanceRecord hero, DeploymentAnchorId anchor, SquadBlueprintRecord? activeBlueprint)
    {
        if (activeBlueprint?.HeroRoleIds != null
            && activeBlueprint.HeroRoleIds.TryGetValue(hero.HeroId, out var roleInstructionId)
            && !string.IsNullOrWhiteSpace(roleInstructionId))
        {
            return roleInstructionId;
        }

        return ResolveDefaultRoleInstructionId(hero.ClassId, anchor);
    }

    private BehaviorProfileDefinition? ResolveBehaviorProfile(HeroInstanceRecord hero)
    {
        if (!string.IsNullOrWhiteSpace(hero.ArchetypeId)
            && _root.CombatContentLookup.TryGetArchetype(hero.ArchetypeId, out var archetype))
        {
            return archetype.BehaviorProfile;
        }

        if (!string.IsNullOrWhiteSpace(hero.HeroId)
            && _root.CombatContentLookup.TryGetArchetype(hero.HeroId, out var heroArchetype))
        {
            return heroArchetype.BehaviorProfile;
        }

        return null;
    }

    private static IReadOnlyList<TacticalWorkshopBiasViewState> BuildBiases(RoleInstructionDefinition? roleInstruction)
    {
        return new[]
        {
            new TacticalWorkshopBiasViewState("캐리 보호", Clamp01(roleInstruction?.ProtectCarryBias ?? 0f)),
            new TacticalWorkshopBiasViewState("후열 압박", Clamp01(roleInstruction?.BacklinePressureBias ?? 0f)),
            new TacticalWorkshopBiasViewState("후퇴 성향", Clamp01(roleInstruction?.RetreatBias ?? 0f)),
        };
    }

    private static string ResolveDefaultRoleInstructionId(string classId, DeploymentAnchorId anchor)
        => ResolveDefaultRoleTag(classId, anchor);

    private static string ResolveDefaultRoleTag(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private static string LocalizeAnchor(DeploymentAnchorId anchor) => anchor switch
    {
        DeploymentAnchorId.FrontTop => "전열 상",
        DeploymentAnchorId.FrontCenter => "전열 중",
        DeploymentAnchorId.FrontBottom => "전열 하",
        DeploymentAnchorId.BackTop => "후열 상",
        DeploymentAnchorId.BackCenter => "후열 중",
        DeploymentAnchorId.BackBottom => "후열 하",
        _ => anchor.ToString(),
    };

    private static string LocalizeFormation(FormationLine? formation) => formation switch
    {
        FormationLine.Frontline => "전열",
        FormationLine.Midline => "중열",
        FormationLine.Backline => "후열",
        _ => "배치 기준",
    };

    private static string LocalizeRange(RangeDiscipline? range) => range switch
    {
        RangeDiscipline.Collapse => "압박 접근",
        RangeDiscipline.HoldBand => "거리 유지",
        RangeDiscipline.KiteBackward => "후퇴 카이팅",
        RangeDiscipline.SideStepHold => "측면 유지",
        RangeDiscipline.AnchorNearFrontline => "전열 근접",
        _ => "기본 교전 거리",
    };

    private static float Clamp01(float value) => Mathf.Clamp01(value);

    // === Static catalog (pindoc V1 wiki SoT 한국어 표시명) ===

    private static readonly DeploymentAnchorId[] AnchorOrder =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom,
    };

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
