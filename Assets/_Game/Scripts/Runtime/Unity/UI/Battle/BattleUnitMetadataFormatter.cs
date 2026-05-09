using System;
using System.Collections.Generic;
using System.Text;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.UI.Battle;

public sealed record BattleSkillSlotViewState(
    string SlotLabel,
    string SkillName,
    string SkillId,
    Texture2D? Icon);

public sealed record BattleSelectedUnitViewState(
    bool IsVisible,
    string Header,
    string Body,
    Texture2D? Portrait = null,
    string UnitId = "",
    IReadOnlyList<BattleSkillSlotViewState>? SkillSlots = null,
    BattleUnitDetailTab ActiveTab = BattleUnitDetailTab.Overview,
    string OverviewTabLabel = "Overview",
    string SkillsTabLabel = "Skills",
    string StatusTabLabel = "Status",
    string RecordTabLabel = "Record",
    string StatusBody = "",
    string CombatRecordBody = "")
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
        string combatRecordBody = "")
    {
        if (unit == null)
        {
            return BattleSelectedUnitViewState.Hidden;
        }

        var character = _contentText.GetCharacterName(unit.CharacterId, unit.ArchetypeId);
        var role = _contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag);
        var roleFamily = _contentText.GetRoleFamilyName(unit.ClassId);
        var builder = new StringBuilder();
        builder.AppendLine($"{AxisLabel("ui.battle.axis.character", "캐릭터", "Character")}: {character}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.role_family", "역할군", "Role Family")}: {roleFamily}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.role", "역할", "Role")}: {role} / {roleFamily}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.state", "상태", "State")}: {BattleReadabilityFormatter.BuildPlayerFacingState(unit)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.target", "대상", "Target")}: {ResolveTarget(unit)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.anchor", "홈 앵커", "Home Anchor")}: {LocalizeAnchor(unit.Anchor)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.range", "선호 사거리", "Preferred Range")}: {FormatRange(unit)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.slot", "교전 슬롯", "Engage Slot")}: {unit.EngagementSlotCount} @ {unit.EngagementSlotRadius:0.0}m");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.attack_speed", "공격 속도", "Attack Speed")}: {unit.AttackSpeed:0.0}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.basic_attack_interval", "기본공격 간격", "Basic Attack Interval")}: {unit.BasicAttackCooldown:0.00}s");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.skill_haste", "스킬 가속", "Skill Haste")}: {unit.SkillHaste:0.##}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.targeting", "타게팅", "Targeting")}: {FormatSelector(unit.CurrentSelector)} / {FormatFallback(unit.CurrentFallback)}");

        if (unit.RetargetLockRemaining > 0.01f)
        {
            builder.AppendLine($"{AxisLabel("ui.battle.axis.retarget_lock", "재타게팅 잠금", "Retarget Lock")}: {unit.RetargetLockRemaining:0.0}s");
        }

        if (unit.FrontlineGuardRadius > 0.01f)
        {
            builder.AppendLine($"{AxisLabel("ui.battle.axis.guard_radius", "가드 반경", "Guard Radius")}: {unit.FrontlineGuardRadius:0.0}m");
        }

        if (unit.ClusterRadius > 0.01f)
        {
            builder.AppendLine($"{AxisLabel("ui.battle.axis.cluster_radius", "클러스터 반경", "Cluster Radius")}: {unit.ClusterRadius:0.0}m");
        }

        return new BattleSelectedUnitViewState(
            isVisible,
            $"{Localize(GameLocalizationTables.UIBattle, "ui.battle.selected.header", "Selected Unit")}: {character}",
            builder.ToString().TrimEnd(),
            _portraitResolver.Resolve(unit),
            unit.Id,
            BuildSkillSlots(unit),
            activeTab,
            AxisLabel("ui.battle.detail.tab.overview", "개요", "Overview"),
            AxisLabel("ui.battle.detail.tab.skills", "스킬", "Skills"),
            AxisLabel("ui.battle.detail.tab.status", "상태", "Status"),
            AxisLabel("ui.battle.detail.tab.record", "전투기록", "Record"),
            BuildStatusDetail(unit),
            string.IsNullOrWhiteSpace(combatRecordBody)
                ? Localize(GameLocalizationTables.UIBattle, "ui.battle.detail.record.empty", "No notable personal events yet.")
                : combatRecordBody);
    }

    private string BuildStatusDetail(BattleUnitReadModel unit)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{AxisLabel("ui.battle.axis.hp", "HP", "HP")}: {Mathf.Max(0f, unit.CurrentHealth):0} / {Mathf.Max(1f, unit.MaxHealth):0}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.state", "상태", "State")}: {BattleReadabilityFormatter.BuildPlayerFacingState(unit)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.cooldown", "쿨다운", "Cooldown")}: {Mathf.Max(0f, unit.CooldownRemaining):0.0}s");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.basic_attack_interval", "기본공격 간격", "Basic Attack Interval")}: {unit.BasicAttackCooldown:0.00}s");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.windup", "시전 진행", "Windup")}: {Mathf.RoundToInt(Mathf.Clamp01(unit.WindupProgress) * 100f)}%");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.role", "역할", "Role")}: {_contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag)}");

        var statusIds = unit.StatusIds ?? Array.Empty<string>();
        builder.AppendLine(statusIds.Count == 0
            ? $"{AxisLabel("ui.battle.axis.effects", "효과", "Effects")}: {Localize(GameLocalizationTables.UICommon, "ui.common.none", "None")}"
            : $"{AxisLabel("ui.battle.axis.effects", "효과", "Effects")}: {string.Join(", ", statusIds)}");
        return builder.ToString().TrimEnd();
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
        };
    }

    private BattleSkillSlotViewState BuildSkillSlot(string slotLabel, string skillId, string skillName, string characterId)
    {
        var resolvedName = ResolveSkillDisplayName(skillId, skillName);
        var icon = _portraitResolver.ResolveSkillIcon(characterId, skillId);
        return new BattleSkillSlotViewState(slotLabel, resolvedName, skillId, icon);
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
