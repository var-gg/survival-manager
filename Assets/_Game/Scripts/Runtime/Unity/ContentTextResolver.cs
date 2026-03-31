using SM.Content.Definitions;

namespace SM.Unity;

public sealed class ContentTextResolver
{
    private readonly GameLocalizationController _localization;
    private readonly RuntimeCombatContentLookup _lookup;

    public ContentTextResolver(GameLocalizationController localization, RuntimeCombatContentLookup lookup)
    {
        _localization = localization;
        _lookup = lookup;
    }

    public string GetItemName(string itemId)
    {
        return _lookup.TryGetItemDefinition(itemId, out var item)
            ? Localize(ContentLocalizationTables.Items, item.NameKey, item.LegacyDisplayName, itemId)
            : itemId;
    }

    public string GetAffixName(string affixId)
    {
        return _lookup.TryGetAffixDefinition(affixId, out var affix)
            ? Localize(ContentLocalizationTables.Affixes, affix.NameKey, affix.LegacyDisplayName, affixId)
            : affixId;
    }

    public string GetAugmentName(string augmentId)
    {
        return _lookup.TryGetAugmentDefinition(augmentId, out var augment)
            ? Localize(ContentLocalizationTables.Augments, augment.NameKey, augment.LegacyDisplayName, augmentId)
            : augmentId;
    }

    public string GetSkillName(string skillId)
    {
        return _lookup.TryGetSkillDefinition(skillId, out var skill)
            ? Localize(ContentLocalizationTables.Skills, skill.NameKey, skill.LegacyDisplayName, skillId)
            : skillId;
    }

    public string GetArchetypeName(string archetypeId)
    {
        return _lookup.TryGetArchetype(archetypeId, out var archetype)
            ? Localize(ContentLocalizationTables.Archetypes, archetype.NameKey, archetype.LegacyDisplayName, archetypeId)
            : archetypeId;
    }

    public string GetRaceName(string raceId)
    {
        return _lookup.TryGetRaceDefinition(raceId, out var race)
            ? Localize(ContentLocalizationTables.Races, race.NameKey, race.LegacyDisplayName, raceId)
            : raceId;
    }

    public string GetClassName(string classId)
    {
        return _lookup.TryGetClassDefinition(classId, out var @class)
            ? Localize(ContentLocalizationTables.Classes, @class.NameKey, @class.LegacyDisplayName, classId)
            : classId;
    }

    public string GetTraitName(string archetypeId, string traitId)
    {
        return _lookup.TryGetTraitEntry(archetypeId, traitId, out var trait)
            ? Localize(ContentLocalizationTables.Traits, trait.NameKey, trait.LegacyDisplayName, traitId)
            : traitId;
    }

    private string Localize(string table, string key, string fallback, string finalFallback)
    {
        return _localization.LocalizePlayerFacingContent(
            table,
            key,
            string.IsNullOrWhiteSpace(fallback) ? finalFallback : fallback);
    }
}
