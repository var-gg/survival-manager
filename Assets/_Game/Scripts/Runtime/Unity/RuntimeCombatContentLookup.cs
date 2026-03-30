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

        _snapshot = new CombatContentSnapshot(
            _archetypeDefinitions.Values.ToDictionary(definition => definition.Id, BuildArchetypeTemplate, StringComparer.Ordinal),
            _traitPools.Values
                .SelectMany(pool => pool.PositiveTraits.Concat(pool.NegativeTraits))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                .ToDictionary(entry => entry.Id, entry => BuildTraitPackage(entry), StringComparer.Ordinal),
            _itemDefinitions.Values.ToDictionary(item => item.Id, item => BuildItemPackage(item), StringComparer.Ordinal),
            _affixDefinitions.Values.ToDictionary(affix => affix.Id, affix => BuildAffixPackage(affix), StringComparer.Ordinal),
            _augmentDefinitions.Values.ToDictionary(augment => augment.Id, augment => BuildAugmentPackage(augment), StringComparer.Ordinal));
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
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = definition.BaseMaxHealth,
                [StatKey.Attack] = definition.BaseAttack,
                [StatKey.Defense] = definition.BaseDefense,
                [StatKey.Speed] = definition.BaseSpeed,
                [StatKey.HealPower] = definition.BaseHealPower,
                [StatKey.MoveSpeed] = definition.BaseMoveSpeed,
                [StatKey.AttackRange] = definition.BaseAttackRange,
                [StatKey.AggroRadius] = definition.BaseAggroRadius,
                [StatKey.AttackWindup] = definition.BaseAttackWindup,
                [StatKey.AttackCooldown] = definition.BaseAttackCooldown,
                [StatKey.LeashDistance] = definition.BaseLeashDistance,
                [StatKey.TargetSwitchDelay] = definition.BaseTargetSwitchDelay
            },
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
                .Select(skill => new SkillDefinition(
                    skill.Id,
                    skill.DisplayName,
                    (SkillKind)skill.Kind,
                    skill.Power,
                    skill.Range))
                .ToList());
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
        return statId switch
        {
            "max_health" => StatKey.MaxHealth,
            "attack" => StatKey.Attack,
            "defense" => StatKey.Defense,
            "speed" => StatKey.Speed,
            "heal_power" => StatKey.HealPower,
            "move_speed" => StatKey.MoveSpeed,
            "attack_range" => StatKey.AttackRange,
            "aggro_radius" => StatKey.AggroRadius,
            "attack_windup" => StatKey.AttackWindup,
            "attack_cooldown" => StatKey.AttackCooldown,
            "leash_distance" => StatKey.LeashDistance,
            "target_switch_delay" => StatKey.TargetSwitchDelay,
            _ => throw new InvalidOperationException($"알 수 없는 StatId '{statId}'")
        };
    }
}
