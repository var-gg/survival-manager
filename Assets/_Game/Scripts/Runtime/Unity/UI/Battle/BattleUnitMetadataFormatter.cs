using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using UnityEngine;

namespace SM.Unity.UI.Battle;

public sealed record BattleSkillSlotViewState(
    string SlotLabel,
    string SkillName,
    string SkillId,
    Texture2D? Icon,
    string Description,
    string TimingText,
    string EffectSummary,
    string ScalingSummary,
    IReadOnlyList<string> Tags,
    bool IsSignatureSlot,
    bool IsFlexSlot,
    bool IsActiveSlot,
    bool HasResolvedDefinition,
    string PresentationStyle);

public enum BattleStatLineCategory
{
    Vital = 0,
    Combat = 1,
    Defense = 2,
    Resource = 3,
    Movement = 4,
    Targeting = 5,
}

public enum BattleStatusEffectSection
{
    Permanent = 0,
    BattleScoped = 1,
}

public sealed record BattleStatLine(
    string Label,
    string Value,
    string Tooltip,
    BattleStatLineCategory Category);

public sealed record BattleStatusEffectChip(
    string StatusId,
    string Label,
    Texture2D? Icon,
    float RemainingSeconds,
    float MaxDurationSeconds,
    int StackCount,
    string SourceActorName,
    BattleStatusEffectSection Section,
    string Description,
    string DurationText,
    string PersistenceText,
    string CleanseText);

public sealed record BattleTacticDial(
    string Label,
    float NormalizedValue,
    string ValueText);

public sealed record BattleTacticSummary(
    string PresetName,
    IReadOnlyList<BattleTacticDial> Dials,
    string RoleInstruction = "",
    string ArchetypeQuirk = "",
    IReadOnlyList<string>? PriorityRules = null);

public sealed record BattlePositionSummary(
    DeploymentAnchorId HomeAnchor,
    IReadOnlyList<DeploymentAnchorId> TeammateAnchors);

public sealed record BattleEquipmentSlotViewState(
    string SlotLabel,
    string ItemName,
    bool IsPlaceholder);

public sealed record BattleSelectedUnitViewState(
    bool IsVisible,
    string Header,
    string Body,
    Texture2D? Portrait = null,
    Texture2D? FullBodyPortrait = null,
    string UnitId = "",
    IReadOnlyList<BattleSkillSlotViewState>? SkillSlots = null,
    BattleUnitDetailTab ActiveTab = BattleUnitDetailTab.Overview,
    string OverviewTabLabel = "Overview",
    string StatsTabLabel = "Stats",
    string SkillsTabLabel = "Skills",
    string EquipmentTabLabel = "Equipment",
    string TacticTabLabel = "Tactic",
    string StatusTabLabel = "Status",
    string RecordTabLabel = "Record",
    IReadOnlyList<BattleStatLine>? StatLines = null,
    IReadOnlyList<BattleStatusEffectChip>? StatusEffects = null,
    BattleTacticSummary? TacticSummary = null,
    BattlePositionSummary? PositionSummary = null,
    IReadOnlyList<BattleEquipmentSlotViewState>? EquipmentSlots = null,
    string StatusBody = "",
    string CombatRecordBody = "",
    float HealthNormalized = 0f,
    float ShieldNormalized = 0f,
    bool HasAilmentTint = false)
{
    public static BattleSelectedUnitViewState Hidden { get; } = new(false, string.Empty, string.Empty);
}

public readonly record struct BattleUnitOverheadText(
    string Header,
    string Subtitle);

public sealed class BattleUnitMetadataFormatter
{
    private readonly GameLocalizationController _localization;
    private readonly ICombatContentLookup _lookup;
    private readonly ContentTextResolver _contentText;
    private readonly ContentIconResolver _iconResolver;
    private readonly BattleUnitPortraitResolver _portraitResolver = new();

    public BattleUnitMetadataFormatter(
        GameLocalizationController localization,
        ICombatContentLookup lookup)
    {
        _localization = localization;
        _lookup = lookup;
        _contentText = new ContentTextResolver(localization, lookup);
        _iconResolver = new ContentIconResolver(lookup);
    }

    public BattleUnitOverheadText BuildOverhead(BattleUnitReadModel unit)
    {
        var character = _contentText.GetCharacterName(unit.CharacterId, unit.ArchetypeId);
        var archetype = _contentText.GetArchetypeName(unit.ArchetypeId);
        var header = string.IsNullOrWhiteSpace(archetype) || string.Equals(character, archetype, StringComparison.Ordinal)
            ? character
            : $"{character} ({archetype})";
        var subtitle = string.Join(" / ", new[]
        {
            _contentText.GetRaceName(unit.RaceId),
            _contentText.GetClassName(unit.ClassId),
            _contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag)
        });

        return new BattleUnitOverheadText(
            header,
            subtitle);
    }

    public string BuildStateText(BattleUnitReadModel unit, BattleSimulationStep? step = null)
    {
        return BattleReadabilityFormatter.BuildPlayerFacingState(unit, step, LocaleCode);
    }

    public BattleSelectedUnitViewState BuildSelectedUnitPanel(
        BattleUnitReadModel? unit,
        bool isVisible = true,
        BattleUnitDetailTab activeTab = BattleUnitDetailTab.Overview,
        string combatRecordBody = "",
        TeamTacticProfile? teamTactic = null,
        IReadOnlyList<BattleUnitReadModel>? teamUnits = null)
    {
        if (unit == null)
        {
            return BattleSelectedUnitViewState.Hidden;
        }

        var character = _contentText.GetCharacterName(unit.CharacterId, unit.ArchetypeId);
        var role = _contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag);
        var roleFamily = _contentText.GetRoleFamilyName(unit.ClassId);
        var statLines = BuildStatLines(unit, character, role, roleFamily);
        var statusEffects = BuildStatusEffectChips(unit, character);
        var tacticSummary = BuildTacticSummary(unit, teamTactic);
        var positionSummary = BuildPositionSummary(unit, teamUnits);

        return new BattleSelectedUnitViewState(
            IsVisible: isVisible,
            Header: $"{Localize(GameLocalizationTables.UIBattle, "ui.battle.selected.header", "Selected Unit")}: {character}",
            Body: BuildOverviewBody(unit, character, role, roleFamily, tacticSummary, positionSummary),
            Portrait: _portraitResolver.Resolve(unit),
            FullBodyPortrait: _portraitResolver.ResolveFullBody(unit),
            UnitId: unit.Id,
            SkillSlots: BuildSkillSlots(unit),
            ActiveTab: activeTab,
            OverviewTabLabel: AxisLabel("ui.battle.detail.tab.overview", "개요", "Overview"),
            StatsTabLabel: AxisLabel("ui.battle.detail.tab.stats", "능력치", "Stats"),
            SkillsTabLabel: AxisLabel("ui.battle.detail.tab.skills", "스킬", "Skills"),
            EquipmentTabLabel: AxisLabel("ui.battle.detail.tab.equipment", "장비", "Equipment"),
            TacticTabLabel: AxisLabel("ui.battle.detail.tab.tactic", "전술", "Tactic"),
            StatusTabLabel: AxisLabel("ui.battle.detail.tab.status", "상태", "Status"),
            RecordTabLabel: AxisLabel("ui.battle.detail.tab.record", "전투기록", "Record"),
            StatLines: statLines,
            StatusEffects: statusEffects,
            TacticSummary: tacticSummary,
            PositionSummary: positionSummary,
            EquipmentSlots: BuildEquipmentSlots(),
            StatusBody: BuildStatusDetail(unit, statusEffects),
            CombatRecordBody: string.IsNullOrWhiteSpace(combatRecordBody)
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.detail.record.empty", "No notable personal events yet.")
                : combatRecordBody,
            HealthNormalized: unit.MaxHealth > 0f ? Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 0f,
            ShieldNormalized: unit.MaxHealth > 0f ? Mathf.Clamp01(unit.Barrier / unit.MaxHealth) : 0f,
            HasAilmentTint: HasAilment(unit));
    }

    private IReadOnlyList<BattleStatLine> BuildStatLines(
        BattleUnitReadModel unit,
        string character,
        string role,
        string roleFamily)
    {
        return new[]
        {
            Line("ui.battle.axis.character", "캐릭터", "Character", character, BattleStatLineCategory.Vital),
            Line("ui.battle.axis.role_family", "역할군", "Role Family", roleFamily, BattleStatLineCategory.Vital),
            Line("ui.battle.axis.role", "역할", "Role", $"{role} / {roleFamily}", BattleStatLineCategory.Vital),
            Line(
                "ui.battle.axis.dominant_hand",
                "손잡이",
                "Dominant Hand",
                FormatDominantHand(unit.DominantHand),
                BattleStatLineCategory.Movement,
                "ui.battle.tooltip.dominant_hand",
                "교전 진입 방향과 평타 후 복귀 방향만 설명합니다. 피해나 방어 수치는 바꾸지 않습니다.",
                "Explains entry side and post-attack reset direction only. It does not change damage or defense numbers."),
            Line("ui.battle.axis.hp", "HP", "HP", $"{Mathf.Max(0f, unit.CurrentHealth):0} / {Mathf.Max(1f, unit.MaxHealth):0}", BattleStatLineCategory.Vital),
            Line("ui.battle.axis.shield", "보호막", "Shield", $"{Mathf.Max(0f, unit.Barrier):0}", BattleStatLineCategory.Defense),
            Line("ui.battle.axis.state", "상태", "State", BuildStateText(unit), BattleStatLineCategory.Defense),
            Line("ui.battle.axis.energy", "에너지", "Energy", $"{Mathf.Max(0f, unit.CurrentEnergy):0} / {Mathf.Max(1f, unit.MaxEnergy):0}", BattleStatLineCategory.Resource),
            Line("ui.battle.axis.attack_speed", "공격 속도", "Attack Speed", $"{unit.AttackSpeed:0.0}", BattleStatLineCategory.Combat),
            Line("ui.battle.axis.basic_attack_interval", "기본공격 간격", "Basic Attack Interval", $"{unit.BasicAttackCooldown:0.00}s", BattleStatLineCategory.Combat),
            Line("ui.battle.axis.skill_haste", "스킬 가속", "Skill Haste", $"{unit.SkillHaste:0.##}", BattleStatLineCategory.Combat),
            Line("ui.battle.axis.windup", "시전 진행", "Windup", $"{Mathf.RoundToInt(Mathf.Clamp01(unit.WindupProgress) * 100f)}%", BattleStatLineCategory.Combat),
            Line("ui.battle.axis.cooldown", "쿨다운", "Cooldown", $"{Mathf.Max(0f, unit.CooldownRemaining):0.0}s", BattleStatLineCategory.Combat),
            Line("ui.battle.axis.position", "현재 위치", "Position", $"{unit.Position.X:0.0}, {unit.Position.Y:0.0}", BattleStatLineCategory.Movement),
            Line("ui.battle.axis.anchor", "홈 앵커", "Home Anchor", LocalizeAnchor(unit.Anchor), BattleStatLineCategory.Movement),
            Line("ui.battle.axis.range", "선호 사거리", "Preferred Range", FormatRange(unit), BattleStatLineCategory.Movement),
            Line("ui.battle.axis.footprint", "충돌 반경", "Footprint", $"{unit.NavigationRadius:0.0}m / {unit.SeparationRadius:0.0}m", BattleStatLineCategory.Movement),
            Line("ui.battle.axis.target", "대상", "Target", ResolveTarget(unit), BattleStatLineCategory.Targeting),
            Line("ui.battle.axis.targeting", "타게팅", "Targeting", $"{FormatSelector(unit.CurrentSelector)} / {FormatFallback(unit.CurrentFallback)}", BattleStatLineCategory.Targeting),
            Line("ui.battle.axis.retarget_lock", "재타게팅 잠금", "Retarget Lock", $"{unit.RetargetLockRemaining:0.0}s", BattleStatLineCategory.Targeting),
            Line("ui.battle.axis.slot", "교전 슬롯", "Engage Slot", $"{unit.EngagementSlotCount} @ {unit.EngagementSlotRadius:0.0}m", BattleStatLineCategory.Targeting),
            Line("ui.battle.axis.guard_radius", "가드 반경", "Guard Radius", $"{unit.FrontlineGuardRadius:0.0}m", BattleStatLineCategory.Defense),
            Line("ui.battle.axis.cluster_radius", "클러스터 반경", "Cluster Radius", $"{unit.ClusterRadius:0.0}m", BattleStatLineCategory.Targeting),
            Line(
                "ui.battle.axis.positioning",
                "포지션 의도",
                "Positioning",
                $"{FormatPositioningIntent(unit.PositioningIntent)} / {FormatReevaluationReason(unit.PositioningReplanReason)}",
                BattleStatLineCategory.Movement),
        };
    }

    private BattleStatLine Line(string key, string koFallback, string enFallback, string value, BattleStatLineCategory category)
    {
        var label = AxisLabel(key, koFallback, enFallback);
        return new BattleStatLine(label, value, label, category);
    }

    private BattleStatLine Line(
        string key,
        string koFallback,
        string enFallback,
        string value,
        BattleStatLineCategory category,
        string tooltipKey,
        string koTooltipFallback,
        string enTooltipFallback)
    {
        var label = AxisLabel(key, koFallback, enFallback);
        var tooltip = AxisLabel(tooltipKey, koTooltipFallback, enTooltipFallback);
        return new BattleStatLine(label, value, tooltip, category);
    }

    private string BuildOverviewBody(
        BattleUnitReadModel unit,
        string character,
        string role,
        string roleFamily,
        BattleTacticSummary tacticSummary,
        BattlePositionSummary positionSummary)
    {
        return string.Join("\n", new[]
        {
            $"{AxisLabel("ui.battle.axis.character", "캐릭터", "Character")}: {character}",
            $"{AxisLabel("ui.battle.axis.role_family", "역할군", "Role Family")}: {roleFamily}",
            $"{AxisLabel("ui.battle.axis.role", "역할", "Role")}: {role} / {roleFamily}",
            $"{AxisLabel("ui.battle.axis.state", "상태", "State")}: {BuildStateText(unit)}",
            $"{AxisLabel("ui.battle.axis.anchor", "홈 앵커", "Home Anchor")}: {LocalizeAnchor(positionSummary.HomeAnchor)}",
            $"{AxisLabel("ui.battle.axis.tactic", "팀 전술", "Team Tactic")}: {tacticSummary.PresetName}",
        });
    }

    private string BuildStatusDetail(BattleUnitReadModel unit, IReadOnlyList<BattleStatusEffectChip> statusEffects)
    {
        var permanent = statusEffects.Where(chip => chip.Section == BattleStatusEffectSection.Permanent).Select(chip => chip.Label).ToArray();
        var battleScoped = statusEffects.Where(chip => chip.Section == BattleStatusEffectSection.BattleScoped).Select(chip => chip.Label).ToArray();
        var none = Localize(GameLocalizationTables.UICommon, "ui.common.none", "None");
        return string.Join("\n", new[]
        {
            $"{AxisLabel("ui.battle.axis.hp", "HP", "HP")}: {Mathf.Max(0f, unit.CurrentHealth):0} / {Mathf.Max(1f, unit.MaxHealth):0}",
            $"{AxisLabel("ui.battle.axis.state", "상태", "State")}: {BuildStateText(unit)}",
            $"{AxisLabel("ui.battle.detail.status.permanent", "영구 효과", "Permanent")}: {(permanent.Length == 0 ? none : string.Join(" / ", permanent))}",
            $"{AxisLabel("ui.battle.detail.status.battle_scoped", "전투 효과", "Battle Scoped")}: {(battleScoped.Length == 0 ? none : string.Join(" / ", battleScoped))}",
        });
    }

    private IReadOnlyList<BattleSkillSlotViewState> BuildSkillSlots(BattleUnitReadModel unit)
    {
        return new[]
        {
            BuildSkillSlot(
                AxisLabel("ui.battle.skill.signature_active", "고유 액티브", "Signature"),
                unit.SignatureActiveId,
                unit.SignatureActiveName,
                unit.CharacterId,
                ActionSlotKind.SignatureActive),
            BuildSkillSlot(
                AxisLabel("ui.battle.skill.flex_active", "교체 액티브", "Flex"),
                unit.FlexActiveId,
                unit.FlexActiveName,
                unit.CharacterId,
                ActionSlotKind.FlexActive),
            BuildSkillSlot(
                AxisLabel("ui.battle.skill.signature_passive", "고유 패시브", "Signature Passive"),
                unit.SignaturePassiveId,
                unit.SignaturePassiveName,
                unit.CharacterId,
                ActionSlotKind.SignaturePassive),
            BuildSkillSlot(
                AxisLabel("ui.battle.skill.flex_passive", "교체 패시브", "Flex Passive"),
                unit.FlexPassiveId,
                unit.FlexPassiveName,
                unit.CharacterId,
                ActionSlotKind.FlexPassive),
        };
    }

    private BattleSkillSlotViewState BuildSkillSlot(
        string slotLabel,
        string skillId,
        string skillName,
        string characterId,
        ActionSlotKind slotKind)
    {
        var resolvedName = ResolveSkillDisplayName(skillId, skillName);
        var icon = _iconResolver.ResolveSkill(skillId, characterId);
        var skill = ResolveCompiledSkill(skillId);
        return new BattleSkillSlotViewState(
            slotLabel,
            resolvedName,
            skillId,
            icon,
            ResolveSkillDescription(skillId, skill),
            BuildSkillTimingText(skill, slotKind),
            BuildSkillEffectSummary(skill),
            BuildSkillScalingSummary(skill),
            BuildSkillTags(skill),
            slotKind is ActionSlotKind.SignatureActive or ActionSlotKind.SignaturePassive,
            slotKind is ActionSlotKind.FlexActive or ActionSlotKind.FlexPassive,
            slotKind is ActionSlotKind.SignatureActive or ActionSlotKind.FlexActive,
            skill != null,
            ResolvePresentationStyle(skill));
    }

    private IReadOnlyList<BattleStatusEffectChip> BuildStatusEffectChips(BattleUnitReadModel unit, string characterName)
    {
        var chips = new List<BattleStatusEffectChip>();
        AddPassiveChip(chips, unit, unit.SignaturePassiveId, unit.SignaturePassiveName, "signature_passive", characterName);
        AddPassiveChip(chips, unit, unit.FlexPassiveId, unit.FlexPassiveName, "flex_passive", characterName);

        foreach (var statusId in (unit.StatusIds ?? Array.Empty<string>())
                     .Where(statusId => !string.IsNullOrWhiteSpace(statusId))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(statusId => statusId, StringComparer.Ordinal))
        {
            chips.Add(new BattleStatusEffectChip(
                statusId,
                ResolveStatusLabel(statusId),
                null,
                0f,
                0f,
                1,
                characterName,
                BattleStatusEffectSection.BattleScoped,
                ResolveStatusDescription(statusId),
                AxisLabel("ui.battle.detail.status.battle_scoped", "전투 효과", "Battle Scoped"),
                AxisLabel("ui.battle.detail.status.battle_scoped", "전투 효과", "Battle Scoped"),
                AxisLabel("ui.battle.status.cleanse.pending", "정화 정보 준비 중", "Cleanse information pending")));
        }

        if (!unit.IsAlive || unit.CurrentHealth <= 0f)
        {
            chips.Add(BuildRuntimeChip("downed", AxisLabel("ui.battle.status.downed", "전투불능", "Downed"), characterName, 0f, 0f));
        }

        if (unit.WindupProgress > 0.01f)
        {
            chips.Add(BuildRuntimeChip(
                "windup",
                AxisLabel("ui.battle.axis.windup", "시전 중", "Windup"),
                characterName,
                Mathf.Max(0f, 1f - Mathf.Clamp01(unit.WindupProgress)),
                1f));
        }

        if (unit.CooldownRemaining > 0.01f)
        {
            chips.Add(BuildRuntimeChip(
                "cooldown",
                AxisLabel("ui.battle.status.cooldown", "재사용 대기", "Cooldown"),
                characterName,
                unit.CooldownRemaining,
                Mathf.Max(unit.BasicAttackCooldown, unit.CooldownRemaining)));
        }

        if (unit.Barrier > 0.01f)
        {
            chips.Add(BuildRuntimeChip(
                "barrier",
                AxisLabel("ui.battle.status.barrier", "보호막", "Barrier"),
                characterName,
                unit.Barrier,
                Mathf.Max(1f, unit.MaxHealth)));
        }

        return chips
            .OrderBy(chip => chip.Section)
            .ThenByDescending(ResolveStatusSeverity)
            .ThenByDescending(chip => chip.RemainingSeconds)
            .ThenBy(chip => chip.StatusId, StringComparer.Ordinal)
            .ToList();
    }

    private void AddPassiveChip(
        ICollection<BattleStatusEffectChip> chips,
        BattleUnitReadModel unit,
        string skillId,
        string skillName,
        string fallbackId,
        string sourceActorName)
    {
        if (string.IsNullOrWhiteSpace(skillId) && string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        var statusId = string.IsNullOrWhiteSpace(skillId) ? fallbackId : skillId;
        chips.Add(new BattleStatusEffectChip(
            statusId,
            ResolveSkillDisplayName(skillId, skillName),
            _iconResolver.ResolveSkill(skillId, unit.CharacterId),
            0f,
            0f,
            1,
            sourceActorName,
            BattleStatusEffectSection.Permanent,
            ResolveSkillDescription(skillId, ResolveCompiledSkill(skillId)),
            AxisLabel("ui.battle.detail.status.permanent", "영구 효과", "Permanent"),
            AxisLabel("ui.battle.detail.status.permanent", "영구 효과", "Permanent"),
            "Not cleanseable"));
    }

    private BattleStatusEffectChip BuildRuntimeChip(
        string statusId,
        string label,
        string sourceActorName,
        float remainingSeconds,
        float maxDurationSeconds)
    {
        return new BattleStatusEffectChip(
            statusId,
            label,
            null,
            remainingSeconds,
            maxDurationSeconds,
            1,
            sourceActorName,
            BattleStatusEffectSection.BattleScoped,
            string.Empty,
            maxDurationSeconds > 0.01f
                ? $"{remainingSeconds:0.#}s / {maxDurationSeconds:0.#}s"
                : AxisLabel("ui.battle.detail.status.battle_scoped", "전투 효과", "Battle Scoped"),
            AxisLabel("ui.battle.detail.status.battle_scoped", "전투 효과", "Battle Scoped"),
            AxisLabel("ui.battle.status.cleanse.pending", "정화 정보 준비 중", "Cleanse information pending"));
    }

    private BattleTacticSummary BuildTacticSummary(BattleUnitReadModel unit, TeamTacticProfile? teamTactic)
    {
        var profile = teamTactic ?? TacticContext.DefaultProfile(TeamPostureType.StandardAdvance);
        var dials = new[]
        {
            Dial("ui.battle.tactic.dial.combat_pace", "전투 템포", "Combat Pace", profile.CombatPace, 0.75f, 1.35f),
            Dial("ui.battle.tactic.dial.focus_bias", "집중 공격", "Focus Bias", profile.FocusModeBias, -1f, 1f),
            Dial("ui.battle.tactic.dial.front_spacing", "전열 간격", "Front Spacing", profile.FrontSpacingBias, 0f, 1f),
            Dial("ui.battle.tactic.dial.back_spacing", "후열 간격", "Back Spacing", profile.BackSpacingBias, 0f, 1f),
            Dial("ui.battle.tactic.dial.protect_carry", "캐리 보호", "Protect Carry", profile.ProtectCarryBias, 0f, 1f),
            Dial("ui.battle.tactic.dial.switch_penalty", "타겟 유지", "Target Lock", profile.TargetSwitchPenalty, 0f, 2f),
            Dial("ui.battle.tactic.dial.compactness", "밀집도", "Compactness", profile.Compactness, 0f, 1f),
            Dial("ui.battle.tactic.dial.width", "폭", "Width", profile.Width, 0.35f, 1.5f),
            Dial("ui.battle.tactic.dial.depth", "깊이", "Depth", profile.Depth, 0.35f, 1.5f),
            Dial("ui.battle.tactic.dial.line_spacing", "라인 간격", "Line Spacing", profile.LineSpacing, 0.35f, 1.5f),
            Dial("ui.battle.tactic.dial.flank_bias", "측면 성향", "Flank Bias", profile.FlankBias, -1f, 1f),
            Dial("ui.battle.tactic.dial.role_range", "역할 사거리", "Role Range", unit.PreferredRangeMax, 0f, 6f),
        };
        var presetName = TeamPostureText.Resolve(_localization, profile.Posture);
        var priorityRules = new[]
        {
            $"{AxisLabel("ui.battle.axis.targeting", "타게팅", "Targeting")}: {FormatSelector(unit.CurrentSelector)}",
            $"{AxisLabel("ui.battle.axis.fallback", "대체 규칙", "Fallback")}: {FormatFallback(unit.CurrentFallback)}",
            $"{AxisLabel("ui.battle.axis.range", "선호 사거리", "Preferred Range")}: {FormatRange(unit)}",
        };

        return new BattleTacticSummary(
            presetName,
            dials,
            _contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag),
            _contentText.GetArchetypeName(unit.ArchetypeId),
            priorityRules);
    }

    private BattleTacticDial Dial(string key, string koFallback, string enFallback, float value, float min, float max)
    {
        var normalized = max <= min ? 0f : Mathf.Clamp01((value - min) / (max - min));
        return new BattleTacticDial(
            AxisLabel(key, koFallback, enFallback),
            normalized,
            $"{value:0.##}");
    }

    private static BattlePositionSummary BuildPositionSummary(
        BattleUnitReadModel unit,
        IReadOnlyList<BattleUnitReadModel>? teamUnits)
    {
        var anchors = teamUnits == null
            ? new[] { unit.Anchor }
            : teamUnits
                .OrderBy(member => member.Anchor)
                .ThenBy(member => member.Id, StringComparer.Ordinal)
                .Select(member => member.Anchor)
                .Distinct()
                .ToArray();

        return new BattlePositionSummary(unit.Anchor, anchors);
    }

    private IReadOnlyList<BattleEquipmentSlotViewState> BuildEquipmentSlots()
    {
        var pending = Localize(GameLocalizationTables.UIBattle, "ui.battle.equipment.pending", "Pending equipment schema");
        return new[]
        {
            EquipmentSlot("ui.battle.equipment.weapon", "무기", "Weapon", pending),
            EquipmentSlot("ui.battle.equipment.offhand", "보조", "Offhand", pending),
            EquipmentSlot("ui.battle.equipment.armor", "방어구", "Armor", pending),
            EquipmentSlot("ui.battle.equipment.accessory", "장신구", "Accessory", pending),
        };
    }

    private BattleEquipmentSlotViewState EquipmentSlot(string key, string koFallback, string enFallback, string itemName)
    {
        return new BattleEquipmentSlotViewState(AxisLabel(key, koFallback, enFallback), itemName, true);
    }

    private static int ResolveStatusSeverity(BattleStatusEffectChip chip)
    {
        var id = chip.StatusId ?? string.Empty;
        if (ContainsAny(id, "down", "stun", "knockback", "dead"))
        {
            return 100;
        }

        if (ContainsAny(id, "marked", "focus", "windup", "cast"))
        {
            return 80;
        }

        if (ContainsAny(id, "cooldown", "recover", "barrier", "shield"))
        {
            return 60;
        }

        return chip.Section == BattleStatusEffectSection.Permanent ? 20 : 40;
    }

    private static bool HasAilment(BattleUnitReadModel unit)
    {
        if (!unit.IsAlive || unit.MaxHealth > 0f && unit.CurrentHealth / unit.MaxHealth <= 0.3f)
        {
            return true;
        }

        return (unit.StatusIds ?? Array.Empty<string>()).Any(statusId => ContainsAny(
            statusId ?? string.Empty,
            "stun",
            "fear",
            "charm",
            "burn",
            "poison",
            "bleed",
            "silence",
            "quiet",
            "knockback",
            "marked"));
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private BattleSkillSpec? ResolveCompiledSkill(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return null;
        }

        try
        {
            return _lookup.Snapshot.SkillCatalog.TryGetValue(skillId, out var skill)
                ? skill
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string ResolveSkillDescription(string skillId, BattleSkillSpec? skill)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return string.Empty;
        }

        var description = _contentText.GetSkillDescription(skillId);
        if (!string.IsNullOrWhiteSpace(description)
            && !string.Equals(description, skillId, StringComparison.Ordinal)
            && !description.StartsWith("content.", StringComparison.Ordinal))
        {
            return description;
        }

        return skill == null
            ? string.Empty
            : BuildSkillEffectSummary(skill);
    }

    private string BuildSkillTimingText(BattleSkillSpec? skill, ActionSlotKind slotKind)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (skill.BaseCooldownSeconds > 0.01f)
        {
            parts.Add($"{AxisLabel("ui.battle.axis.cooldown", "쿨다운", "Cooldown")} {skill.BaseCooldownSeconds:0.#}s");
        }
        else if (slotKind is ActionSlotKind.SignaturePassive or ActionSlotKind.FlexPassive)
        {
            parts.Add(AxisLabel("ui.battle.detail.status.permanent", "영구 효과", "Permanent"));
        }
        else
        {
            parts.Add(Localize(GameLocalizationTables.UICommon, "ui.common.none", "None"));
        }

        if (skill.CastWindupSeconds > 0.01f)
        {
            parts.Add($"{AxisLabel("ui.battle.axis.windup", "시전", "Windup")} {skill.CastWindupSeconds:0.#}s");
        }

        if (skill.ManaCost > 0.01f)
        {
            parts.Add($"{AxisLabel("ui.battle.axis.energy", "에너지", "Energy")} {skill.ManaCost:0.#}");
        }

        return string.Join(" / ", parts);
    }

    private string BuildSkillEffectSummary(BattleSkillSpec? skill)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        var range = skill.Range <= 0.05f
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.range.contact", "Contact")
            : $"{skill.Range:0.#}m";
        return $"{FormatSkillKind(skill.Kind)} / {FormatDamage(skill.DamageType)} / {FormatDelivery(skill.Delivery)} / {FormatTarget(skill.TargetRule)} / {AxisLabel("ui.battle.axis.range", "사거리", "Range")} {range}";
    }

    private string BuildSkillScalingSummary(BattleSkillSpec? skill)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        var parts = new List<string>
        {
            $"{AxisLabel("ui.battle.skill.scaling.power", "위력", "Power")} {skill.Power:0.##}"
        };
        if (Math.Abs(skill.PowerFlat) > 0.001f)
        {
            parts.Add($"{AxisLabel("ui.battle.skill.scaling.flat", "고정값", "Flat")} {skill.PowerFlat:0.##}");
        }

        AddCoeff(parts, AxisLabel("ui.battle.skill.scaling.physical", "물리 계수", "Physical"), skill.PhysCoeff);
        AddCoeff(parts, AxisLabel("ui.battle.skill.scaling.magical", "마법 계수", "Magical"), skill.MagCoeff);
        AddCoeff(parts, AxisLabel("ui.battle.skill.scaling.healing", "회복 계수", "Healing"), skill.HealCoeff);
        AddCoeff(parts, AxisLabel("ui.battle.skill.scaling.health", "생명력 계수", "Health"), skill.HealthCoeff);
        return string.Join(" / ", parts);
    }

    private static void AddCoeff(ICollection<string> parts, string label, float value)
    {
        if (Math.Abs(value) > 0.001f)
        {
            parts.Add($"{label} x{value:0.##}");
        }
    }

    private static IReadOnlyList<string> BuildSkillTags(BattleSkillSpec? skill)
    {
        return Array.Empty<string>();
    }

    private static string ResolvePresentationStyle(BattleSkillSpec? skill)
    {
        if (skill == null)
        {
            return "missing";
        }

        return skill.EffectivePresentation.Family switch
        {
            SkillPresentationFamily.Heal => "heal",
            SkillPresentationFamily.Shield => "guard",
            SkillPresentationFamily.Debuff => "control",
            SkillPresentationFamily.Projectile or SkillPresentationFamily.Ranged => "projectile",
            SkillPresentationFamily.Nova or SkillPresentationFamily.Zone or SkillPresentationFamily.Trap => "area",
            SkillPresentationFamily.Reposition => "mobility",
            SkillPresentationFamily.Aura or SkillPresentationFamily.PassiveProc => "support",
            _ => "strike",
        };
    }

    private string FormatSkillKind(SkillKind kind)
    {
        return kind switch
        {
            SkillKind.Strike => AxisLabel("ui.battle.skill.kind.strike", "공격", "Strike"),
            SkillKind.Heal => AxisLabel("ui.battle.skill.kind.heal", "회복", "Heal"),
            SkillKind.Shield => AxisLabel("ui.battle.skill.kind.shield", "보호막", "Shield"),
            SkillKind.Buff => AxisLabel("ui.battle.skill.kind.buff", "강화", "Buff"),
            SkillKind.Debuff => AxisLabel("ui.battle.skill.kind.debuff", "약화", "Debuff"),
            SkillKind.Utility => AxisLabel("ui.battle.skill.kind.utility", "지원", "Utility"),
            _ => AxisLabel("ui.battle.skill.kind.unknown", "알 수 없는 유형", "Unknown type"),
        };
    }

    private string FormatDamage(DamageType damage)
    {
        return damage switch
        {
            DamageType.Physical => AxisLabel("ui.battle.skill.damage.physical", "물리", "Physical"),
            DamageType.Magical => AxisLabel("ui.battle.skill.damage.magical", "마법", "Magical"),
            DamageType.Healing => AxisLabel("ui.battle.skill.damage.healing", "회복", "Healing"),
            DamageType.True => AxisLabel("ui.battle.skill.damage.true", "고정", "True"),
            _ => AxisLabel("ui.battle.skill.damage.unknown", "알 수 없는 피해", "Unknown damage"),
        };
    }

    private string FormatDelivery(SkillDelivery delivery)
    {
        return delivery switch
        {
            SkillDelivery.Melee => AxisLabel("ui.battle.skill.delivery.melee", "근접", "Melee"),
            SkillDelivery.Ranged => AxisLabel("ui.battle.skill.delivery.ranged", "원거리", "Ranged"),
            SkillDelivery.Projectile => AxisLabel("ui.battle.skill.delivery.projectile", "투사체", "Projectile"),
            SkillDelivery.Nova => AxisLabel("ui.battle.skill.delivery.nova", "주변 폭발", "Nova"),
            SkillDelivery.Aura => AxisLabel("ui.battle.skill.delivery.aura", "오라", "Aura"),
            SkillDelivery.Trap => AxisLabel("ui.battle.skill.delivery.trap", "함정", "Trap"),
            SkillDelivery.Zone => AxisLabel("ui.battle.skill.delivery.zone", "영역", "Zone"),
            _ => AxisLabel("ui.battle.skill.delivery.unknown", "알 수 없는 전달 방식", "Unknown delivery"),
        };
    }

    private string FormatTarget(SkillTargetRule target)
    {
        return target switch
        {
            SkillTargetRule.NearestEnemy => AxisLabel("ui.battle.skill.target.nearest_enemy", "가장 가까운 적", "Nearest Enemy"),
            SkillTargetRule.LowestHpEnemy => AxisLabel("ui.battle.skill.target.lowest_hp_enemy", "생명력이 가장 낮은 적", "Lowest HP Enemy"),
            SkillTargetRule.MostExposedEnemy => AxisLabel("ui.battle.skill.target.most_exposed_enemy", "가장 노출된 적", "Most Exposed Enemy"),
            SkillTargetRule.LowestHpAlly => AxisLabel("ui.battle.skill.target.lowest_hp_ally", "생명력이 가장 낮은 아군", "Lowest HP Ally"),
            SkillTargetRule.ProtectedAlly => AxisLabel("ui.battle.skill.target.protected_ally", "보호 대상 아군", "Protected Ally"),
            SkillTargetRule.Self => AxisLabel("ui.battle.skill.target.self", "자신", "Self"),
            SkillTargetRule.MarkedTarget => AxisLabel("ui.battle.skill.target.marked_target", "표식 대상", "Marked Target"),
            _ => AxisLabel("ui.battle.skill.target.unknown", "알 수 없는 대상", "Unknown target"),
        };
    }

    private string ResolveStatusLabel(string statusId)
    {
        var label = _contentText.GetStatusName(statusId);
        return string.IsNullOrWhiteSpace(label) || string.Equals(label, statusId, StringComparison.Ordinal)
            ? AxisLabel("ui.battle.status.unknown", "알 수 없는 상태 효과", "Unknown status")
            : label;
    }

    private string ResolveStatusDescription(string statusId)
    {
        var description = _contentText.GetStatusDescription(statusId);
        return string.IsNullOrWhiteSpace(description) || string.Equals(description, statusId, StringComparison.Ordinal)
            ? string.Empty
            : description;
    }

    private string ResolveSkillDisplayName(string skillId, string skillName)
    {
        if (IsResolvedSkillDisplayName(skillName, skillId))
        {
            return skillName;
        }

        if (string.IsNullOrWhiteSpace(skillId))
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.skill.empty", "Empty");
        }

        var localized = _contentText.GetSkillName(skillId);
        return IsResolvedSkillDisplayName(localized, skillId)
            ? localized
            : Localize(GameLocalizationTables.UICommon, "ui.common.unknown_skill", "Unknown skill");
    }

    private static bool IsResolvedSkillDisplayName(string value, string skillId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, skillId, StringComparison.Ordinal))
        {
            return false;
        }

        return !value.StartsWith("content.skill.", StringComparison.Ordinal);
    }

    private string ResolveTarget(BattleUnitReadModel unit)
    {
        return string.IsNullOrWhiteSpace(unit.TargetName)
            ? Localize(GameLocalizationTables.UIBattle, "ui.battle.target.none", "No current target")
            : unit.TargetName!;
    }

    private string FormatRange(BattleUnitReadModel unit)
    {
        if (unit.PreferredRangeMax <= 0.05f)
        {
            return Localize(GameLocalizationTables.UIBattle, "ui.battle.range.contact", "Contact");
        }

        if (unit.PreferredRangeMin <= 0.05f)
        {
            return $"0 - {unit.PreferredRangeMax:0.0}m";
        }

        return $"{unit.PreferredRangeMin:0.0} - {unit.PreferredRangeMax:0.0}m";
    }

    private string FormatDominantHand(DominantHand hand)
    {
        return hand switch
        {
            DominantHand.Left => AxisLabel("ui.battle.hand.left", "왼손", "Left"),
            DominantHand.Ambidextrous => AxisLabel("ui.battle.hand.ambidextrous", "양손잡이", "Ambidextrous"),
            _ => AxisLabel("ui.battle.hand.right", "오른손", "Right"),
        };
    }

    private string FormatSelector(string selector)
    {
        return selector switch
        {
            nameof(TargetSelector.CurrentTarget) => AxisLabel("ui.battle.targeting.selector.current", "현재 대상", "Current target"),
            nameof(TargetSelector.NearestReachableEnemy) => AxisLabel("ui.battle.targeting.selector.nearest_reachable_enemy", "도달 가능한 가장 가까운 적", "Nearest reachable enemy"),
            nameof(TargetSelector.NearestFrontlineEnemy) => AxisLabel("ui.battle.targeting.selector.nearest_frontline_enemy", "가장 가까운 전열 적", "Nearest frontline enemy"),
            nameof(TargetSelector.LowestCurrentHpEnemy) => AxisLabel("ui.battle.targeting.selector.lowest_current_hp_enemy", "현재 생명력이 가장 낮은 적", "Lowest current HP enemy"),
            nameof(TargetSelector.LowestHpPercentEnemy) => AxisLabel("ui.battle.targeting.selector.lowest_hp_percent_enemy", "생명력 비율이 가장 낮은 적", "Lowest HP percent enemy"),
            nameof(TargetSelector.LowestEhpEnemy) => AxisLabel("ui.battle.targeting.selector.lowest_ehp_enemy", "유효 생명력이 가장 낮은 적", "Lowest effective HP enemy"),
            nameof(TargetSelector.MarkedEnemy) => AxisLabel("ui.battle.targeting.selector.marked_enemy", "표식이 있는 적", "Marked enemy"),
            nameof(TargetSelector.LargestEnemyCluster) => AxisLabel("ui.battle.targeting.selector.largest_enemy_cluster", "가장 큰 적 무리", "Largest enemy cluster"),
            nameof(TargetSelector.BacklineExposedEnemy) => AxisLabel("ui.battle.targeting.selector.backline_exposed_enemy", "노출된 후열 적", "Exposed backline enemy"),
            nameof(TargetSelector.Self) => AxisLabel("ui.battle.targeting.selector.self", "자신", "Self"),
            nameof(TargetSelector.LowestCurrentHpAlly) => AxisLabel("ui.battle.targeting.selector.lowest_current_hp_ally", "현재 생명력이 가장 낮은 아군", "Lowest current HP ally"),
            nameof(TargetSelector.LowestHpPercentAlly) => AxisLabel("ui.battle.targeting.selector.lowest_hp_percent_ally", "생명력 비율이 가장 낮은 아군", "Lowest HP percent ally"),
            nameof(TargetSelector.LowestEhpAlly) => AxisLabel("ui.battle.targeting.selector.lowest_ehp_ally", "유효 생명력이 가장 낮은 아군", "Lowest effective HP ally"),
            nameof(TargetSelector.NearestInjuredAlly) => AxisLabel("ui.battle.targeting.selector.nearest_injured_ally", "가장 가까운 부상 아군", "Nearest injured ally"),
            nameof(TargetSelector.EmptyPointNearSelf) => AxisLabel("ui.battle.targeting.selector.empty_point_near_self", "자신 주변 빈 위치", "Open point near self"),
            nameof(TargetSelector.EmptyPointNearTarget) => AxisLabel("ui.battle.targeting.selector.empty_point_near_target", "대상 주변 빈 위치", "Open point near target"),
            _ => AxisLabel("ui.battle.targeting.selector.unknown", "알 수 없는 대상 규칙", "Unknown target rule"),
        };
    }

    private string FormatFallback(string fallback)
    {
        return fallback switch
        {
            nameof(TargetFallbackPolicy.Abort) => AxisLabel("ui.battle.targeting.fallback.abort", "행동 중단", "Abort action"),
            nameof(TargetFallbackPolicy.KeepCurrentIfStillValid) => AxisLabel("ui.battle.targeting.fallback.keep_current", "유효하면 현재 대상 유지", "Keep current if valid"),
            nameof(TargetFallbackPolicy.NearestReachableEnemy) => AxisLabel("ui.battle.targeting.fallback.nearest_reachable_enemy", "도달 가능한 가장 가까운 적", "Nearest reachable enemy"),
            nameof(TargetFallbackPolicy.LowestCurrentHpEnemy) => AxisLabel("ui.battle.targeting.fallback.lowest_current_hp_enemy", "현재 생명력이 가장 낮은 적", "Lowest current HP enemy"),
            nameof(TargetFallbackPolicy.Self) => AxisLabel("ui.battle.targeting.fallback.self", "자신", "Self"),
            _ => AxisLabel("ui.battle.targeting.fallback.unknown", "알 수 없는 대체 규칙", "Unknown fallback"),
        };
    }

    private string FormatPositioningIntent(PositioningIntentKind intent)
    {
        return intent switch
        {
            PositioningIntentKind.None => AxisLabel("ui.battle.positioning.intent.none", "대기", "Waiting"),
            PositioningIntentKind.Frontline => AxisLabel("ui.battle.positioning.intent.frontline", "전열 유지", "Hold frontline"),
            PositioningIntentKind.FlankLeft => AxisLabel("ui.battle.positioning.intent.flank_left", "왼쪽 측면", "Left flank"),
            PositioningIntentKind.FlankRight => AxisLabel("ui.battle.positioning.intent.flank_right", "오른쪽 측면", "Right flank"),
            PositioningIntentKind.BacklineDive => AxisLabel("ui.battle.positioning.intent.backline_dive", "후열 돌파", "Dive backline"),
            PositioningIntentKind.MaintainRange => AxisLabel("ui.battle.positioning.intent.maintain_range", "거리 유지", "Maintain range"),
            _ => AxisLabel("ui.battle.positioning.intent.unknown", "알 수 없는 포지션", "Unknown positioning"),
        };
    }

    private string FormatReevaluationReason(ReevaluationReason reason)
    {
        return reason switch
        {
            ReevaluationReason.None => AxisLabel("ui.battle.positioning.reason.none", "변경 없음", "No change"),
            ReevaluationReason.Cadence => AxisLabel("ui.battle.positioning.reason.cadence", "주기 판단", "Periodic review"),
            ReevaluationReason.TargetLost => AxisLabel("ui.battle.positioning.reason.target_lost", "대상 상실", "Target lost"),
            ReevaluationReason.SlotLost => AxisLabel("ui.battle.positioning.reason.slot_lost", "교전 위치 상실", "Engagement slot lost"),
            ReevaluationReason.TookHit => AxisLabel("ui.battle.positioning.reason.took_hit", "피격 대응", "Responding to hit"),
            ReevaluationReason.SkillReady => AxisLabel("ui.battle.positioning.reason.skill_ready", "스킬 준비", "Skill ready"),
            ReevaluationReason.MobilityReady => AxisLabel("ui.battle.positioning.reason.mobility_ready", "이동기 준비", "Mobility ready"),
            ReevaluationReason.RangeBreak => AxisLabel("ui.battle.positioning.reason.range_break", "사거리 이탈", "Range broken"),
            ReevaluationReason.TargetMoved => AxisLabel("ui.battle.positioning.reason.target_moved", "대상 이동", "Target moved"),
            _ => AxisLabel("ui.battle.positioning.reason.unknown", "알 수 없는 판단", "Unknown reason"),
        };
    }

    private string LocalizeAnchor(DeploymentAnchorId anchor)
    {
        return Localize(GameLocalizationTables.UICommon, anchor.ToLocalizationKey(), anchor.ToDisplayName());
    }

    private string AxisLabel(string key, string koFallback, string enFallback)
    {
        return Localize(
            GameLocalizationTables.UIBattle,
            key,
            string.Equals(_localization.CurrentLocale?.Identifier.Code, "ko", StringComparison.OrdinalIgnoreCase)
                ? koFallback
                : enFallback);
    }

    private string LocaleCode => _localization.CurrentLocale?.Identifier.Code ?? string.Empty;

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
