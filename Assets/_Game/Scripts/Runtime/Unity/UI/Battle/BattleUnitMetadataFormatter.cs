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
        var characterName = _contentText.GetCharacterName(unit.CharacterId, unit.ArchetypeId);
        var archetypeName = _contentText.GetArchetypeName(unit.ArchetypeId);
        var header = string.Equals(characterName, archetypeName, System.StringComparison.Ordinal)
            ? characterName
            : $"{characterName} ({archetypeName})";

        return new BattleUnitOverheadText(
            header,
            BuildAxisSummary(unit));
    }

    public BattleSelectedUnitViewState BuildSelectedUnitPanel(BattleUnitReadModel? unit)
    {
        if (unit == null)
        {
            return BattleSelectedUnitViewState.Hidden;
        }

        var overhead = BuildOverhead(unit);
        var builder = new StringBuilder();
        builder.AppendLine($"{AxisLabel("ui.battle.axis.character", "캐릭터", "Character")}: {_contentText.GetCharacterName(unit.CharacterId, unit.ArchetypeId)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.archetype", "전투 원형", "Archetype")}: {_contentText.GetArchetypeName(unit.ArchetypeId)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.race", "종족", "Race")}: {_contentText.GetRaceName(unit.RaceId)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.class", "직업", "Class")}: {_contentText.GetClassName(unit.ClassId)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.role", "역할", "Role")}: {_contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.role_family", "역할군", "Role Family")}: {_contentText.GetRoleFamilyName(unit.ClassId)}");
        builder.AppendLine($"{AxisLabel("ui.battle.axis.anchor", "배치", "Anchor")}: {LocalizeAnchor(unit.Anchor)}");

        return new BattleSelectedUnitViewState(
            true,
            $"{Localize(GameLocalizationTables.UIBattle, "ui.battle.selected.header", "Selected Unit")}: {overhead.Header}",
            builder.ToString().TrimEnd());
    }

    private string BuildAxisSummary(BattleUnitReadModel unit)
    {
        return string.Join(" / ", new[]
        {
            _contentText.GetRaceName(unit.RaceId),
            _contentText.GetClassName(unit.ClassId),
            _contentText.GetRoleName(unit.RoleInstructionId, unit.RoleTag),
        });
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
            string.Equals(_localization.CurrentLocale?.Identifier.Code, "ko", System.StringComparison.OrdinalIgnoreCase)
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
