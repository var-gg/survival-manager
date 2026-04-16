using System;
using System.Text;
using SM.Combat.Model;

namespace SM.Unity.UI.Battle;

public sealed record BattleSelectedUnitViewState(
    bool IsVisible,
    string Header,
    string Body)
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

    public BattleSelectedUnitViewState BuildSelectedUnitPanel(BattleUnitReadModel? unit)
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
            true,
            $"{Localize(GameLocalizationTables.UIBattle, "ui.battle.selected.header", "Selected Unit")}: {character}",
            builder.ToString().TrimEnd());
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
