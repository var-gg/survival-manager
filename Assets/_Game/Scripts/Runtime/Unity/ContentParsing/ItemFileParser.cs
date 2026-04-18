using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class ItemFileParser
{
    internal static IReadOnlyList<ItemBaseDefinition> LoadItems(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Items", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildItemNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildItemDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.SlotType = (ItemSlotType)ExtractInt(lines, "SlotType:");
            definition.IdentityKind = (ItemIdentityValue)ExtractInt(lines, "IdentityKind:");
            definition.ItemFamilyTag = ExtractValue(lines, "ItemFamilyTag:");
            definition.WeaponFamilyTag = ExtractValue(lines, "WeaponFamilyTag:");
            definition.RarityTier = (ItemRarityTierValue)ExtractInt(lines, "RarityTier:");
            definition.AffixPoolTag = ExtractValue(lines, "AffixPoolTag:");
            definition.CraftCategory = ExtractValue(lines, "CraftCategory:");
            definition.BudgetBand = ExtractValue(lines, "BudgetBand:");
            definition.CraftCurrencyTag = ExtractValue(lines, "CraftCurrencyTag:");
            definition.GrantedSkills = ParseReferenceList(lines, "GrantedSkills:", guidToPath, skills);
            definition.CompileTags = ParseReferenceList(lines, "CompileTags:", guidToPath, stableTags);
            definition.RuleModifierTags = ParseReferenceList(lines, "RuleModifierTags:", guidToPath, stableTags);
            definition.AllowedClassTags = ParseReferenceList(lines, "AllowedClassTags:", guidToPath, stableTags);
            definition.AllowedArchetypeTags = ParseReferenceList(lines, "AllowedArchetypeTags:", guidToPath, stableTags);
            definition.UniqueRuleTags = ParseReferenceList(lines, "UniqueRuleTags:", guidToPath, stableTags);
            definition.AllowedCraftOperations = ParsePackedEnumList<CraftOperationKindValue>(ExtractValue(lines, "AllowedCraftOperations:"));
            definition.BaseModifiers = ParseModifiers(lines, "BaseModifiers:");
            return definition;
        }, guidToPath).Values.ToList();
    }

    internal static IReadOnlyList<AffixDefinition> LoadAffixes(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Affixes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<AffixDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = $"content.affix.{ContentLocalizationTables.NormalizeId(definition.Id)}.name";
            definition.DescriptionKey = $"content.affix.{ContentLocalizationTables.NormalizeId(definition.Id)}.desc";
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            definition.Category = (AffixCategoryValue)ExtractInt(lines, "Category:");
            definition.Tier = (AffixTierValue)ExtractInt(lines, "Tier:");
            definition.AffixFamily = (AffixFamilyValue)ExtractInt(lines, "AffixFamily:");
            definition.EffectType = (AffixEffectTypeValue)ExtractInt(lines, "EffectType:");
            definition.ValueMin = ExtractFloat(lines, "ValueMin:");
            definition.ValueMax = ExtractFloat(lines, "ValueMax:");
            definition.AllowedSlotTypes = ParsePackedEnumList<ItemSlotType>(ExtractValue(lines, "AllowedSlotTypes:"));
            definition.CompileTags = ParseReferenceList(lines, "CompileTags:", guidToPath, stableTags);
            definition.RuleModifierTags = ParseReferenceList(lines, "RuleModifierTags:", guidToPath, stableTags);
            definition.RequiredTags = ParseReferenceList(lines, "RequiredTags:", guidToPath, stableTags);
            definition.ExcludedTags = ParseReferenceList(lines, "ExcludedTags:", guidToPath, stableTags);
            definition.ItemLevelMin = ExtractInt(lines, "ItemLevelMin:");
            definition.SpawnWeight = ExtractFloat(lines, "SpawnWeight:");
            definition.ExclusiveGroupId = ExtractValue(lines, "ExclusiveGroupId:");
            definition.BudgetScore = ExtractFloat(lines, "BudgetScore:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.TextTemplateKey = ExtractValue(lines, "TextTemplateKey:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            ApplyFallbackIdentity(definition, path);
            ApplyAffixFallbacks(definition);
            return definition;
        }, guidToPath).Values.ToList();
    }

    internal static IReadOnlyList<AugmentDefinition> LoadAugments(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Augments", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<AugmentDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildAugmentNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildAugmentDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            definition.Rarity = ExtractInt(lines, "Rarity:") switch
            {
                1 => ContentRarity.Rare,
                2 => ContentRarity.Epic,
                3 => ContentRarity.Epic,
                _ => ContentRarity.Common,
            };
            definition.IsPermanent = ExtractInt(lines, "IsPermanent:") != 0;
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.Category = (AugmentCategoryValue)ExtractInt(lines, "Category:");
            definition.FamilyId = ExtractValue(lines, "FamilyId:");
            definition.Tier = ExtractInt(lines, "Tier:");
            definition.OfferWeight = ExtractFloat(lines, "OfferWeight:");
            definition.OfferBucket = (AugmentOfferBucketValue)ExtractInt(lines, "OfferBucket:");
            definition.RiskRewardClass = (AugmentRiskRewardClassValue)ExtractInt(lines, "RiskRewardClass:");
            definition.BudgetScore = ExtractFloat(lines, "BudgetScore:");
            definition.SuppressIfPermanentEquipped = ExtractBool(lines, "SuppressIfPermanentEquipped:");
            definition.Tags = ParseReferenceList(lines, "Tags:", guidToPath, stableTags);
            definition.BuildBiasTags = ParseReferenceList(lines, "BuildBiasTags:", guidToPath, stableTags);
            definition.ProtectionTags = ParseReferenceList(lines, "ProtectionTags:", guidToPath, stableTags);
            definition.MutualExclusionTags = ParseReferenceList(lines, "MutualExclusionTags:", guidToPath, stableTags);
            definition.RequiresTags = ParseReferenceList(lines, "RequiresTags:", guidToPath, stableTags);
            definition.RuleModifierTags = ParseReferenceList(lines, "RuleModifierTags:", guidToPath, stableTags);
            definition.EligibleModes = (AugmentEligibleModeValue)ExtractInt(lines, "EligibleModes:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.RosterSlotDelta = ExtractInt(lines, "RosterSlotDelta:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            ApplyFallbackIdentity(definition, path);
            ApplyAugmentFallbacks(definition);
            return definition;
        }, guidToPath).Values.ToList();
    }

    internal static Dictionary<string, StableTagDefinition> LoadStableTags()
    {
        return RuntimeCombatContentFileParser.LoadAssets("StableTags", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<StableTagDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            ApplyFallbackIdentity(definition, path);
            return definition;
        });
    }

    internal static void ApplyAffixFallbacks(AffixDefinition definition)
    {
        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            return;
        }

        var vector = definition.Category switch
        {
            AffixCategoryValue.OffenseFlat => MakeBudgetVector(3, 2, 0, 0, 0, 0, 0, 1),
            AffixCategoryValue.DefenseFlat => MakeBudgetVector(0, 0, 5, 0, 0, 0, 0, 1),
            AffixCategoryValue.DefenseScaling => MakeBudgetVector(0, 0, 4, 0, 0, 1, 0, 1),
            _ => MakeBudgetVector(0, 0, 1, 0, 1, 3, 0, 1),
        };
        AdjustBudgetFinalScore(vector, LoopCContentGovernance.AffixBudgetTargets[ContentRarity.Common].Target);
        definition.BudgetCard = BuildBudgetCard(
            BudgetDomain.Affix,
            ContentRarity.Common,
            PowerBand.Standard,
            CombatRoleBudgetProfile.None,
            vector,
            1,
            0,
            0,
            Array.Empty<ThreatPattern>(),
            Array.Empty<CounterToolContribution>());
    }

    internal static void ApplyAugmentFallbacks(AugmentDefinition definition)
    {
        definition.FamilyId = Coalesce(definition.FamilyId, definition.Id);
        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            var budgetTarget = LoopCContentGovernance.AugmentBudgetTargets[definition.Rarity];
            var caps = LoopCContentGovernance.RarityComplexityCaps[definition.Rarity];
            definition.BudgetCard.Rarity = definition.Rarity;
            definition.BudgetCard.PowerBand = definition.Rarity switch
            {
                ContentRarity.Common => PowerBand.Major,
                ContentRarity.Rare => PowerBand.Signature,
                _ => PowerBand.Keystone,
            };
            definition.BudgetCard.KeywordCount = Math.Min(definition.BudgetCard.KeywordCount, caps.KeywordCount);
            definition.BudgetCard.ConditionClauseCount = Math.Min(definition.BudgetCard.ConditionClauseCount, caps.ConditionClauseCount);
            definition.BudgetCard.RuleExceptionCount = Math.Min(definition.BudgetCard.RuleExceptionCount, caps.RuleExceptionCount);
            if (Math.Abs(definition.BudgetCard.Vector.FinalScore - budgetTarget.Target) > budgetTarget.Tolerance)
            {
                AdjustBudgetFinalScore(definition.BudgetCard.Vector, budgetTarget.Target);
            }

            return;
        }

        var target = LoopCContentGovernance.AugmentBudgetTargets[definition.Rarity].Target;
        var band = definition.Rarity switch
        {
            ContentRarity.Common => PowerBand.Major,
            ContentRarity.Rare => PowerBand.Signature,
            _ => PowerBand.Keystone,
        };
        var statId = definition.Modifiers.FirstOrDefault()?.StatId ?? string.Empty;
        var vector = statId switch
        {
            "armor" or "max_health" or "resist" => MakeBudgetVector(0, 0, target / 2, 0, 0, target / 6, 0, target / 3),
            "heal_power" => MakeBudgetVector(0, 0, target / 6, 0, 0, target / 2, 0, target / 3),
            "attack_speed" => MakeBudgetVector(target / 3, target / 6, 0, 0, target / 6, 0, 0, target / 3),
            _ => MakeBudgetVector(target / 3, target / 3, 0, 0, 0, 0, 0, target / 3),
        };
        AdjustBudgetFinalScore(vector, target);
        definition.BudgetCard = BuildBudgetCard(
            BudgetDomain.Augment,
            definition.Rarity,
            band,
            CombatRoleBudgetProfile.None,
            vector,
            definition.Rarity == ContentRarity.Common ? 2 : definition.Rarity == ContentRarity.Rare ? 3 : 4,
            definition.Rarity == ContentRarity.Common ? 1 : 2,
            definition.Rarity == ContentRarity.Epic ? 1 : 0,
            Array.Empty<ThreatPattern>(),
            Array.Empty<CounterToolContribution>());
    }
}
