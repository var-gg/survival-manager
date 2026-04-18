using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Unity;

namespace SM.Editor.Validation;

internal interface ICatalogValidationRule
{
    void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues);
}

internal sealed class CatalogValidationContext
{
    public CatalogValidationContext(ValidationAssetCatalog catalog)
    {
        Catalog = catalog;
        Chapters = catalog.OfType<CampaignChapterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Sites = catalog.OfType<ExpeditionSiteDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Encounters = catalog.OfType<EncounterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Squads = catalog.OfType<EnemySquadTemplateDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Overlays = catalog.OfType<BossOverlayDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Archetypes = catalog.OfType<UnitArchetypeDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        PassiveBoards = catalog.OfType<PassiveBoardDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Statuses = catalog.OfType<StatusFamilyDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        CleanseProfiles = catalog.OfType<CleanseProfileDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        ControlRules = catalog.OfType<ControlDiminishingRuleDefinition>().ToList();
        RewardSources = catalog.OfType<RewardSourceDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        DropTables = catalog.OfType<DropTableDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        LootBundles = catalog.OfType<LootBundleDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        TraitTokens = catalog.OfType<TraitTokenDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        Skills = catalog.OfType<SkillDefinitionAsset>().ToList();
        Items = catalog.OfType<ItemBaseDefinition>().ToList();
        Synergies = catalog.OfType<SynergyDefinition>().ToList();
        ArchetypeIds = Archetypes.Keys.ToHashSet(StringComparer.Ordinal);
        FirstPlayableSliceAsset = catalog.OfType<FirstPlayableSliceDefinitionAsset>().FirstOrDefault();
    }

    internal ValidationAssetCatalog Catalog { get; }
    internal IReadOnlyDictionary<string, CampaignChapterDefinition> Chapters { get; }
    internal IReadOnlyDictionary<string, ExpeditionSiteDefinition> Sites { get; }
    internal IReadOnlyDictionary<string, EncounterDefinition> Encounters { get; }
    internal IReadOnlyDictionary<string, EnemySquadTemplateDefinition> Squads { get; }
    internal IReadOnlyDictionary<string, BossOverlayDefinition> Overlays { get; }
    internal IReadOnlyDictionary<string, UnitArchetypeDefinition> Archetypes { get; }
    internal IReadOnlyDictionary<string, PassiveBoardDefinition> PassiveBoards { get; }
    internal IReadOnlyDictionary<string, StatusFamilyDefinition> Statuses { get; }
    internal IReadOnlyDictionary<string, CleanseProfileDefinition> CleanseProfiles { get; }
    internal IReadOnlyList<ControlDiminishingRuleDefinition> ControlRules { get; }
    internal IReadOnlyDictionary<string, RewardSourceDefinition> RewardSources { get; }
    internal IReadOnlyDictionary<string, DropTableDefinition> DropTables { get; }
    internal IReadOnlyDictionary<string, LootBundleDefinition> LootBundles { get; }
    internal IReadOnlyDictionary<string, TraitTokenDefinition> TraitTokens { get; }
    internal IReadOnlyList<SkillDefinitionAsset> Skills { get; }
    internal IReadOnlyList<ItemBaseDefinition> Items { get; }
    internal IReadOnlyList<SynergyDefinition> Synergies { get; }
    internal IReadOnlyCollection<string> ArchetypeIds { get; }
    internal FirstPlayableSliceDefinitionAsset? FirstPlayableSliceAsset { get; }

    internal string GetPath(UnityEngine.Object asset)
    {
        return Catalog.GetPath(asset);
    }
}

internal sealed class CatalogValidationRuleRegistry
{
    private readonly IReadOnlyList<ICatalogValidationRule> _rules;

    public CatalogValidationRuleRegistry(IReadOnlyList<ICatalogValidationRule> rules)
    {
        _rules = rules;
    }

    internal void Validate(ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues)
    {
        var context = new CatalogValidationContext(catalog);
        foreach (var rule in _rules)
        {
            rule.Validate(context, issues);
        }
    }

    internal static CatalogValidationRuleRegistry CreateDefault()
    {
        return new CatalogValidationRuleRegistry(new ICatalogValidationRule[]
        {
            new CampaignCatalogValidator(),
            new FirstPlayableSliceCatalogValidator(),
            new EncounterAuthoringCatalogValidator(),
            new StatusCatalogValidator(),
            new RewardCatalogValidator(),
            new BuildLaneCoverageCatalogValidator(),
            new SkillCatalogValidator(),
            new ItemCatalogValidator(),
            new FactionIsolationValidator(),
        });
    }
}

internal static class ContentDefinitionCatalogRules
{
    internal static void ValidateLaunchFloorCatalogs(ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues)
    {
        CatalogValidationRuleRegistry.CreateDefault().Validate(catalog, issues);
    }
}

internal sealed class CampaignCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        if (context.Chapters.Count != 3)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.chapter_count", $"Story chapters must be locked to 3. Found {context.Chapters.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (context.Sites.Count != 6)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.site_count", $"Expedition sites must be locked to 6. Found {context.Sites.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (context.Encounters.Count != 24)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.encounter_count", $"Encounter catalog must be locked to 24 battle encounters. Found {context.Encounters.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        foreach (var chapter in context.Chapters.Values)
        {
            var assetPath = context.GetPath(chapter);
            if (chapter.SiteIds.Distinct(StringComparer.Ordinal).Count() != 2)
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.chapter_site_count", "Campaign chapter must own exactly 2 expedition sites.", assetPath);
            }

            foreach (var siteId in chapter.SiteIds)
            {
                if (!context.Sites.ContainsKey(siteId))
                {
                    ContentValidationIssueFactory.AddError(issues, "campaign.chapter_site_ref", $"Campaign chapter references missing site '{siteId}'.", assetPath);
                }
            }
        }

        foreach (var site in context.Sites.Values)
        {
            var assetPath = context.GetPath(site);
            if (!context.Chapters.ContainsKey(site.ChapterId))
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.site_chapter_ref", $"Expedition site references missing chapter '{site.ChapterId}'.", assetPath);
            }

            if (site.EncounterIds.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.site_encounter_count", "Expedition site must own exactly 4 battle encounters (2 skirmish / 1 elite / 1 boss).", assetPath);
            }

            if (string.IsNullOrWhiteSpace(site.ExtractRewardSourceId) || !context.RewardSources.ContainsKey(site.ExtractRewardSourceId))
            {
                ContentValidationIssueFactory.AddError(issues, "campaign.site_extract_source", "Expedition site is missing a valid extract reward source.", assetPath);
            }

            foreach (var encounterId in site.EncounterIds)
            {
                if (!context.Encounters.ContainsKey(encounterId))
                {
                    ContentValidationIssueFactory.AddError(issues, "campaign.site_encounter_ref", $"Expedition site references missing encounter '{encounterId}'.", assetPath);
                }
            }
        }

        foreach (var encounter in context.Encounters.Values)
        {
            var assetPath = context.GetPath(encounter);
            if (!context.Sites.ContainsKey(encounter.SiteId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.site_ref", $"Encounter references missing site '{encounter.SiteId}'.", assetPath);
            }

            if (!context.Squads.ContainsKey(encounter.EnemySquadTemplateId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.squad_ref", $"Encounter references missing enemy squad '{encounter.EnemySquadTemplateId}'.", assetPath);
            }

            if (string.IsNullOrWhiteSpace(encounter.RewardSourceId) || !context.RewardSources.ContainsKey(encounter.RewardSourceId))
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
                if (string.IsNullOrWhiteSpace(encounter.BossOverlayId) || !context.Overlays.ContainsKey(encounter.BossOverlayId))
                {
                    ContentValidationIssueFactory.AddError(issues, "encounter.boss_overlay_ref", "Boss encounter must reference a valid boss overlay.", assetPath);
                }
            }
            else if (!string.IsNullOrWhiteSpace(encounter.BossOverlayId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.non_boss_overlay", "Only boss encounters may reference a boss overlay.", assetPath);
            }
        }

        foreach (var squad in context.Squads.Values)
        {
            var assetPath = context.GetPath(squad);
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
                if (!context.ArchetypeIds.Contains(member.ArchetypeId))
                {
                    ContentValidationIssueFactory.AddError(issues, "enemy_squad.member_archetype_ref", $"Enemy squad member references missing archetype '{member.ArchetypeId}'.", assetPath);
                }
            }
        }
    }
}

internal sealed class StatusCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        var statusIds = context.Statuses.Keys.ToHashSet(StringComparer.Ordinal);
        if (!ContentValidationPolicyCatalog.RequiredLaunchStatusIds.SetEquals(statusIds))
        {
            ContentValidationIssueFactory.AddError(issues, "status.catalog_floor", $"Launch-floor status catalog must match [{string.Join(", ", ContentValidationPolicyCatalog.RequiredLaunchStatusIds.OrderBy(id => id, StringComparer.Ordinal))}].", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (!ContentValidationPolicyCatalog.RequiredCleanseProfileIds.SetEquals(context.CleanseProfiles.Keys))
        {
            ContentValidationIssueFactory.AddError(issues, "status.cleanse_floor", $"Cleanse profile catalog must match [{string.Join(", ", ContentValidationPolicyCatalog.RequiredCleanseProfileIds.OrderBy(id => id, StringComparer.Ordinal))}].", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (context.ControlRules.Count != 1)
        {
            ContentValidationIssueFactory.AddError(issues, "status.dr_rule_count", $"Launch-floor control diminishing rules must be locked to 1. Found {context.ControlRules.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }
        else
        {
            var rule = context.ControlRules[0];
            var assetPath = context.GetPath(rule);
            if (Math.Abs(rule.WindowSeconds - 1.5f) > 0.001f || Math.Abs(rule.ControlResistMultiplier - 0.5f) > 0.001f)
            {
                ContentValidationIssueFactory.AddError(issues, "status.dr_values", "Launch-floor DR must stay at 1.5s window and 50% control resist.", assetPath);
            }
        }

        foreach (var profile in context.CleanseProfiles.Values)
        {
            var assetPath = context.GetPath(profile);
            foreach (var statusId in profile.RemovesStatusIds)
            {
                if (!statusIds.Contains(statusId))
                {
                    ContentValidationIssueFactory.AddError(issues, "status.cleanse_target_ref", $"Cleanse profile references missing status '{statusId}'.", assetPath);
                }
            }
        }

        foreach (var skill in context.Skills)
        {
            var assetPath = context.GetPath(skill);
            foreach (var status in skill.AppliedStatuses.Where(status => status != null))
            {
                if (!statusIds.Contains(status.StatusId))
                {
                    ContentValidationIssueFactory.AddError(issues, "status.skill_status_ref", $"Skill '{skill.Id}' references missing status '{status.StatusId}'.", assetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId) && !context.CleanseProfiles.ContainsKey(skill.CleanseProfileId))
            {
                ContentValidationIssueFactory.AddError(issues, "status.skill_cleanse_ref", $"Skill '{skill.Id}' references missing cleanse profile '{skill.CleanseProfileId}'.", assetPath);
            }
        }
    }
}

internal sealed class RewardCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        if (context.RewardSources.Count != 6)
        {
            ContentValidationIssueFactory.AddError(issues, "reward.source_count", $"Launch-floor reward sources must be locked to 6. Found {context.RewardSources.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (context.DropTables.Count != 6)
        {
            ContentValidationIssueFactory.AddError(issues, "reward.drop_table_count", $"Launch-floor drop tables must be locked to 6. Found {context.DropTables.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (!ContentValidationPolicyCatalog.RequiredTraitTokenIds.SetEquals(context.TraitTokens.Keys))
        {
            ContentValidationIssueFactory.AddError(issues, "reward.trait_token_floor", $"Trait token catalog must match [{string.Join(", ", ContentValidationPolicyCatalog.RequiredTraitTokenIds.OrderBy(id => id, StringComparer.Ordinal))}].", ContentValidationPolicyCatalog.ReportFolderName);
        }

        var mappedSourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rewardSource in context.RewardSources.Values)
        {
            var assetPath = context.GetPath(rewardSource);
            if (string.IsNullOrWhiteSpace(rewardSource.DropTableId) || !context.DropTables.ContainsKey(rewardSource.DropTableId))
            {
                ContentValidationIssueFactory.AddError(issues, "reward.source_drop_table_ref", $"Reward source '{rewardSource.Id}' references missing drop table '{rewardSource.DropTableId}'.", assetPath);
                continue;
            }

            if (!mappedSourceIds.Add(rewardSource.DropTableId))
            {
                ContentValidationIssueFactory.AddError(issues, "reward.duplicate_source_tag_mapping", $"Drop table '{rewardSource.DropTableId}' is mapped by more than one reward source.", assetPath);
            }

            var dropTable = context.DropTables[rewardSource.DropTableId];
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

        foreach (var lootBundle in context.LootBundles.Values)
        {
            var assetPath = context.GetPath(lootBundle);
            if (string.IsNullOrWhiteSpace(lootBundle.RewardSourceId) || !context.RewardSources.ContainsKey(lootBundle.RewardSourceId))
            {
                ContentValidationIssueFactory.AddError(issues, "reward.loot_bundle_source_ref", $"Loot bundle '{lootBundle.Id}' references missing reward source '{lootBundle.RewardSourceId}'.", assetPath);
            }
        }
    }
}

internal sealed class SkillCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        var statusIds = context.Statuses.Keys.ToHashSet(StringComparer.Ordinal);
        var cleanseProfileIds = context.CleanseProfiles.Keys.ToHashSet(StringComparer.Ordinal);
        var supportModifierSkills = context.Skills.Where(skill => skill.Id.StartsWith("support_", StringComparison.Ordinal)).ToList();
        if (supportModifierSkills.Count != 12)
        {
            ContentValidationIssueFactory.AddError(issues, "skill.support_modifier_floor", $"Support modifier floor must stay at 12. Found {supportModifierSkills.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        foreach (var skill in context.Skills)
        {
            var assetPath = context.GetPath(skill);
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
}

internal sealed class ItemCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        foreach (var item in context.Items)
        {
            var assetPath = context.GetPath(item);
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

internal sealed class FactionIsolationValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        var factionIds = context.Sites.Values.Select(site => site.FactionId)
            .Concat(context.Encounters.Values.Select(encounter => encounter.FactionId))
            .Concat(context.Squads.Values.Select(squad => squad.FactionId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var synergy in context.Synergies)
        {
            var assetPath = context.GetPath(synergy);
            if (factionIds.Contains(synergy.CountedTagId))
            {
                ContentValidationIssueFactory.AddError(issues, "faction.synergy_leak", $"Faction id '{synergy.CountedTagId}' must not leak into synergy counted tags.", assetPath);
            }
        }
    }
}
