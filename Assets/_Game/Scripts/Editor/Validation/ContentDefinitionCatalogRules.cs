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
        Characters = catalog.OfType<CharacterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        ExtraActors = catalog.OfType<ExtraActorCharacterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
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
        Affixes = catalog.OfType<AffixDefinition>().ToList();
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
    internal IReadOnlyDictionary<string, CharacterDefinition> Characters { get; }
    internal IReadOnlyDictionary<string, ExtraActorCharacterDefinition> ExtraActors { get; }
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
    internal IReadOnlyList<AffixDefinition> Affixes { get; }
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
            new CharacterCatalogValidator(),
            new ExtraActorCatalogValidator(),
            new StatusCatalogValidator(),
            new RewardCatalogValidator(),
            new BuildLaneCoverageCatalogValidator(),
            new SkillCatalogValidator(),
            new ItemCatalogValidator(),
            new EquipmentContentV1CatalogValidator(),
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
        if (context.Chapters.Count != 5)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.chapter_count", $"Story chapters must be locked to 5. Found {context.Chapters.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (context.Sites.Count != 10)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.site_count", $"Expedition sites must be locked to 10. Found {context.Sites.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        if (context.Encounters.Count != 40)
        {
            ContentValidationIssueFactory.AddError(issues, "campaign.encounter_count", $"Encounter catalog must be locked to 40 battle encounters. Found {context.Encounters.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
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

internal sealed class CharacterCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        var actualIds = context.Characters.Keys.ToHashSet(StringComparer.Ordinal);
        if (!ContentValidationPolicyCatalog.RequiredExecutableCharacterIds.SetEquals(actualIds))
        {
            var required = string.Join(", ", ContentValidationPolicyCatalog.RequiredExecutableCharacterIdsInRosterOrder);
            var deferred = string.Join(", ", ContentValidationPolicyCatalog.DeferredRuntimeCharacterIds);
            var missing = ContentValidationPolicyCatalog.RequiredExecutableCharacterIds
                .Except(actualIds, StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var extra = actualIds
                .Except(ContentValidationPolicyCatalog.RequiredExecutableCharacterIds, StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var drift = new List<string>();
            if (missing.Count > 0)
            {
                drift.Add($"Missing [{string.Join(", ", missing)}].");
            }

            if (extra.Count > 0)
            {
                drift.Add($"Unexpected [{string.Join(", ", extra)}].");
            }

            ContentValidationIssueFactory.AddError(
                issues,
                "character.executable_catalog_floor",
                $"Executable CharacterDefinition catalog must match the current 16-id runtime set [{required}]. {string.Join(" ", drift)} Deferred runtime ids stay lore-only until Relicborn race/archetype authoring closes: [{deferred}].",
                ContentValidationPolicyCatalog.ReportFolderName);
        }
    }
}

internal sealed class ExtraActorCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        var expectedIds = ExtraActorCharacterRegistry.CharacterIds.ToHashSet(StringComparer.Ordinal);
        var actualIds = context.ExtraActors.Keys.ToHashSet(StringComparer.Ordinal);
        if (!expectedIds.SetEquals(actualIds))
        {
            var missing = expectedIds
                .Except(actualIds, StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var extra = actualIds
                .Except(expectedIds, StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var drift = new List<string>();
            if (missing.Count > 0)
            {
                drift.Add($"Missing [{string.Join(", ", missing)}].");
            }

            if (extra.Count > 0)
            {
                drift.Add($"Unexpected [{string.Join(", ", extra)}].");
            }

            ContentValidationIssueFactory.AddError(
                issues,
                "extra_actor.catalog_floor",
                $"ExtraActorCharacterDefinition catalog must match the encounter-visible extra actor registry. {string.Join(" ", drift)}",
                ContentValidationPolicyCatalog.ReportFolderName);
        }

        foreach (var profile in ExtraActorCharacterRegistry.Profiles)
        {
            if (!context.ExtraActors.TryGetValue(profile.ActorId, out var definition))
            {
                continue;
            }

            ValidateProfileMirror(context, issues, profile, definition);
        }

        foreach (var squad in context.Squads.Values)
        {
            var assetPath = context.GetPath(squad);
            foreach (var member in squad.Members)
            {
                if (string.IsNullOrWhiteSpace(member.CharacterId) || !member.CharacterId.StartsWith("extra_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!context.ExtraActors.ContainsKey(member.CharacterId))
                {
                    ContentValidationIssueFactory.AddError(
                        issues,
                        "extra_actor.enemy_squad_ref",
                        $"Enemy squad member references unregistered extra actor character '{member.CharacterId}'.",
                        assetPath);
                }
            }
        }
    }

    private static void ValidateProfileMirror(
        CatalogValidationContext context,
        ICollection<ContentValidationIssue> issues,
        ExtraActorCharacterProfile profile,
        ExtraActorCharacterDefinition definition)
    {
        var assetPath = context.GetPath(definition);
        if (!context.Chapters.ContainsKey(definition.ChapterId))
        {
            ContentValidationIssueFactory.AddError(issues, "extra_actor.chapter_ref", $"Extra actor references missing chapter '{definition.ChapterId}'.", assetPath);
        }

        if (!context.Sites.TryGetValue(definition.SiteId, out var site))
        {
            ContentValidationIssueFactory.AddError(issues, "extra_actor.site_ref", $"Extra actor references missing site '{definition.SiteId}'.", assetPath);
        }
        else if (!string.Equals(site.ChapterId, definition.ChapterId, StringComparison.Ordinal))
        {
            ContentValidationIssueFactory.AddError(issues, "extra_actor.site_chapter_mismatch", "Extra actor site must belong to the same chapter as the extra actor profile.", assetPath);
        }

        if (!context.ArchetypeIds.Contains(definition.CombatArchetypeId))
        {
            ContentValidationIssueFactory.AddError(issues, "extra_actor.archetype_ref", $"Extra actor references missing combat archetype '{definition.CombatArchetypeId}'.", assetPath);
        }

        if (!BattleP09AppearanceRoster.TryGetDefinedDisplayName(definition.Id, out var displayName))
        {
            ContentValidationIssueFactory.AddError(issues, "extra_actor.p09_roster_ref", "Extra actor must have a P09 roster display name.", assetPath);
        }
        else if (!string.Equals(displayName, profile.DisplayName, StringComparison.Ordinal))
        {
            ContentValidationIssueFactory.AddError(issues, "extra_actor.p09_roster_name", "Extra actor P09 roster display name must mirror the registry profile.", assetPath);
        }

        ValidateMirror(issues, definition.ExposureTier.ToString(), profile.ExposureTier.ToString(), "ExposureTier", assetPath);
        ValidateMirror(issues, definition.FirstClearSpawnPolicy.ToString(), profile.FirstClearSpawnPolicy.ToString(), "FirstClearSpawnPolicy", assetPath);
        ValidateMirror(issues, definition.IllustrationTier.ToString(), profile.IllustrationTier.ToString(), "IllustrationTier", assetPath);
        ValidateMirror(issues, definition.ChapterId, profile.ChapterId, "ChapterId", assetPath);
        ValidateMirror(issues, definition.SiteId, profile.SiteId, "SiteId", assetPath);
        ValidateMirror(issues, definition.StorySafety, profile.StorySafety, "StorySafety", assetPath);
        ValidateMirror(issues, definition.FactionId, profile.FactionId, "FactionId", assetPath);
        ValidateMirror(issues, definition.CombatArchetypeId, profile.CombatArchetypeId, "CombatArchetypeId", assetPath);
        ValidateMirror(issues, definition.P09BasePresetId, profile.P09BasePresetId, "P09BasePresetId", assetPath);
        ValidateMirror(issues, definition.ModelArchetype, profile.ModelArchetype, "ModelArchetype", assetPath);
        ValidateMirror(issues, definition.BarkSetId, profile.BarkSetId, "BarkSetId", assetPath);
        ValidateMirror(issues, definition.DossierHook, profile.DossierHook, "DossierHook", assetPath);

        if (definition.GachaEligible != profile.GachaEligible)
        {
            ContentValidationIssueFactory.AddError(
                issues,
                "extra_actor.registry_mismatch",
                "Extra actor GachaEligible must mirror the registry profile.",
                assetPath,
                "ExtraActorCharacterDefinition.GachaEligible");
        }
    }

    private static void ValidateMirror(
        ICollection<ContentValidationIssue> issues,
        string actual,
        string expected,
        string fieldName,
        string assetPath)
    {
        if (string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        ContentValidationIssueFactory.AddError(
            issues,
            "extra_actor.registry_mismatch",
            $"Extra actor {fieldName} must mirror registry value '{expected}'.",
            assetPath,
            $"ExtraActorCharacterDefinition.{fieldName}");
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

                if (entry.Id.StartsWith("item_", StringComparison.Ordinal)
                    && entry.RewardType != RewardType.Item)
                {
                    ContentValidationIssueFactory.AddError(issues, "reward.item_entry_type", $"Drop table entry '{entry.Id}' must use RewardType.Item.", assetPath);
                }

                if (entry.RewardType == RewardType.Item
                    && context.Items.Count > 0
                    && context.Items.All(item => !string.Equals(item.Id, entry.Id, StringComparison.Ordinal)))
                {
                    ContentValidationIssueFactory.AddError(issues, "reward.item_entry_ref", $"Drop table item reward '{entry.Id}' has no matching ItemBaseDefinition.", assetPath);
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
        if (supportModifierSkills.Count < 12)
        {
            ContentValidationIssueFactory.AddError(issues, "skill.support_modifier_floor", $"Support modifier floor must be at least 12. Found {supportModifierSkills.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
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

            if (!skill.Id.StartsWith("support_", StringComparison.Ordinal)
                && skill.SlotKind is not SkillSlotKindValue.Support
                && requiredClassIds.Count == 0)
            {
                ContentValidationIssueFactory.AddError(issues, "skill.class_gate_required", $"Class-owned skill '{skill.Id}' must define at least one RequiredClassTags entry.", assetPath);
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

                if (!allowedTagIds.Overlaps(ContentValidationPolicyCatalog.CanonicalClassIds)
                    && !allowedTagIds.Overlaps(ContentValidationPolicyCatalog.AllowedRoleFamilyTags))
                {
                    ContentValidationIssueFactory.AddError(issues, "skill.support_gate_anchor", $"Support modifier '{skill.Id}' must include at least one class or role-family gate tag.", assetPath);
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

            var operations = NormalizeCraftOperations(item);
            if (!operations.Contains(CraftOperationKindValue.Reforge))
            {
                ContentValidationIssueFactory.AddError(issues, "item.refit_missing", $"Item '{item.Id}' must expose Reforge as the V1 Echo refit operation.", assetPath);
            }

            var deferredOperations = operations
                .Where(operation => operation != CraftOperationKindValue.Reforge)
                .Distinct()
                .ToList();
            foreach (var operation in deferredOperations)
            {
                ContentValidationIssueFactory.AddError(issues, "item.deferred_craft_operation", $"Item '{item.Id}' exposes deferred craft operation '{operation}'. V1 only allows Echo Refit/Reforge.", assetPath);
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

        return "echo";
    }

    private static IReadOnlyList<CraftOperationKindValue> NormalizeCraftOperations(ItemBaseDefinition item)
    {
        if (item.AllowedCraftOperations.Count > 0)
        {
            return item.AllowedCraftOperations;
        }

        return new List<CraftOperationKindValue> { CraftOperationKindValue.Reforge };
    }
}

internal sealed class EquipmentContentV1CatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        if (!HasFullEquipmentCatalog(context))
        {
            return;
        }

        ValidateItemDistribution(context, issues);
        ValidateAffixDistribution(context, issues);
        ValidateDropTableItems(context, issues);
    }

    private static bool HasFullEquipmentCatalog(CatalogValidationContext context)
    {
        return context.Items.Count >= EquipmentContentV1Contract.ItemCount
            && context.Affixes.Count >= EquipmentContentV1Contract.AffixCount
            && context.DropTables.Count >= 3;
    }

    private static void ValidateItemDistribution(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        if (context.Items.Count != EquipmentContentV1Contract.ItemCount)
        {
            ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_count", $"Equipment V1 item catalog must contain {EquipmentContentV1Contract.ItemCount} bases. Found {context.Items.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        var commonCount = context.Items.Count(item => item.RarityTier == ItemRarityTierValue.Common);
        var rareCount = context.Items.Count(item => item.RarityTier == ItemRarityTierValue.Rare);
        var epicCount = context.Items.Count(item => item.RarityTier == ItemRarityTierValue.Epic);
        if (commonCount != 30 || rareCount != 9 || epicCount != 3)
        {
            ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_rarity_mix", $"Equipment V1 rarity mix must be Common/Rare/Epic = 30/9/3. Found {commonCount}/{rareCount}/{epicCount}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        var baselineCount = context.Items.Count(item => item.IdentityKind == ItemIdentityValue.Baseline);
        var namedCount = context.Items.Count(item => item.IdentityKind == ItemIdentityValue.Named);
        var uniqueCount = context.Items.Count(item => item.IdentityKind == ItemIdentityValue.Unique);
        if (baselineCount != 34 || namedCount != 6 || uniqueCount != 2)
        {
            ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_identity_mix", $"Equipment V1 identity mix must be Baseline/Named/Unique = 34/6/2. Found {baselineCount}/{namedCount}/{uniqueCount}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        var skillIds = context.Skills.Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in context.Items)
        {
            var assetPath = context.GetPath(item);
            if (!string.Equals(item.CraftCurrencyTag, EquipmentContentV1Contract.RefitCurrencyTag, StringComparison.Ordinal))
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.refit_currency", $"Item '{item.Id}' must use '{EquipmentContentV1Contract.RefitCurrencyTag}' as the V1 refit currency.", assetPath);
            }

            if (item.AllowedCraftOperations.Count != 1 || item.AllowedCraftOperations[0] != CraftOperationKindValue.Reforge)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.refit_only", $"Item '{item.Id}' must expose only Reforge for the V1 crafting surface.", assetPath);
            }

            if (EquipmentContentV1Contract.RareItemIds.Contains(item.Id) && item.RarityTier != ItemRarityTierValue.Rare)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_rarity_id", $"Item '{item.Id}' is locked to Rare in the V1 equipment contract.", assetPath);
            }

            if (EquipmentContentV1Contract.EpicItemIds.Contains(item.Id) && item.RarityTier != ItemRarityTierValue.Epic)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_rarity_id", $"Item '{item.Id}' is locked to Epic in the V1 equipment contract.", assetPath);
            }

            if (EquipmentContentV1Contract.NamedItemIds.Contains(item.Id) && item.IdentityKind != ItemIdentityValue.Named)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_identity_id", $"Item '{item.Id}' is locked to Named in the V1 equipment contract.", assetPath);
            }

            if (EquipmentContentV1Contract.UniqueItemIds.Contains(item.Id) && item.IdentityKind != ItemIdentityValue.Unique)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_identity_id", $"Item '{item.Id}' is locked to Unique in the V1 equipment contract.", assetPath);
            }

            if ((item.IdentityKind == ItemIdentityValue.Named || item.IdentityKind == ItemIdentityValue.Unique)
                && item.GrantedSkills.Count == 0
                && item.UniqueRuleTags.Count == 0
                && item.RuleModifierTags.Count == 0)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.identity_payload", $"Named/Unique item '{item.Id}' must define a granted skill or rule payload.", assetPath);
            }

            foreach (var skill in item.GrantedSkills.Where(skill => skill != null))
            {
                if (!skillIds.Contains(skill.Id))
                {
                    ContentValidationIssueFactory.AddError(issues, "equipment_v1.granted_skill_ref", $"Item '{item.Id}' grants missing skill '{skill.Id}'.", assetPath);
                }
            }
        }
    }

    private static void ValidateAffixDistribution(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        var affixesById = context.Affixes
            .Where(affix => !string.IsNullOrWhiteSpace(affix.Id))
            .ToDictionary(affix => affix.Id, affix => affix, StringComparer.Ordinal);
        var liveAffixes = context.Affixes
            .Where(affix => EquipmentContentV1Contract.LiveAffixIds.Contains(affix.Id))
            .ToList();
        var reservedAffixes = context.Affixes
            .Where(affix => EquipmentContentV1Contract.ReservedAffixIds.Contains(affix.Id))
            .ToList();

        if (liveAffixes.Count != EquipmentContentV1Contract.LiveAffixCount || reservedAffixes.Count != EquipmentContentV1Contract.ReservedAffixCount)
        {
            ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_live_reserved_mix", $"Equipment V1 affix mix must be live/reserved = 24/6. Found {liveAffixes.Count}/{reservedAffixes.Count}.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        var tierMix = liveAffixes
            .GroupBy(affix => affix.Tier)
            .ToDictionary(group => group.Key, group => group.Count());
        if (GetCount(tierMix, AffixTierValue.Implicit) != 6
            || GetCount(tierMix, AffixTierValue.Prefix) != 12
            || GetCount(tierMix, AffixTierValue.Suffix) != 6)
        {
            ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_tier_mix", $"Live affix tiers must be Implicit/Prefix/Suffix = 6/12/6.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        var familyMix = liveAffixes
            .GroupBy(affix => affix.AffixFamily)
            .ToDictionary(group => group.Key, group => group.Count());
        if (GetCount(familyMix, AffixFamilyValue.CoreScalar) != 14
            || GetCount(familyMix, AffixFamilyValue.ConditionalTagged) != 6
            || GetCount(familyMix, AffixFamilyValue.BuildShaping) != 4)
        {
            ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_family_mix", $"Live affix families must be CoreScalar/ConditionalTagged/BuildShaping = 14/6/4.", ContentValidationPolicyCatalog.ReportFolderName);
        }

        foreach (var spec in EquipmentContentV1Contract.AffixSpecs)
        {
            if (!affixesById.TryGetValue(spec.Id, out var affix))
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_missing", $"Equipment V1 affix '{spec.Id}' is missing.", ContentValidationPolicyCatalog.ReportFolderName);
                continue;
            }

            var assetPath = context.GetPath(affix);
            if (affix.Tier != spec.Tier || affix.AffixFamily != spec.Family || affix.EffectType != spec.EffectType)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_contract", $"Affix '{affix.Id}' tier/family/effect type drifted from the V1 contract.", assetPath);
            }

            if (EquipmentContentV1Contract.LiveAffixIds.Contains(affix.Id))
            {
                if (affix.SpawnWeight <= 0f || affix.ItemLevelMin >= EquipmentContentV1Contract.ReservedAffixItemLevelMin)
                {
                    ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_live_spawn", $"Live affix '{affix.Id}' must be spawnable in V1.", assetPath);
                }

                if (string.IsNullOrWhiteSpace(affix.TextTemplateKey)
                    || string.IsNullOrWhiteSpace(affix.ExclusiveGroupId)
                    || affix.ValueMax <= 0f
                    || affix.Modifiers.Count == 0)
                {
                    ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_authoring_payload", $"Live affix '{affix.Id}' must define text template, exclusive group, value range, and numeric modifier.", assetPath);
                }
            }
            else if (affix.SpawnWeight != 0f || affix.ItemLevelMin < EquipmentContentV1Contract.ReservedAffixItemLevelMin)
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.affix_reserved_spawn", $"Reserved affix '{affix.Id}' must be non-spawning in V1.", assetPath);
            }
        }
    }

    private static void ValidateDropTableItems(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        foreach (var (tableId, itemIds) in EquipmentContentV1Contract.RequiredItemDropsByTable)
        {
            if (!context.DropTables.TryGetValue(tableId, out var table))
            {
                ContentValidationIssueFactory.AddError(issues, "equipment_v1.drop_table_missing", $"Equipment V1 drop table '{tableId}' is missing.", ContentValidationPolicyCatalog.ReportFolderName);
                continue;
            }

            var assetPath = context.GetPath(table);
            foreach (var itemId in itemIds)
            {
                if (table.Entries.All(entry => !string.Equals(entry.Id, itemId, StringComparison.Ordinal) || entry.RewardType != RewardType.Item))
                {
                    ContentValidationIssueFactory.AddError(issues, "equipment_v1.item_drop_route", $"Drop table '{tableId}' must expose item reward '{itemId}'.", assetPath);
                }
            }
        }
    }

    private static int GetCount<TKey>(IReadOnlyDictionary<TKey, int> counts, TKey key)
    {
        return counts.TryGetValue(key, out var count) ? count : 0;
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
