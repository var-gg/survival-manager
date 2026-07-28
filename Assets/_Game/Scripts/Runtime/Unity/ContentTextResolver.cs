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
            ? Localize(ContentLocalizationTables.Items, item.NameKey, item.LegacyDisplayName, Unknown("ui.common.unknown_item", "Unknown item"))
            : Unknown("ui.common.unknown_item", "Unknown item");
    }

    public string GetAffixName(string affixId)
    {
        return _lookup.TryGetAffixDefinition(affixId, out var affix)
            ? Localize(ContentLocalizationTables.Affixes, affix.NameKey, affix.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetAugmentName(string augmentId)
    {
        return _lookup.TryGetAugmentDefinition(augmentId, out var augment)
            ? Localize(ContentLocalizationTables.Augments, augment.NameKey, augment.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetAugmentDescription(string augmentId)
    {
        return _lookup.TryGetAugmentDefinition(augmentId, out var augment)
            ? Localize(ContentLocalizationTables.Augments, augment.DescriptionKey, augment.LegacyDescription, DescriptionUnavailable())
            : DescriptionUnavailable();
    }

    public string GetSkillName(string skillId)
    {
        return _lookup.TryGetSkillDefinition(skillId, out var skill)
            ? Localize(ContentLocalizationTables.Skills, skill.NameKey, skill.LegacyDisplayName, Unknown("ui.common.unknown_skill", "Unknown skill"))
            : Unknown("ui.common.unknown_skill", "Unknown skill");
    }

    public string GetSkillDescription(string skillId)
    {
        return _lookup.TryGetSkillDefinition(skillId, out var skill)
            ? Localize(ContentLocalizationTables.Skills, skill.DescriptionKey, string.Empty, DescriptionUnavailable())
            : DescriptionUnavailable();
    }

    public string GetStatusName(string statusId)
    {
        return Localize(
            ContentLocalizationTables.Status,
            ContentLocalizationTables.BuildStatusNameKey(statusId),
            string.Empty,
            UnknownContent());
    }

    public string GetStatusDescription(string statusId)
    {
        return Localize(
            ContentLocalizationTables.Status,
            ContentLocalizationTables.BuildStatusDescriptionKey(statusId),
            string.Empty,
            DescriptionUnavailable());
    }

    // ADR-0028 P2b — 정치 세력/서약 표시명. StringTable(Content_Factions/Warrants) 우선, 비면 WarrantDisplayDefaults(label layer).
    public string GetFactionName(string factionId)
    {
        return Localize(
            ContentLocalizationTables.Factions,
            ContentLocalizationTables.BuildFactionNameKey(factionId),
            WarrantDisplayDefaults.FactionName(factionId),
            UnknownContent());
    }

    public string GetWarrantName(string warrantId)
    {
        return Localize(
            ContentLocalizationTables.Warrants,
            ContentLocalizationTables.BuildWarrantNameKey(warrantId),
            WarrantDisplayDefaults.WarrantName(warrantId),
            UnknownContent());
    }

    public string GetArchetypeName(string archetypeId)
    {
        return _lookup.TryGetArchetype(archetypeId, out var archetype)
            ? Localize(ContentLocalizationTables.Archetypes, archetype.NameKey, archetype.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetRaceName(string raceId)
    {
        return _lookup.TryGetRaceDefinition(raceId, out var race)
            ? Localize(ContentLocalizationTables.Races, race.NameKey, race.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetClassName(string classId)
    {
        return _lookup.TryGetClassDefinition(classId, out var @class)
            ? Localize(ContentLocalizationTables.Classes, @class.NameKey, @class.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetCharacterName(string characterId, string fallbackArchetypeId = "")
    {
        if (_lookup.TryGetCharacterDefinition(characterId, out var character))
        {
            var fallback = !string.IsNullOrWhiteSpace(character.LegacyDisplayName)
                ? character.LegacyDisplayName
                : !string.IsNullOrWhiteSpace(fallbackArchetypeId)
                    ? GetArchetypeName(fallbackArchetypeId)
                    : UnknownContent();
            return Localize(ContentLocalizationTables.Characters, character.NameKey, fallback, UnknownContent());
        }

        if (BattleP09AppearanceRoster.TryGetDefinedDisplayName(characterId, out var p09DisplayName))
        {
            return p09DisplayName;
        }

        return !string.IsNullOrWhiteSpace(fallbackArchetypeId)
            ? GetArchetypeName(fallbackArchetypeId)
            : UnknownContent();
    }

    public string GetCharacterDescription(string characterId, string fallbackArchetypeId = "")
    {
        if (_lookup.TryGetCharacterDefinition(characterId, out var character))
        {
            var fallback = !string.IsNullOrWhiteSpace(character.LegacyDescription)
                ? character.LegacyDescription
                : !string.IsNullOrWhiteSpace(fallbackArchetypeId)
                    ? GetArchetypeName(fallbackArchetypeId)
                    : DescriptionUnavailable();
            return Localize(ContentLocalizationTables.Characters, character.DescriptionKey, fallback, DescriptionUnavailable());
        }

        return !string.IsNullOrWhiteSpace(fallbackArchetypeId)
            ? GetArchetypeName(fallbackArchetypeId)
            : DescriptionUnavailable();
    }

    public string GetRoleName(string roleInstructionId, string fallbackRoleTag = "")
    {
        if (_lookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction))
        {
            var localeCode = _localization.CurrentLocale?.Identifier.Code;
            var fallback = !string.IsNullOrWhiteSpace(roleInstruction.LegacyDisplayName)
                ? roleInstruction.LegacyDisplayName
                : RoleGlossary.GetLocalizedRoleTagFallback(roleInstruction.RoleTag, localeCode);
            return Localize(ContentLocalizationTables.Roles, roleInstruction.NameKey, fallback, UnknownContent());
        }

        var roleTag = string.IsNullOrWhiteSpace(fallbackRoleTag) ? roleInstructionId : fallbackRoleTag;
        return RoleGlossary.GetLocalizedRoleTagFallback(roleTag, _localization.CurrentLocale?.Identifier.Code);
    }

    public string GetPassiveBoardName(string boardId)
    {
        return _lookup.TryGetPassiveBoardDefinition(boardId, out var board)
            ? Localize(ContentLocalizationTables.Passives, board.NameKey, board.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetPassiveBoardDescription(string boardId)
    {
        return _lookup.TryGetPassiveBoardDefinition(boardId, out var board)
            ? Localize(ContentLocalizationTables.Passives, board.DescriptionKey, board.LegacyDisplayName, DescriptionUnavailable())
            : DescriptionUnavailable();
    }

    public string GetPassiveNodeName(string nodeId)
    {
        return _lookup.TryGetPassiveNodeDefinition(nodeId, out var node)
            ? Localize(ContentLocalizationTables.Passives, node.NameKey, node.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetPassiveNodeDescription(string nodeId)
    {
        return _lookup.TryGetPassiveNodeDefinition(nodeId, out var node)
            ? Localize(ContentLocalizationTables.Passives, node.DescriptionKey, node.LegacyDescription, DescriptionUnavailable())
            : DescriptionUnavailable();
    }

    public string GetTeamTacticName(string teamTacticId)
    {
        return _lookup.TryGetTeamTacticDefinition(teamTacticId, out var teamTactic)
            ? Localize(ContentLocalizationTables.TeamTactics, teamTactic.NameKey, teamTactic.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetSynergyName(string synergyId)
    {
        return _lookup.TryGetSynergyDefinition(synergyId, out var synergy)
            ? Localize(ContentLocalizationTables.Synergies, synergy.NameKey, synergy.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetSynergyDescription(string synergyId)
    {
        return _lookup.TryGetSynergyDefinition(synergyId, out var synergy)
            ? Localize(ContentLocalizationTables.Synergies, synergy.DescriptionKey, string.Empty, DescriptionUnavailable())
            : DescriptionUnavailable();
    }

    public string GetRoleFamilyName(string classId)
    {
        var roleFamilyTag = RoleGlossary.GetRoleFamilyTagOrDefault(classId);
        if (string.Equals(roleFamilyTag, classId, System.StringComparison.Ordinal))
        {
            return GetClassName(classId);
        }

        var fallback = RoleGlossary.GetLocalizedRoleFamilyFallback(roleFamilyTag, _localization.CurrentLocale?.Identifier.Code);
        return Localize(ContentLocalizationTables.Roles, ContentLocalizationTables.BuildRoleNameKey(roleFamilyTag), fallback, UnknownContent());
    }

    public string GetTraitName(string archetypeId, string traitId)
    {
        return _lookup.TryGetTraitEntry(archetypeId, traitId, out var trait)
            ? Localize(ContentLocalizationTables.Traits, trait.NameKey, trait.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetCampaignChapterName(string chapterId)
    {
        return _lookup.TryGetCampaignChapterDefinition(chapterId, out var chapter)
            ? Localize(ContentLocalizationTables.Campaign, chapter.NameKey, chapter.LegacyDisplayName, UnknownContent())
            : UnknownContent();
    }

    public string GetCampaignChapterDescription(string chapterId)
    {
        return _lookup.TryGetCampaignChapterDefinition(chapterId, out var chapter)
            ? Localize(ContentLocalizationTables.Campaign, chapter.DescriptionKey, chapter.LegacyDescription, DescriptionUnavailable())
            : DescriptionUnavailable();
    }

    public string GetExpeditionSiteName(string siteId)
    {
        return _lookup.TryGetExpeditionSiteDefinition(siteId, out var site)
            ? Localize(ContentLocalizationTables.Campaign, site.NameKey, site.LegacyDisplayName, Unknown("ui.common.unknown_site", "Unknown site"))
            : Unknown("ui.common.unknown_site", "Unknown site");
    }

    public string GetExpeditionSiteDescription(string siteId)
    {
        return _lookup.TryGetExpeditionSiteDefinition(siteId, out var site)
            ? Localize(ContentLocalizationTables.Campaign, site.DescriptionKey, site.LegacyDescription, DescriptionUnavailable())
            : DescriptionUnavailable();
    }

    public string GetEncounterName(string encounterId)
    {
        return _lookup.TryGetEncounterDefinition(encounterId, out var encounter)
            ? Localize(ContentLocalizationTables.Encounters, encounter.NameKey, encounter.LegacyDisplayName, Unknown("ui.common.unknown_encounter", "Unknown encounter"))
            : Unknown("ui.common.unknown_encounter", "Unknown encounter");
    }

    public string GetRewardSourceName(string rewardSourceId)
    {
        return _lookup.Snapshot.RewardSources is { } rewardSources
               && rewardSources.TryGetValue(rewardSourceId, out var rewardSource)
            ? Localize(
                ContentLocalizationTables.Rewards,
                ContentLocalizationTables.BuildRewardSourceNameKey(rewardSourceId),
                rewardSource.Name,
                Unknown("ui.common.unknown_reward_source", "Unknown reward source"))
            : Unknown("ui.common.unknown_reward_source", "Unknown reward source");
    }

    internal string LocalizeUi(
        string table,
        string key,
        string fallback,
        params object[] arguments)
        => _localization.LocalizeOrFallback(table, key, fallback, arguments);

    private string Unknown(string key, string fallback)
        => _localization.LocalizeOrFallback(
            GameLocalizationTables.UICommon,
            key,
            fallback);

    private string UnknownContent()
        => Unknown("ui.common.unknown_content", "Unknown content");

    private string DescriptionUnavailable()
        => Unknown("ui.common.description_unavailable", "Description unavailable");

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
            ? "Unknown content"
            : finalFallback;
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

}
