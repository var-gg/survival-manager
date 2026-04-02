using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;

namespace SM.Editor.Validation;

internal static class ContentDefinitionCatalogRules
{
    internal static void ValidateLaunchFloorCatalogs(ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues)
    {
        var chapters = catalog.OfType<CampaignChapterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var sites = catalog.OfType<ExpeditionSiteDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var encounters = catalog.OfType<EncounterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var squads = catalog.OfType<EnemySquadTemplateDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var overlays = catalog.OfType<BossOverlayDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var statuses = catalog.OfType<StatusFamilyDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var cleanseProfiles = catalog.OfType<CleanseProfileDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var controlRules = catalog.OfType<ControlDiminishingRuleDefinition>().ToList();
        var rewardSources = catalog.OfType<RewardSourceDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var dropTables = catalog.OfType<DropTableDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var lootBundles = catalog.OfType<LootBundleDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var traitTokens = catalog.OfType<TraitTokenDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var skills = catalog.OfType<SkillDefinitionAsset>().ToList();
        var items = catalog.OfType<ItemBaseDefinition>().ToList();
        var synergies = catalog.OfType<SynergyDefinition>().ToList();
        var archetypeIds = catalog.OfType<UnitArchetypeDefinition>().Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);

        ValidateCampaignCatalog(catalog, chapters, sites, encounters, squads, overlays, rewardSources, archetypeIds, issues);
        ValidateStatusCatalog(catalog, statuses, cleanseProfiles, controlRules, skills, issues);
        ValidateRewardCatalog(catalog, rewardSources, dropTables, lootBundles, traitTokens, issues);
        ValidateSkillCatalog(catalog, skills, statuses.Keys.ToHashSet(StringComparer.Ordinal), cleanseProfiles.Keys.ToHashSet(StringComparer.Ordinal), issues);
        ValidateItemCatalog(catalog, items, issues);
        ValidateFactionIsolation(catalog, sites.Values, encounters.Values, squads.Values, synergies, issues);
    }

    private static void ValidateCampaignCatalog(
        ValidationAssetCatalog catalog,
        IReadOnlyDictionary<string, CampaignChapterDefinition> chapters,
        IReadOnlyDictionary<string, ExpeditionSiteDefinition> sites,
        IReadOnlyDictionary<string, EncounterDefinition> encounters,
        IReadOnlyDictionary<string, EnemySquadTemplateDefinition> squads,
        IReadOnlyDictionary<string, BossOverlayDefinition> overlays,
        IReadOnlyDictionary<string, RewardSourceDefinition> rewardSources,
        IReadOnlyCollection<string> archetypeIds,
        ICollection<ContentValidationIssue> issues)
    {
        if (chapters.Count != 3)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.chapter_count", $"Story chapters must be locked to 3. Found {chapters.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (sites.Count != 6)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.site_count", $"Expedition sites must be locked to 6. Found {sites.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (encounters.Count != 24)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.encounter_count", $"Encounter catalog must be locked to 24 battle encounters. Found {encounters.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        foreach (var chapter in chapters.Values)
        {
            var assetPath = catalog.GetPath(chapter);
            if (chapter.SiteIds.Distinct(StringComparer.Ordinal).Count() != 2)
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.chapter_site_count", "Campaign chapter must own exactly 2 expedition sites.", assetPath);
            }

            foreach (var siteId in chapter.SiteIds)
            {
                if (!sites.ContainsKey(siteId))
                {
                    ContentValidationIssueFactory.AddError(issues, "campaign.chapter_site_ref", $"Campaign chapter references missing site '{siteId}'.", assetPath);
                }
            }
        }

        foreach (var site in sites.Values)
        {
            var assetPath = catalog.GetPath(site);
            if (!chapters.ContainsKey(site.ChapterId))
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.site_chapter_ref", $"Expedition site references missing chapter '{site.ChapterId}'.", assetPath);
            }

            if (site.EncounterIds.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.site_encounter_count", "Expedition site must own exactly 4 battle encounters (2 skirmish / 1 elite / 1 boss).", assetPath);
            }

            if (string.IsNullOrWhiteSpace(site.ExtractRewardSourceId) || !rewardSources.ContainsKey(site.ExtractRewardSourceId))
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.site_extract_source", "Expedition site is missing a valid extract reward source.", assetPath);
            }

            foreach (var encounterId in site.EncounterIds)
            {
                if (!encounters.ContainsKey(encounterId))
                {
                    ContentValidationIssueFactory.AddError(issues, "campaign.site_encounter_ref", $"Expedition site references missing encounter '{encounterId}'.", assetPath);
                }
            }
        }

        foreach (var encounter in encounters.Values)
        {
            var assetPath = catalog.GetPath(encounter);
            if (!sites.ContainsKey(encounter.SiteId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.site_ref", $"Encounter references missing site '{encounter.SiteId}'.", assetPath);
            }

            if (!squads.ContainsKey(encounter.EnemySquadTemplateId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.squad_ref", $"Encounter references missing enemy squad '{encounter.EnemySquadTemplateId}'.", assetPath);
            }

            if (string.IsNullOrWhiteSpace(encounter.RewardSourceId) || !rewardSources.ContainsKey(encounter.RewardSourceId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.reward_source_ref", "Encounter is missing a valid reward source.", assetPath);
            }

            if (string.IsNullOrWhiteSpace(encounter.FactionId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.faction_id", "Encounter must keep a faction/allegiance id.", assetPath);
            }

            if (encounter.ThreatCost is < 1 or > 3 || encounter.ThreatSkulls is < 1 or > 3)
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.threat_budget", "Encounter threat cost and skulls must stay within the 1/2/3 launch-floor grammar.", assetPath);
            }

            if (string.IsNullOrWhiteSpace(encounter.DifficultyBand))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.difficulty_band", "Encounter must define a difficulty band.", assetPath);
            }

            if (encounter.RewardDropTags.Count == 0)
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.reward_drop_tags", "Encounter must define reward/drop tags.", assetPath);
            }

            if (encounter.Kind == EncounterKindValue.Boss)
            {
                if (string.IsNullOrWhiteSpace(encounter.BossOverlayId) || !overlays.ContainsKey(encounter.BossOverlayId))
                {
                    ContentValidationIssueFactory.AddError(issues, "encounter.boss_overlay_ref", "Boss encounter must reference a valid boss overlay.", assetPath);
                }
            }
            else if (!string.IsNullOrWhiteSpace(encounter.BossOverlayId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.non_boss_overlay", "Only boss encounters may reference a boss overlay.", assetPath);
            }
        }

        foreach (var squad in squads.Values)
        {
            var assetPath = catalog.GetPath(squad);
            if (string.IsNullOrWhiteSpace(squad.FactionId))
            {
                ContentValidationIssueFactory.AddError(issues, "enemy_squad.faction_id", "Enemy squad must keep a faction/allegiance id.", assetPath);
            }

            if (squad.Members.Count == 0)
            {
                ContentValidationIssueFactory.AddError(issues, "enemy_squad.member_count", "Enemy squad must define at least one member.", assetPath);
                continue;
            }

            var captainCount = squad.Members.Count(member => member.Role == EnemySquadMemberRoleValue.Captain);
            var escortCount = squad.Members.Count(member => member.Role == EnemySquadMemberRoleValue.Escort);
            if (captainCount > 0 && (captainCount != 1 || escortCount < 2))
            {
                ContentValidationIssueFactory.AddError(issues, "enemy_squad.boss_structure", "Boss squads must follow BossCaptain + 2~3 Escorts.", assetPath);
            }

            foreach (var member in squad.Members)
            {
                if (!archetypeIds.Contains(member.ArchetypeId))
                {
                    ContentValidationIssueFactory.AddError(issues, "enemy_squad.member_archetype_ref", $"Enemy squad member references missing archetype '{member.ArchetypeId}'.", assetPath);
                }
            }
        }
    }

    private static void ValidateStatusCatalog(
        ValidationAssetCatalog catalog,
        IReadOnlyDictionary<string, StatusFamilyDefinition> statuses,
        IReadOnlyDictionary<string, CleanseProfileDefinition> cleanseProfiles,
        IReadOnlyList<ControlDiminishingRuleDefinition> controlRules,
        IReadOnlyList<SkillDefinitionAsset> skills,
        ICollection<ContentValidationIssue> issues)
    {
        var statusIds = statuses.Keys.ToHashSet(StringComparer.Ordinal);
        if (!ContentValidationPolicyCatalog.RequiredLaunchStatusIds.SetEquals(statusIds))
        {
            ContentValidationIssueFactory.AddError(issues, "status.catalog_floor", $"Launch-floor status catalog must match [{string.Join(", ", ContentValidationPolicyCatalog.RequiredLaunchStatusIds.OrderBy(id => id, StringComparer.Ordinal))}].", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (!ContentValidationPolicyCatalog.RequiredCleanseProfileIds.SetEquals(cleanseProfiles.Keys))
        {
            ContentValidationIssueFactory.AddError(issues, "status.cleanse_floor", $"Cleanse profile catalog must match [{string.Join(", ", ContentValidationPolicyCatalog.RequiredCleanseProfileIds.OrderBy(id => id, StringComparer.Ordinal))}].", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (controlRules.Count != 1)
        {
            ContentValidationIssueFactory.AddError(issues, "status.dr_rule_count", $"Launch-floor control diminishing rules must be locked to 1. Found {controlRules.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }
        else
        {
            var rule = controlRules[0];
            var assetPath = catalog.GetPath(rule);
            if (Math.Abs(rule.WindowSeconds - 1.5f) > 0.001f || Math.Abs(rule.ControlResistMultiplier - 0.5f) > 0.001f)
            {
                ContentValidationIssueFactory.AddError(issues, "status.dr_values", "Launch-floor DR must stay at 1.5s window and 50% control resist.", assetPath);
            }
        }

        foreach (var profile in cleanseProfiles.Values)
        {
            var assetPath = catalog.GetPath(profile);
            foreach (var statusId in profile.RemovesStatusIds)
            {
                if (!statusIds.Contains(statusId))
                {
                    ContentValidationIssueFactory.AddError(issues, "status.cleanse_target_ref", $"Cleanse profile references missing status '{statusId}'.", assetPath);
                }
            }
        }

        foreach (var skill in skills)
        {
            var assetPath = catalog.GetPath(skill);
            foreach (var status in skill.AppliedStatuses.Where(status => status != null))
            {
                if (!statusIds.Contains(status.StatusId))
                {
                    ContentValidationIssueFactory.AddError(issues, "status.skill_status_ref", $"Skill '{skill.Id}' references missing status '{status.StatusId}'.", assetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId) && !cleanseProfiles.ContainsKey(skill.CleanseProfileId))
            {
                ContentValidationIssueFactory.AddError(issues, "status.skill_cleanse_ref", $"Skill '{skill.Id}' references missing cleanse profile '{skill.CleanseProfileId}'.", assetPath);
            }
        }
    }

    private static void ValidateRewardCatalog(
        ValidationAssetCatalog catalog,
        IReadOnlyDictionary<string, RewardSourceDefinition> rewardSources,
        IReadOnlyDictionary<string, DropTableDefinition> dropTables,
        IReadOnlyDictionary<string, LootBundleDefinition> lootBundles,
        IReadOnlyDictionary<string, TraitTokenDefinition> traitTokens,
        ICollection<ContentValidationIssue> issues)
    {
        if (rewardSources.Count != 6)
        {
            ContentValidationIssueFactory.AddError(issues, "reward.source_count", $"Launch-floor reward sources must be locked to 6. Found {rewardSources.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (dropTables.Count != 6)
        {
            ContentValidationIssueFactory.AddError(issues, "reward.drop_table_count", $"Launch-floor drop tables must be locked to 6. Found {dropTables.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (!ContentValidationPolicyCatalog.RequiredTraitTokenIds.SetEquals(traitTokens.Keys))
        {
            ContentValidationIssueFactory.AddError(issues, "reward.trait_token_floor", $"Trait token catalog must match [{string.Join(", ", ContentValidationPolicyCatalog.RequiredTraitTokenIds.OrderBy(id => id, StringComparer.Ordinal))}].", ContentValidationPolicyCatalog.ReportFolderName);
        }

        var mappedSourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rewardSource in rewardSources.Values)
        {
            var assetPath = catalog.GetPath(rewardSource);
            if (string.IsNullOrWhiteSpace(rewardSource.DropTableId) || !dropTables.ContainsKey(rewardSource.DropTableId))
            {
                ContentValidationIssueFactory.AddError(issues, "reward.source_drop_table_ref", $"Reward source '{rewardSource.Id}' references missing drop table '{rewardSource.DropTableId}'.", assetPath);
                continue;
            }

            if (!mappedSourceIds.Add(rewardSource.DropTableId))
            {
                ContentValidationIssueFactory.AddError(issues, "reward.duplicate_source_tag_mapping", $"Drop table '{rewardSource.DropTableId}' is mapped by more than one reward source.", assetPath);
            }

            var dropTable = dropTables[rewardSource.DropTableId];
            var unreachableBands = rewardSource.AllowedRarityBrackets
                .Where(bracket => dropTable.Entries.All(entry => entry.RarityBracket != bracket))
                .ToList();
            foreach (var bracket in unreachableBands)
            {
                ContentValidationIssueFactory.AddWarning(issues, "reward.unreachable_rarity_band", $"Reward source '{rewardSource.Id}' exposes rarity bracket '{bracket}' but the drop table never rolls it.", assetPath);
            }

            foreach (var entry in dropTable.Entries)
            {
                if (!rewardSource.AllowedRarityBrackets.Contains(entry.RarityBracket))
                {
                    ContentValidationIssueFactory.AddError(issues, "reward.rarity_band_out_of_source", $"Drop table entry '{entry.Id}' uses rarity '{entry.RarityBracket}' outside reward source '{rewardSource.Id}'.", assetPath);
                }
            }
        }

        foreach (var lootBundle in lootBundles.Values)
        {
            var assetPath = catalog.GetPath(lootBundle);
            if (string.IsNullOrWhiteSpace(lootBundle.RewardSourceId) || !rewardSources.ContainsKey(lootBundle.RewardSourceId))
            {
                ContentValidationIssueFactory.AddError(issues, "reward.loot_bundle_source_ref", $"Loot bundle '{lootBundle.Id}' references missing reward source '{lootBundle.RewardSourceId}'.", assetPath);
            }
        }
    }

    private static void ValidateSkillCatalog(
        ValidationAssetCatalog catalog,
        IReadOnlyList<SkillDefinitionAsset> skills,
        IReadOnlyCollection<string> statusIds,
        IReadOnlyCollection<string> cleanseProfileIds,
        ICollection<ContentValidationIssue> issues)
    {
        var supportModifierSkills = skills.Where(skill => skill.Id.StartsWith("support_", StringComparison.Ordinal)).ToList();
        if (supportModifierSkills.Count != 12)
        {
            ContentValidationIssueFactory.AddError(issues, "skill.support_modifier_floor", $"Support modifier floor must stay at 12. Found {supportModifierSkills.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        foreach (var skill in skills)
        {
            var assetPath = catalog.GetPath(skill);
            var compileTagIds = skill.CompileTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var allowedTagIds = skill.SupportAllowedTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var blockedTagIds = skill.SupportBlockedTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var requiredWeaponIds = skill.RequiredWeaponTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var requiredClassIds = skill.RequiredClassTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);

            if (allowedTagIds.Overlaps(blockedTagIds))
            {
                ContentValidationIssueFactory.AddError(issues, "skill.support_conflict", $"Skill '{skill.Id}' contains support tags in both include and exclude lists.", assetPath);
            }

            foreach (var weaponId in requiredWeaponIds)
            {
                if (!ContentValidationPolicyCatalog.AllowedWeaponFamilyIds.Contains(weaponId))
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.weapon_family_ref", $"Skill '{skill.Id}' references unsupported weapon family '{weaponId}'.", assetPath);
                }
            }

            foreach (var classId in requiredClassIds)
            {
                if (!ContentValidationPolicyCatalog.CanonicalClassIds.Contains(classId))
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.class_tag_ref", $"Skill '{skill.Id}' references unsupported class tag '{classId}'.", assetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId))
            {
                if (!cleanseProfileIds.Contains(skill.CleanseProfileId))
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.cleanse_profile_ref", $"Skill '{skill.Id}' references missing cleanse profile '{skill.CleanseProfileId}'.", assetPath);
                }

                if (!compileTagIds.Contains("cleanse"))
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.cleanse_tag", $"Skill '{skill.Id}' uses cleanse without the canonical 'cleanse' compile tag.", assetPath);
                }
            }

            var duplicateStatuses = skill.AppliedStatuses
                .Where(status => status != null)
                .GroupBy(status => status.StatusId, StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            foreach (var statusId in duplicateStatuses)
            {
                ContentValidationIssueFactory.AddError(issues, "skill.duplicate_status_application", $"Skill '{skill.Id}' applies status '{statusId}' more than once.", assetPath);
            }

            foreach (var status in skill.AppliedStatuses.Where(status => status != null))
            {
                if (!statusIds.Contains(status.StatusId))
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.status_ref", $"Skill '{skill.Id}' references missing status '{status.StatusId}'.", assetPath);
                }
            }

            if (skill.Id.StartsWith("support_", StringComparison.Ordinal))
            {
                if (skill.SlotKind != SkillSlotKindValue.Support)
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.support_slot", $"Support modifier '{skill.Id}' must use the canonical support slot.", assetPath);
                }

                if (allowedTagIds.Count == 0)
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.support_allowed_tags", $"Support modifier '{skill.Id}' must define at least one include tag.", assetPath);
                }
            }
        }
    }

    private static void ValidateItemCatalog(ValidationAssetCatalog catalog, IReadOnlyList<ItemBaseDefinition> items, ICollection<ContentValidationIssue> issues)
    {
        foreach (var item in items)
        {
            var assetPath = catalog.GetPath(item);
            var weaponFamilyId = NormalizeWeaponFamilyId(item);
            if (item.SlotType == ItemSlotType.Weapon && !ContentValidationPolicyCatalog.AllowedWeaponFamilyIds.Contains(weaponFamilyId))
            {
                ContentValidationIssueFactory.AddError(issues, "item.weapon_family_ref", $"Weapon item '{item.Id}' must use one of [{string.Join(", ", ContentValidationPolicyCatalog.AllowedWeaponFamilyIds)}].", assetPath);
            }

            var craftCurrencyId = NormalizeCraftCurrencyId(item);
            if (!ContentValidationPolicyCatalog.AllowedCraftCurrencyIds.Contains(craftCurrencyId))
            {
                ContentValidationIssueFactory.AddError(issues, "item.craft_currency_ref", $"Item '{item.Id}' references unsupported craft currency '{craftCurrencyId}'.", assetPath);
            }

            if (item.IdentityKind == ItemIdentityValue.Unique && !string.Equals(craftCurrencyId, "boss_sigil", StringComparison.Ordinal))
            {
                ContentValidationIssueFactory.AddError(issues, "item.unique_craft_currency", "Unique/boss items must use 'boss_sigil' as their imprint currency.", assetPath);
            }

            var operations = NormalizeCraftOperations(item);
            if (operations.Count > 5)
            {
                ContentValidationIssueFactory.AddError(issues, "item.affix_slot_overfill", $"Item '{item.Id}' exposes more than 5 launch-floor craft operations.", assetPath);
            }

            if (!operations.Contains(CraftOperationKindValue.Salvage))
            {
                ContentValidationIssueFactory.AddError(issues, "item.salvage_missing", $"Item '{item.Id}' must support salvage in the launch-floor crafting contract.", assetPath);
            }
        }
    }

    private static void ValidateFactionIsolation(
        ValidationAssetCatalog catalog,
        IEnumerable<ExpeditionSiteDefinition> sites,
        IEnumerable<EncounterDefinition> encounters,
        IEnumerable<EnemySquadTemplateDefinition> squads,
        IEnumerable<SynergyDefinition> synergies,
        ICollection<ContentValidationIssue> issues)
    {
        var factionIds = sites.Select(site => site.FactionId)
            .Concat(encounters.Select(encounter => encounter.FactionId))
            .Concat(squads.Select(squad => squad.FactionId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var synergy in synergies)
        {
            var assetPath = catalog.GetPath(synergy);
            if (factionIds.Contains(synergy.CountedTagId))
            {
                ContentValidationIssueFactory.AddError(issues, "faction.synergy_leak", $"Faction id '{synergy.CountedTagId}' must not leak into synergy counted tags.", assetPath);
            }
        }
    }

    private static string NormalizeWeaponFamilyId(ItemBaseDefinition item)
    {
        if (item.SlotType != ItemSlotType.Weapon)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.WeaponFamilyTag))
        {
            return item.WeaponFamilyTag;
        }

        if (!string.IsNullOrWhiteSpace(item.ItemFamilyTag))
        {
            return item.ItemFamilyTag;
        }

        if (item.Id.Contains("shield", StringComparison.Ordinal))
        {
            return "shield";
        }

        if (item.Id.Contains("bow", StringComparison.Ordinal))
        {
            return "bow";
        }

        if (item.Id.Contains("focus", StringComparison.Ordinal) || item.Id.Contains("bead", StringComparison.Ordinal))
        {
            return "focus";
        }

        return "blade";
    }

    private static string NormalizeCraftCurrencyId(ItemBaseDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(item.CraftCurrencyTag))
        {
            return item.CraftCurrencyTag;
        }

        return item.IdentityKind == ItemIdentityValue.Unique ? "boss_sigil" : "ember_dust";
    }

    private static IReadOnlyList<CraftOperationKindValue> NormalizeCraftOperations(ItemBaseDefinition item)
    {
        if (item.AllowedCraftOperations.Count > 0)
        {
            return item.AllowedCraftOperations;
        }

        var operations = new List<CraftOperationKindValue>
        {
            CraftOperationKindValue.Temper,
            CraftOperationKindValue.Reforge,
            CraftOperationKindValue.Seal,
            CraftOperationKindValue.Salvage,
        };

        if (item.IdentityKind == ItemIdentityValue.Unique)
        {
            operations.Insert(3, CraftOperationKindValue.Imprint);
        }

        return operations;
    }
}
