using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Editor.Validation;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

public static class EquipmentContentV1Assetizer
{
    [MenuItem("SM/Internal/Content/Apply Equipment Content V1 Assetization")]
    public static void Apply()
    {
        var skills = LoadDefinitionsById<SkillDefinitionAsset>("Skills");
        var tags = LoadDefinitionsById<StableTagDefinition>("StableTags");
        var changed = 0;

        changed += ApplyItems(skills, tags);
        changed += ApplyAffixes(tags);
        changed += ApplyDropTables();
        changed += ApplyFirstPlayableSlice();

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        Debug.Log($"Equipment content V1 assetization applied. Changed assets={changed}.");
    }

    private static int ApplyItems(
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills,
        IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        var changed = 0;
        foreach (var item in LoadDefinitions<ItemBaseDefinition>("Items"))
        {
            var rarity = ResolveRarity(item.Id);
            var identity = ResolveIdentity(item.Id);
            item.RarityTier = rarity;
            item.IdentityKind = identity;
            item.CraftCurrencyTag = EquipmentContentV1Contract.RefitCurrencyTag;
            item.AllowedCraftOperations = new List<CraftOperationKindValue> { CraftOperationKindValue.Reforge };

            var familyTag = ResolveItemFamily(item);
            item.ItemFamilyTag = familyTag;
            item.WeaponFamilyTag = item.SlotType == ItemSlotType.Weapon ? ResolveWeaponFamily(item.Id, familyTag) : string.Empty;
            item.AffixPoolTag = string.IsNullOrWhiteSpace(item.AffixPoolTag) ? $"pool_{familyTag}" : item.AffixPoolTag;
            item.CraftCategory = ResolveCraftCategory(item.Id, item.SlotType, familyTag);
            item.CompileTags = ResolveItemCompileTags(item, tags);
            item.AllowedClassTags = ResolveItemAllowedClassTags(item, tags);

            if (EquipmentContentV1Contract.GrantedSkillByItemId.TryGetValue(item.Id, out var skillId)
                && skills.TryGetValue(skillId, out var skill))
            {
                item.GrantedSkills = new List<SkillDefinitionAsset> { skill };
            }
            else
            {
                item.GrantedSkills = new List<SkillDefinitionAsset>();
            }

            EditorUtility.SetDirty(item);
            changed++;
        }

        return changed;
    }

    private static int ApplyAffixes(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        var changed = 0;
        foreach (var affix in LoadDefinitions<AffixDefinition>("Affixes"))
        {
            if (!EquipmentContentV1Contract.AffixSpecsById.TryGetValue(affix.Id, out var spec))
            {
                continue;
            }

            affix.Tier = spec.Tier;
            affix.AffixFamily = spec.Family;
            affix.EffectType = spec.EffectType;
            affix.Category = spec.Category;
            affix.ValueMin = spec.ValueMin;
            affix.ValueMax = spec.ValueMax;
            affix.AllowedSlotTypes = spec.AllowedSlots.ToList();
            affix.CompileTags = ResolveTags(tags, spec.CompileTagIds);
            affix.RequiredTags = ResolveTags(tags, spec.RequiredTagIds);
            affix.RuleModifierTags = ResolveTags(tags, spec.RuleModifierTagIds);
            affix.ExcludedTags = affix.ExcludedTags.Where(IsValidTagReference).Distinct().ToList();
            affix.ItemLevelMin = spec.ItemLevelMin;
            affix.SpawnWeight = spec.SpawnWeight;
            affix.ExclusiveGroupId = spec.ExclusiveGroupId;
            affix.BudgetScore = spec.BudgetScore;
            var hasDrawback = spec.AdditionalModifiers?.Any(modifier => modifier.Value < 0f) == true;
            var hasTrigger = spec.TriggeredEffects is { Count: > 0 };
            var budgetTarget = LoopCContentGovernance.AffixBudgetTargets[spec.BudgetRarity].Target;
            affix.BudgetCard = new BudgetCard
            {
                Domain = BudgetDomain.Affix,
                Rarity = spec.BudgetRarity,
                PowerBand = spec.Family == AffixFamilyValue.BuildShaping ? PowerBand.Major : PowerBand.Minor,
                Vector = hasDrawback
                    ? new BudgetVector { Reliability = budgetTarget + 1, DrawbackCredit = 1 }
                    : new BudgetVector { Reliability = budgetTarget },
                KeywordCount = hasTrigger || spec.RuleModifierTagIds.Count > 0 ? 2 : 1,
                ConditionClauseCount = hasTrigger || spec.RuleModifierTagIds.Count > 0 ? 1 : 0,
            };
            affix.TextTemplateKey = spec.TextTemplateKey;
            affix.AuthorityLayer = AuthorityLayer.Affix;
            affix.Modifiers = new List<SerializableStatModifier>();
            if (!string.IsNullOrWhiteSpace(spec.StatId))
            {
                affix.Modifiers.Add(new SerializableStatModifier
                {
                    StatId = spec.StatId,
                    Op = spec.Operation,
                    Value = (spec.ValueMin + spec.ValueMax) * 0.5f,
                });
            }

            affix.Modifiers.AddRange((spec.AdditionalModifiers ?? Array.Empty<EquipmentAffixModifierV1Spec>())
                .Select(modifier => new SerializableStatModifier
                {
                    StatId = modifier.StatId,
                    Op = modifier.Operation,
                    Value = modifier.Value,
                }));
            affix.TriggeredEffects = (spec.TriggeredEffects ?? Array.Empty<EquipmentAffixTriggerV1Spec>())
                .Select(effect => new TriggeredEffectSpec
                {
                    Trigger = effect.Trigger,
                    Op = effect.Op,
                    Scope = effect.Scope,
                    Magnitude = effect.Magnitude,
                    ThresholdRatio = effect.ThresholdRatio,
                    StatusId = effect.StatusId,
                    DurationSeconds = effect.DurationSeconds,
                    MaxStacks = effect.MaxStacks,
                })
                .ToList();
            affix.Effects = new List<EffectDescriptor>
            {
                new()
                {
                    Layer = AuthorityLayer.Affix,
                    Scope = EffectScope.Self,
                    Capabilities = spec.Capabilities,
                }
            };

            EditorUtility.SetDirty(affix);
            changed++;
        }

        return changed;
    }

    private static int ApplyDropTables()
    {
        var changed = 0;
        foreach (var table in LoadDefinitions<DropTableDefinition>("DropTables"))
        {
            foreach (var entry in table.Entries)
            {
                entry.RewardType = InferRewardType(entry.Id);
            }

            if (EquipmentContentV1Contract.RequiredItemDropsByTable.TryGetValue(table.Id, out var requiredItemIds))
            {
                foreach (var itemId in requiredItemIds)
                {
                    EnsureItemDropEntry(table, itemId);
                }
            }

            EditorUtility.SetDirty(table);
            changed++;
        }

        return changed;
    }

    private static int ApplyFirstPlayableSlice()
    {
        var slice = LoadDefinitions<FirstPlayableSliceDefinitionAsset>("FirstPlayable").FirstOrDefault();
        if (slice == null)
        {
            return 0;
        }

        slice.AffixCap = EquipmentContentV1Contract.LiveAffixCount;
        slice.AffixIds = EquipmentContentV1Contract.LiveAffixOrder.ToList();

        foreach (var reservedId in EquipmentContentV1Contract.ReservedAffixIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!slice.ParkingLotContentIds.Contains(reservedId, StringComparer.Ordinal))
            {
                slice.ParkingLotContentIds.Add(reservedId);
            }
        }

        slice.ParkingLotContentIds = slice.ParkingLotContentIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && !slice.AffixIds.Contains(id, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        EditorUtility.SetDirty(slice);
        return 1;
    }

    private static ItemRarityTierValue ResolveRarity(string itemId)
    {
        if (EquipmentContentV1Contract.EpicItemIds.Contains(itemId))
        {
            return ItemRarityTierValue.Epic;
        }

        return EquipmentContentV1Contract.RareItemIds.Contains(itemId)
            ? ItemRarityTierValue.Rare
            : ItemRarityTierValue.Common;
    }

    private static ItemIdentityValue ResolveIdentity(string itemId)
    {
        if (EquipmentContentV1Contract.UniqueItemIds.Contains(itemId))
        {
            return ItemIdentityValue.Unique;
        }

        return EquipmentContentV1Contract.NamedItemIds.Contains(itemId)
            ? ItemIdentityValue.Named
            : ItemIdentityValue.Baseline;
    }

    private static RewardType InferRewardType(string entryId)
    {
        if (entryId.StartsWith("item_", StringComparison.Ordinal))
        {
            return RewardType.Item;
        }

        if (entryId.StartsWith("skill_", StringComparison.Ordinal))
        {
            return entryId.Contains("skirmish", StringComparison.Ordinal) || entryId == "skill_power_strike"
                ? RewardType.SkillShard
                : RewardType.SkillManual;
        }

        if (entryId.Contains("boss_sigil", StringComparison.Ordinal))
        {
            return RewardType.BossSigil;
        }

        if (entryId.Contains("echo_crystal", StringComparison.Ordinal))
        {
            return RewardType.EchoCrystal;
        }

        if (entryId.Contains("ember", StringComparison.Ordinal))
        {
            return RewardType.EmberDust;
        }

        if (entryId.Contains("trait_lock", StringComparison.Ordinal))
        {
            return RewardType.TraitLockToken;
        }

        if (entryId.Contains("trait_purge", StringComparison.Ordinal))
        {
            return RewardType.TraitPurgeToken;
        }

        if (entryId.Contains("echo", StringComparison.Ordinal) || entryId.Contains("reroll", StringComparison.Ordinal))
        {
            return RewardType.Echo;
        }

        return RewardType.Gold;
    }

    private static void EnsureItemDropEntry(DropTableDefinition table, string itemId)
    {
        var existing = table.Entries.FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
        if (existing != null)
        {
            existing.RewardType = RewardType.Item;
            existing.Amount = Math.Max(1, existing.Amount);
            existing.Weight = Math.Max(1, existing.Weight);
            return;
        }

        table.Entries.Add(new LootBundleEntryDefinition
        {
            Id = itemId,
            RewardType = RewardType.Item,
            Amount = 1,
            RarityBracket = table.Id switch
            {
                "drop_table_boss" => RarityBracketValue.Boss,
                "drop_table_elite" => RarityBracketValue.Elite,
                _ => RarityBracketValue.Advanced,
            },
            Weight = table.Id == "drop_table_boss" ? 2 : 3,
            IsGuaranteed = false,
            RequiredContextTags = new List<string>(),
        });
    }

    private static string ResolveItemFamily(ItemBaseDefinition item)
    {
        if (item.SlotType == ItemSlotType.Armor)
        {
            return string.IsNullOrWhiteSpace(item.ItemFamilyTag) ? "armor" : item.ItemFamilyTag;
        }

        if (item.SlotType == ItemSlotType.Accessory)
        {
            return string.IsNullOrWhiteSpace(item.ItemFamilyTag) ? "accessory" : item.ItemFamilyTag;
        }

        return ResolveWeaponFamily(item.Id, item.ItemFamilyTag);
    }

    private static string ResolveWeaponFamily(string itemId, string currentFamily)
    {
        if (!string.IsNullOrWhiteSpace(currentFamily)
            && currentFamily is "shield" or "bow" or "focus" or "blade" or "greatblade" or "polearm")
        {
            return currentFamily;
        }

        if (itemId.Contains("shield", StringComparison.Ordinal))
        {
            return "shield";
        }

        if (itemId.Contains("bow", StringComparison.Ordinal))
        {
            return "bow";
        }

        if (itemId.Contains("focus", StringComparison.Ordinal) || itemId.Contains("bead", StringComparison.Ordinal))
        {
            return "focus";
        }

        return "blade";
    }

    private static string ResolveCraftCategory(string itemId, ItemSlotType slotType, string familyTag)
    {
        if (itemId.Contains("warden", StringComparison.Ordinal)
            || itemId.Contains("guardian", StringComparison.Ordinal)
            || itemId.Contains("bulwark", StringComparison.Ordinal)
            || itemId.Contains("penitent", StringComparison.Ordinal)
            || familyTag.Contains("vanguard", StringComparison.Ordinal)
            || familyTag == "shield")
        {
            return "vanguard";
        }

        if (itemId.Contains("slayer", StringComparison.Ordinal)
            || itemId.Contains("raider", StringComparison.Ordinal)
            || itemId.Contains("reaver", StringComparison.Ordinal)
            || familyTag == "blade")
        {
            return "duelist";
        }

        if (itemId.Contains("hunter", StringComparison.Ordinal)
            || itemId.Contains("scout", StringComparison.Ordinal)
            || itemId.Contains("marksman", StringComparison.Ordinal)
            || itemId.Contains("wayfinder", StringComparison.Ordinal)
            || itemId.Contains("rift", StringComparison.Ordinal)
            || familyTag == "bow")
        {
            return "ranger";
        }

        if (itemId.Contains("priest", StringComparison.Ordinal)
            || itemId.Contains("hexer", StringComparison.Ordinal)
            || itemId.Contains("shaman", StringComparison.Ordinal)
            || itemId.Contains("cantor", StringComparison.Ordinal)
            || itemId.Contains("oath", StringComparison.Ordinal)
            || itemId.Contains("prayer", StringComparison.Ordinal)
            || familyTag == "focus")
        {
            return "mystic";
        }

        return slotType.ToString().ToLowerInvariant();
    }

    private static List<StableTagDefinition> ResolveItemCompileTags(
        ItemBaseDefinition item,
        IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.WeaponFamilyTag))
        {
            ids.Add(item.WeaponFamilyTag);
        }

        ids.Add(item.SlotType switch
        {
            ItemSlotType.Weapon when item.WeaponFamilyTag == "focus" => "magical",
            ItemSlotType.Weapon when item.WeaponFamilyTag == "bow" => "projectile",
            ItemSlotType.Weapon => "physical",
            ItemSlotType.Armor => "sustain",
            _ => "tempo",
        });

        ids.Add(item.CraftCategory);
        return ResolveTags(tags, ids);
    }

    private static List<StableTagDefinition> ResolveItemAllowedClassTags(
        ItemBaseDefinition item,
        IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        return item.CraftCategory is "vanguard" or "duelist" or "ranger" or "mystic"
            ? ResolveTags(tags, new[] { item.CraftCategory })
            : new List<StableTagDefinition>();
    }

    private static List<StableTagDefinition> ResolveTags(
        IReadOnlyDictionary<string, StableTagDefinition> tags,
        IEnumerable<string> ids)
    {
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => tags.TryGetValue(id, out var tag) ? tag : null)
            .Where(tag => tag != null)
            .Select(tag => tag!)
            .ToList();
    }

    private static bool IsValidTagReference(StableTagDefinition? tag)
    {
        return tag != null && !string.IsNullOrWhiteSpace(tag.Id);
    }

    private static IReadOnlyDictionary<string, T> LoadDefinitionsById<T>(string folder) where T : ScriptableObject
    {
        return LoadDefinitions<T>(folder)
            .Select(asset => new { Asset = asset, Id = ResolveId(asset) })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .ToDictionary(entry => entry.Id, entry => entry.Asset, StringComparer.Ordinal);
    }

    private static List<T> LoadDefinitions<T>(string folder) where T : ScriptableObject
    {
        var root = $"{SampleSeedGenerator.ResourcesRoot}/{folder}";
        if (!AssetDatabase.IsValidFolder(root))
        {
            return new List<T>();
        }

        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .Select(asset => asset!)
            .ToList();
    }

    private static string ResolveId(ScriptableObject asset)
    {
        return asset switch
        {
            SkillDefinitionAsset skill => skill.Id,
            StableTagDefinition tag => tag.Id,
            _ => string.Empty,
        };
    }
}
