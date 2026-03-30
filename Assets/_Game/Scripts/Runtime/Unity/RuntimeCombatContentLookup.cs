using System;
using System.Collections.Generic;
using System.Linq;
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
        "hexer"
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
    private readonly Dictionary<string, ItemBaseDefinition> _itemDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AffixDefinition> _affixDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AugmentDefinition> _augmentDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillDefinitionAsset> _skillDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TeamTacticDefinition> _teamTacticDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoleInstructionDefinition> _roleInstructionDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PassiveNodeDefinition> _passiveNodeDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SynergyDefinition> _synergyDefinitions = new(StringComparer.Ordinal);
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
        var archetypes = LoadDefinitions<UnitArchetypeDefinition>("_Game/Content/Definitions/Archetypes", "Assets/Resources/_Game/Content/Definitions/Archetypes");
        var items = LoadDefinitions<ItemBaseDefinition>("_Game/Content/Definitions/Items", "Assets/Resources/_Game/Content/Definitions/Items");
        var affixes = LoadDefinitions<AffixDefinition>("_Game/Content/Definitions/Affixes", "Assets/Resources/_Game/Content/Definitions/Affixes");
        var augments = LoadDefinitions<AugmentDefinition>("_Game/Content/Definitions/Augments", "Assets/Resources/_Game/Content/Definitions/Augments");
        var skills = LoadDefinitions<SkillDefinitionAsset>("_Game/Content/Definitions/Skills", "Assets/Resources/_Game/Content/Definitions/Skills");
        var teamTactics = LoadDefinitions<TeamTacticDefinition>("_Game/Content/Definitions/TeamTactics", "Assets/Resources/_Game/Content/Definitions/TeamTactics");
        var roleInstructions = LoadDefinitions<RoleInstructionDefinition>("_Game/Content/Definitions/RoleInstructions", "Assets/Resources/_Game/Content/Definitions/RoleInstructions");
        var passiveNodes = LoadDefinitions<PassiveNodeDefinition>("_Game/Content/Definitions/PassiveNodes", "Assets/Resources/_Game/Content/Definitions/PassiveNodes");
        var synergies = LoadDefinitions<SynergyDefinition>("_Game/Content/Definitions/Synergies", "Assets/Resources/_Game/Content/Definitions/Synergies");

        if (archetypes.Length == 0)
        {
            if (!RuntimeCombatContentFileParser.TryLoad(out var parsed, out var parseError))
            {
                throw new InvalidOperationException($"전투 archetype 정의를 Resources에서 찾을 수 없습니다. {parseError}");
            }

            archetypes = parsed.Archetypes.ToArray();
            items = parsed.Items.ToArray();
            affixes = parsed.Affixes.ToArray();
            augments = parsed.Augments.ToArray();
        }

        _archetypeDefinitions.Clear();
        _traitPools.Clear();
        _itemDefinitions.Clear();
        _affixDefinitions.Clear();
        _augmentDefinitions.Clear();
        _skillDefinitions.Clear();
        _teamTacticDefinitions.Clear();
        _roleInstructionDefinitions.Clear();
        _passiveNodeDefinitions.Clear();
        _synergyDefinitions.Clear();

        foreach (var archetype in archetypes.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)))
        {
            _archetypeDefinitions[archetype.Id] = archetype;
            if (archetype.TraitPool != null && !string.IsNullOrWhiteSpace(archetype.TraitPool.ArchetypeId))
            {
                _traitPools[archetype.TraitPool.ArchetypeId] = archetype.TraitPool;
            }
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

        _snapshot = new CombatContentSnapshot(
            _archetypeDefinitions.Values.ToDictionary(definition => definition.Id, BuildArchetypeTemplate, StringComparer.Ordinal),
            _traitPools.Values
                .SelectMany(pool => pool.PositiveTraits.Concat(pool.NegativeTraits))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                .ToDictionary(entry => entry.Id, entry => BuildTraitPackage(entry), StringComparer.Ordinal),
            _itemDefinitions.Values.ToDictionary(item => item.Id, item => BuildItemPackage(item), StringComparer.Ordinal),
            _affixDefinitions.Values.ToDictionary(affix => affix.Id, affix => BuildAffixPackage(affix), StringComparer.Ordinal),
            _augmentDefinitions.Values.ToDictionary(augment => augment.Id, augment => BuildAugmentPackage(augment), StringComparer.Ordinal),
            _skillDefinitions.Values.ToDictionary(skill => skill.Id, skill => BuildSkillSpec(skill), StringComparer.Ordinal),
            _teamTacticDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildTeamTacticTemplate(definition), StringComparer.Ordinal),
            _roleInstructionDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildRoleInstructionTemplate(definition), StringComparer.Ordinal),
            _passiveNodeDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildPassiveNodeTemplate(definition), StringComparer.Ordinal),
            _augmentDefinitions.Values.ToDictionary(definition => definition.Id, definition => BuildAugmentCatalogEntry(definition), StringComparer.Ordinal),
            _synergyDefinitions.Values
                .SelectMany(definition => BuildSynergyTemplates(definition))
                .ToDictionary(template => template.Id, template => template, StringComparer.Ordinal),
            _itemDefinitions.Values.ToDictionary(
                definition => definition.Id,
                definition => (IReadOnlyList<BattleSkillSpec>)definition.GrantedSkills
                    .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                    .Select(BuildSkillSpec)
                    .ToList(),
                StringComparer.Ordinal));
    }

    private static T[] LoadDefinitions<T>(string resourcesPath, string editorFolderPath) where T : UnityEngine.Object
    {
        var fromResources = Resources.LoadAll<T>(resourcesPath);
        if (fromResources.Length > 0)
        {
            return fromResources;
        }

#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(editorFolderPath))
        {
            return Array.Empty<T>();
        }

        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { editorFolderPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadMainAssetAtPath)
            .OfType<T>()
            .ToArray();
#else
        return Array.Empty<T>();
#endif
    }

    private static CombatArchetypeTemplate BuildArchetypeTemplate(UnitArchetypeDefinition definition)
    {
        return new CombatArchetypeTemplate(
            definition.Id,
            definition.DisplayName,
            definition.Race.Id,
            definition.Class.Id,
            (DeploymentAnchorId)definition.DefaultAnchor,
            BuildBaseStats(definition),
            definition.TacticPreset
                .OrderBy(entry => entry.Priority)
                .Select(entry => new TacticRule(
                    entry.Priority,
                    (TacticConditionType)entry.ConditionType,
                    entry.Threshold,
                    (BattleActionType)entry.ActionType,
                    (TargetSelectorType)entry.TargetSelector,
                    entry.Skill == null ? null : entry.Skill.Id))
                .ToList(),
            definition.Skills
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                .Select(BuildSkillSpec)
                .ToList(),
            string.IsNullOrWhiteSpace(definition.RoleTag) ? "auto" : definition.RoleTag,
            PreferPrimaryOrFallback(definition.BasePreferredDistance, definition.BaseAttackRange),
            definition.BaseProtectRadius,
            new ManaEnvelope(
                definition.BaseManaMax,
                definition.BaseManaGainOnAttack,
                definition.BaseManaGainOnHit));
    }

    private static BattleSkillSpec BuildSkillSpec(SkillDefinitionAsset skill)
    {
        return new BattleSkillSpec(
            skill.Id,
            skill.DisplayName,
            (SkillKind)skill.Kind,
            skill.Power,
            skill.Range,
            skill.SlotKind switch
            {
                SkillSlotKindValue.UtilityActive => "utility_active",
                SkillSlotKindValue.Passive => "passive",
                SkillSlotKindValue.Support => "support",
                _ => "core_active",
            },
            skill.CompileTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
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
            skill.RuleModifierTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList());
    }

    private static CombatModifierPackage BuildTraitPackage(TraitEntry trait)
    {
        return new CombatModifierPackage(
            trait.Id,
            ModifierSource.Trait,
            trait.Modifiers.Select(modifier => BuildStatModifier(modifier, ModifierSource.Trait, trait.Id)).ToList());
    }

    private static CombatModifierPackage BuildItemPackage(ItemBaseDefinition item)
    {
        return new CombatModifierPackage(
            item.Id,
            ModifierSource.Item,
            item.BaseModifiers.Select(modifier => BuildStatModifier(modifier, ModifierSource.Item, item.Id)).ToList());
    }

    private static CombatModifierPackage BuildAffixPackage(AffixDefinition affix)
    {
        return new CombatModifierPackage(
            affix.Id,
            ModifierSource.Item,
            affix.Modifiers.Select(modifier => BuildStatModifier(modifier, ModifierSource.Item, affix.Id)).ToList());
    }

    private static CombatModifierPackage BuildAugmentPackage(AugmentDefinition augment)
    {
        return new CombatModifierPackage(
            augment.Id,
            ModifierSource.Augment,
            augment.Modifiers.Select(modifier => BuildStatModifier(modifier, ModifierSource.Augment, augment.Id)).ToList());
    }

    private static TeamTacticTemplate BuildTeamTacticTemplate(TeamTacticDefinition definition)
    {
        return new TeamTacticTemplate(
            definition.Id,
            new TeamTacticProfile(
                definition.Id,
                definition.DisplayName,
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
                definition.Modifiers.Select(modifier => BuildStatModifier(modifier, ModifierSource.Other, definition.Id)).ToList()),
            definition.CompileTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
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
            definition.Tags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            definition.MutualExclusionTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            BuildAugmentPackage(definition),
            BuildRulePackage(definition.Id, ModifierSource.Augment, definition.RuleModifierTags));
    }

    private static IEnumerable<SynergyTierTemplate> BuildSynergyTemplates(SynergyDefinition definition)
    {
        foreach (var tier in definition.Tiers.Where(tier => tier != null && tier.Threshold > 0))
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
