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

namespace SM.Unity.UI.Town;

/// <summary>
/// TacticalSetup production presenter — TownSquadBuilder/SquadBuilderPresenter는 legacy implementation alias.
///
/// 헤드리스-순수화 (EquipmentRefitPresenter 패턴): 씬 세션 루트(MonoBehaviour) 대신 순수
/// GameSessionState + ICombatContentLookup, 콘크리트 View 대신 ISquadBuilderView, ContentTextResolver
/// (→GameLocalizationController MonoBehaviour) 대신 이름 resolver delegate를 받아 씬·엔진 없이 구동.
/// LoadoutView는 ProfileQueries(세션 어댑터) 경유라 Func delegate로, 저장은 Action delegate로 seam화.
///
/// V1 동작:
/// - anchor 버튼 클릭 → SessionState.CycleDeploymentAssignment(anchor) (다음 hero로 cycle, 빈 슬롯 포함)
/// - posture 버튼 클릭 → SessionState.SetTeamPosture(posture)
/// - ESC 또는 close 버튼으로 닫기
///
/// 후속 (별도 task):
/// - drag-drop hero card → anchor 직접 배치
/// - posture별 movement preview / threat coverage hint
/// </summary>
public sealed class SquadBuilderPresenter : ISquadBuilderActions
{
    private const int DeploymentCap = 6;

    private readonly GameSessionState _session;
    private readonly ICombatContentLookup _lookup;
    private readonly ISquadBuilderView _view;
    private readonly Func<SM.Meta.Model.LoadoutView?> _loadoutView;
    private readonly Action _saveProfile;
    private readonly Func<string, string> _className;
    private readonly Func<string, string> _raceName;
    private readonly Func<string, string> _synergyName;
    private readonly Func<string, string, string> _roleName;   // (roleInstructionId, fallbackRoleTag)
    private readonly Func<string, string> _archetypeName;
    private readonly Func<string, string, string>? _characterName;

    private DeploymentAnchorId _selectedAnchor = DeploymentAnchorId.FrontCenter;
    private string _statusText = "편성 상태를 확인하세요.";
    private bool _isOpen;

    // formation board / posture rail의 고정 표시 순서 — UXML 버튼 6+5개와 1:1.
    private static readonly DeploymentAnchorId[] AnchorOrder =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom,
    };

    private static readonly TeamPostureType[] PostureOrder =
    {
        TeamPostureType.HoldLine,
        TeamPostureType.StandardAdvance,
        TeamPostureType.ProtectCarry,
        TeamPostureType.CollapseWeakSide,
        TeamPostureType.AllInBackline,
    };

    public SquadBuilderPresenter(
        GameSessionState session,
        ICombatContentLookup lookup,
        ISquadBuilderView view,
        Func<SM.Meta.Model.LoadoutView?> loadoutView,
        Action saveProfile,
        Func<string, string> className,
        Func<string, string> raceName,
        Func<string, string> synergyName,
        Func<string, string, string> roleName,
        Func<string, string> archetypeName,
        Func<string, string, string>? characterName = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _loadoutView = loadoutView ?? throw new ArgumentNullException(nameof(loadoutView));
        _saveProfile = saveProfile ?? throw new ArgumentNullException(nameof(saveProfile));
        _className = className ?? throw new ArgumentNullException(nameof(className));
        _raceName = raceName ?? throw new ArgumentNullException(nameof(raceName));
        _synergyName = synergyName ?? throw new ArgumentNullException(nameof(synergyName));
        _roleName = roleName ?? throw new ArgumentNullException(nameof(roleName));
        _archetypeName = archetypeName ?? throw new ArgumentNullException(nameof(archetypeName));
        _characterName = characterName;
    }

    public void Initialize()
    {
        _view.Bind(this);
        Refresh();
    }

    public void Open()
    {
        _isOpen = true;
        Refresh();
        _view.FocusModal();
    }

    public void Close()
    {
        _isOpen = false;
        Refresh();
    }

    public void Refresh() => _view.Render(BuildState());

    // === ISquadBuilderActions ===

    void ISquadBuilderActions.OnAnchorClicked(DeploymentAnchorId anchor)
    {
        _selectedAnchor = anchor;
        _session.CycleDeploymentAssignment(anchor);
        _statusText = $"배치 갱신: {LocalizeAnchor(anchor)}";
        Refresh();
    }

    void ISquadBuilderActions.OnPostureClicked(TeamPostureType posture)
    {
        _session.SetTeamPosture(posture);
        _statusText = $"팀 태세 갱신: {LocalizePosture(posture)}";
        Refresh();
    }

    void ISquadBuilderActions.OnTargetDirectiveCycled(string heroId)
    {
        // P1 유닛별 타겟 지시 — 클릭 cycle. 세션 SetHeroTargetDirective → 로드아웃 compile hash까지 흐른다.
        var next = _session.CycleHeroTargetDirective(heroId);
        _statusText = $"타겟 지시 변경: {LocalizeDirective(next)}";
        Refresh();
    }

    void ISquadBuilderActions.OnReset()
    {
        foreach (var anchor in AnchorOrder)
        {
            _session.AssignHeroToAnchor(anchor, null);
        }
        _session.SetTeamPosture(TeamPostureType.StandardAdvance);
        _selectedAnchor = DeploymentAnchorId.FrontCenter;
        _statusText = "편성을 초기화했습니다.";
        Refresh();
    }

    void ISquadBuilderActions.OnConfirm()
    {
        _saveProfile();
        _statusText = "편성을 저장하고 출정 준비를 마쳤습니다.";
        Refresh();
        Close();
    }

    // === ViewState builder — 세션 truth → 순수 read model ===

    public SquadBuilderViewState BuildState()
    {
        var session = _session;
        var loadout = _loadoutView();
        var heroById = session.Profile.Heroes.ToDictionary(h => h.HeroId, StringComparer.Ordinal);
        var anchorByHeroId = BuildAnchorByHeroId(loadout);
        var expeditionSet = new HashSet<string>(loadout?.ExpeditionSquadHeroIds ?? Array.Empty<string>(), StringComparer.Ordinal);

        // formation board 6슬롯
        var anchorSlots = AnchorOrder
            .Select(anchor =>
            {
                var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == anchor);
                var heroId = deployment?.HeroId ?? string.Empty;
                SquadBuilderHeroRowViewState? row = null;
                if (!string.IsNullOrEmpty(heroId) && heroById.TryGetValue(heroId, out var hero))
                {
                    row = BuildHeroRow(session, hero, anchorByHeroId, expeditionSet);
                }

                return new SquadBuilderAnchorSlotViewState(anchor, ShortAnchorLabel(anchor), row, anchor == _selectedAnchor);
            })
            .ToList();

        var selectedPosture = session.SelectedTeamPosture;
        var postures = PostureOrder
            .Select(posture => new SquadBuilderPostureViewState(posture, posture == selectedPosture))
            .ToList();

        // roster — 배치 > 원정 > 이름 Ordinal
        var rosterRows = session.Profile.Heroes
            .OrderByDescending(hero => anchorByHeroId.ContainsKey(hero.HeroId))
            .ThenByDescending(hero => expeditionSet.Contains(hero.HeroId))
            .ThenBy(hero => ResolveHeroDisplayName(hero), StringComparer.Ordinal)
            .Select(hero => BuildHeroRow(session, hero, anchorByHeroId, expeditionSet))
            .ToList();

        var selectedDetail = BuildSelectedDetail(session, loadout, heroById, anchorByHeroId, expeditionSet, out var selectedRow);
        var operationRows = BuildOperationRows(session, anchorByHeroId.Count, selectedRow);
        var targetDirective = selectedRow != null
            ? new SquadBuilderTargetDirectiveViewState(selectedRow.HeroId, LocalizeDirective(session.GetHeroTargetDirective(selectedRow.HeroId)))
            : null;
        var (synergyChips, synergyEmptyText) = BuildSynergyChips(anchorByHeroId, heroById);
        var responseSummary = BuildResponseSummary(session, anchorByHeroId, heroById);

        // wave-58 mockup chip strip — 배치 X/6, 태세, 위험 점수 (heuristic).
        var deployedCount = loadout?.Deployments?.Count(d => !string.IsNullOrEmpty(d.HeroId)) ?? 0;
        // V1 단순 heuristic: 배치 미달 + posture 위험성 가중. 정식 risk score는 wave-59+ Atlas/Encounter wire.
        var riskScore = Math.Max(0, DeploymentCap - deployedCount) * 3 + PostureRiskWeight(selectedPosture);

        return new SquadBuilderViewState(
            IsOpen: _isOpen,
            AnchorSlots: anchorSlots,
            Postures: postures,
            RosterRows: rosterRows,
            RosterCount: rosterRows.Count,
            SelectedDetail: selectedDetail,
            OperationRows: operationRows,
            TargetDirective: targetDirective,
            SynergyChips: synergyChips,
            SynergyEmptyText: synergyEmptyText,
            ResponseSummary: responseSummary,
            DeploymentChipLabel: $"배치 {deployedCount}/{DeploymentCap}",
            PostureChipLabel: $"태세 {LocalizePosture(selectedPosture)}",
            RiskChipLabel: deployedCount == 0 ? "위험 점수 —" : $"위험 점수 {riskScore}",
            StatusText: $"{_statusText} · 현재 팀 태세: {LocalizePosture(selectedPosture)}");
    }

    private SquadBuilderSelectedDetailViewState BuildSelectedDetail(
        GameSessionState session,
        SM.Meta.Model.LoadoutView? loadout,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        HashSet<string> expeditionSet,
        out SquadBuilderHeroRowViewState? selectedRow)
    {
        var deployment = loadout?.Deployments.FirstOrDefault(d => d.Anchor == _selectedAnchor);
        var heroId = deployment?.HeroId ?? string.Empty;
        var anchorLabel = $"선택 앵커 · {LocalizeAnchor(_selectedAnchor)}";

        if (string.IsNullOrWhiteSpace(heroId) || !heroById.TryGetValue(heroId, out var hero))
        {
            selectedRow = null;
            return new SquadBuilderSelectedDetailViewState(
                SelectedAnchorLabel: anchorLabel,
                Name: "비어있음",
                Meta: "이 anchor에는 hero가 없습니다.",
                Loadout: "formation board의 anchor를 누르면 기존 순환 규칙으로 배치가 갱신됩니다.",
                Tags: new[] { "empty", LocalizePosture(session.SelectedTeamPosture) });
        }

        var row = BuildHeroRow(session, hero, anchorByHeroId, expeditionSet);
        selectedRow = row;
        return new SquadBuilderSelectedDetailViewState(
            SelectedAnchorLabel: anchorLabel,
            Name: row.DisplayName,
            Meta: row.MetaLabel,
            Loadout: row.LoadoutLabel,
            Tags: new[]
            {
                row.DeploymentLabel,
                row.FormationLabel,
                row.BiasLabel,
                row.RarityLabel,
                $"팀 태세 {LocalizePosture(session.SelectedTeamPosture)}",
            });
    }

    // 운용(operation) 행 — 전열/역할/거리/편성. "지시" cycle 행은 TargetDirective ViewState로 별도 전달
    // (View가 "역할" 행 아래에 렌더). RenderTacticalDecisionRows 후신.
    private IReadOnlyList<SquadBuilderOperationRowViewState> BuildOperationRows(
        GameSessionState session,
        int deployedCount,
        SquadBuilderHeroRowViewState? selectedRow)
    {
        return new[]
        {
            new SquadBuilderOperationRowViewState("전열", selectedRow?.DeploymentLabel ?? LocalizeAnchor(_selectedAnchor)),
            new SquadBuilderOperationRowViewState("역할", selectedRow?.RoleLabel ?? "선택 없음"),
            new SquadBuilderOperationRowViewState("거리", selectedRow?.RangeLabel ?? "기본 교전 거리"),
            new SquadBuilderOperationRowViewState("편성", $"배치 {deployedCount}/{DeploymentCap} · 원정 {session.ExpeditionSquadHeroIds.Count}/4"),
        };
    }

    // 배치된 분대의 활성 시너지를 표면화 — 전투/밸런스가 쓰는 content.SynergyCatalog 와 동일 SoT.
    // (이전엔 이 자리 SquadBuilderSynergyChips 에 posture/역할 chip 이 들어가 이름과 내용이 어긋났음.
    //  per-hero formation/bias 는 선택 영웅 디테일 태그로 이동.)
    private (IReadOnlyList<SquadBuilderSynergyChipViewState> Chips, string EmptyText) BuildSynergyChips(
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById)
    {
        var none = (IReadOnlyList<SquadBuilderSynergyChipViewState>)Array.Empty<SquadBuilderSynergyChipViewState>();
        if (anchorByHeroId.Count == 0)
        {
            return (none, "배치하면 시너지가 표시됩니다");
        }

        if (!_lookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return (none, "시너지 데이터를 불러오지 못했습니다");
        }

        var deployedTags = new List<IReadOnlyList<string>>(anchorByHeroId.Count);
        foreach (var heroId in anchorByHeroId.Keys)
        {
            if (!heroById.TryGetValue(heroId, out var hero))
            {
                continue;
            }

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
                var name = _synergyName(surface.SynergyId);
                var bound = surface.IsActive
                    ? surface.ActiveThreshold
                    : (surface.NextThreshold > 0 ? surface.NextThreshold : surface.ActiveThreshold);
                return new SquadBuilderSynergyChipViewState($"{name} {surface.CurrentCount}/{bound}", surface.IsActive);
            })
            .ToList();
        return (chips, string.Empty);
    }

    // 응답("대응") 요약 — posture 기준 + 배치 분대의 카운터 커버리지(강함/취약)를 한 줄로 표면화.
    // 위협 그리드 UI 가 프로덕션 SquadBuilder 엔 없으므로 신규 UXML 없이 기존 요약 라벨에 텍스트로 surface.
    private string BuildResponseSummary(
        GameSessionState session,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById)
    {
        var posture = LocalizePosture(session.SelectedTeamPosture);
        var coverageLine = BuildCoverageLine(anchorByHeroId, heroById);
        var disclaimer = "확정된 전투 예측이 아니라, 지금 편성으로 읽은 대응 힌트입니다.";
        return string.IsNullOrEmpty(coverageLine)
            ? $"{posture} 기준. {disclaimer}"
            : $"{posture} 기준 · {coverageLine}\n{disclaimer}";
    }

    // 배치 분대 → 아키타입 governance → 팀 카운터 커버리지. 전투/거버넌스와 동일 SoT(CounterCoverageAggregationService).
    private string BuildCoverageLine(
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        IReadOnlyDictionary<string, HeroInstanceRecord> heroById)
    {
        if (anchorByHeroId.Count == 0
            || !_lookup.TryGetCombatSnapshot(out var snapshot, out _))
        {
            return string.Empty;
        }

        var templates = new List<SM.Meta.Model.CombatArchetypeTemplate>(anchorByHeroId.Count);
        foreach (var heroId in anchorByHeroId.Keys)
        {
            if (heroById.TryGetValue(heroId, out var hero)
                && !string.IsNullOrWhiteSpace(hero.ArchetypeId)
                && snapshot.Archetypes.TryGetValue(hero.ArchetypeId, out var template))
            {
                templates.Add(template);
            }
        }

        if (templates.Count == 0)
        {
            return string.Empty;
        }

        var (strong, gaps) = SquadCounterCoveragePreview.Classify(SquadCounterCoveragePreview.Evaluate(templates));
        var parts = new List<string>();
        if (strong.Count > 0)
        {
            parts.Add($"대응 강함: {string.Join("·", strong.Select(LocalizeCounterTool))}");
        }
        if (gaps.Count > 0)
        {
            parts.Add($"취약: {string.Join("·", gaps.Select(LocalizeCounterTool))}");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : "대응 도구 없음 — 보강 필요";
    }

    private SquadBuilderHeroRowViewState BuildHeroRow(
        GameSessionState session,
        HeroInstanceRecord hero,
        IReadOnlyDictionary<string, DeploymentAnchorId> anchorByHeroId,
        HashSet<string> expeditionSet)
    {
        var progression = session.Profile.HeroProgressions
            .FirstOrDefault(entry => string.Equals(entry.HeroId, hero.HeroId, StringComparison.Ordinal));
        var loadout = session.Profile.HeroLoadouts
            .FirstOrDefault(entry => string.Equals(entry.HeroId, hero.HeroId, StringComparison.Ordinal));
        var level = progression?.Level ?? 1;
        var xpPct = progression != null ? Mathf.Clamp(progression.Experience % 100, 0, 100) : 0;
        var equippedItemCount = loadout?.EquippedItemInstanceIds.Count(id => !string.IsNullOrWhiteSpace(id))
            ?? hero.EquippedItemIds.Count(id => !string.IsNullOrWhiteSpace(id));
        var equippedSkillCount = loadout?.EquippedSkillInstanceIds.Count(id => !string.IsNullOrWhiteSpace(id)) ?? 0;
        var passiveCount = loadout?.SelectedPassiveNodeIds.Count(id => !string.IsNullOrWhiteSpace(id)) ?? 0;
        var className = string.IsNullOrWhiteSpace(hero.ClassId) ? "class 미정" : _className(hero.ClassId);
        var raceName = string.IsNullOrWhiteSpace(hero.RaceId) ? "race 미정" : _raceName(hero.RaceId);
        var isDeployed = anchorByHeroId.ContainsKey(hero.HeroId);
        var deploymentLabel = anchorByHeroId.TryGetValue(hero.HeroId, out var anchor)
            ? LocalizeAnchor(anchor)
            : expeditionSet.Contains(hero.HeroId)
                ? "원정 후보"
                : "대기";
        var operationAnchor = anchorByHeroId.TryGetValue(hero.HeroId, out var assignedAnchor)
            ? assignedAnchor
            : DeploymentAnchorId.FrontCenter;
        var activeBlueprint = ResolveActiveBlueprint(session);
        var roleInstructionId = ResolveRoleInstructionId(hero, operationAnchor, activeBlueprint);
        var fallbackRoleTag = ResolveDefaultRoleTag(hero.ClassId, operationAnchor);
        RoleInstructionDefinition? roleInstruction = null;
        if (!string.IsNullOrWhiteSpace(roleInstructionId)
            && _lookup.TryGetRoleInstructionDefinition(roleInstructionId, out var resolvedRole))
        {
            roleInstruction = resolvedRole;
        }

        var behaviorProfile = ResolveBehaviorProfile(hero);
        var roleLabel = _roleName(roleInstructionId, roleInstruction?.RoleTag ?? fallbackRoleTag);
        var formationLabel = LocalizeFormation(behaviorProfile?.FormationLine);
        var rangeLabel = LocalizeRange(behaviorProfile?.RangeDiscipline);
        var biasLabel = BuildBiasLabel(roleInstruction);
        var displayName = ResolveHeroDisplayName(hero);

        return new SquadBuilderHeroRowViewState(
            HeroId: hero.HeroId,
            DisplayName: displayName,
            MetaLabel: $"{className} / {raceName} · Lv {level} · XP {xpPct}%",
            LoadoutLabel: $"장비 {equippedItemCount} · 스킬 {equippedSkillCount} · 패시브 {passiveCount}",
            DeploymentLabel: deploymentLabel,
            RoleLabel: roleLabel,
            FormationLabel: formationLabel,
            RangeLabel: rangeLabel,
            BiasLabel: biasLabel,
            ClassKey: NormalizeClassKey(hero.ClassId),
            RarityLabel: hero.RecruitTier.ToString().ToLowerInvariant(),
            IsDeployed: isDeployed,
            CharacterId: string.IsNullOrWhiteSpace(hero.CharacterId) ? hero.ArchetypeId : hero.CharacterId,
            PipCount: ResolveRosterPipCount(isDeployed, deploymentLabel),
            Glyph: BuildRosterGlyph(displayName, isDeployed));
    }

    private static int ResolveRosterPipCount(bool isDeployed, string deploymentLabel)
    {
        if (isDeployed) return 5;
        return deploymentLabel == "원정 후보" ? 4 : 3;
    }

    private static string BuildRosterGlyph(string displayName, bool isDeployed)
    {
        var name = displayName?.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            return name[..1];
        }

        return isDeployed ? "◆" : "◇";
    }

    private static int PostureRiskWeight(TeamPostureType posture) => posture switch
    {
        TeamPostureType.HoldLine => 4,
        TeamPostureType.StandardAdvance => 8,
        TeamPostureType.ProtectCarry => 6,
        TeamPostureType.CollapseWeakSide => 12,
        TeamPostureType.AllInBackline => 14,
        _ => 8,
    };

    private static string ShortAnchorLabel(DeploymentAnchorId anchor) => anchor switch
    {
        DeploymentAnchorId.FrontTop => "전 상",
        DeploymentAnchorId.FrontCenter => "전 중",
        DeploymentAnchorId.FrontBottom => "전 하",
        DeploymentAnchorId.BackTop => "후 상",
        DeploymentAnchorId.BackCenter => "후 중",
        DeploymentAnchorId.BackBottom => "후 하",
        _ => "배치",
    };

    private static string NormalizeClassKey(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId)) return "unknown";
        var normalized = classId.Trim().ToLowerInvariant();
        return normalized is "vanguard" or "duelist" or "ranger" or "mystic"
            ? normalized
            : "unknown";
    }

    private SquadBlueprintRecord? ResolveActiveBlueprint(GameSessionState session)
    {
        return session.Profile.SquadBlueprints.FirstOrDefault(record =>
                   string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
               ?? session.Profile.SquadBlueprints.FirstOrDefault();
    }

    private string ResolveRoleInstructionId(
        HeroInstanceRecord hero,
        DeploymentAnchorId anchor,
        SquadBlueprintRecord? activeBlueprint)
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
            && _lookup.TryGetArchetype(hero.ArchetypeId, out var archetype))
        {
            return archetype.BehaviorProfile;
        }

        if (!string.IsNullOrWhiteSpace(hero.HeroId)
            && _lookup.TryGetArchetype(hero.HeroId, out var heroArchetype))
        {
            return heroArchetype.BehaviorProfile;
        }

        return null;
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

    private static string BuildBiasLabel(RoleInstructionDefinition? roleInstruction)
    {
        // 세 성향(ProtectCarryBias / BacklinePressureBias / RetreatBias) 중 가장 큰 것을 이름표로 쓴다.
        // 코드 필드명은 bias 지만 화면에는 "성향" 으로 적는다 — 나머지 둘은 이미 그렇게 돼 있었다.
        if (roleInstruction == null)
        {
            return "기본 성향";
        }

        var protect = Mathf.Clamp01(roleInstruction.ProtectCarryBias);
        var pressure = Mathf.Clamp01(roleInstruction.BacklinePressureBias);
        var retreat = Mathf.Clamp01(roleInstruction.RetreatBias);
        if (protect >= pressure && protect >= retreat) return "보호 성향";
        if (pressure >= retreat) return "후열 압박";
        return "후퇴 성향";
    }

    private static IReadOnlyDictionary<string, DeploymentAnchorId> BuildAnchorByHeroId(SM.Meta.Model.LoadoutView? loadout)
    {
        var result = new Dictionary<string, DeploymentAnchorId>(StringComparer.Ordinal);
        if (loadout == null) return result;

        foreach (var deployment in loadout.Deployments)
        {
            if (!string.IsNullOrWhiteSpace(deployment.HeroId))
            {
                result[deployment.HeroId] = deployment.Anchor;
            }
        }

        return result;
    }

    private string ResolveHeroDisplayName(HeroInstanceRecord hero)
        => HeroDisplayLabelFormatter.ResolvePersonAndJob(hero, _characterName, _archetypeName);

    // 전술 어휘 표시명은 TacticsLexicon 단일 소스 — 전술 공방(TacticalWorkshop)과 라벨 드리프트 방지.
    private static string LocalizeAnchor(DeploymentAnchorId anchor) => TacticsLexicon.Anchor(anchor);

    private static string LocalizePosture(TeamPostureType posture) => TacticsLexicon.Posture(posture);

    private static string LocalizeFormation(FormationLine? formation) => TacticsLexicon.Formation(formation);

    private static string LocalizeRange(RangeDiscipline? range) => TacticsLexicon.Range(range);

    private static string LocalizeDirective(PlayerTargetDirective directive) => TacticsLexicon.Directive(directive);

    private static string LocalizeCounterTool(string tool) => TacticsLexicon.CounterTool(tool);
}
