using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;

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

    public string GetSkillDescription(string skillId)
    {
        return _lookup.TryGetSkillDefinition(skillId, out var skill)
            ? Localize(ContentLocalizationTables.Skills, skill.DescriptionKey, string.Empty, skillId)
            : skillId;
    }

    public string GetStatusName(string statusId)
    {
        return Localize(ContentLocalizationTables.Status, ContentLocalizationTables.BuildStatusNameKey(statusId), string.Empty, statusId);
    }

    public string GetStatusDescription(string statusId)
    {
        return Localize(ContentLocalizationTables.Status, ContentLocalizationTables.BuildStatusDescriptionKey(statusId), string.Empty, statusId);
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

        if (BattleP09AppearanceRoster.TryGetDefinedDisplayName(characterId, out var p09DisplayName))
        {
            return p09DisplayName;
        }

        return !string.IsNullOrWhiteSpace(fallbackArchetypeId)
            ? GetArchetypeName(fallbackArchetypeId)
            : characterId;
    }

    public string GetCharacterDescription(string characterId, string fallbackArchetypeId = "")
    {
        if (_lookup.TryGetCharacterDefinition(characterId, out var character))
        {
            var fallback = !string.IsNullOrWhiteSpace(character.LegacyDescription)
                ? character.LegacyDescription
                : !string.IsNullOrWhiteSpace(fallbackArchetypeId)
                    ? GetArchetypeName(fallbackArchetypeId)
                    : characterId;
            return Localize(ContentLocalizationTables.Characters, character.DescriptionKey, fallback, characterId);
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

    public string GetSynergyDescription(string synergyId)
    {
        return _lookup.TryGetSynergyDefinition(synergyId, out var synergy)
            ? Localize(ContentLocalizationTables.Synergies, synergy.DescriptionKey, string.Empty, synergyId)
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

    public string GetEncounterName(string encounterId)
    {
        return _lookup.TryGetEncounterDefinition(encounterId, out var encounter)
            ? Localize(ContentLocalizationTables.Encounters, encounter.NameKey, encounter.LegacyDisplayName, encounterId)
            : encounterId;
    }

    private string Localize(string table, string key, string fallback, string finalFallback)
    {
        var safeFallback = SelectSafeFallback(fallback, finalFallback);
        var result = _localization.LocalizePlayerFacingContent(table, key, safeFallback);
        // Localization init 미완료 또는 key 미정의 시 raw key (예 "content.archetype.warden.name") 반환 케이스 방어.
        if (string.IsNullOrEmpty(result)
            || result == key
            || LooksLikeRawLocalizationKey(result))
        {
            return safeFallback;
        }
        return result;
    }

    private static string SelectSafeFallback(string fallback, string finalFallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback) && !LooksLikeRawLocalizationKey(fallback))
        {
            return fallback;
        }

        return string.IsNullOrWhiteSpace(finalFallback)
            ? "unknown"
            : HumanizeIdentifier(finalFallback);
    }

    private static bool LooksLikeRawLocalizationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("content.", System.StringComparison.Ordinal)
               || trimmed.StartsWith("ui.", System.StringComparison.Ordinal)
               || trimmed.StartsWith("No translation found", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var token = value.Trim();
        if (token.Contains('.', System.StringComparison.Ordinal))
        {
            var parts = token.Split(new[] { '.' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                if (!string.Equals(parts[i], "name", System.StringComparison.Ordinal)
                    && !string.Equals(parts[i], "desc", System.StringComparison.Ordinal))
                {
                    token = parts[i];
                    break;
                }
            }
        }

        foreach (var prefix in new[] { "item_", "augment_", "reward_source_", "site_" })
        {
            if (token.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                token = token[prefix.Length..];
                break;
            }
        }

        var words = token.Replace('_', ' ').Replace('-', ' ').Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? value
            : string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word[1..] : string.Empty)));
    }
}
