using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;

namespace SM.Editor.Validation;

internal static class LoopAContractValidator
{
    public static void ValidateArchetype(UnitArchetypeDefinition archetype, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        var signatureActive = archetype.Loadout?.SignatureActive
            ?? archetype.LockedSignatureActiveSkill
            ?? archetype.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.CoreActive);
        var flexActive = archetype.Loadout?.FlexActive
            ?? archetype.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.UtilityActive);
        if (signatureActive == null)
        {
            AddError(issues, "loop_a.loadout.signature_active", "Loop A loadout must resolve a signature active slot.", assetPath);
        }

        if (flexActive == null)
        {
            AddError(issues, "loop_a.loadout.flex_active", "Loop A loadout must resolve a flex active slot.", assetPath);
        }

        if (archetype.Loadout == null)
        {
            AddError(issues, "loop_a.loadout.missing", "Loop A archetypes must carry an explicit UnitLoadoutDefinition.", assetPath);
            return;
        }

        ValidateEffectDescriptors(
            issues,
            archetype.Id,
            AuthorityLayer.UnitKit,
            archetype.Loadout.BasicAttack?.Effects,
            assetPath,
            new[] { EffectScope.Self, EffectScope.CurrentTarget, EffectScope.GroundArea },
            allowedCapabilities: EffectCapability.DealDamage | EffectCapability.ApplyStatus | EffectCapability.HealOrBarrier | EffectCapability.Reposition);

        if (archetype.Loadout.MobilityReaction != null)
        {
            if (archetype.Loadout.MobilityReaction.ActivationModel != ActivationModel.Trigger)
            {
                AddError(issues, "loop_a.mobility.activation_model", "MobilityReaction must use Trigger activation.", assetPath);
            }

            if (archetype.Loadout.MobilityReaction.Lane != ActionLane.Reaction)
            {
                AddError(issues, "loop_a.mobility.lane", "MobilityReaction must occupy the Reaction lane.", assetPath);
            }
        }
    }

    public static void ValidateSkill(SkillDefinitionAsset skill, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateEffectDescriptors(
            issues,
            skill.Id,
            AuthorityLayer.Skill,
            skill.Effects,
            assetPath,
            new[] { EffectScope.Self, EffectScope.CurrentTarget, EffectScope.GroundArea, EffectScope.AlliedCombatants, EffectScope.EnemyCombatants },
            allowedCapabilities:
            EffectCapability.DealDamage |
            EffectCapability.HealOrBarrier |
            EffectCapability.ApplyStatus |
            EffectCapability.Reposition |
            EffectCapability.ModifyResource |
            EffectCapability.ModifyCooldown |
            EffectCapability.SpawnSummon |
            EffectCapability.SpawnDeployable);

        if (skill.SlotKind == SkillSlotKindValue.UtilityActive && skill.ActivationModel == ActivationModel.Energy)
        {
            AddError(issues, "loop_a.flex_active.energy", "Flex active skills cannot use Energy activation.", assetPath);
        }

        if (skill.SlotKind == SkillSlotKindValue.CoreActive && skill.ActivationModel == ActivationModel.Trigger)
        {
            AddWarning(issues, "loop_a.signature_active.legacy_alias", "Core active skill still carries legacy activation metadata; compile path overrides it to Energy for Loop A.", assetPath);
        }

        if (skill.AuthorityLayer != AuthorityLayer.Skill)
        {
            AddError(issues, "loop_a.skill.authority_layer", "SkillDefinitionAsset.AuthorityLayer must remain Skill.", assetPath);
        }

        if (skill.SummonProfile != null && skill.Effects.Any(effect => effect.AllowsPersistentSummonChain))
        {
            AddError(issues, "loop_a.summon_chain", "Persistent summon/deployable chain authoring is blocked in Loop A.", assetPath);
        }
    }

    public static void ValidateAffix(AffixDefinition affix, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateEffectDescriptors(
            issues,
            affix.Id,
            AuthorityLayer.Affix,
            affix.Effects,
            assetPath,
            new[] { EffectScope.Self, EffectScope.OwnerOwnedSummons },
            allowedCapabilities:
            EffectCapability.ModifyStats |
            EffectCapability.ApplyStatus |
            EffectCapability.ModifyResource |
            EffectCapability.ModifyCooldown |
            EffectCapability.GrantPassive);
    }

    public static void ValidateAugment(AugmentDefinition augment, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (augment.RosterSlotDelta != 0)
        {
            AddError(issues, "loop_a.augment.loadout_delta", "Augment cannot modify roster unit slot topology in Loop A.", assetPath);
        }

        if (augment.Effects.Any(effect => effect.AllowsPersistentSummonChain))
        {
            AddError(issues, "loop_a.augment.summon_chain", "Augment cannot authorize persistent summon chains in Loop A.", assetPath);
        }
    }

    public static void ValidateSynergy(SynergyDefinition synergy, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        foreach (var tier in synergy.Tiers.Where(tier => tier != null))
        {
            ValidateEffectDescriptors(
                issues,
                $"{synergy.Id}:{tier.Threshold}",
                AuthorityLayer.Synergy,
                tier.Effects,
                assetPath,
                new[] { EffectScope.AlliedRosterUnits, EffectScope.AlliedCombatants, EffectScope.EnemyCombatants },
                allowedCapabilities:
                EffectCapability.ModifyStats |
                EffectCapability.ApplyStatus |
                EffectCapability.GrantPassive |
                EffectCapability.ModifyCompositionPayoff);
        }
    }

    public static void ValidateStatusFamily(StatusFamilyDefinition statusFamily, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateEffectDescriptors(
            issues,
            statusFamily.Id,
            AuthorityLayer.Status,
            statusFamily.Effects,
            assetPath,
            new[] { EffectScope.Self, EffectScope.CurrentTarget, EffectScope.AlliedCombatants, EffectScope.EnemyCombatants },
            allowedCapabilities:
            EffectCapability.ApplyStatus |
            EffectCapability.ModifyStats |
            EffectCapability.ModifyCooldown |
            EffectCapability.ModifyResource);
    }

    private static void ValidateEffectDescriptors(
        ICollection<ContentValidationIssue> issues,
        string sourceId,
        AuthorityLayer expectedLayer,
        IEnumerable<EffectDescriptor>? effects,
        string assetPath,
        EffectScope[] allowedScopes,
        EffectCapability allowedCapabilities)
    {
        foreach (var effect in effects ?? Array.Empty<EffectDescriptor>())
        {
            if (effect.Layer != expectedLayer)
            {
                AddError(issues, "loop_a.effect.layer", $"Effect on '{sourceId}' must remain in authority layer '{expectedLayer}'.", assetPath);
            }

            if (!allowedScopes.Contains(effect.Scope))
            {
                AddError(issues, "loop_a.effect.scope", $"Effect on '{sourceId}' uses forbidden scope '{effect.Scope}' for layer '{expectedLayer}'.", assetPath);
            }

            if (allowedCapabilities != EffectCapability.None && (effect.Capabilities & ~allowedCapabilities) != 0)
            {
                AddError(issues, "loop_a.effect.capability", $"Effect on '{sourceId}' uses forbidden capability '{effect.Capabilities & ~allowedCapabilities}' for layer '{expectedLayer}'.", assetPath);
            }

            if (effect.LoadoutTopologyDelta != 0)
            {
                AddError(issues, "loop_a.effect.loadout_topology", $"Effect on '{sourceId}' cannot mutate loadout topology in Loop A.", assetPath);
            }
        }
    }

    private static void AddError(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath)
    {
        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, code, message, assetPath));
    }

    private static void AddWarning(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath)
    {
        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, code, message, assetPath));
    }
}
