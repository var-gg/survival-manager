using SM.Content.Definitions;

namespace SM.Unity;

public sealed class ContentTextResolver
{
    private readonly GameLocalizationController _localization;
    private readonly ICombatContentLookup _lookup;

    public ContentTextResolver(GameLocalizationController localization, ICombatContentLookup lookup)
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

    public string GetAugmentDescription(string augmentId)
    {
        return _lookup.TryGetAugmentDefinition(augmentId, out var augment)
            ? Localize(ContentLocalizationTables.Augments, augment.DescriptionKey, augment.LegacyDescription, augmentId)
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

    public string GetCharacterName(string characterId, string fallbackArchetypeId = "")
    {
        if (_lookup.TryGetCharacterDefinition(characterId, out var character))
        {
            var fallback = !string.IsNullOrWhiteSpace(character.LegacyDisplayName)
                ? character.LegacyDisplayName
                : !string.IsNullOrWhiteSpace(fallbackArchetypeId)
                    ? GetArchetypeName(fallbackArchetypeId)
                    : characterId;
            return Localize(ContentLocalizationTables.Characters, character.NameKey, fallback, characterId);
        }

        return !string.IsNullOrWhiteSpace(fallbackArchetypeId)
            ? GetArchetypeName(fallbackArchetypeId)
            : characterId;
    }

    public string GetRoleName(string roleInstructionId, string fallbackRoleTag = "")
    {
        if (_lookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction))
        {
            var localeCode = _localization.CurrentLocale?.Identifier.Code;
            var fallback = !string.IsNullOrWhiteSpace(roleInstruction.LegacyDisplayName)
                ? roleInstruction.LegacyDisplayName
                : RoleGlossary.GetLocalizedRoleTagFallback(roleInstruction.RoleTag, localeCode);
            return Localize(ContentLocalizationTables.Roles, roleInstruction.NameKey, fallback, roleInstructionId);
        }

        var roleTag = string.IsNullOrWhiteSpace(fallbackRoleTag) ? roleInstructionId : fallbackRoleTag;
        return RoleGlossary.GetLocalizedRoleTagFallback(roleTag, _localization.CurrentLocale?.Identifier.Code);
    }

    public string GetPassiveBoardName(string boardId)
    {
        return _lookup.TryGetPassiveBoardDefinition(boardId, out var board)
            ? Localize(ContentLocalizationTables.Passives, board.NameKey, board.LegacyDisplayName, boardId)
            : boardId;
    }

    public string GetPassiveBoardDescription(string boardId)
    {
        return _lookup.TryGetPassiveBoardDefinition(boardId, out var board)
            ? Localize(ContentLocalizationTables.Passives, board.DescriptionKey, board.LegacyDisplayName, boardId)
            : boardId;
    }

    public string GetPassiveNodeName(string nodeId)
    {
        return _lookup.TryGetPassiveNodeDefinition(nodeId, out var node)
            ? Localize(ContentLocalizationTables.Passives, node.NameKey, node.LegacyDisplayName, nodeId)
            : nodeId;
    }

    public string GetPassiveNodeDescription(string nodeId)
    {
        return _lookup.TryGetPassiveNodeDefinition(nodeId, out var node)
            ? Localize(ContentLocalizationTables.Passives, node.DescriptionKey, node.LegacyDescription, nodeId)
            : nodeId;
    }

    public string GetTeamTacticName(string teamTacticId)
    {
        return _lookup.TryGetTeamTacticDefinition(teamTacticId, out var teamTactic)
            ? Localize(ContentLocalizationTables.TeamTactics, teamTactic.NameKey, teamTactic.LegacyDisplayName, teamTacticId)
            : teamTacticId;
    }

    public string GetSynergyName(string synergyId)
    {
        return _lookup.TryGetSynergyDefinition(synergyId, out var synergy)
            ? Localize(ContentLocalizationTables.Synergies, synergy.NameKey, synergy.LegacyDisplayName, synergyId)
            : synergyId;
    }

    public string GetRoleFamilyName(string classId)
    {
        var roleFamilyTag = RoleGlossary.GetRoleFamilyTagOrDefault(classId);
        if (string.Equals(roleFamilyTag, classId, System.StringComparison.Ordinal))
        {
            return GetClassName(classId);
        }

        var fallback = RoleGlossary.GetLocalizedRoleFamilyFallback(roleFamilyTag, _localization.CurrentLocale?.Identifier.Code);
        return Localize(ContentLocalizationTables.Roles, ContentLocalizationTables.BuildRoleNameKey(roleFamilyTag), fallback, roleFamilyTag);
    }

    public string GetTraitName(string archetypeId, string traitId)
    {
        return _lookup.TryGetTraitEntry(archetypeId, traitId, out var trait)
            ? Localize(ContentLocalizationTables.Traits, trait.NameKey, trait.LegacyDisplayName, traitId)
            : traitId;
    }

    public string GetCampaignChapterName(string chapterId)
    {
        return _lookup.TryGetCampaignChapterDefinition(chapterId, out var chapter)
            ? Localize(ContentLocalizationTables.Campaign, chapter.NameKey, chapter.LegacyDisplayName, chapterId)
            : chapterId;
    }

    public string GetCampaignChapterDescription(string chapterId)
    {
        return _lookup.TryGetCampaignChapterDefinition(chapterId, out var chapter)
            ? Localize(ContentLocalizationTables.Campaign, chapter.DescriptionKey, chapter.LegacyDescription, chapterId)
            : chapterId;
    }

    public string GetExpeditionSiteName(string siteId)
    {
        return _lookup.TryGetExpeditionSiteDefinition(siteId, out var site)
            ? Localize(ContentLocalizationTables.Campaign, site.NameKey, site.LegacyDisplayName, siteId)
            : siteId;
    }

    public string GetExpeditionSiteDescription(string siteId)
    {
        return _lookup.TryGetExpeditionSiteDefinition(siteId, out var site)
            ? Localize(ContentLocalizationTables.Campaign, site.DescriptionKey, site.LegacyDescription, siteId)
            : siteId;
    }

    private string Localize(string table, string key, string fallback, string finalFallback)
    {
        return _localization.LocalizePlayerFacingContent(
            table,
            key,
            string.IsNullOrWhiteSpace(fallback) ? finalFallback : fallback);
    }
}
