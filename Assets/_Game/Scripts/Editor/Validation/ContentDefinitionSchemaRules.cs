using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;

namespace SM.Editor.Validation;

internal static class ContentValidationIssueFactory
{
    internal static void AddError(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath, string scope = "")
    {
        AddIssue(issues, ContentValidationSeverity.Error, code, message, assetPath, scope);
    }

    internal static void AddWarning(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath, string scope = "")
    {
        AddIssue(issues, ContentValidationSeverity.Warning, code, message, assetPath, scope);
    }

    internal static void AddIssue(
        ICollection<ContentValidationIssue> issues,
        ContentValidationSeverity severity,
        string code,
        string message,
        string assetPath,
        string scope = "")
    {
        issues.Add(new ContentValidationIssue(severity, code, message, assetPath, scope));
    }
}

internal sealed class ClassDefinitionSchemaRule : DefinitionSchemaRule<ClassDefinition>
{
    public ClassDefinitionSchemaRule()
        : base(nameof(ClassDefinition))
    {
    }

    protected override string GetCanonicalId(ClassDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        ClassDefinition definition,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (ContentValidationPolicyCatalog.TryGetCanonicalClassIdForRoleFamily(definition.Id, out var canonicalClassId)
            && !string.Equals(canonicalClassId, definition.Id, StringComparison.Ordinal))
        {
            ContentValidationIssueFactory.AddError(issues, "glossary.class_id", "Class canonical id must remain 'duelist'; 'Striker' is player-facing only.", assetPath, "ClassDefinition.Id");
        }
    }
}

internal sealed class CharacterDefinitionSchemaRule : DefinitionSchemaRule<CharacterDefinition>
{
    public CharacterDefinitionSchemaRule()
        : base(nameof(CharacterDefinition))
    {
    }

    protected override string GetCanonicalId(CharacterDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        CharacterDefinition definition,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(definition.LegacyDisplayName))
        {
            if (ContentLocalizationPolicy.TreatsLegacyTextAsError)
            {
                ContentValidationIssueFactory.AddError(issues, "localization.legacy_text", "Legacy localized prose remains in CharacterDefinition.LegacyDisplayName.", assetPath, "CharacterDefinition.LegacyDisplayName");
            }
            else
            {
                ContentValidationIssueFactory.AddWarning(issues, "localization.legacy_text", "Legacy localized prose remains in CharacterDefinition.LegacyDisplayName.", assetPath, "CharacterDefinition.LegacyDisplayName");
            }
        }

        if (definition.Race == null || definition.Class == null || definition.DefaultArchetype == null || definition.DefaultRoleInstruction == null)
        {
            ContentValidationIssueFactory.AddError(issues, "character.references", "Character is missing race/class/default archetype/default role references.", assetPath);
            return;
        }

        if (!string.Equals(definition.DefaultArchetype.Race?.Id, definition.Race.Id, StringComparison.Ordinal))
        {
            ContentValidationIssueFactory.AddError(issues, "character.race_mismatch", "Character race must match the default archetype race.", assetPath);
        }

        if (!string.Equals(definition.DefaultArchetype.Class?.Id, definition.Class.Id, StringComparison.Ordinal))
        {
            ContentValidationIssueFactory.AddError(issues, "character.class_mismatch", "Character class must match the default archetype class.", assetPath);
        }
    }
}

internal sealed class TraitPoolSchemaRule : DefinitionSchemaRule<TraitPoolDefinition>
{
    public TraitPoolSchemaRule()
        : base(nameof(TraitPoolDefinition))
    {
    }

    protected override string GetCanonicalId(TraitPoolDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        TraitPoolDefinition definition,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (definition.PositiveTraits.Count < 3 || definition.NegativeTraits.Count < 3)
        {
            ContentValidationIssueFactory.AddError(issues, "traitpool.structure", "Trait pool must keep the 3+3 positive/negative structure.", assetPath);
        }
    }
}

internal sealed class ExpeditionSchemaRule : DefinitionSchemaRule<ExpeditionDefinition>
{
    public ExpeditionSchemaRule()
        : base(nameof(ExpeditionDefinition))
    {
    }

    protected override string GetCanonicalId(ExpeditionDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        ExpeditionDefinition definition,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (definition.Nodes.Any(node => node.RewardTable == null))
        {
            ContentValidationIssueFactory.AddError(issues, "expedition.reward_table", "Expedition node is missing a reward table reference.", assetPath);
        }
    }
}

internal sealed class ArchetypeSchemaRule : DefinitionSchemaRule<UnitArchetypeDefinition>
{
    public ArchetypeSchemaRule()
        : base(nameof(UnitArchetypeDefinition))
    {
    }

    protected override string GetCanonicalId(UnitArchetypeDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        UnitArchetypeDefinition archetype,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (archetype.Race == null || archetype.Class == null || archetype.TraitPool == null)
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.references", "Archetype is missing race/class/trait pool references.", assetPath);
        }

        if (archetype.TacticPreset == null || archetype.TacticPreset.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.tactic_preset", "Archetype is missing a tactic preset.", assetPath);
        }

        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(archetype.ScopeKind, "Archetype scope", assetPath, issues);
        if (string.IsNullOrWhiteSpace(archetype.RoleFamilyTag))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.role_family", "Archetype is missing RoleFamilyTag.", assetPath);
        }
        else if (!ContentValidationPolicyCatalog.AllowedRoleFamilyTags.Contains(archetype.RoleFamilyTag))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.role_family", $"Archetype role family tag must be one of [{string.Join(", ", ContentValidationPolicyCatalog.AllowedRoleFamilyTags)}].", assetPath);
        }

        if (!string.IsNullOrWhiteSpace(archetype.Class?.Id)
            && ContentValidationPolicyCatalog.TryGetRoleFamilyForCanonicalClassId(archetype.Class.Id, out var expectedRoleFamily)
            && !string.Equals(expectedRoleFamily, archetype.Class.Id, StringComparison.Ordinal)
            && !string.Equals(archetype.RoleFamilyTag, expectedRoleFamily, StringComparison.Ordinal))
        {
            ContentValidationIssueFactory.AddError(issues, "glossary.duelist_role_family", "Duelist archetypes must expose the player-facing role family tag 'striker'.", assetPath);
        }

        if (string.IsNullOrWhiteSpace(archetype.PrimaryWeaponFamilyTag))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.weapon_family", "Archetype is missing PrimaryWeaponFamilyTag.", assetPath);
        }

        if (archetype.Skills.Count == 0
            && (archetype.Loadout == null || !archetype.Loadout.IsComplete()))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.skill_contract", "Archetype must resolve a Loop A loadout or provide legacy migration skills.", assetPath);
        }

        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, archetype.SupportModifierBiasTags, assetPath, "Archetype support modifier bias");
        if (!string.IsNullOrWhiteSpace(archetype.LockedAttackProfileId))
        {
            ContentDefinitionSchemaRuleSupport.ValidateCanonicalId(archetype.LockedAttackProfileId, assetPath, "UnitArchetypeDefinition.LockedAttackProfileId", issues);
        }

        if (!string.IsNullOrWhiteSpace(archetype.LockedAttackProfileTag))
        {
            ContentDefinitionSchemaRuleSupport.ValidateCanonicalId(archetype.LockedAttackProfileTag, assetPath, "UnitArchetypeDefinition.LockedAttackProfileTag", issues);
        }

        if (archetype.LockedSignatureActiveSkill != null)
        {
            if (archetype.LockedSignatureActiveSkill.SlotKind != SkillSlotKindValue.CoreActive)
            {
                ContentValidationIssueFactory.AddError(issues, "archetype.locked_signature_active", "Locked signature active must reference a core_active skill.", assetPath);
            }

            if (!archetype.Skills.Contains(archetype.LockedSignatureActiveSkill))
            {
                ContentValidationIssueFactory.AddError(issues, "archetype.locked_signature_active_ref", "Locked signature active must also exist in the 4-slot compiled skill list.", assetPath);
            }
        }

        if (archetype.LockedSignaturePassiveSkill != null)
        {
            if (archetype.LockedSignaturePassiveSkill.SlotKind != SkillSlotKindValue.Passive)
            {
                ContentValidationIssueFactory.AddError(issues, "archetype.locked_signature_passive", "Locked signature passive must reference a passive skill.", assetPath);
            }

            if (!archetype.Skills.Contains(archetype.LockedSignaturePassiveSkill))
            {
                ContentValidationIssueFactory.AddError(issues, "archetype.locked_signature_passive_ref", "Locked signature passive must also exist in the 4-slot compiled skill list.", assetPath);
            }
        }

        if (archetype.FlexUtilitySkillPool.Any(skill => skill == null))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.flex_utility_pool", "Flex utility pool contains a missing skill reference.", assetPath);
        }
        else if (archetype.FlexUtilitySkillPool.Any(skill => skill.SlotKind != SkillSlotKindValue.UtilityActive))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.flex_utility_pool", "Flex utility pool must contain only utility_active skills.", assetPath);
        }

        if (archetype.FlexSupportSkillPool.Any(skill => skill == null))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.flex_support_pool", "Flex support pool contains a missing skill reference.", assetPath);
        }
        else if (archetype.FlexSupportSkillPool.Any(skill => skill.SlotKind != SkillSlotKindValue.Support))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.flex_support_pool", "Flex support pool must contain only support slot skills.", assetPath);
        }

        LoopAContractValidator.ValidateArchetype(archetype, assetPath, issues);
        LoopBContractValidator.ValidateArchetype(archetype, assetPath, issues);
    }
}

internal sealed class SkillSchemaRule : DefinitionSchemaRule<SkillDefinitionAsset>
{
    public SkillSchemaRule()
        : base(nameof(SkillDefinitionAsset))
    {
    }

    protected override string GetCanonicalId(SkillDefinitionAsset asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        SkillDefinitionAsset skill,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(skill.TemplateType, "Skill template type", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(skill.Kind, "Skill kind", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(skill.SlotKind, "Skill slot kind", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(skill.DamageType, "Skill damage type", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(skill.Delivery, "Skill delivery", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(skill.TargetRule, "Skill target rule", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(skill.LearnSource, "Skill learn source", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, skill.CompileTags, assetPath, "Skill compile");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, skill.RuleModifierTags, assetPath, "Skill rule modifier");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, skill.SupportAllowedTags, assetPath, "Skill support allowed");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, skill.SupportBlockedTags, assetPath, "Skill support blocked");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, skill.RequiredWeaponTags, assetPath, "Skill required weapon");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, skill.RequiredClassTags, assetPath, "Skill required class");

        if (skill.RangeMin < 0f)
        {
            ContentValidationIssueFactory.AddError(issues, "skill.range_band", "Skill RangeMin must be non-negative.", assetPath);
        }

        var resolvedRangeMax = skill.RangeMax >= 0f ? skill.RangeMax : skill.Range;
        if (resolvedRangeMax < Math.Max(0f, skill.RangeMin))
        {
            ContentValidationIssueFactory.AddError(issues, "skill.range_band", "Skill RangeMax must be greater than or equal to RangeMin.", assetPath);
        }

        if (skill.Radius < 0f || skill.Width < 0f || skill.ArcDegrees < 0f || skill.ArcDegrees > 360f)
        {
            ContentValidationIssueFactory.AddError(issues, "skill.shape", "Skill radius/width/arc must stay within non-negative bounds and arc must not exceed 360 degrees.", assetPath);
        }

        if (skill.ResourceCost < -1f || skill.CooldownSeconds < -1f || skill.RecoverySeconds < -1f || skill.PowerBudget < 0f)
        {
            ContentValidationIssueFactory.AddError(issues, "skill.schema_budget", "Skill resource/cooldown/recovery values must be non-negative or use -1 fallback, and PowerBudget must be non-negative.", assetPath);
        }

        if (skill.AiIntents.Distinct().Count() != skill.AiIntents.Count)
        {
            ContentValidationIssueFactory.AddError(issues, "skill.ai_intents", "Skill AI intents contain duplicates.", assetPath);
        }

        if (skill.AiScoreHints is { } hints)
        {
            if (hints.MinimumTargetHealthRatio < 0f
                || hints.MaximumTargetHealthRatio > 1f
                || hints.MaximumTargetHealthRatio < hints.MinimumTargetHealthRatio
                || hints.MinimumDistance < 0f
                || hints.MaximumDistance < 0f
                || (hints.MaximumDistance > 0f && hints.MaximumDistance < hints.MinimumDistance))
            {
                ContentValidationIssueFactory.AddError(issues, "skill.ai_score_hints", "Skill AI score hints must keep health ratios within 0..1 and distance bands ordered.", assetPath);
            }
        }

        ContentDefinitionSchemaRuleSupport.ValidateSchemaIdOrKey(skill.AnimationHookId, "skill.animation_hook", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateSchemaIdOrKey(skill.VfxHookId, "skill.vfx_hook", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateSchemaIdOrKey(skill.SfxHookId, "skill.sfx_hook", assetPath, issues);

        foreach (var status in skill.AppliedStatuses.Where(status => status != null))
        {
            ContentDefinitionSchemaRuleSupport.ValidateStatusApplicationRule(status, assetPath, issues);
        }

        LoopAContractValidator.ValidateSkill(skill, assetPath, issues);
    }
}

internal sealed class AugmentSchemaRule : DefinitionSchemaRule<AugmentDefinition>
{
    public AugmentSchemaRule()
        : base(nameof(AugmentDefinition))
    {
    }

    protected override string GetCanonicalId(AugmentDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        AugmentDefinition augment,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(augment.OfferBucket, "Augment offer bucket", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(augment.RiskRewardClass, "Augment risk reward class", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateModifiers(issues, augment.Modifiers, assetPath, "AugmentDefinition.Modifiers");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, augment.Tags, assetPath, "Augment tags");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, augment.BuildBiasTags, assetPath, "Augment build bias");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, augment.ProtectionTags, assetPath, "Augment protection");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, augment.MutualExclusionTags, assetPath, "Augment mutual exclusion");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, augment.RequiresTags, assetPath, "Augment requires");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, augment.RuleModifierTags, assetPath, "Augment rule modifier");
        if (string.IsNullOrWhiteSpace(augment.FamilyId))
        {
            ContentValidationIssueFactory.AddError(issues, "augment.family_id", "Augment is missing FamilyId.", assetPath);
        }

        if (augment.BudgetScore < 0f)
        {
            ContentValidationIssueFactory.AddError(issues, "augment.budget_score", "Augment BudgetScore must be non-negative.", assetPath);
        }

        if (augment.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
        {
            ContentValidationIssueFactory.AddError(issues, "augment.rule_tag", "Augment has an empty rule modifier tag.", assetPath);
        }

        if (augment.BuildBiasTags.Select(tag => tag?.Id ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Intersect(augment.ProtectionTags.Select(tag => tag?.Id ?? string.Empty), StringComparer.Ordinal)
            .Any())
        {
            ContentValidationIssueFactory.AddError(issues, "augment.bias_tag_overlap", "Augment build bias tags and protection tags must not overlap.", assetPath);
        }

        if (augment.OfferBucket == AugmentOfferBucketValue.SynergyLinked && augment.BuildBiasTags.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "augment.offer_metadata", "Synergy-linked augments must define at least one build bias tag.", assetPath);
        }

        if (augment.MutualExclusionTags.Select(tag => tag == null ? string.Empty : tag.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count()
            != augment.MutualExclusionTags.Count(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)))
        {
            ContentValidationIssueFactory.AddError(issues, "augment.mutual_exclusion", "Augment has duplicate mutual exclusion tags.", assetPath);
        }

        LoopAContractValidator.ValidateAugment(augment, assetPath, issues);
    }
}

internal sealed class ItemSchemaRule : DefinitionSchemaRule<ItemBaseDefinition>
{
    public ItemSchemaRule()
        : base(nameof(ItemBaseDefinition))
    {
    }

    protected override string GetCanonicalId(ItemBaseDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        ItemBaseDefinition item,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(item.SlotType, "Item slot type", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(item.IdentityKind, "Item identity kind", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateModifiers(issues, item.BaseModifiers, assetPath, "ItemBaseDefinition.BaseModifiers");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, item.CompileTags, assetPath, "Item compile");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, item.RuleModifierTags, assetPath, "Item rule modifier");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, item.AllowedArchetypeTags, assetPath, "Item allowed archetype");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, item.UniqueRuleTags, assetPath, "Item unique rule");
        if (item.IdentityKind == ItemIdentityValue.Unique
            && item.GrantedSkills.Count == 0
            && item.RuleModifierTags.Count == 0
            && item.UniqueRuleTags.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "item.unique_payload", "Unique item must define granted skill or rule payload.", assetPath);
        }
    }
}

internal sealed class AffixSchemaRule : DefinitionSchemaRule<AffixDefinition>
{
    public AffixSchemaRule()
        : base(nameof(AffixDefinition))
    {
    }

    protected override string GetCanonicalId(AffixDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        AffixDefinition affix,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(affix.Category, "Affix category", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(affix.AffixFamily, "Affix family", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(affix.EffectType, "Affix effect type", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateModifiers(issues, affix.Modifiers, assetPath, "AffixDefinition.Modifiers");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, affix.CompileTags, assetPath, "Affix compile");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, affix.RuleModifierTags, assetPath, "Affix rule modifier");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, affix.RequiredTags, assetPath, "Affix required");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, affix.ExcludedTags, assetPath, "Affix excluded");

        if (affix.ValueMax < affix.ValueMin)
        {
            ContentValidationIssueFactory.AddError(issues, "affix.value_band", "Affix ValueMax must be greater than or equal to ValueMin.", assetPath);
        }

        if (affix.ItemLevelMin < 0 || affix.SpawnWeight < 0f || affix.BudgetScore < 0f)
        {
            ContentValidationIssueFactory.AddError(issues, "affix.schema_budget", "Affix ItemLevelMin, SpawnWeight, and BudgetScore must be non-negative.", assetPath);
        }

        if (affix.RequiredTags.Select(tag => tag?.Id ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Intersect(affix.ExcludedTags.Select(tag => tag?.Id ?? string.Empty), StringComparer.Ordinal)
            .Any())
        {
            ContentValidationIssueFactory.AddError(issues, "affix.tag_overlap", "Affix required tags and excluded tags must not overlap.", assetPath);
        }

        if (!string.IsNullOrWhiteSpace(affix.ExclusiveGroupId))
        {
            ContentDefinitionSchemaRuleSupport.ValidateCanonicalId(affix.ExclusiveGroupId, assetPath, "AffixDefinition.ExclusiveGroupId", issues);
        }

        ContentDefinitionSchemaRuleSupport.ValidateSchemaIdOrKey(affix.TextTemplateKey, "affix.text_template", assetPath, issues);

        if (affix.EffectType == AffixEffectTypeValue.StatModifier && affix.Modifiers.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "affix.effect_payload", "StatModifier affixes must define at least one stat modifier.", assetPath);
        }

        if (affix.EffectType == AffixEffectTypeValue.RuleModifier && affix.RuleModifierTags.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "affix.effect_payload", "RuleModifier affixes must define at least one rule modifier tag.", assetPath);
        }

        if (affix.EffectType == AffixEffectTypeValue.ConditionalTagged && affix.RequiredTags.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "affix.effect_payload", "ConditionalTagged affixes must define at least one required tag.", assetPath);
        }

        LoopAContractValidator.ValidateAffix(affix, assetPath, issues);
    }
}

internal sealed class StatusFamilySchemaRule : DefinitionSchemaRule<StatusFamilyDefinition>
{
    public StatusFamilySchemaRule()
        : base(nameof(StatusFamilyDefinition))
    {
    }

    protected override string GetCanonicalId(StatusFamilyDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        StatusFamilyDefinition statusFamily,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(statusFamily.Group, "Status group", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(statusFamily.DefaultStackPolicy, "Status default stack policy", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(statusFamily.DefaultRefreshPolicy, "Status default refresh policy", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(statusFamily.DefaultProcAttributionPolicy, "Status default proc attribution policy", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(statusFamily.DefaultOwnershipPolicy, "Status default ownership policy", assetPath, issues);
        if (!statusFamily.IsRuleModifierOnly && statusFamily.DefaultStackCap < 1)
        {
            ContentValidationIssueFactory.AddError(issues, "status.family_defaults", "Non-rule-only status families must define DefaultStackCap >= 1.", assetPath);
        }

        if (statusFamily.VisualPriority < 0)
        {
            ContentValidationIssueFactory.AddError(issues, "status.visual_priority", "Status VisualPriority must be non-negative.", assetPath);
        }

        LoopAContractValidator.ValidateStatusFamily(statusFamily, assetPath, issues);
    }
}

internal sealed class RoleInstructionSchemaRule : DefinitionSchemaRule<RoleInstructionDefinition>
{
    public RoleInstructionSchemaRule()
        : base(nameof(RoleInstructionDefinition))
    {
    }

    protected override string GetCanonicalId(RoleInstructionDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        RoleInstructionDefinition roleInstruction,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(roleInstruction.LegacyDisplayName))
        {
            if (ContentLocalizationPolicy.TreatsLegacyTextAsError)
            {
                ContentValidationIssueFactory.AddError(issues, "localization.legacy_text", "Legacy localized prose remains in RoleInstructionDefinition.LegacyDisplayName.", assetPath, "RoleInstructionDefinition.LegacyDisplayName");
            }
            else
            {
                ContentValidationIssueFactory.AddWarning(issues, "localization.legacy_text", "Legacy localized prose remains in RoleInstructionDefinition.LegacyDisplayName.", assetPath, "RoleInstructionDefinition.LegacyDisplayName");
            }
        }

        if (string.IsNullOrWhiteSpace(roleInstruction.RoleTag))
        {
            ContentValidationIssueFactory.AddError(issues, "role_instruction.role_tag", "Role instruction is missing RoleTag.", assetPath);
            return;
        }

        if (!ContentValidationPolicyCatalog.AllowedRoleInstructionTags.Contains(roleInstruction.RoleTag))
        {
            ContentValidationIssueFactory.AddError(issues, "role_instruction.role_tag", $"Role instruction tag must be one of [{string.Join(", ", ContentValidationPolicyCatalog.AllowedRoleInstructionTags)}].", assetPath);
        }
    }
}

internal sealed class PassiveBoardSchemaRule : DefinitionSchemaRule<PassiveBoardDefinition>
{
    public PassiveBoardSchemaRule()
        : base(nameof(PassiveBoardDefinition))
    {
    }

    protected override string GetCanonicalId(PassiveBoardDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        PassiveBoardDefinition board,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(board.ClassId))
        {
            ContentValidationIssueFactory.AddError(issues, "passive_board.class_id", "Passive board is missing ClassId.", assetPath);
        }
        else if (!ContentValidationPolicyCatalog.CanonicalClassIds.Contains(board.ClassId))
        {
            ContentValidationIssueFactory.AddError(issues, "passive_board.class_id", $"Passive board class id must be one of [{string.Join(", ", ContentValidationPolicyCatalog.CanonicalClassIds)}].", assetPath);
        }

        if (board.Nodes.Any(node => node == null))
        {
            ContentValidationIssueFactory.AddError(issues, "passive_board.node_ref", "Passive board has a missing node reference.", assetPath);
        }
    }
}

internal sealed class PassiveNodeSchemaRule : DefinitionSchemaRule<PassiveNodeDefinition>
{
    public PassiveNodeSchemaRule()
        : base(nameof(PassiveNodeDefinition))
    {
    }

    protected override string GetCanonicalId(PassiveNodeDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        PassiveNodeDefinition passiveNode,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(passiveNode.NodeKind, "Passive node kind", assetPath, issues);
        ContentDefinitionSchemaRuleSupport.ValidateModifiers(issues, passiveNode.Modifiers, assetPath, "PassiveNodeDefinition.Modifiers");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, passiveNode.CompileTags, assetPath, "Passive node compile");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, passiveNode.RuleModifierTags, assetPath, "Passive node rule modifier");
        ContentDefinitionSchemaRuleSupport.ValidateStableTags(issues, passiveNode.MutualExclusionTags, assetPath, "Passive node mutual exclusion");
        if (string.IsNullOrWhiteSpace(passiveNode.BoardId))
        {
            ContentValidationIssueFactory.AddError(issues, "passive_node.board_id", "Passive node is missing BoardId.", assetPath);
        }

        if (passiveNode.BoardDepth < 0)
        {
            ContentValidationIssueFactory.AddError(issues, "passive_node.board_depth", "Passive node BoardDepth cannot be negative.", assetPath);
        }

        if (passiveNode.PrerequisiteNodeIds.Any(string.IsNullOrWhiteSpace))
        {
            ContentValidationIssueFactory.AddError(issues, "passive_node.prerequisite", "Passive node has an empty prerequisite id.", assetPath);
        }
    }
}

internal sealed class SynergySchemaRule : DefinitionSchemaRule<SynergyDefinition>
{
    public SynergySchemaRule()
        : base(nameof(SynergyDefinition))
    {
    }

    protected override string GetCanonicalId(SynergyDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        SynergyDefinition synergy,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(synergy.CountedTagId))
        {
            ContentValidationIssueFactory.AddError(issues, "synergy.counted_tag", "Synergy is missing CountedTagId.", assetPath);
        }

        if (synergy.Tiers.Any(tier => tier == null))
        {
            ContentValidationIssueFactory.AddError(issues, "synergy.tier_ref", "Synergy has a missing tier reference.", assetPath);
            return;
        }

        var thresholds = synergy.Tiers
            .Where(tier => tier != null)
            .Select(tier => tier.Threshold)
            .OrderBy(value => value)
            .ToList();
        if (!FirstPlayableAuthoringContract.GetExpectedSynergyThresholds(synergy).OrderBy(value => value).SequenceEqual(thresholds))
        {
            var expected = string.Join(", ", FirstPlayableAuthoringContract.GetExpectedSynergyThresholds(synergy));
            ContentValidationIssueFactory.AddError(issues, "synergy.thresholds", $"Synergy must define exact [{expected}] breakpoint tiers.", assetPath);
        }

        LoopAContractValidator.ValidateSynergy(synergy, assetPath, issues);
    }
}

internal sealed class SynergyTierSchemaRule : DefinitionSchemaRule<SynergyTierDefinition>
{
    public SynergyTierSchemaRule()
        : base(nameof(SynergyTierDefinition))
    {
    }

    protected override string GetCanonicalId(SynergyTierDefinition asset)
    {
        return asset.Id;
    }

    protected override void ValidateAsset(
        SynergyTierDefinition tier,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
        ContentDefinitionSchemaRuleSupport.ValidateModifiers(issues, tier.Modifiers, assetPath, "SynergyTierDefinition.Modifiers");
        if (!ContentValidationPolicyCatalog.AllowedSynergyTierThresholds.Contains(tier.Threshold))
        {
            ContentValidationIssueFactory.AddError(issues, "synergy_tier.threshold", "Synergy tier threshold must be one of 2, 3, or 4.", assetPath);
        }
    }
}

internal interface IDefinitionSchemaRule
{
    Type AssetType { get; }
    string Kind { get; }
    string GetCanonicalId(ScriptableObject asset);
    void Validate(ValidationAssetDescriptor descriptor, ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues);
}

internal abstract class DefinitionSchemaRule<TAsset> : IDefinitionSchemaRule
    where TAsset : ScriptableObject
{
    protected DefinitionSchemaRule(string kind)
    {
        Kind = kind;
    }

    public Type AssetType => typeof(TAsset);
    public string Kind { get; }

    public string GetCanonicalId(ScriptableObject asset)
    {
        return GetCanonicalId((TAsset)asset);
    }

    public void Validate(ValidationAssetDescriptor descriptor, ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues)
    {
        ValidateAsset((TAsset)descriptor.Asset, descriptor.AssetPath, catalog, issues);
    }

    protected abstract string GetCanonicalId(TAsset asset);

    protected abstract void ValidateAsset(
        TAsset asset,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues);
}

internal sealed class DefinitionIdentityOnlyRule<TAsset> : DefinitionSchemaRule<TAsset>
    where TAsset : ScriptableObject
{
    private readonly Func<TAsset, string> _canonicalIdSelector;

    public DefinitionIdentityOnlyRule(string kind, Func<TAsset, string> canonicalIdSelector)
        : base(kind)
    {
        _canonicalIdSelector = canonicalIdSelector;
    }

    protected override string GetCanonicalId(TAsset asset)
    {
        return _canonicalIdSelector(asset);
    }

    protected override void ValidateAsset(
        TAsset asset,
        string assetPath,
        ValidationAssetCatalog catalog,
        ICollection<ContentValidationIssue> issues)
    {
    }
}

internal sealed class DefinitionSchemaRuleRegistry
{
    private readonly IReadOnlyDictionary<Type, IDefinitionSchemaRule> _rules;

    private DefinitionSchemaRuleRegistry(IEnumerable<IDefinitionSchemaRule> rules)
    {
        _rules = rules.ToDictionary(rule => rule.AssetType);
    }

    internal bool TryGetRule(Type assetType, out IDefinitionSchemaRule rule)
    {
        return _rules.TryGetValue(assetType, out rule!);
    }

    internal static DefinitionSchemaRuleRegistry CreateDefault()
    {
        return new DefinitionSchemaRuleRegistry(new IDefinitionSchemaRule[]
        {
            new DefinitionIdentityOnlyRule<StatDefinition>(nameof(StatDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<RaceDefinition>(nameof(RaceDefinition), asset => asset.Id),
            new ClassDefinitionSchemaRule(),
            new CharacterDefinitionSchemaRule(),
            new TraitPoolSchemaRule(),
            new ArchetypeSchemaRule(),
            new SkillSchemaRule(),
            new AugmentSchemaRule(),
            new ItemSchemaRule(),
            new AffixSchemaRule(),
            new DefinitionIdentityOnlyRule<StableTagDefinition>(nameof(StableTagDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<TeamTacticDefinition>(nameof(TeamTacticDefinition), asset => asset.Id),
            new RoleInstructionSchemaRule(),
            new PassiveBoardSchemaRule(),
            new PassiveNodeSchemaRule(),
            new SynergySchemaRule(),
            new SynergyTierSchemaRule(),
            new ExpeditionSchemaRule(),
            new DefinitionIdentityOnlyRule<RewardTableDefinition>(nameof(RewardTableDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<CampaignChapterDefinition>(nameof(CampaignChapterDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<ExpeditionSiteDefinition>(nameof(ExpeditionSiteDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<EncounterDefinition>(nameof(EncounterDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<EnemySquadTemplateDefinition>(nameof(EnemySquadTemplateDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<BossOverlayDefinition>(nameof(BossOverlayDefinition), asset => asset.Id),
            new StatusFamilySchemaRule(),
            new DefinitionIdentityOnlyRule<CleanseProfileDefinition>(nameof(CleanseProfileDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<ControlDiminishingRuleDefinition>(nameof(ControlDiminishingRuleDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<RewardSourceDefinition>(nameof(RewardSourceDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<DropTableDefinition>(nameof(DropTableDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<LootBundleDefinition>(nameof(LootBundleDefinition), asset => asset.Id),
            new DefinitionIdentityOnlyRule<TraitTokenDefinition>(nameof(TraitTokenDefinition), asset => asset.Id),
        });
    }
}

internal static class ContentDefinitionSchemaRuleSupport
{
    internal static void ValidateCanonicalId(string id, string assetPath, string scope, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            ContentValidationIssueFactory.AddError(issues, "id.missing", $"Missing canonical id on {scope}.", assetPath, scope);
            return;
        }

        if (!ContentValidationPolicyCatalog.CanonicalIdPattern.IsMatch(id))
        {
            ContentValidationIssueFactory.AddError(issues, "id.invalid_pattern", $"Canonical id '{id}' must stay lower-case with '.' or '_' separators.", assetPath, scope);
        }
    }

    internal static void ValidateModifiers(
        ICollection<ContentValidationIssue> issues,
        IEnumerable<SerializableStatModifier> modifiers,
        string assetPath,
        string scope)
    {
        foreach (var modifier in modifiers)
        {
            switch (ContentValidationPolicyCatalog.GetStatIdStatus(modifier.StatId))
            {
                case StatIdValidationStatus.Unsupported:
                    ContentValidationIssueFactory.AddError(issues, "stat.unsupported", $"Unsupported stat id '{modifier.StatId}'.", assetPath, scope);
                    break;
                case StatIdValidationStatus.LegacyAlias:
                    ContentValidationIssueFactory.AddWarning(issues, "stat.legacy_alias", $"Legacy stat alias '{modifier.StatId}' should migrate to canonical id.", assetPath, scope);
                    break;
            }
        }
    }

    internal static void ValidateDefinedEnum<TEnum>(TEnum value, string label, string assetPath, ICollection<ContentValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            ContentValidationIssueFactory.AddError(issues, "enum.undefined", $"{label} is not a defined enum value.", assetPath);
        }
    }

    internal static void ValidateStableTags(ICollection<ContentValidationIssue> issues, IEnumerable<StableTagDefinition> tags, string assetPath, string scope)
    {
        if (tags == null)
        {
            return;
        }

        var ids = new List<string>();
        foreach (var tag in tags)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.Id))
            {
                ContentValidationIssueFactory.AddError(issues, "tag.missing_id", $"{scope} tag is missing id.", assetPath);
                continue;
            }

            ids.Add(tag.Id);
        }

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            ContentValidationIssueFactory.AddError(issues, "tag.duplicate", $"{scope} tags contain duplicates.", assetPath);
        }
    }

    internal static void ValidateSchemaIdOrKey(string value, string code, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!ContentValidationPolicyCatalog.CanonicalIdPattern.IsMatch(value))
        {
            ContentValidationIssueFactory.AddError(issues, code, $"'{value}' must use the canonical lower-case id/key pattern.", assetPath);
        }
    }

    internal static void ValidateStatusApplicationRule(StatusApplicationRule statusRule, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(statusRule.StatusId))
        {
            ContentValidationIssueFactory.AddError(issues, "status.rule_status_id", "Status application rule is missing StatusId.", assetPath);
        }

        if (statusRule.DurationSeconds < 0f || statusRule.MaxStacks < 1 || statusRule.StackCap < 0)
        {
            ContentValidationIssueFactory.AddError(issues, "status.rule_range", "Status duration must be non-negative, MaxStacks must be >= 1, and StackCap must be >= 0.", assetPath);
        }

        ValidateDefinedEnum(statusRule.StackPolicy, "Status stack policy", assetPath, issues);
        ValidateDefinedEnum(statusRule.RefreshPolicy, "Status refresh policy", assetPath, issues);
        ValidateDefinedEnum(statusRule.ProcAttributionPolicy, "Status proc attribution policy", assetPath, issues);
        ValidateDefinedEnum(statusRule.OwnershipPolicy, "Status ownership policy", assetPath, issues);

        if (statusRule.StackCap > 0 && statusRule.MaxStacks > statusRule.StackCap)
        {
            ContentValidationIssueFactory.AddError(issues, "status.rule_stack_cap", "Status rule MaxStacks must not exceed StackCap when StackCap is explicitly set.", assetPath);
        }
    }
}
