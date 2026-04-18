using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Model;
using static SM.Unity.ContentConversion.ContentConversionShared;

namespace SM.Unity.ContentConversion;

internal sealed class SnapshotAssembler
{
    private readonly IReadOnlyDictionary<string, UnitArchetypeDefinition> _archetypeDefinitions;
    private readonly IReadOnlyDictionary<string, TraitPoolDefinition> _traitPools;
    private readonly IReadOnlyDictionary<string, ItemBaseDefinition> _itemDefinitions;
    private readonly IReadOnlyDictionary<string, AffixDefinition> _affixDefinitions;
    private readonly IReadOnlyDictionary<string, AugmentDefinition> _augmentDefinitions;
    private readonly IReadOnlyDictionary<string, SkillDefinitionAsset> _skillDefinitions;
    private readonly IReadOnlyDictionary<string, CharacterDefinition> _characterDefinitions;
    private readonly IReadOnlyDictionary<string, TeamTacticDefinition> _teamTacticDefinitions;
    private readonly IReadOnlyDictionary<string, RoleInstructionDefinition> _roleInstructionDefinitions;
    private readonly IReadOnlyDictionary<string, PassiveNodeDefinition> _passiveNodeDefinitions;
    private readonly IReadOnlyDictionary<string, SynergyDefinition> _synergyDefinitions;
    private readonly IReadOnlyDictionary<string, CampaignChapterDefinition> _campaignChapterDefinitions;
    private readonly IReadOnlyDictionary<string, ExpeditionSiteDefinition> _expeditionSiteDefinitions;
    private readonly IReadOnlyDictionary<string, EncounterDefinition> _encounterDefinitions;
    private readonly IReadOnlyDictionary<string, EnemySquadTemplateDefinition> _enemySquadDefinitions;
    private readonly IReadOnlyDictionary<string, BossOverlayDefinition> _bossOverlayDefinitions;
    private readonly IReadOnlyDictionary<string, StatusFamilyDefinition> _statusFamilyDefinitions;
    private readonly IReadOnlyDictionary<string, CleanseProfileDefinition> _cleanseProfileDefinitions;
    private readonly IReadOnlyDictionary<string, ControlDiminishingRuleDefinition> _controlDiminishingDefinitions;
    private readonly IReadOnlyDictionary<string, RewardSourceDefinition> _rewardSourceDefinitions;
    private readonly IReadOnlyDictionary<string, DropTableDefinition> _dropTableDefinitions;
    private readonly IReadOnlyDictionary<string, LootBundleDefinition> _lootBundleDefinitions;
    private readonly IReadOnlyDictionary<string, TraitTokenDefinition> _traitTokenDefinitions;
    private readonly FirstPlayableSliceDefinition? _firstPlayableSlice;

    internal SnapshotAssembler(ContentDefinitionRegistry registry)
    {
        _archetypeDefinitions = registry.ArchetypeDefinitions;
        _traitPools = registry.TraitPools;
        _itemDefinitions = registry.ItemDefinitions;
        _affixDefinitions = registry.AffixDefinitions;
        _augmentDefinitions = registry.AugmentDefinitions;
        _skillDefinitions = registry.SkillDefinitions;
        _characterDefinitions = registry.CharacterDefinitions;
        _teamTacticDefinitions = registry.TeamTacticDefinitions;
        _roleInstructionDefinitions = registry.RoleInstructionDefinitions;
        _passiveNodeDefinitions = registry.PassiveNodeDefinitions;
        _synergyDefinitions = registry.SynergyDefinitions;
        _campaignChapterDefinitions = registry.CampaignChapterDefinitions;
        _expeditionSiteDefinitions = registry.ExpeditionSiteDefinitions;
        _encounterDefinitions = registry.EncounterDefinitions;
        _enemySquadDefinitions = registry.EnemySquadDefinitions;
        _bossOverlayDefinitions = registry.BossOverlayDefinitions;
        _statusFamilyDefinitions = registry.StatusFamilyDefinitions;
        _cleanseProfileDefinitions = registry.CleanseProfileDefinitions;
        _controlDiminishingDefinitions = registry.ControlDiminishingDefinitions;
        _rewardSourceDefinitions = registry.RewardSourceDefinitions;
        _dropTableDefinitions = registry.DropTableDefinitions;
        _lootBundleDefinitions = registry.LootBundleDefinitions;
        _traitTokenDefinitions = registry.TraitTokenDefinitions;
        _firstPlayableSlice = registry.FirstPlayableSlice;
    }

    internal CombatContentSnapshot Assemble()
    {
        var archetypeConverter = new ArchetypeConverter(_skillDefinitions, _firstPlayableSlice);

        var archetypeTemplates = BuildSection("archetype templates", () =>
            _archetypeDefinitions.Values.ToDictionary(definition => definition.Id, archetypeConverter.BuildArchetypeTemplate, StringComparer.Ordinal));
        var traitPackages = BuildSection("trait packages", () =>
            _traitPools.Values
                .SelectMany(pool => Enumerate(pool.PositiveTraits).Concat(Enumerate(pool.NegativeTraits)))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                .ToDictionary(entry => entry.Id, entry => ModifierPackageConverter.BuildTraitPackage(entry), StringComparer.Ordinal));
        var itemPackages = BuildSection("item packages", () =>
            _itemDefinitions.Values.ToDictionary(item => item.Id, item => ModifierPackageConverter.BuildItemPackage(item), StringComparer.Ordinal));
        var affixPackages = BuildSection("affix packages", () =>
            _affixDefinitions.Values.ToDictionary(affix => affix.Id, affix => ModifierPackageConverter.BuildAffixPackage(affix), StringComparer.Ordinal));
        var augmentPackages = BuildSection("augment packages", () =>
            _augmentDefinitions.Values.ToDictionary(augment => augment.Id, augment => ModifierPackageConverter.BuildAugmentPackage(augment), StringComparer.Ordinal));
        var skillCatalog = BuildSection("skill catalog", () =>
            _skillDefinitions.Values.ToDictionary(skill => skill.Id, skill => SkillConverter.BuildSkillSpec(skill), StringComparer.Ordinal));
        var teamTacticsCatalog = BuildSection("team tactics", () =>
            _teamTacticDefinitions.Values.ToDictionary(definition => definition.Id, definition => CatalogEntryConverter.BuildTeamTacticTemplate(definition), StringComparer.Ordinal));
        var roleInstructionCatalog = BuildSection("role instructions", () =>
            _roleInstructionDefinitions.Values.ToDictionary(definition => definition.Id, definition => CatalogEntryConverter.BuildRoleInstructionTemplate(definition), StringComparer.Ordinal));
        var characterCatalog = BuildSection("characters", () =>
            _characterDefinitions.Values.ToDictionary(definition => definition.Id, definition => CatalogEntryConverter.BuildCharacterTemplate(definition), StringComparer.Ordinal));
        var passiveNodeCatalog = BuildSection("passive nodes", () =>
            _passiveNodeDefinitions.Values.ToDictionary(definition => definition.Id, definition => CatalogEntryConverter.BuildPassiveNodeTemplate(definition), StringComparer.Ordinal));
        var augmentCatalog = BuildSection("augment catalog", () =>
            _augmentDefinitions.Values.ToDictionary(definition => definition.Id, definition => CatalogEntryConverter.BuildAugmentCatalogEntry(definition), StringComparer.Ordinal));
        var synergyCatalog = BuildSection("synergy catalog", () =>
            _synergyDefinitions.Values
                .SelectMany(definition => CatalogEntryConverter.BuildSynergyTemplates(definition))
                .ToDictionary(template => template.Id, template => template, StringComparer.Ordinal));
        var itemGrantedSkills = BuildSection("item granted skills", () =>
            _itemDefinitions.Values.ToDictionary(
                definition => definition.Id,
                definition => (IReadOnlyList<BattleSkillSpec>)Enumerate(definition.GrantedSkills)
                    .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                    .Select(SkillConverter.BuildSkillSpec)
                    .ToList(),
                StringComparer.Ordinal));
        var campaignChaptersCatalog = BuildSection("campaign chapters", () =>
            _campaignChapterDefinitions.Values.ToDictionary(definition => definition.Id, CampaignConverter.BuildCampaignChapterTemplate, StringComparer.Ordinal));
        var expeditionSitesCatalog = BuildSection("expedition sites", () =>
            _expeditionSiteDefinitions.Values.ToDictionary(definition => definition.Id, CampaignConverter.BuildExpeditionSiteTemplate, StringComparer.Ordinal));
        var encounterCatalog = BuildSection("encounters", () =>
            _encounterDefinitions.Values.ToDictionary(definition => definition.Id, CampaignConverter.BuildEncounterTemplate, StringComparer.Ordinal));
        var enemySquadCatalog = BuildSection("enemy squads", () =>
            _enemySquadDefinitions.Values.ToDictionary(definition => definition.Id, CampaignConverter.BuildEnemySquadTemplate, StringComparer.Ordinal));
        var bossOverlayCatalog = BuildSection("boss overlays", () =>
            _bossOverlayDefinitions.Values.ToDictionary(definition => definition.Id, CampaignConverter.BuildBossOverlayTemplate, StringComparer.Ordinal));
        var statusFamilyCatalog = BuildSection("status families", () =>
            _statusFamilyDefinitions.Values.ToDictionary(definition => definition.Id, StatusConverter.BuildStatusFamilyTemplate, StringComparer.Ordinal));
        var cleanseProfileCatalog = BuildSection("cleanse profiles", () =>
            _cleanseProfileDefinitions.Values.ToDictionary(definition => definition.Id, StatusConverter.BuildCleanseProfileTemplate, StringComparer.Ordinal));
        var controlDiminishingCatalog = BuildSection("control diminishing", () =>
            _controlDiminishingDefinitions.Values.ToDictionary(definition => definition.Id, StatusConverter.BuildControlDiminishingTemplate, StringComparer.Ordinal));
        var rewardSourceCatalog = BuildSection("reward sources", () =>
            _rewardSourceDefinitions.Values.ToDictionary(definition => definition.Id, RewardConverter.BuildRewardSourceTemplate, StringComparer.Ordinal));
        var dropTableCatalog = BuildSection("drop tables", () =>
            _dropTableDefinitions.Values.ToDictionary(definition => definition.Id, RewardConverter.BuildDropTableTemplate, StringComparer.Ordinal));
        var lootBundleCatalog = BuildSection("loot bundles", () =>
            _lootBundleDefinitions.Values.ToDictionary(definition => definition.Id, RewardConverter.BuildLootBundleTemplate, StringComparer.Ordinal));
        var traitTokenCatalog = BuildSection("trait tokens", () =>
            _traitTokenDefinitions.Values.ToDictionary(definition => definition.Id, RewardConverter.BuildTraitTokenTemplate, StringComparer.Ordinal));

        return new CombatContentSnapshot(
            archetypeTemplates,
            traitPackages,
            itemPackages,
            affixPackages,
            augmentPackages,
            skillCatalog,
            teamTacticsCatalog,
            roleInstructionCatalog,
            passiveNodeCatalog,
            augmentCatalog,
            synergyCatalog,
            itemGrantedSkills,
            campaignChaptersCatalog,
            expeditionSitesCatalog,
            encounterCatalog,
            enemySquadCatalog,
            bossOverlayCatalog,
            statusFamilyCatalog,
            cleanseProfileCatalog,
            controlDiminishingCatalog,
            rewardSourceCatalog,
            dropTableCatalog,
            lootBundleCatalog,
            traitTokenCatalog,
            _firstPlayableSlice,
            characterCatalog);
    }
}
