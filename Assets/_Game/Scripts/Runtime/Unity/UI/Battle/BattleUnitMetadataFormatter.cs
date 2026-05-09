using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.UI.Battle;

public sealed record BattleSkillSlotViewState(
    string SlotLabel,
    string SkillName,
    string SkillId,
    Texture2D? Icon);

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
    BattleStatusEffectSection Section);

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
    private readonly ContentTextResolver _contentText;
    private readonly BattleUnitPortraitResolver _portraitResolver = new();

    public BattleUnitMetadataFormatter(
        GameLocalizationController localization,
        ICombatContentLookup lookup)
    {
        _localization = localization;
        _contentText = new ContentTextResolver(localization, lookup);
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
            Line("ui.battle.axis.hp", "HP", "HP", $"{Mathf.Max(0f, unit.CurrentHealth):0} / {Mathf.Max(1f, unit.MaxHealth):0}", BattleStatLineCategory.Vital),
            Line("ui.battle.axis.shield", "보호막", "Shield", $"{Mathf.Max(0f, unit.Barrier):0}", BattleStatLineCategory.Defense),
            Line("ui.battle.axis.state", "상태", "State", BattleReadabilityFormatter.BuildPlayerFacingState(unit), BattleStatLineCategory.Defense),
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
            Line("ui.battle.axis.positioning", "포지션 의도", "Positioning", $"{unit.PositioningIntent} / {unit.PositioningReplanReason} #{unit.PositioningIntentRevision}", BattleStatLineCategory.Movement),
        };
    }

    private BattleStatLine Line(string key, string koFallback, string enFallback, string value, BattleStatLineCategory category)
    {
        var label = AxisLabel(key, koFallback, enFallback);
        return new BattleStatLine(label, value, label, category);
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
            $"{AxisLabel("ui.battle.axis.state", "상태", "State")}: {BattleReadabilityFormatter.BuildPlayerFacingState(unit)}",
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
            $"{AxisLabel("ui.battle.axis.state", "상태", "State")}: {BattleReadabilityFormatter.BuildPlayerFacingState(unit)}",
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
                unit.CharacterId),
            BuildSkillSlot(
                AxisLabel("ui.battle.skill.flex_active", "교체 액티브", "Flex"),
                unit.FlexActiveId,
                unit.FlexActiveName,
                unit.CharacterId),
            BuildSkillSlot(
                AxisLabel("ui.battle.skill.signature_passive", "고유 패시브", "Signature Passive"),
                unit.SignaturePassiveId,
                unit.SignaturePassiveName,
                unit.CharacterId),
            BuildSkillSlot(
                AxisLabel("ui.battle.skill.flex_passive", "교체 패시브", "Flex Passive"),
                unit.FlexPassiveId,
                unit.FlexPassiveName,
                unit.CharacterId),
        };
    }

    private BattleSkillSlotViewState BuildSkillSlot(string slotLabel, string skillId, string skillName, string characterId)
    {
        var resolvedName = ResolveSkillDisplayName(skillId, skillName);
        var icon = _portraitResolver.ResolveSkillIcon(characterId, skillId);
        return new BattleSkillSlotViewState(slotLabel, resolvedName, skillId, icon);
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
                BattleReadabilityFormatter.HumanizeToken(statusId, statusId),
                null,
                0f,
                0f,
                1,
                characterName,
                BattleStatusEffectSection.BattleScoped));
        }

        if (!unit.IsAlive || unit.CurrentHealth <= 0f)
        {
            chips.Add(BuildRuntimeChip("downed", AxisLabel("ui.battle.status.downed", "전투불능", "Downed"), characterName, 0f, 0f));
        }

        if (unit.WindupProgress > 0.01f)
        {
            chips.Add(BuildRuntimeChip(
                "windup",
                AxisLabel("ui.battle.status.windup", "시전 중", "Windup"),
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
            _portraitResolver.ResolveSkillIcon(unit.CharacterId, skillId),
            0f,
            0f,
            1,
            sourceActorName,
            BattleStatusEffectSection.Permanent));
    }

    private static BattleStatusEffectChip BuildRuntimeChip(
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
            BattleStatusEffectSection.BattleScoped);
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
        var presetName = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? profile.Id
            : profile.DisplayName;
        var priorityRules = unit.TacticRuleSummaries is { Count: > 0 }
            ? unit.TacticRuleSummaries
            : new[]
            {
                $"{AxisLabel("ui.battle.axis.targeting", "타게팅", "Targeting")}: {FormatSelector(unit.CurrentSelector)}",
                $"{AxisLabel("ui.battle.axis.fallback", "대체 규칙", "Fallback")}: {FormatFallback(unit.CurrentFallback)}",
                $"{AxisLabel("ui.battle.axis.range", "선호 사거리", "Preferred Range")}: {FormatRange(unit)}",
            };

        return new BattleTacticSummary(
            presetName,
            dials,
            _contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag),
            BattleReadabilityFormatter.HumanizeToken(unit.ArchetypeId, unit.ArchetypeId),
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
            : BuildReadableSkillFallback(skillId);
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

    private static string BuildReadableSkillFallback(string skillId)
    {
        var token = skillId.Trim();
        if (token.StartsWith("skill_", StringComparison.Ordinal))
        {
            token = token["skill_".Length..];
        }
        else if (token.StartsWith("support_", StringComparison.Ordinal))
        {
            token = token["support_".Length..];
        }

        var words = BattleReadabilityFormatter.HumanizeToken(token, skillId).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];
        }

        return string.Join(" ", words);
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

    private string FormatSelector(string selector)
    {
        return BattleReadabilityFormatter.HumanizeToken(selector, Localize(GameLocalizationTables.UIBattle, "ui.battle.targeting.current", "Current target"));
    }

    private string FormatFallback(string fallback)
    {
        return BattleReadabilityFormatter.HumanizeToken(fallback, Localize(GameLocalizationTables.UIBattle, "ui.battle.targeting.keep_current", "Keep current"));
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

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        var localized = _localization.Localize(table, key, args);
        if (!string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        if (args.Length == 0)
        {
            return fallback;
        }

        try
        {
            return string.Format(fallback, args);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}
