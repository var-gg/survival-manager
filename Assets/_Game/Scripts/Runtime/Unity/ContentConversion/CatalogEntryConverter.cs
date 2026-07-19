using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using static SM.Unity.ContentConversion.ContentConversionShared;

namespace SM.Unity.ContentConversion;

internal static class CatalogEntryConverter
{
    internal static TeamTacticTemplate BuildTeamTacticTemplate(TeamTacticDefinition definition)
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
                definition.TargetSwitchPenalty,
                definition.Compactness,
                definition.Width,
                definition.Depth,
                definition.LineSpacing,
                definition.FlankBias));
    }

    internal static RoleInstructionTemplate BuildRoleInstructionTemplate(RoleInstructionDefinition definition)
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

    internal static CharacterTemplate BuildCharacterTemplate(CharacterDefinition definition)
    {
        return new CharacterTemplate(
            definition.Id,
            definition.Race != null ? definition.Race.Id : string.Empty,
            definition.Class != null ? definition.Class.Id : string.Empty,
            definition.DefaultArchetype != null ? definition.DefaultArchetype.Id : string.Empty,
            definition.DefaultRoleInstruction != null ? definition.DefaultRoleInstruction.Id : string.Empty,
            definition.DominantHand);
    }

    internal static PassiveNodeTemplate BuildPassiveNodeTemplate(PassiveNodeDefinition definition)
    {
        return new PassiveNodeTemplate(
            definition.Id,
            new CombatModifierPackage(
                definition.Id,
                ModifierSource.Other,
                Enumerate(definition.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Other, definition.Id)).ToList()),
            Enumerate(definition.CompileTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            BuildRulePackage(definition.Id, ModifierSource.Other, definition.RuleModifierTags),
            definition.BoardId,
            definition.BoardDepth,
            definition.NodeKind,
            definition.PrerequisiteNodeIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
            Enumerate(definition.MutualExclusionTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            definition.GrantedSkillId ?? string.Empty);
    }

    internal static ItemTemplate BuildItemTemplate(ItemBaseDefinition definition)
    {
        return new ItemTemplate(
            definition.Id,
            Enumerate(definition.CompileTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            definition.WeaponFamilyTag ?? string.Empty,
            definition.SlotType.ToString(),
            Enumerate(definition.AllowedClassTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            definition.RarityTier,
            definition.IdentityKind);
    }

    internal static AffixTemplate BuildAffixTemplate(AffixDefinition definition)
    {
        return new AffixTemplate(
            definition.Id,
            Enumerate(definition.CompileTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            Enumerate(definition.RequiredTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            Enumerate(definition.ExcludedTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList(),
            BuildRulePackage(definition.Id, ModifierSource.Item, definition.RuleModifierTags),
            Enumerate(definition.AllowedSlotTypes).Select(slot => slot.ToString()).ToList(),
            definition.BudgetScore,
            definition.SpawnWeight,
            definition.Tier.ToString(),
            definition.ItemLevelMin,
            definition.ExclusiveGroupId ?? string.Empty);
    }

    internal static AugmentCatalogEntry BuildAugmentCatalogEntry(AugmentDefinition definition)
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
            ModifierPackageConverter.BuildAugmentPackage(definition),
            BuildRulePackage(definition.Id, ModifierSource.Augment, definition.RuleModifierTags),
            BuildGovernanceSummary(definition.BudgetCard),
            BuildTriggeredEffects(definition),
            definition.OfferBucket.ToString(),
            definition.RiskRewardClass.ToString(),
            Enumerate(definition.BuildBiasTags).Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToList());
    }

    private static IReadOnlyList<CombatTriggeredEffect>? BuildTriggeredEffects(AugmentDefinition definition)
    {
        if (definition.TriggeredEffects == null || definition.TriggeredEffects.Count == 0)
        {
            return null;
        }

        return definition.TriggeredEffects
            .Where(spec => spec != null)
            .Select(spec => new CombatTriggeredEffect(
                definition.Id,
                spec.Trigger,
                spec.Op,
                spec.Scope,
                spec.Magnitude,
                spec.ThresholdRatio,
                spec.StatusId ?? string.Empty,
                spec.DurationSeconds,
                spec.MaxStacks <= 0 ? 1 : spec.MaxStacks))
            .ToList();
    }

    internal static IEnumerable<SynergyTierTemplate> BuildSynergyTemplates(SynergyDefinition definition)
    {
        foreach (var tier in Enumerate(definition.Tiers).Where(tier => tier != null && tier.Threshold > 0))
        {
            yield return new SynergyTierTemplate(
                $"{definition.Id}:{tier.Threshold}",
                new TeamSynergyTierRule(
                    definition.Id,
                    definition.CountedTagId,
                    tier.Threshold,
                    tier.Modifiers.Select(modifier => BuildStatModifier(modifier, ModifierSource.Synergy, $"{definition.Id}:{tier.Threshold}")).ToList()),
                BuildGovernanceSummary(tier.BudgetCard));
        }
    }
}
