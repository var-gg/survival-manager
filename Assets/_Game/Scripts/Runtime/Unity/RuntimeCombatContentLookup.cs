using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Stats;
using SM.Meta.Model;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity;

public sealed class RuntimeCombatContentLookup
{
    private static readonly string[] CanonicalArchetypeOrder =
    {
        "warden",
        "guardian",
        "slayer",
        "raider",
        "hunter",
        "scout",
        "priest",
        "hexer",
        "bulwark",
        "reaver",
        "marksman",
        "shaman"
    };

    private static readonly string[] LegacyItemFallbackOrder =
    {
        "item_iron_sword",
        "item_bone_blade",
        "item_leather_armor",
        "item_bone_plate",
        "item_lucky_charm",
        "item_prayer_bead"
    };

    private static readonly string[] LegacyAugmentFallbackOrder =
    {
        "augment_silver_guard",
        "augment_silver_focus",
        "augment_silver_stride",
        "augment_gold_bastion",
        "augment_gold_fury",
        "augment_gold_mending"
    };

    private readonly Dictionary<string, UnitArchetypeDefinition> _archetypeDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TraitPoolDefinition> _traitPools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RaceDefinition> _raceDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClassDefinition> _classDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemBaseDefinition> _itemDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AffixDefinition> _affixDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AugmentDefinition> _augmentDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillDefinitionAsset> _skillDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TeamTacticDefinition> _teamTacticDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoleInstructionDefinition> _roleInstructionDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PassiveNodeDefinition> _passiveNodeDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SynergyDefinition> _synergyDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CampaignChapterDefinition> _campaignChapterDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpeditionSiteDefinition> _expeditionSiteDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EncounterDefinition> _encounterDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EnemySquadTemplateDefinition> _enemySquadDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BossOverlayDefinition> _bossOverlayDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StatusFamilyDefinition> _statusFamilyDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CleanseProfileDefinition> _cleanseProfileDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ControlDiminishingRuleDefinition> _controlDiminishingDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RewardSourceDefinition> _rewardSourceDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DropTableDefinition> _dropTableDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LootBundleDefinition> _lootBundleDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TraitTokenDefinition> _traitTokenDefinitions = new(StringComparer.Ordinal);
    private CombatContentSnapshot? _snapshot;
    private bool _loaded;

    public CombatContentSnapshot Snapshot
    {
        get
        {
            EnsureLoaded();
            return _snapshot!;
        }
    }

    public IReadOnlyDictionary<string, UnitArchetypeDefinition> ArchetypeDefinitions
    {
        get
        {
            EnsureLoaded();
            return _archetypeDefinitions;
        }
    }

    public bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error)
    {
        try
        {
            snapshot = Snapshot;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null!;
            error = ex.Message;
#if UNITY_EDITOR
            Debug.LogError($"[RuntimeCombatContentLookup] Failed to build combat snapshot: {ex}");
#endif
            return false;
        }
    }

    public IReadOnlyList<string> GetCanonicalArchetypeIds()
    {
        EnsureLoaded();
        var resolved = CanonicalArchetypeOrder.Where(id => _archetypeDefinitions.ContainsKey(id)).ToList();
        if (resolved.Count > 0)
        {
            return resolved;
        }

        return _archetypeDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> GetCanonicalItemIds()
    {
        EnsureLoaded();
        var ordered = LegacyItemFallbackOrder.Where(id => _itemDefinitions.ContainsKey(id)).ToList();
        ordered.AddRange(_itemDefinitions.Keys.Where(id => !ordered.Contains(id)).OrderBy(id => id, StringComparer.Ordinal));
        return ordered;
    }

    public IReadOnlyList<string> GetCanonicalAffixIds()
    {
        EnsureLoaded();
        return _affixDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds()
    {
        EnsureLoaded();
        var ordered = LegacyAugmentFallbackOrder.Where(id => _augmentDefinitions.TryGetValue(id, out var augment) && !augment.IsPermanent).ToList();
        ordered.AddRange(_augmentDefinitions.Values
            .Where(augment => !augment.IsPermanent && !ordered.Contains(augment.Id))
            .OrderBy(augment => augment.Id, StringComparer.Ordinal)
            .Select(augment => augment.Id));
        return ordered;
    }

    public bool TryGetArchetype(string archetypeId, out UnitArchetypeDefinition archetype)
    {
        EnsureLoaded();
        return _archetypeDefinitions.TryGetValue(archetypeId, out archetype!);
    }

    public bool TryGetItemDefinition(string itemId, out ItemBaseDefinition item)
    {
        EnsureLoaded();
        return _itemDefinitions.TryGetValue(itemId, out item!);
    }

    public bool TryGetRaceDefinition(string raceId, out RaceDefinition race)
    {
        EnsureLoaded();
        return _raceDefinitions.TryGetValue(raceId, out race!);
    }

    public bool TryGetClassDefinition(string classId, out ClassDefinition @class)
    {
        EnsureLoaded();
        return _classDefinitions.TryGetValue(classId, out @class!);
    }

    public bool TryGetAugmentDefinition(string augmentId, out AugmentDefinition augment)
    {
        EnsureLoaded();
        return _augmentDefinitions.TryGetValue(augmentId, out augment!);
    }

    public bool TryGetSkillDefinition(string skillId, out SkillDefinitionAsset skill)
    {
        EnsureLoaded();
        return _skillDefinitions.TryGetValue(skillId, out skill!);
    }

    public bool TryGetAffixDefinition(string affixId, out AffixDefinition affix)
    {
        EnsureLoaded();
        return _affixDefinitions.TryGetValue(affixId, out affix!);
    }

    public bool TryGetCampaignChapterDefinition(string chapterId, out CampaignChapterDefinition chapter)
    {
        EnsureLoaded();
        return _campaignChapterDefinitions.TryGetValue(chapterId, out chapter!);
    }

    public bool TryGetExpeditionSiteDefinition(string siteId, out ExpeditionSiteDefinition site)
    {
        EnsureLoaded();
        return _expeditionSiteDefinitions.TryGetValue(siteId, out site!);
    }

    public bool TryGetEncounterDefinition(string encounterId, out EncounterDefinition encounter)
    {
        EnsureLoaded();
        return _encounterDefinitions.TryGetValue(encounterId, out encounter!);
    }

    public IReadOnlyList<CampaignChapterDefinition> GetOrderedCampaignChapters()
    {
        EnsureLoaded();
        return _campaignChapterDefinitions.Values
            .OrderBy(definition => definition.StoryOrder)
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .ToList();
    }

    public bool TryGetTraitEntry(string archetypeId, string traitId, out TraitEntry trait)
    {
        EnsureLoaded();
        trait = null!;

        if (!_traitPools.TryGetValue(archetypeId, out var pool))
        {
            return false;
        }

        trait = pool.PositiveTraits.Concat(pool.NegativeTraits)
            .FirstOrDefault(entry => entry != null && string.Equals(entry.Id, traitId, StringComparison.Ordinal))!;
        return trait != null;
    }

    public bool TryGetTraitIds(string archetypeId, out IReadOnlyList<string> positiveTraitIds, out IReadOnlyList<string> negativeTraitIds)
    {
        EnsureLoaded();
        positiveTraitIds = Array.Empty<string>();
        negativeTraitIds = Array.Empty<string>();

        if (!_traitPools.TryGetValue(archetypeId, out var pool))
        {
            return false;
        }

        positiveTraitIds = pool.PositiveTraits
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .Select(entry => entry.Id)
            .ToList();
        negativeTraitIds = pool.NegativeTraits
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .Select(entry => entry.Id)
            .ToList();
        return positiveTraitIds.Count > 0 && negativeTraitIds.Count > 0;
    }

    public string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(archetypeId) && _archetypeDefinitions.ContainsKey(archetypeId))
        {
            return archetypeId;
        }

        var exactMatch = _archetypeDefinitions.Values.FirstOrDefault(definition =>
            string.Equals(definition.Race.Id, raceId, StringComparison.Ordinal) &&
            string.Equals(definition.Class.Id, classId, StringComparison.Ordinal));
        if (exactMatch != null)
        {
            return exactMatch.Id;
        }

        var ordered = GetCanonicalArchetypeIds();
        if (ordered.Count == 0)
        {
            throw new InvalidOperationException("전투 archetype canonical 목록이 비어 있습니다.");
        }

        return ordered[Math.Abs(fallbackIndex) % ordered.Count];
    }

    public string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex)
    {
        EnsureLoaded();
        if (TryGetTraitIds(archetypeId, out var positives, out _) && positives.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(traitId) && positives.Contains(traitId))
            {
                return traitId;
            }

            return positives[Math.Abs(fallbackIndex) % positives.Count];
        }

        return string.Empty;
    }

    public string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex)
    {
        EnsureLoaded();
        if (TryGetTraitIds(archetypeId, out _, out var negatives) && negatives.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(traitId) && negatives.Contains(traitId))
            {
                return traitId;
            }

            return negatives[Math.Abs(fallbackIndex) % negatives.Count];
        }

        return string.Empty;
    }

    public string NormalizeItemBaseId(string itemBaseId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(itemBaseId) && _itemDefinitions.ContainsKey(itemBaseId))
        {
            return itemBaseId;
        }

        var items = GetCanonicalItemIds();
        if (items.Count == 0)
        {
            return string.Empty;
        }

        return items[Math.Abs(fallbackIndex) % items.Count];
    }

    public string NormalizeAffixId(string affixId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(affixId) && _affixDefinitions.ContainsKey(affixId))
        {
            return affixId;
        }

        var affixes = GetCanonicalAffixIds();
        if (affixes.Count == 0)
        {
            return string.Empty;
        }

        return affixes[Math.Abs(fallbackIndex) % affixes.Count];
    }

    public string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(augmentId)
            && _augmentDefinitions.TryGetValue(augmentId, out var augment)
            && !augment.IsPermanent)
        {
            return augmentId;
        }

        var augments = GetCanonicalTemporaryAugmentIds();
        if (augments.Count == 0)
        {
            return string.Empty;
        }

        return augments[Math.Abs(fallbackIndex) % augments.Count];
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        LoadContent();
        _loaded = true;
    }

    private void LoadContent()
    {
#if UNITY_EDITOR
        RequireEditorCanonicalSampleContentReady();
#endif
        var archetypes = LoadDefinitions<UnitArchetypeDefinition>("_Game/Content/Definitions/Archetypes", "Assets/Resources/_Game/Content/Definitions/Archetypes");
        var traitPools = LoadDefinitions<TraitPoolDefinition>("_Game/Content/Definitions/Traits", "Assets/Resources/_Game/Content/Definitions/Traits");
        var races = LoadDefinitions<RaceDefinition>("_Game/Content/Definitions/Races", "Assets/Resources/_Game/Content/Definitions/Races");
        var classes = LoadDefinitions<ClassDefinition>("_Game/Content/Definitions/Classes", "Assets/Resources/_Game/Content/Definitions/Classes");
        var items = LoadDefinitions<ItemBaseDefinition>("_Game/Content/Definitions/Items", "Assets/Resources/_Game/Content/Definitions/Items");
        var affixes = LoadDefinitions<AffixDefinition>("_Game/Content/Definitions/Affixes", "Assets/Resources/_Game/Content/Definitions/Affixes");
        var augments = LoadDefinitions<AugmentDefinition>("_Game/Content/Definitions/Augments", "Assets/Resources/_Game/Content/Definitions/Augments");
        var skills = LoadDefinitions<SkillDefinitionAsset>("_Game/Content/Definitions/Skills", "Assets/Resources/_Game/Content/Definitions/Skills");
        var teamTactics = LoadDefinitions<TeamTacticDefinition>("_Game/Content/Definitions/TeamTactics", "Assets/Resources/_Game/Content/Definitions/TeamTactics");
        var roleInstructions = LoadDefinitions<RoleInstructionDefinition>("_Game/Content/Definitions/RoleInstructions", "Assets/Resources/_Game/Content/Definitions/RoleInstructions");
        var passiveNodes = LoadDefinitions<PassiveNodeDefinition>("_Game/Content/Definitions/PassiveNodes", "Assets/Resources/_Game/Content/Definitions/PassiveNodes");
        var synergies = LoadDefinitions<SynergyDefinition>("_Game/Content/Definitions/Synergies", "Assets/Resources/_Game/Content/Definitions/Synergies");
        var campaignChapters = LoadDefinitions<CampaignChapterDefinition>("_Game/Content/Definitions/CampaignChapters", "Assets/Resources/_Game/Content/Definitions/CampaignChapters");
        var expeditionSites = LoadDefinitions<ExpeditionSiteDefinition>("_Game/Content/Definitions/ExpeditionSites", "Assets/Resources/_Game/Content/Definitions/ExpeditionSites");
        var encounters = LoadDefinitions<EncounterDefinition>("_Game/Content/Definitions/Encounters", "Assets/Resources/_Game/Content/Definitions/Encounters");
        var enemySquads = LoadDefinitions<EnemySquadTemplateDefinition>("_Game/Content/Definitions/EnemySquads", "Assets/Resources/_Game/Content/Definitions/EnemySquads");
        var bossOverlays = LoadDefinitions<BossOverlayDefinition>("_Game/Content/Definitions/BossOverlays", "Assets/Resources/_Game/Content/Definitions/BossOverlays");
        var statusFamilies = LoadDefinitions<StatusFamilyDefinition>("_Game/Content/Definitions/StatusFamilies", "Assets/Resources/_Game/Content/Definitions/StatusFamilies");
        var cleanseProfiles = LoadDefinitions<CleanseProfileDefinition>("_Game/Content/Definitions/CleanseProfiles", "Assets/Resources/_Game/Content/Definitions/CleanseProfiles");
        var controlDiminishingRules = LoadDefinitions<ControlDiminishingRuleDefinition>("_Game/Content/Definitions/ControlDiminishingRules", "Assets/Resources/_Game/Content/Definitions/ControlDiminishingRules");
        var rewardSources = LoadDefinitions<RewardSourceDefinition>("_Game/Content/Definitions/RewardSources", "Assets/Resources/_Game/Content/Definitions/RewardSources");
        var dropTables = LoadDefinitions<DropTableDefinition>("_Game/Content/Definitions/DropTables", "Assets/Resources/_Game/Content/Definitions/DropTables");
        var lootBundles = LoadDefinitions<LootBundleDefinition>("_Game/Content/Definitions/LootBundles", "Assets/Resources/_Game/Content/Definitions/LootBundles");
        var traitTokens = LoadDefinitions<TraitTokenDefinition>("_Game/Content/Definitions/TraitTokens", "Assets/Resources/_Game/Content/Definitions/TraitTokens");

        var requiresFileFallback =
            archetypes.Length == 0 ||
            skills.Length == 0 ||
            campaignChapters.Length == 0 ||
            expeditionSites.Length == 0 ||
            encounters.Length == 0 ||
            enemySquads.Length == 0 ||
            bossOverlays.Length == 0 ||
            rewardSources.Length == 0 ||
            dropTables.Length == 0 ||
            lootBundles.Length == 0;

        if (requiresFileFallback)
        {
            if (!RuntimeCombatContentFileParser.TryLoad(out var parsed, out var parseError))
            {
                throw new InvalidOperationException($"전투 archetype 정의를 Resources에서 찾을 수 없습니다. {parseError}");
            }

            if (archetypes.Length == 0)
            {
                archetypes = parsed.Archetypes.ToArray();
            }

            if (skills.Length == 0)
            {
                skills = parsed.Skills.ToArray();
            }

            if (items.Length == 0)
            {
                items = parsed.Items.ToArray();
            }

            if (affixes.Length == 0)
            {
                affixes = parsed.Affixes.ToArray();
            }

            if (augments.Length == 0)
            {
                augments = parsed.Augments.ToArray();
            }

            if (campaignChapters.Length == 0)
            {
                campaignChapters = parsed.CampaignChapters.ToArray();
            }

            if (expeditionSites.Length == 0)
            {
                expeditionSites = parsed.ExpeditionSites.ToArray();
            }

            if (encounters.Length == 0)
            {
                encounters = parsed.Encounters.ToArray();
            }

            if (enemySquads.Length == 0)
            {
                enemySquads = parsed.EnemySquads.ToArray();
            }

            if (bossOverlays.Length == 0)
            {
                bossOverlays = parsed.BossOverlays.ToArray();
            }

            if (rewardSources.Length == 0)
            {
                rewardSources = parsed.RewardSources.ToArray();
            }

            if (dropTables.Length == 0)
            {
                dropTables = parsed.DropTables.ToArray();
            }

            if (lootBundles.Length == 0)
            {
                lootBundles = parsed.LootBundles.ToArray();
            }
        }

        _archetypeDefinitions.Clear();
        _traitPools.Clear();
        _raceDefinitions.Clear();
        _classDefinitions.Clear();
        _itemDefinitions.Clear();
        _affixDefinitions.Clear();
        _augmentDefinitions.Clear();
        _skillDefinitions.Clear();
        _teamTacticDefinitions.Clear();
        _roleInstructionDefinitions.Clear();
        _passiveNodeDefinitions.Clear();
        _synergyDefinitions.Clear();
        _campaignChapterDefinitions.Clear();
        _expeditionSiteDefinitions.Clear();
        _encounterDefinitions.Clear();
        _enemySquadDefinitions.Clear();
        _bossOverlayDefinitions.Clear();
        _statusFamilyDefinitions.Clear();
        _cleanseProfileDefinitions.Clear();
        _controlDiminishingDefinitions.Clear();
        _rewardSourceDefinitions.Clear();
        _dropTableDefinitions.Clear();
        _lootBundleDefinitions.Clear();
        _traitTokenDefinitions.Clear();

        foreach (var race in races.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _raceDefinitions[race.Id] = race;
        }

        foreach (var @class in classes.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _classDefinitions[@class.Id] = @class;
        }

        foreach (var archetype in archetypes.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _archetypeDefinitions[archetype.Id] = archetype;
        }

        foreach (var traitPool in traitPools.Where(definition => definition != null))
        {
            var archetypeId = ResolveTraitPoolArchetypeId(traitPool);
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                continue;
            }

            _traitPools[archetypeId] = traitPool;
        }

        foreach (var archetype in archetypes.Where(definition => definition != null && definition.TraitPool != null))
        {
            var archetypeId = ResolveTraitPoolArchetypeId(archetype.TraitPool);
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                continue;
            }

            _traitPools[archetypeId] = archetype.TraitPool;
        }

        foreach (var item in items.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _itemDefinitions[item.Id] = item;
        }

        foreach (var affix in affixes.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _affixDefinitions[affix.Id] = affix;
        }

        foreach (var augment in augments.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _augmentDefinitions[augment.Id] = augment;
        }

        foreach (var skill in skills.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _skillDefinitions[skill.Id] = skill;
        }

        foreach (var teamTactic in teamTactics.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _teamTacticDefinitions[teamTactic.Id] = teamTactic;
        }

        foreach (var roleInstruction in roleInstructions.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _roleInstructionDefinitions[roleInstruction.Id] = roleInstruction;
        }

        foreach (var passiveNode in passiveNodes.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _passiveNodeDefinitions[passiveNode.Id] = passiveNode;
        }

        foreach (var synergy in synergies.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _synergyDefinitions[synergy.Id] = synergy;
        }

        foreach (var chapter in campaignChapters.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _campaignChapterDefinitions[chapter.Id] = chapter;
        }

        foreach (var site in expeditionSites.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _expeditionSiteDefinitions[site.Id] = site;
        }

        foreach (var encounter in encounters.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _encounterDefinitions[encounter.Id] = encounter;
        }

        foreach (var squad in enemySquads.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _enemySquadDefinitions[squad.Id] = squad;
        }

        foreach (var overlay in bossOverlays.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _bossOverlayDefinitions[overlay.Id] = overlay;
        }

        foreach (var family in statusFamilies.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _statusFamilyDefinitions[family.Id] = family;
        }

        foreach (var profile in cleanseProfiles.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _cleanseProfileDefinitions[profile.Id] = profile;
        }

        foreach (var rule in controlDiminishingRules.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _controlDiminishingDefinitions[rule.Id] = rule;
        }

        foreach (var rewardSource in rewardSources.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _rewardSourceDefinitions[rewardSource.Id] = rewardSource;
        }

        foreach (var dropTable in dropTables.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _dropTableDefinitions[dropTable.Id] = dropTable;
        }

        foreach (var lootBundle in lootBundles.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _lootBundleDefinitions[lootBundle.Id] = lootBundle;
        }

        foreach (var traitToken in traitTokens.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _traitTokenDefinitions[traitToken.Id] = traitToken;
        }

        var archetypeTemplates = BuildSection("archetype templates", () =>
            _archetypeDefinitions.Values.ToDictionary(definition => definition.Id, BuildArchetypeTemplate, StringComparer.Ordinal));
        var traitPackages = BuildSection("trait packages", () =>
            _traitPools.Values
                .SelectMany(pool => Enumerate(pool.PositiveTraits).Concat(Enumerate(pool.NegativeTraits)))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                .ToDictionary(entry => entry.Id, entry => BuildTraitPackage(entry), StringComparer.Ordinal));
        var itemPackages = BuildSection("item packages", () =>
            _itemDefinitions.Values.ToDictionary(item => item.Id, item => BuildItemPackage(item), StringComparer.Ordinal));
        var affixPackages = BuildSection("affix packages", () =>
            _affixDefinitions.Values.ToDictionary(affix => affix.Id, affix => BuildAffixPackage(affix), StringComparer.Ordinal));
        var augmentPackages = BuildSection("augment packages", () =>
            _augmentDefinitions.Values.ToDictionary(augment => augment.Id, augment => BuildAugmentPackage(augment), StringComparer.Ordinal));
        var skillCatalog = BuildSection("skill catalog", () =>
            _skillDefinitions.Values.ToDictionary(skill => skill.Id, skill => BuildSkillSpec(skill), StringComparer.Ordinal));
        var teamTacticsCatalog = BuildSection("team tactics", () =>
            _teamTacticDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildTeamTacticTemplate(definition), StringComparer.Ordinal));
        var roleInstructionCatalog = BuildSection("role instructions", () =>
            _roleInstructionDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildRoleInstructionTemplate(definition), StringComparer.Ordinal));
        var passiveNodeCatalog = BuildSection("passive nodes", () =>
            _passiveNodeDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildPassiveNodeTemplate(definition), StringComparer.Ordinal));
        var augmentCatalog = BuildSection("augment catalog", () =>
            _augmentDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildAugmentCatalogEntry(definition), StringComparer.Ordinal));
        var synergyCatalog = BuildSection("synergy catalog", () =>
            _synergyDefinitions.Values
                .SelectMany(definition => BuildSynergyTemplates(definition))
                .ToDictionary(template => template.Id, template => template, StringComparer.Ordinal));
        var itemGrantedSkills = BuildSection("item granted skills", () =>
            _itemDefinitions.Values.ToDictionary(
                definition => definition.Id,
                definition => (IReadOnlyList<BattleSkillSpec>)Enumerate(definition.GrantedSkills)
                    .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                    .Select(BuildSkillSpec)
                    .ToList(),
                StringComparer.Ordinal));
        var campaignChaptersCatalog = BuildSection("campaign chapters", () =>
            _campaignChapterDefinitions.Values.ToDictionary(definition => definition.Id, BuildCampaignChapterTemplate, StringComparer.Ordinal));
        var expeditionSitesCatalog = BuildSection("expedition sites", () =>
            _expeditionSiteDefinitions.Values.ToDictionary(definition => definition.Id, BuildExpeditionSiteTemplate, StringComparer.Ordinal));
        var encounterCatalog = BuildSection("encounters", () =>
            _encounterDefinitions.Values.ToDictionary(definition => definition.Id, BuildEncounterTemplate, StringComparer.Ordinal));
        var enemySquadCatalog = BuildSection("enemy squads", () =>
            _enemySquadDefinitions.Values.ToDictionary(definition => definition.Id, BuildEnemySquadTemplate, StringComparer.Ordinal));
        var bossOverlayCatalog = BuildSection("boss overlays", () =>
            _bossOverlayDefinitions.Values.ToDictionary(definition => definition.Id, BuildBossOverlayTemplate, StringComparer.Ordinal));
        var statusFamilyCatalog = BuildSection("status families", () =>
            _statusFamilyDefinitions.Values.ToDictionary(definition => definition.Id, BuildStatusFamilyTemplate, StringComparer.Ordinal));
        var cleanseProfileCatalog = BuildSection("cleanse profiles", () =>
            _cleanseProfileDefinitions.Values.ToDictionary(definition => definition.Id, BuildCleanseProfileTemplate, StringComparer.Ordinal));
        var controlDiminishingCatalog = BuildSection("control diminishing", () =>
            _controlDiminishingDefinitions.Values.ToDictionary(definition => definition.Id, BuildControlDiminishingTemplate, StringComparer.Ordinal));
        var rewardSourceCatalog = BuildSection("reward sources", () =>
            _rewardSourceDefinitions.Values.ToDictionary(definition => definition.Id, BuildRewardSourceTemplate, StringComparer.Ordinal));
        var dropTableCatalog = BuildSection("drop tables", () =>
            _dropTableDefinitions.Values.ToDictionary(definition => definition.Id, BuildDropTableTemplate, StringComparer.Ordinal));
        var lootBundleCatalog = BuildSection("loot bundles", () =>
            _lootBundleDefinitions.Values.ToDictionary(definition => definition.Id, BuildLootBundleTemplate, StringComparer.Ordinal));
        var traitTokenCatalog = BuildSection("trait tokens", () =>
            _traitTokenDefinitions.Values.ToDictionary(definition => definition.Id, BuildTraitTokenTemplate, StringComparer.Ordinal));

        _snapshot = new CombatContentSnapshot(
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
            traitTokenCatalog);
    }

    private static T[] LoadDefinitions<T>(string resourcesPath, string editorFolderPath) where T : UnityEngine.Object
    {
        var results = new List<T>();
#if UNITY_EDITOR
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
#endif

        foreach (var asset in Resources.LoadAll<T>(resourcesPath))
        {
            if (asset == null)
            {
                continue;
            }

            results.Add(asset);
#if UNITY_EDITOR
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                seenPaths.Add(assetPath);
            }
#endif
        }

#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(editorFolderPath))
        {
            return results.ToArray();
        }

        foreach (var path in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { editorFolderPath })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(path => !string.IsNullOrWhiteSpace(path) && seenPaths.Add(path)))
        {
            var asset = LoadEditorAssetAtPath<T>(path);
            if (asset != null)
            {
                results.Add(asset);
            }
        }

        foreach (var path in AssetDatabase.FindAssets(string.Empty, new[] { editorFolderPath })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && seenPaths.Add(path)))
        {
            var asset = LoadEditorAssetAtPath<T>(path);
            if (asset != null)
            {
                results.Add(asset);
            }
        }

        return results.ToArray();
#else
        return results.ToArray();
#endif
    }

#if UNITY_EDITOR
    private static T? LoadEditorAssetAtPath<T>(string path) where T : UnityEngine.Object
    {
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as T;
        if (mainAsset != null)
        {
            return mainAsset;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault();
    }
#endif

    private CombatArchetypeTemplate BuildArchetypeTemplate(UnitArchetypeDefinition definition)
    {
        var resolvedSkills = ResolveArchetypeSkills(definition);
        return new CombatArchetypeTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.Race.Id,
            definition.Class.Id,
            (DeploymentAnchorId)definition.DefaultAnchor,
            BuildBaseStats(definition),
            Enumerate(definition.TacticPreset)
                .OrderBy(entry => entry.Priority)
                .Select(entry => new TacticRule(
                    entry.Priority,
                    (TacticConditionType)entry.ConditionType,
                    entry.Threshold,
                    (BattleActionType)entry.ActionType,
                    (TargetSelectorType)entry.TargetSelector,
                    entry.Skill == null ? null : entry.Skill.Id))
                .ToList(),
            resolvedSkills
                .Select(BuildSkillSpec)
                .ToList(),
            string.IsNullOrWhiteSpace(definition.RoleTag) ? "auto" : definition.RoleTag,
            BuildFootprintProfile(definition),
            BuildBehaviorProfile(definition),
            BuildMobilityProfile(definition),
            PreferPrimaryOrFallback(definition.BasePreferredDistance, definition.BaseAttackRange),
            definition.BaseProtectRadius,
            new ManaEnvelope(
                definition.BaseManaMax,
                definition.BaseManaGainOnAttack,
                definition.BaseManaGainOnHit));
    }

    private static FootprintProfile BuildFootprintProfile(UnitArchetypeDefinition definition)
    {
        if (definition.FootprintProfile != null)
        {
            return new FootprintProfile(
                Mathf.Max(0.15f, definition.FootprintProfile.NavigationRadius),
                Mathf.Max(0.2f, definition.FootprintProfile.SeparationRadius),
                Mathf.Max(0.4f, definition.FootprintProfile.CombatReach),
                new FloatRange(
                    Mathf.Max(0f, definition.FootprintProfile.PreferredRangeMin),
                    Mathf.Max(definition.FootprintProfile.PreferredRangeMin, definition.FootprintProfile.PreferredRangeMax)),
                Mathf.Max(1, definition.FootprintProfile.EngagementSlotCount),
                Mathf.Max(0.4f, definition.FootprintProfile.EngagementSlotRadius),
                (BodySizeCategory)definition.FootprintProfile.BodySizeCategory,
                Mathf.Max(1.1f, definition.FootprintProfile.HeadAnchorHeight));
        }

        var attackRange = Mathf.Max(0.8f, definition.BaseAttackRange);
        var collisionRadius = Mathf.Max(0.15f, definition.BaseCollisionRadius);
        return definition.Class != null ? definition.Class.Id switch
        {
            "vanguard" => new FootprintProfile(
                Mathf.Max(0.45f, collisionRadius + 0.1f),
                Mathf.Max(0.65f, collisionRadius + 0.22f),
                Mathf.Min(1.4f, attackRange),
                new FloatRange(0.9f, 1.25f),
                5,
                1.3f,
                definition.BaseMaxHealth >= 25f || collisionRadius >= 0.55f ? BodySizeCategory.Large : BodySizeCategory.Medium,
                2.15f),
            "duelist" => new FootprintProfile(
                Mathf.Max(0.4f, collisionRadius),
                Mathf.Max(0.62f, collisionRadius + 0.18f),
                Mathf.Min(1.3f, attackRange),
                new FloatRange(0.95f, 1.45f),
                6,
                1.3f,
                BodySizeCategory.Medium,
                2.0f),
            "ranger" => new FootprintProfile(
                Mathf.Max(0.32f, collisionRadius * 0.8f),
                Mathf.Max(0.72f, collisionRadius + 0.24f),
                attackRange,
                new FloatRange(Mathf.Max(2.3f, attackRange - 0.65f), Mathf.Max(2.8f, attackRange + 0.15f)),
                3,
                Mathf.Max(1.5f, attackRange * 0.7f),
                BodySizeCategory.Small,
                1.92f),
            "mystic" => new FootprintProfile(
                Mathf.Max(0.32f, collisionRadius * 0.8f),
                Mathf.Max(0.76f, collisionRadius + 0.28f),
                attackRange,
                new FloatRange(Mathf.Max(2.1f, attackRange - 0.55f), Mathf.Max(2.7f, attackRange + 0.2f)),
                3,
                Mathf.Max(1.45f, attackRange * 0.68f),
                BodySizeCategory.Small,
                1.96f),
            _ => new FootprintProfile(
                Mathf.Max(0.4f, collisionRadius),
                Mathf.Max(0.64f, collisionRadius + 0.2f),
                Mathf.Min(1.3f, attackRange),
                new FloatRange(Mathf.Max(0.9f, attackRange - 0.2f), attackRange),
                4,
                1.2f,
                BodySizeCategory.Medium,
                2f),
        } : new FootprintProfile(
            Mathf.Max(0.4f, collisionRadius),
            Mathf.Max(0.64f, collisionRadius + 0.2f),
            Mathf.Min(1.3f, attackRange),
            new FloatRange(Mathf.Max(0.9f, attackRange - 0.2f), attackRange),
            4,
            1.2f,
            BodySizeCategory.Medium,
            2f);
    }

    private static BehaviorProfile BuildBehaviorProfile(UnitArchetypeDefinition definition)
    {
        if (definition.BehaviorProfile != null)
        {
            return new BehaviorProfile(
                Mathf.Max(0.1f, definition.BehaviorProfile.ReevaluationInterval),
                Mathf.Max(0f, definition.BehaviorProfile.RangeHysteresis),
                Mathf.Clamp01(definition.BehaviorProfile.RetreatBias),
                Mathf.Clamp01(definition.BehaviorProfile.MaintainRangeBias),
                Mathf.Clamp01(definition.BehaviorProfile.Opportunism),
                Mathf.Clamp01(definition.BehaviorProfile.Discipline),
                Mathf.Clamp01(definition.BehaviorProfile.DodgeChance),
                Mathf.Clamp01(definition.BehaviorProfile.BlockChance),
                Mathf.Clamp(definition.BehaviorProfile.BlockMitigation, 0f, 0.9f),
                Mathf.Clamp01(definition.BehaviorProfile.Stability),
                Mathf.Max(0f, definition.BehaviorProfile.BlockCooldownSeconds));
        }

        return definition.Class != null ? definition.Class.Id switch
        {
            "vanguard" => new BehaviorProfile(0.42f, 0.16f, 0.04f, 0.05f, 0.34f, 0.82f, 0.02f, 0.28f, 0.38f, 0.88f, 1f),
            "duelist" => new BehaviorProfile(0.3f, 0.22f, 0.22f, 0.24f, 0.72f, 0.58f, 0.08f, 0.12f, 0.18f, 0.62f, 1.15f),
            "ranger" => new BehaviorProfile(0.24f, 0.28f, 0.72f, 0.84f, 0.58f, 0.74f, 0.12f, 0.04f, 0.12f, 0.34f, 1.5f),
            "mystic" => new BehaviorProfile(0.28f, 0.3f, 0.68f, 0.78f, 0.5f, 0.84f, 0.06f, 0.06f, 0.18f, 0.45f, 1.35f),
            _ => new BehaviorProfile(0.35f, 0.2f, 0.15f, 0.15f, 0.5f, 0.5f, 0.04f, 0.08f, 0.2f, 0.5f, 1.2f),
        } : new BehaviorProfile(0.35f, 0.2f, 0.15f, 0.15f, 0.5f, 0.5f, 0.04f, 0.08f, 0.2f, 0.5f, 1.2f);
    }

    private static MobilityActionProfile? BuildMobilityProfile(UnitArchetypeDefinition definition)
    {
        if (definition.MobilityProfile != null)
        {
            return new MobilityActionProfile(
                (MobilityStyle)definition.MobilityProfile.Style,
                (MobilityPurpose)definition.MobilityProfile.Purpose,
                Mathf.Max(0f, definition.MobilityProfile.Distance),
                Mathf.Max(0f, definition.MobilityProfile.Cooldown),
                Mathf.Max(0f, definition.MobilityProfile.CastTime),
                Mathf.Max(0f, definition.MobilityProfile.Recovery),
                Mathf.Max(0f, definition.MobilityProfile.TriggerMinDistance),
                Mathf.Max(0f, definition.MobilityProfile.TriggerMaxDistance),
                Mathf.Clamp(definition.MobilityProfile.LateralBias, -1f, 1f));
        }

        return definition.Class != null ? definition.Class.Id switch
        {
            "vanguard" => new MobilityActionProfile(MobilityStyle.Dash, MobilityPurpose.Engage, 0.9f, 5f, 0f, 0.18f, 1.4f, 2.8f, 0f),
            "duelist" => new MobilityActionProfile(MobilityStyle.Dash, MobilityPurpose.Engage, 1.15f, 4.2f, 0f, 0.16f, 1.3f, 3f, 0.2f),
            "ranger" => new MobilityActionProfile(MobilityStyle.Roll, MobilityPurpose.MaintainRange, 1.45f, 3.4f, 0f, 0.22f, 0f, 1.45f, 0.68f),
            "mystic" => new MobilityActionProfile(MobilityStyle.Blink, MobilityPurpose.Disengage, 1.85f, 4.4f, 0f, 0.3f, 0f, 1.35f, 0.35f),
            _ => null,
        } : null;
    }

    private IReadOnlyList<SkillDefinitionAsset> ResolveArchetypeSkills(UnitArchetypeDefinition definition)
    {
        var resolved = new List<SkillDefinitionAsset>();
        var occupiedSlots = new HashSet<SkillSlotKindValue>();

        void AddSkill(SkillDefinitionAsset? skill)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id))
            {
                return;
            }

            var slot = NormalizeSkillSlot(skill);
            if (!occupiedSlots.Add(slot))
            {
                return;
            }

            resolved.Add(skill);
        }

        foreach (var skill in Enumerate(definition.Skills))
        {
            AddSkill(skill);
        }

        foreach (var skillId in GetDefaultArchetypeSkillIds(definition.Id))
        {
            if (_skillDefinitions.TryGetValue(skillId, out var skill))
            {
                AddSkill(skill);
            }
        }

        foreach (var skillId in GetDefaultClassSkillIds(definition.Class != null ? definition.Class.Id : string.Empty))
        {
            if (_skillDefinitions.TryGetValue(skillId, out var skill))
            {
                AddSkill(skill);
            }
        }

        return resolved
            .OrderBy(skill => GetSkillSlotOrder(NormalizeSkillSlot(skill)))
            .ThenBy(skill => skill.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> GetDefaultArchetypeSkillIds(string archetypeId)
    {
        return archetypeId switch
        {
            "warden" => new[] { "skill_power_strike", "skill_warden_utility" },
            "guardian" => new[] { "skill_guardian_core", "skill_guardian_utility" },
            "bulwark" => new[] { "skill_bulwark_core", "skill_bulwark_utility" },
            "slayer" => new[] { "skill_slayer_core", "skill_slayer_utility" },
            "raider" => new[] { "skill_raider_core", "skill_raider_utility" },
            "reaver" => new[] { "skill_reaver_core", "skill_reaver_utility" },
            "hunter" => new[] { "skill_precision_shot", "skill_hunter_utility" },
            "scout" => new[] { "skill_scout_core", "skill_scout_utility" },
            "marksman" => new[] { "skill_marksman_core", "skill_marksman_utility" },
            "priest" => new[] { "skill_priest_core", "skill_minor_heal" },
            "hexer" => new[] { "skill_hexer_core", "skill_hexer_utility" },
            "shaman" => new[] { "skill_shaman_core", "skill_shaman_utility" },
            _ => Array.Empty<string>(),
        };
    }

    private static IEnumerable<string> GetDefaultClassSkillIds(string classId)
    {
        return classId switch
        {
            "vanguard" => new[] { "skill_vanguard_passive_1", "skill_vanguard_support_1" },
            "duelist" => new[] { "skill_duelist_passive_1", "skill_duelist_support_1" },
            "ranger" => new[] { "skill_ranger_passive_1", "skill_ranger_support_1" },
            "mystic" => new[] { "skill_mystic_passive_1", "skill_mystic_support_1" },
            _ => Array.Empty<string>(),
        };
    }

    private static SkillSlotKindValue NormalizeSkillSlot(SkillDefinitionAsset skill)
    {
        return skill.SlotKind switch
        {
            SkillSlotKindValue.UtilityActive => SkillSlotKindValue.UtilityActive,
            SkillSlotKindValue.Passive => SkillSlotKindValue.Passive,
            SkillSlotKindValue.Support => SkillSlotKindValue.Support,
            _ => SkillSlotKindValue.CoreActive,
        };
    }

    private static int GetSkillSlotOrder(SkillSlotKindValue slotKind)
    {
        return slotKind switch
        {
            SkillSlotKindValue.CoreActive => 0,
            SkillSlotKindValue.UtilityActive => 1,
            SkillSlotKindValue.Passive => 2,
            SkillSlotKindValue.Support => 3,
            _ => int.MaxValue,
        };
    }

    private static BattleSkillSpec BuildSkillSpec(SkillDefinitionAsset skill)
    {
        return new BattleSkillSpec(
            skill.Id,
            ResolveLegacyName(skill.NameKey, skill.LegacyDisplayName, skill.Id),
            (SkillKind)skill.Kind,
            skill.Power,
            skill.Range,
            skill.SlotKind switch
            {
                SkillSlotKindValue.UtilityActive => CompiledSkillSlots.UtilityActive,
                SkillSlotKindValue.Passive => CompiledSkillSlots.Passive,
                SkillSlotKindValue.Support => CompiledSkillSlots.Support,
                _ => CompiledSkillSlots.CoreActive,
            },
            ExtractTagIds(skill.CompileTags),
            skill.DamageType switch
            {
                DamageTypeValue.Magical => DamageType.Magical,
                DamageTypeValue.Healing => DamageType.Healing,
                DamageTypeValue.True => DamageType.True,
                _ => DamageType.Physical,
            },
            skill.PowerFlat,
            skill.PhysCoeff,
            skill.MagCoeff,
            skill.HealCoeff,
            skill.ManaCost,
            skill.BaseCooldownSeconds,
            skill.CastWindupSeconds,
            ExtractTagIds(skill.RuleModifierTags),
            skill.HealthCoeff,
            skill.CanCrit,
            (SkillDelivery)skill.Delivery,
            (SkillTargetRule)skill.TargetRule,
            ExtractTagIds(skill.SupportAllowedTags),
            ExtractTagIds(skill.SupportBlockedTags),
            ExtractTagIds(skill.RequiredWeaponTags),
            ExtractTagIds(skill.RequiredClassTags),
            Enumerate(skill.AppliedStatuses)
                .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.StatusId))
                .Select(rule => new StatusApplicationSpec(
                    string.IsNullOrWhiteSpace(rule.Id) ? $"{skill.Id}:{rule.StatusId}" : rule.Id,
                    rule.StatusId,
                    rule.DurationSeconds,
                    rule.Magnitude,
                    Math.Max(1, rule.MaxStacks),
                    rule.RefreshDurationOnReapply))
                .ToList(),
            skill.CleanseProfileId ?? string.Empty);
    }

    private static CampaignChapterTemplate BuildCampaignChapterTemplate(CampaignChapterDefinition definition)
    {
        return new CampaignChapterTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.StoryOrder,
            Enumerate(definition.SiteIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            definition.UnlocksEndlessOnClear);
    }

    private static ExpeditionSiteTemplate BuildExpeditionSiteTemplate(ExpeditionSiteDefinition definition)
    {
        return new ExpeditionSiteTemplate(
            definition.Id,
            definition.ChapterId,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.SiteOrder,
            definition.FactionId,
            Enumerate(definition.EncounterIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            definition.ExtractRewardSourceId,
            (int)definition.ThreatTier);
    }

    private static EncounterTemplate BuildEncounterTemplate(EncounterDefinition definition)
    {
        return new EncounterTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.SiteId,
            definition.EnemySquadTemplateId,
            definition.BossOverlayId,
            definition.RewardSourceId,
            definition.FactionId,
            (int)definition.ThreatTier,
            Math.Max(1, definition.ThreatCost),
            Math.Max(1, definition.ThreatSkulls),
            definition.DifficultyBand,
            definition.Kind,
            Enumerate(definition.RewardDropTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList());
    }

    private static EnemySquadTemplate BuildEnemySquadTemplate(EnemySquadTemplateDefinition definition)
    {
        return new EnemySquadTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.FactionId,
            (TeamPostureType)definition.EnemyPosture,
            (int)definition.ThreatTier,
            Math.Max(1, definition.ThreatCost),
            Enumerate(definition.RewardDropTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Enumerate(definition.Members)
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.ArchetypeId))
                .Select(member => new EnemySquadMemberTemplate(
                    string.IsNullOrWhiteSpace(member.Id) ? $"{definition.Id}:{member.ArchetypeId}:{member.Anchor}" : member.Id,
                    ResolveLegacyName(member.NameKey, member.LegacyDisplayName, member.ArchetypeId),
                    member.ArchetypeId,
                    (DeploymentAnchorId)member.Anchor,
                    member.PositiveTraitId,
                    member.NegativeTraitId,
                    member.Role,
                    Enumerate(member.RuleModifierTags)
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Distinct(StringComparer.Ordinal)
                        .ToList()))
                .ToList());
    }

    private static BossOverlayTemplate BuildBossOverlayTemplate(BossOverlayDefinition definition)
    {
        return new BossOverlayTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.PhaseTrigger,
            Math.Max(1, definition.ThreatCost),
            definition.SignatureAuraTag,
            definition.SignatureUtilityTag,
            Enumerate(definition.RewardDropTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Enumerate(definition.AppliedStatuses)
                .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.StatusId))
                .Select(rule => new StatusApplicationSpec(
                    string.IsNullOrWhiteSpace(rule.Id) ? $"{definition.Id}:{rule.StatusId}" : rule.Id,
                    rule.StatusId,
                    rule.DurationSeconds,
                    rule.Magnitude,
                    Math.Max(1, rule.MaxStacks),
                    rule.RefreshDurationOnReapply))
                .ToList());
    }

    private static StatusFamilyTemplate BuildStatusFamilyTemplate(StatusFamilyDefinition definition)
    {
        return new StatusFamilyTemplate(
            definition.Id,
            definition.Group,
            definition.IsHardControl,
            definition.UsesControlDiminishing,
            definition.AffectedByTenacity,
            definition.TenacityScale,
            definition.IsRuleModifierOnly,
            Enumerate(definition.CompileTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList());
    }

    private static CleanseProfileTemplate BuildCleanseProfileTemplate(CleanseProfileDefinition definition)
    {
        return new CleanseProfileTemplate(
            definition.Id,
            Enumerate(definition.RemovesStatusIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            definition.RemovesOneHardControl,
            definition.GrantsUnstoppable,
            definition.GrantedUnstoppableDurationSeconds);
    }

    private static ControlDiminishingTemplate BuildControlDiminishingTemplate(ControlDiminishingRuleDefinition definition)
    {
        return new ControlDiminishingTemplate(
            definition.Id,
            definition.ControlResistMultiplier,
            definition.WindowSeconds,
            Enumerate(definition.FullTenacityStatusIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Enumerate(definition.PartialTenacityStatusIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList());
    }

    private static RewardSourceTemplate BuildRewardSourceTemplate(RewardSourceDefinition definition)
    {
        return new RewardSourceTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.Kind,
            definition.DropTableId,
            definition.UsesRewardCards,
            Enumerate(definition.AllowedRarityBrackets)
                .Distinct()
                .ToList());
    }

    private static DropTableTemplate BuildDropTableTemplate(DropTableDefinition definition)
    {
        return new DropTableTemplate(
            definition.Id,
            definition.RewardSourceId,
            Enumerate(definition.Entries)
                .Where(entry => entry != null)
                .Select(BuildLootBundleEntryTemplate)
                .ToList());
    }

    private static LootBundleTemplate BuildLootBundleTemplate(LootBundleDefinition definition)
    {
        return new LootBundleTemplate(
            definition.Id,
            definition.RewardSourceId,
            Enumerate(definition.Entries)
                .Where(entry => entry != null)
                .Select(BuildLootBundleEntryTemplate)
                .ToList());
    }

    private static TraitTokenTemplate BuildTraitTokenTemplate(TraitTokenDefinition definition)
    {
        return new TraitTokenTemplate(
            definition.Id,
            definition.RewardType);
    }

    private static LootBundleEntryTemplate BuildLootBundleEntryTemplate(LootBundleEntryDefinition definition)
    {
        return new LootBundleEntryTemplate(
            string.IsNullOrWhiteSpace(definition.Id) ? $"{definition.RewardType}:{definition.RarityBracket}:{definition.Amount}" : definition.Id,
            definition.RewardType,
            Math.Max(1, definition.Amount),
            definition.RarityBracket,
            Math.Max(1, definition.Weight),
            definition.IsGuaranteed);
    }

    private static CombatModifierPackage BuildTraitPackage(TraitEntry trait)
    {
        return new CombatModifierPackage(
            trait.Id,
            ModifierSource.Trait,
            Enumerate(trait.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Trait, trait.Id)).ToList());
    }

    private static CombatModifierPackage BuildItemPackage(ItemBaseDefinition item)
    {
        return new CombatModifierPackage(
            item.Id,
            ModifierSource.Item,
            Enumerate(item.BaseModifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Item, item.Id)).ToList());
    }

    private static CombatModifierPackage BuildAffixPackage(AffixDefinition affix)
    {
        return new CombatModifierPackage(
            affix.Id,
            ModifierSource.Item,
            Enumerate(affix.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Item, affix.Id)).ToList());
    }

    private static CombatModifierPackage BuildAugmentPackage(AugmentDefinition augment)
    {
        return new CombatModifierPackage(
            augment.Id,
            ModifierSource.Augment,
            Enumerate(augment.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Augment, augment.Id)).ToList());
    }

    private static TeamTacticTemplate BuildTeamTacticTemplate(TeamTacticDefinition definition)
    {
        return new TeamTacticTemplate(
            definition.Id,
            new TeamTacticProfile(
                definition.Id,
                ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
                (TeamPostureType)definition.Posture,
                definition.CombatPace,
                definition.FocusModeBias,
                definition.FrontSpacingBias,
                definition.BackSpacingBias,
                definition.ProtectCarryBias,
                definition.TargetSwitchPenalty));
    }

    private static RoleInstructionTemplate BuildRoleInstructionTemplate(RoleInstructionDefinition definition)
    {
        return new RoleInstructionTemplate(
            definition.Id,
            new SlotRoleInstruction(
                (DeploymentAnchorId)definition.Anchor,
                definition.RoleTag,
                definition.ProtectCarryBias,
                definition.BacklinePressureBias,
                definition.RetreatBias));
    }

    private static PassiveNodeTemplate BuildPassiveNodeTemplate(PassiveNodeDefinition definition)
    {
        return new PassiveNodeTemplate(
            definition.Id,
            new CombatModifierPackage(
                definition.Id,
                ModifierSource.Other,
                Enumerate(definition.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Other, definition.Id)).ToList()),
            Enumerate(definition.CompileTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            BuildRulePackage(definition.Id, ModifierSource.Other, definition.RuleModifierTags));
    }

    private static AugmentCatalogEntry BuildAugmentCatalogEntry(AugmentDefinition definition)
    {
        return new AugmentCatalogEntry(
            definition.Id,
            definition.Category switch
            {
                AugmentCategoryValue.Synergy => "synergy",
                AugmentCategoryValue.EconomyLoot => "economy_loot",
                AugmentCategoryValue.RunUtility => "run_utility",
                _ => "combat",
            },
            string.IsNullOrWhiteSpace(definition.FamilyId) ? definition.Id : definition.FamilyId,
            Math.Max(1, definition.Tier),
            definition.IsPermanent,
            definition.SuppressIfPermanentEquipped,
            Enumerate(definition.Tags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            Enumerate(definition.MutualExclusionTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            BuildAugmentPackage(definition),
            BuildRulePackage(definition.Id, ModifierSource.Augment, definition.RuleModifierTags));
    }

    private static IEnumerable<SynergyTierTemplate> BuildSynergyTemplates(SynergyDefinition definition)
    {
        foreach (var tier in Enumerate(definition.Tiers).Where(tier => tier != null && tier.Threshold > 0))
        {
            yield return new SynergyTierTemplate(
                $"{definition.Id}:{tier.Threshold}",
                new TeamSynergyTierRule(
                    definition.Id,
                    definition.CountedTagId,
                    tier.Threshold,
                    tier.Modifiers.Select(modifier => BuildStatModifier(modifier, ModifierSource.Synergy, $"{definition.Id}:{tier.Threshold}")).ToList()));
        }
    }

    private static StatModifier BuildStatModifier(SerializableStatModifier modifier, ModifierSource source, string sourceId)
    {
        return new StatModifier(
            ToStatKey(modifier.StatId),
            modifier.Op,
            modifier.Value,
            source,
            sourceId);
    }

    private static StatKey ToStatKey(string statId)
    {
        if (StatKey.TryResolve(statId, out var key))
        {
            return key;
        }

        throw new InvalidOperationException($"알 수 없는 StatId '{statId}'");
    }

    private static Dictionary<StatKey, float> BuildBaseStats(UnitArchetypeDefinition definition)
    {
        return new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = definition.BaseMaxHealth,
            [StatKey.Armor] = PreferPrimaryOrFallback(definition.BaseArmor, definition.BaseDefense),
            [StatKey.Resist] = definition.BaseResist,
            [StatKey.BarrierPower] = definition.BaseBarrierPower,
            [StatKey.Tenacity] = definition.BaseTenacity,
            [StatKey.HealPower] = definition.BaseHealPower,
            [StatKey.PhysPower] = PreferPrimaryOrFallback(definition.BasePhysPower, definition.BaseAttack),
            [StatKey.MagPower] = definition.BaseMagPower,
            [StatKey.AttackSpeed] = PreferPrimaryOrFallback(definition.BaseAttackSpeed, definition.BaseSpeed),
            [StatKey.MoveSpeed] = definition.BaseMoveSpeed,
            [StatKey.AttackRange] = definition.BaseAttackRange,
            [StatKey.ManaMax] = definition.BaseManaMax,
            [StatKey.ManaGainOnAttack] = definition.BaseManaGainOnAttack,
            [StatKey.ManaGainOnHit] = definition.BaseManaGainOnHit,
            [StatKey.CooldownRecovery] = definition.BaseCooldownRecovery,
            [StatKey.CritChance] = definition.BaseCritChance,
            [StatKey.CritMultiplier] = definition.BaseCritMultiplier,
            [StatKey.PhysPen] = definition.BasePhysPen,
            [StatKey.MagPen] = definition.BaseMagPen,
            [StatKey.AggroRadius] = definition.BaseAggroRadius,
            [StatKey.LeashDistance] = definition.BaseLeashDistance,
            [StatKey.TargetSwitchDelay] = definition.BaseTargetSwitchDelay,
            [StatKey.PreferredDistance] = PreferPrimaryOrFallback(definition.BasePreferredDistance, definition.BaseAttackRange),
            [StatKey.ProtectRadius] = definition.BaseProtectRadius,
            [StatKey.AttackWindup] = definition.BaseAttackWindup,
            [StatKey.CastWindup] = definition.BaseCastWindup,
            [StatKey.ProjectileSpeed] = definition.BaseProjectileSpeed,
            [StatKey.CollisionRadius] = definition.BaseCollisionRadius,
            [StatKey.RepositionCooldown] = definition.BaseRepositionCooldown,
            [StatKey.AttackCooldown] = definition.BaseAttackCooldown,
        };
    }

    private static float PreferPrimaryOrFallback(float primary, float fallback)
    {
        return primary != 0f ? primary : fallback;
    }

    private static List<string> ExtractTagIds(IEnumerable<StableTagDefinition> tags)
    {
        return Enumerate(tags)
            .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
            .Select(tag => tag.Id)
            .ToList();
    }

    private static IEnumerable<T> Enumerate<T>(IEnumerable<T> values)
    {
        return values ?? Array.Empty<T>();
    }

    private static T BuildSection<T>(string label, Func<T> factory)
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to build {label}.", ex);
        }
    }

    private static string ResolveLegacyName(string nameKey, string legacyDisplayName, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(legacyDisplayName))
        {
            return legacyDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(nameKey))
        {
            return nameKey;
        }

        return fallback;
    }

    private static string ResolveTraitPoolArchetypeId(TraitPoolDefinition traitPool)
    {
        if (!string.IsNullOrWhiteSpace(traitPool.ArchetypeId))
        {
            return traitPool.ArchetypeId;
        }

        if (!string.IsNullOrWhiteSpace(traitPool.Id) && traitPool.Id.StartsWith("traitpool_", StringComparison.Ordinal))
        {
            return traitPool.Id["traitpool_".Length..];
        }

        if (!string.IsNullOrWhiteSpace(traitPool.name) && traitPool.name.StartsWith("traitpool_", StringComparison.Ordinal))
        {
            return traitPool.name["traitpool_".Length..];
        }

        return string.Empty;
    }

#if UNITY_EDITOR
    private static void RequireEditorCanonicalSampleContentReady()
    {
        var generatorType = Type.GetType("SM.Editor.SeedData.SampleSeedGenerator, SM.Editor");
        if (generatorType == null)
        {
            return;
        }

        var requireMethod = generatorType.GetMethod("RequireCanonicalSampleContentReady", BindingFlags.Public | BindingFlags.Static);
        if (requireMethod != null)
        {
            try
            {
                requireMethod.Invoke(null, new object[] { nameof(RuntimeCombatContentLookup) });
                return;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        var hasMinimumMethod = generatorType.GetMethod("HasCanonicalMinimumContent", BindingFlags.Public | BindingFlags.Static);
        if (hasMinimumMethod == null)
        {
            return;
        }

        if (hasMinimumMethod.Invoke(null, null) is true)
        {
            return;
        }

        throw new InvalidOperationException(
            "SM canonical sample content is not preflight-ready for RuntimeCombatContentLookup. " +
            "Run 'pwsh -File tools/unity-bridge.ps1 seed-content' or Unity menu 'SM/Seed/Generate Sample Content' before using editor-side runtime lookup.");
    }
#endif

    private static CombatRuleModifierPackage? BuildRulePackage(
        string sourceId,
        ModifierSource source,
        IEnumerable<StableTagDefinition> tags)
    {
        var modifiers = tags
            .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
            .Select(tag => new RuleModifier(RuleModifierKind.BehaviorTag, tag.Id))
            .ToList();
        return modifiers.Count == 0
            ? null
            : new CombatRuleModifierPackage(sourceId, source, modifiers);
    }
}
