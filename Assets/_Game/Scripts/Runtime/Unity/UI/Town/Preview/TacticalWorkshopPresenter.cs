using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity.UI;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Tactical Workshop(전술 공방) 프로덕션 presenter — GameSessionState → TacticalWorkshopViewState 변환.
///
/// 책임 경계 (audit §2.2 / panel-responsibility-matrix §2):
/// - 편집 가능: 팀 태세(posture 5카드) + per-unit 타겟 지시(P1 directive cycle) + 전술 초기화.
/// - read-only: anchor pad(배치 편집은 SquadBuilder=전술 설정 책임), role/behavior 요약, 시너지/위협 답수.
/// - 시너지는 SquadBuilder와 동일 SoT(SquadSynergyPreview + snapshot.SynergyCatalog),
///   위협 lane은 SquadCounterCoveragePreview.Dimensions에서 파생 — 정적 사본 어휘를 두지 않는다.
///
/// 순수성: MonoBehaviour/Resources 접근 없음. sprite 로드는 SpriteLoader delegate 주입(모두 optional —
/// 런타임은 USS art class로 배경을 입히므로 null 허용), 이름 해석은 Func 주입(FastUnit은 identity 람다).
///
/// Codex legacy `SM.Unity.UI.TacticalWorkshop.TacticalWorkshopPresenter`와 별개 — V1 redesign 자리.
/// </summary>
public sealed class TacticalWorkshopPresenter : ITacticalWorkshopActions
{
    public delegate Texture2D? SpriteLoader(string spriteKey);

    private readonly GameSessionState _session;
    private readonly ICombatContentLookup _contentLookup;
    private readonly ITacticalWorkshopView _view;
    private readonly Func<string, string, string> _characterName;   // (characterId, fallbackArchetypeId)
    private readonly Func<string, string, string> _roleName;        // (roleInstructionId, fallbackRoleTag)
    private readonly Func<string, string> _synergyName;
    private readonly Func<string, string>? _archetypeName;
    private readonly SpriteLoader? _postureSprite;
    private readonly SpriteLoader? _threatSprite;
    private readonly SpriteLoader? _classSprite;

    public TacticalWorkshopPresenter(
        GameSessionState session,
        ICombatContentLookup contentLookup,
        ITacticalWorkshopView view,
        Func<string, string, string> characterName,
        Func<string, string, string> roleName,
        Func<string, string> synergyName,
        SpriteLoader? postureSprite = null,
        SpriteLoader? threatSprite = null,
        SpriteLoader? classSprite = null,
        Func<string, string>? archetypeName = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _contentLookup = contentLookup ?? throw new ArgumentNullException(nameof(contentLookup));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _characterName = characterName ?? throw new ArgumentNullException(nameof(characterName));
        _roleName = roleName ?? throw new ArgumentNullException(nameof(roleName));
        _synergyName = synergyName ?? throw new ArgumentNullException(nameof(synergyName));
        _archetypeName = archetypeName;
        _postureSprite = postureSprite;
        _threatSprite = threatSprite;
        _classSprite = classSprite;
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

    public void Close() => _view.Close();

    public void Refresh() => _view.Render(BuildState());

    // === ITacticalWorkshopActions ===

    void ITacticalWorkshopActions.OnPostureSelected(string postureId)
    {
        // 카드 클릭 → enum parse → SetTeamPosture → blueprint capture → 전투 구성에 즉시 반영.
        if (Enum.TryParse<TeamPostureType>(postureId, out var posture))
        {
            _session.SetTeamPosture(posture);
        }
        Refresh();
    }

    void ITacticalWorkshopActions.OnTacticDirectiveCycled(string heroId)
    {
        if (!string.IsNullOrWhiteSpace(heroId))
        {
            _session.CycleHeroTargetDirective(heroId);
        }
        Refresh();
    }

    void ITacticalWorkshopActions.OnTacticsReset()
    {
        // 전술만 초기화 — 배치(anchor)는 SquadBuilder 소유라 건드리지 않는다.
        // 지시는 배치 여부와 무관하게 전 로스터를 비운다 — 벤치 유닛에 남은 지시가
        // 나중에 배치될 때 조용히 되살아나는 함정 방지.
        _session.SetTeamPosture(TeamPostureType.StandardAdvance);
        foreach (var hero in _session.Profile.Heroes)
        {
            _session.SetHeroTargetDirective(hero.HeroId, PlayerTargetDirective.Default);
        }
        Refresh();
    }

    // anchor pad는 read-only reference — anchor 편집은 SquadBuilder 책임 (audit §2.2).
    // OnAnchorClicked 액션 없음: CycleDeploymentAssignment edit는 SquadBuilder surface 소유.

    // === ViewState builder ===

    public TacticalWorkshopViewState BuildState()
    {
        var assignments = BuildAssignments();
        var deployedHeroes = assignments
            .Where(entry => entry.Hero != null)
            .Select(entry => entry.Hero!)
            .ToList();
        var snapshotAvailable = _contentLookup.TryGetCombatSnapshot(out var snapshot, out _);

        var threats = BuildThreats(deployedHeroes, snapshotAvailable, snapshot);
        var (synergyChips, synergyEmptyText) = BuildSynergyChips(deployedHeroes, snapshotAvailable, snapshot);
        var evaluated = threats.Any(t => !string.IsNullOrEmpty(t.AnswerState));
        var answeredCount = threats.Count(t => t.AnswerState is "answered" or "partial");
        var selected = _session.SelectedTeamPosture;

        return new TacticalWorkshopViewState(
            Anchors: BuildAnchors(assignments),
            Postures: BuildPostures(selected),
            SelectedPostureId: selected.ToString(),
            SynergyChips: synergyChips,
            SynergyEmptyText: synergyEmptyText,
            Threats: threats,
            Tactics: BuildTactics(assignments),
            DeployChipLabel: $"배치 {deployedHeroes.Count}/{assignments.Count}",
            PostureChipLabel: $"태세 · {TacticsLexicon.Posture(selected)}",
            AnswerChipLabel: evaluated ? $"위협 답수 {answeredCount}/{threats.Count}" : "위협 답수 —",
            AnswerChipWarn: threats.Any(t => t.AnswerState == "unanswered"));
    }

    private IReadOnlyList<(DeploymentAnchorId Anchor, HeroInstanceRecord? Hero)> BuildAssignments()
    {
        var heroById = _session.Profile.Heroes.ToDictionary(h => h.HeroId, StringComparer.Ordinal);
        var rows = new List<(DeploymentAnchorId, HeroInstanceRecord?)>(6);
        foreach (var anchor in _session.DeploymentAnchors)
        {
            var heroId = _session.GetAssignedHeroId(anchor);
            var hero = !string.IsNullOrEmpty(heroId) && heroById.TryGetValue(heroId!, out var found) ? found : null;
            rows.Add((anchor, hero));
        }

        return rows;
    }

    private IReadOnlyList<TacticalWorkshopAnchorViewState> BuildAnchors(
        IReadOnlyList<(DeploymentAnchorId Anchor, HeroInstanceRecord? Hero)> assignments)
    {
        // SquadBuilder가 anchor 편집 → 세션 truth에 즉시 반영. TW는 read-only 시각화.
        return assignments
            .Select(entry =>
            {
                var classKey = entry.Hero?.ClassId ?? string.Empty;
                return new TacticalWorkshopAnchorViewState(
                    AnchorId: entry.Anchor.ToString(),
                    AssignedHeroId: entry.Hero?.HeroId ?? string.Empty,
                    AssignedFigure: string.IsNullOrEmpty(classKey) ? null : _classSprite?.Invoke(classKey),
                    ClassKey: classKey);
            })
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopPostureViewState> BuildPostures(TeamPostureType selected)
    {
        return PostureCatalog
            .Select(p => new TacticalWorkshopPostureViewState(
                PostureId: p.Id,
                SpriteKey: p.SpriteKey,
                Sprite: _postureSprite?.Invoke(p.SpriteKey),
                KoLabel: p.KoLabel,
                IsSelected: string.Equals(p.Id, selected.ToString(), StringComparison.Ordinal)))
            .ToList();
    }

    private (IReadOnlyList<TacticalWorkshopSynergyChipViewState> Chips, string EmptyText) BuildSynergyChips(
        IReadOnlyList<HeroInstanceRecord> deployedHeroes,
        bool snapshotAvailable,
        SM.Meta.Model.CombatContentSnapshot? snapshot)
    {
        var none = (IReadOnlyList<TacticalWorkshopSynergyChipViewState>)Array.Empty<TacticalWorkshopSynergyChipViewState>();
        if (deployedHeroes.Count == 0)
        {
            return (none, "배치하면 발동 시너지가 표시됩니다.");
        }

        if (!snapshotAvailable || snapshot == null)
        {
            return (none, "시너지 데이터를 불러오지 못했습니다.");
        }

        // 배치 분대 태그 집계 — SquadBuilder RenderSynergyChips와 동일 규칙(같은 SoT, 두 화면 동일 판정).
        var deployedTags = new List<IReadOnlyList<string>>(deployedHeroes.Count);
        foreach (var hero in deployedHeroes)
        {
            var tags = new List<string> { hero.RaceId, hero.ClassId };
            if (!string.IsNullOrWhiteSpace(hero.ArchetypeId)
                && snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var template)
                && template.RecruitPlanTags != null)
            {
                tags.AddRange(template.RecruitPlanTags);
            }

            deployedTags.Add(tags);
        }

        var surfaces = SquadSynergyPreview.Evaluate(deployedTags, snapshot.SynergyCatalog)
            .Where(surface => surface.CurrentCount > 0)
            .ToList();

        if (surfaces.Count == 0)
        {
            return (none, "활성 시너지 없음 · 같은 세력/직업 2명 이상 배치");
        }

        var chips = surfaces
            .Select(surface =>
            {
                var bound = surface.IsActive
                    ? surface.ActiveThreshold
                    : (surface.NextThreshold > 0 ? surface.NextThreshold : surface.ActiveThreshold);
                return new TacticalWorkshopSynergyChipViewState(
                    SynergyId: surface.SynergyId,
                    KoLabel: _synergyName(surface.SynergyId),
                    CountLabel: $"{surface.CurrentCount}/{bound}",
                    IsActive: surface.IsActive);
            })
            .ToList();
        return (chips, string.Empty);
    }

    private IReadOnlyList<TacticalWorkshopThreatViewState> BuildThreats(
        IReadOnlyList<HeroInstanceRecord> deployedHeroes,
        bool snapshotAvailable,
        SM.Meta.Model.CombatContentSnapshot? snapshot)
    {
        // 배치 분대 → 아키타입 governance → 팀 카운터 커버리지. SquadBuilder 대응 요약과 동일 SoT.
        var templates = new List<SM.Meta.Model.CombatArchetypeTemplate>(deployedHeroes.Count);
        if (snapshotAvailable && snapshot != null)
        {
            foreach (var hero in deployedHeroes)
            {
                if (!string.IsNullOrWhiteSpace(hero.ArchetypeId)
                    && snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var template))
                {
                    templates.Add(template);
                }
            }
        }

        var strongSet = new HashSet<string>(StringComparer.Ordinal);
        var gapSet = new HashSet<string>(StringComparer.Ordinal);
        var evaluated = templates.Count > 0;
        if (evaluated)
        {
            var (strong, gaps) = SquadCounterCoveragePreview.Classify(SquadCounterCoveragePreview.Evaluate(templates));
            strongSet.UnionWith(strong);
            gapSet.UnionWith(gaps);
        }

        // lane id는 SquadCounterCoveragePreview.Dimensions에서 파생 — 정적 사본 목록을 두지 않는다.
        return SquadCounterCoveragePreview.Dimensions
            .Select(dimension =>
            {
                var answerState = !evaluated
                    ? string.Empty
                    : strongSet.Contains(dimension) ? "answered"
                    : gapSet.Contains(dimension) ? "unanswered"
                    : "partial";
                var spriteKey = ThreatSpriteKey(dimension);
                return new TacticalWorkshopThreatViewState(
                    LaneId: dimension,
                    SpriteKey: spriteKey,
                    Sprite: _threatSprite?.Invoke(spriteKey),
                    KoLabel: TacticsLexicon.CounterTool(dimension),
                    AnswerState: answerState);
            })
            .ToList();
    }

    private IReadOnlyList<TacticalWorkshopHeroTacticViewState> BuildTactics(
        IReadOnlyList<(DeploymentAnchorId Anchor, HeroInstanceRecord? Hero)> assignments)
    {
        // deployed hero × role instruction + behavior profile + P1 타겟 지시 요약.
        // condition→action→target rule chain은 runtime 모델에 없으므로 가짜 RuleSet을 만들지 않는다.
        var activeBlueprint = ResolveActiveBlueprint();
        var rows = new List<TacticalWorkshopHeroTacticViewState>();

        foreach (var (anchor, hero) in assignments)
        {
            if (hero == null)
            {
                continue;
            }

            var roleInstructionId = ResolveRoleInstructionId(hero, anchor, activeBlueprint);
            var fallbackRoleTag = ResolveDefaultRoleTag(hero.ClassId, anchor);
            RoleInstructionDefinition? roleInstruction = null;
            if (!string.IsNullOrWhiteSpace(roleInstructionId)
                && _contentLookup.TryGetRoleInstructionDefinition(roleInstructionId, out var resolvedRole))
            {
                roleInstruction = resolvedRole;
            }

            var behaviorProfile = ResolveBehaviorProfile(hero);
            rows.Add(new TacticalWorkshopHeroTacticViewState(
                HeroId: hero.HeroId,
                DisplayName: ResolveHeroDisplayName(hero),
                AnchorLabel: TacticsLexicon.Anchor(anchor),
                RoleLabel: _roleName(roleInstructionId, roleInstruction?.RoleTag ?? fallbackRoleTag),
                FormationLabel: TacticsLexicon.Formation(behaviorProfile?.FormationLine),
                RangeLabel: TacticsLexicon.Range(behaviorProfile?.RangeDiscipline),
                DirectiveLabel: TacticsLexicon.Directive(_session.GetHeroTargetDirective(hero.HeroId)),
                Biases: BuildBiases(roleInstruction)));
        }

        return rows;
    }

    private SquadBlueprintRecord? ResolveActiveBlueprint()
    {
        return _session.Profile.SquadBlueprints.FirstOrDefault(record =>
                   string.Equals(record.BlueprintId, _session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
               ?? _session.Profile.SquadBlueprints.FirstOrDefault();
    }

    private string ResolveHeroDisplayName(HeroInstanceRecord hero)
        => HeroDisplayLabelFormatter.ResolvePersonAndJob(hero, _characterName, _archetypeName);

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
            && _contentLookup.TryGetArchetype(hero.ArchetypeId, out var archetype))
        {
            return archetype.BehaviorProfile;
        }

        if (!string.IsNullOrWhiteSpace(hero.HeroId)
            && _contentLookup.TryGetArchetype(hero.HeroId, out var heroArchetype))
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

    private static float Clamp01(float value) => Mathf.Clamp01(value);

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

    /// <summary>counter-coverage 차원 → 위협 glyph sprite 키 (Sprites/Threat/threat_{key}.png).</summary>
    private static string ThreatSpriteKey(string dimension) => dimension switch
    {
        "ArmorShred" => "pierce",
        "Exposure" => "burst",
        "GuardBreakMultiHit" => "dive",
        "TrackingArea" => "swarm",
        "TenacityStability" => "sustain",
        "AntiHealShatter" => "heal",
        "InterceptPeel" => "control",
        "CleaveWaveclear" => "summon",
        _ => "pierce",
    };

    public static IReadOnlyList<(string Id, string SpriteKey, string KoLabel)> Postures
        => PostureCatalog.Select(p => (p.Id, p.SpriteKey, p.KoLabel)).ToList();

    public static IReadOnlyList<(string Id, string SpriteKey, string KoLabel)> Threats
        => SquadCounterCoveragePreview.Dimensions
            .Select(d => (d, ThreatSpriteKey(d), TacticsLexicon.CounterTool(d)))
            .ToList();
}
