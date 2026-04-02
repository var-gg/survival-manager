using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
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

internal interface IValidationAssetRule
{
    Type AssetType { get; }
    string Kind { get; }
    string GetCanonicalId(ScriptableObject asset);
    void Validate(ValidationAssetDescriptor descriptor, ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues);
}

internal sealed class ValidationAssetRule<TAsset> : IValidationAssetRule
    where TAsset : ScriptableObject
{
    private readonly Func<TAsset, string> _canonicalIdSelector;
    private readonly Action<TAsset, string, ValidationAssetCatalog, ICollection<ContentValidationIssue>> _validator;

    internal ValidationAssetRule(
        string kind,
        Func<TAsset, string> canonicalIdSelector,
        Action<TAsset, string, ValidationAssetCatalog, ICollection<ContentValidationIssue>> validator)
    {
        Kind = kind;
        _canonicalIdSelector = canonicalIdSelector;
        _validator = validator;
    }

    public Type AssetType => typeof(TAsset);
    public string Kind { get; }

    public string GetCanonicalId(ScriptableObject asset)
    {
        return _canonicalIdSelector((TAsset)asset);
    }

    public void Validate(ValidationAssetDescriptor descriptor, ValidationAssetCatalog catalog, ICollection<ContentValidationIssue> issues)
    {
        _validator((TAsset)descriptor.Asset, descriptor.AssetPath, catalog, issues);
    }
}

internal sealed class DefinitionSchemaRuleRegistry
{
    private readonly IReadOnlyDictionary<Type, IValidationAssetRule> _rules;

    private DefinitionSchemaRuleRegistry(IEnumerable<IValidationAssetRule> rules)
    {
        _rules = rules.ToDictionary(rule => rule.AssetType);
    }

    internal bool TryGetRule(Type assetType, out IValidationAssetRule rule)
    {
        return _rules.TryGetValue(assetType, out rule!);
    }

    internal static DefinitionSchemaRuleRegistry CreateDefault()
    {
        return new DefinitionSchemaRuleRegistry(new IValidationAssetRule[]
        {
            CreateNoopRule<StatDefinition>(asset => asset.Id),
            CreateNoopRule<RaceDefinition>(asset => asset.Id),
            new ValidationAssetRule<ClassDefinition>(nameof(ClassDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateClassDefinition(asset, path, issues)),
            new ValidationAssetRule<TraitPoolDefinition>(nameof(TraitPoolDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateTraitPoolDefinition(asset, path, issues)),
            new ValidationAssetRule<UnitArchetypeDefinition>(nameof(UnitArchetypeDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateArchetype(asset, path, issues)),
            new ValidationAssetRule<SkillDefinitionAsset>(nameof(SkillDefinitionAsset), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateSkill(asset, path, issues)),
            new ValidationAssetRule<AugmentDefinition>(nameof(AugmentDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateAugment(asset, path, issues)),
            new ValidationAssetRule<ItemBaseDefinition>(nameof(ItemBaseDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateItem(asset, path, issues)),
            new ValidationAssetRule<AffixDefinition>(nameof(AffixDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateAffix(asset, path, issues)),
            CreateNoopRule<StableTagDefinition>(asset => asset.Id),
            CreateNoopRule<TeamTacticDefinition>(asset => asset.Id),
            new ValidationAssetRule<RoleInstructionDefinition>(nameof(RoleInstructionDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateRoleInstruction(asset, path, issues)),
            new ValidationAssetRule<PassiveBoardDefinition>(nameof(PassiveBoardDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidatePassiveBoard(asset, path, issues)),
            new ValidationAssetRule<PassiveNodeDefinition>(nameof(PassiveNodeDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidatePassiveNode(asset, path, issues)),
            new ValidationAssetRule<SynergyDefinition>(nameof(SynergyDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateSynergy(asset, path, issues)),
            new ValidationAssetRule<SynergyTierDefinition>(nameof(SynergyTierDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateSynergyTier(asset, path, issues)),
            new ValidationAssetRule<ExpeditionDefinition>(nameof(ExpeditionDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateExpeditionDefinition(asset, path, issues)),
            CreateNoopRule<RewardTableDefinition>(asset => asset.Id),
            CreateNoopRule<CampaignChapterDefinition>(asset => asset.Id),
            CreateNoopRule<ExpeditionSiteDefinition>(asset => asset.Id),
            CreateNoopRule<EncounterDefinition>(asset => asset.Id),
            CreateNoopRule<EnemySquadTemplateDefinition>(asset => asset.Id),
            CreateNoopRule<BossOverlayDefinition>(asset => asset.Id),
            new ValidationAssetRule<StatusFamilyDefinition>(nameof(StatusFamilyDefinition), asset => asset.Id, static (asset, path, _, issues) => ContentDefinitionSchemaRules.ValidateStatusFamily(asset, path, issues)),
            CreateNoopRule<CleanseProfileDefinition>(asset => asset.Id),
            CreateNoopRule<ControlDiminishingRuleDefinition>(asset => asset.Id),
            CreateNoopRule<RewardSourceDefinition>(asset => asset.Id),
            CreateNoopRule<DropTableDefinition>(asset => asset.Id),
            CreateNoopRule<LootBundleDefinition>(asset => asset.Id),
            CreateNoopRule<TraitTokenDefinition>(asset => asset.Id),
        });
    }

    private static ValidationAssetRule<TAsset> CreateNoopRule<TAsset>(Func<TAsset, string> canonicalIdSelector)
        where TAsset : ScriptableObject
    {
        return new ValidationAssetRule<TAsset>(typeof(TAsset).Name, canonicalIdSelector, static (_, _, _, _) => { });
    }
}

internal static class ContentDefinitionSchemaRules
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

    internal static void ValidateClassDefinition(ClassDefinition definition, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.Equals(definition.Id, "striker", StringComparison.Ordinal))
        {
            ContentValidationIssueFactory.AddError(issues, "glossary.class_id", "Class canonical id must remain 'duelist'; 'Striker' is player-facing only.", assetPath, "ClassDefinition.Id");
        }
    }

    internal static void ValidateTraitPoolDefinition(TraitPoolDefinition definition, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (definition.PositiveTraits.Count < 3 || definition.NegativeTraits.Count < 3)
        {
            ContentValidationIssueFactory.AddError(issues, "traitpool.structure", "Trait pool must keep the 3+3 positive/negative structure.", assetPath);
        }
    }

    internal static void ValidateExpeditionDefinition(ExpeditionDefinition definition, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (definition.Nodes.Any(node => node.RewardTable == null))
        {
            ContentValidationIssueFactory.AddError(issues, "expedition.reward_table", "Expedition node is missing a reward table reference.", assetPath);
        }
    }

    internal static void ValidateArchetype(UnitArchetypeDefinition archetype, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (archetype.Race == null || archetype.Class == null || archetype.TraitPool == null)
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.references", "Archetype is missing race/class/trait pool references.", assetPath);
        }

        if (archetype.TacticPreset == null || archetype.TacticPreset.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.tactic_preset", "Archetype is missing a tactic preset.", assetPath);
        }

        ValidateDefinedEnum(archetype.ScopeKind, "Archetype scope", assetPath, issues);
        if (string.IsNullOrWhiteSpace(archetype.RoleFamilyTag))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.role_family", "Archetype is missing RoleFamilyTag.", assetPath);
        }
        else if (!ContentValidationPolicyCatalog.AllowedRoleFamilyTags.Contains(archetype.RoleFamilyTag))
        {
            ContentValidationIssueFactory.AddError(issues, "archetype.role_family", $"Archetype role family tag must be one of [{string.Join(", ", ContentValidationPolicyCatalog.AllowedRoleFamilyTags)}].", assetPath);
        }

        if (string.Equals(archetype.Class?.Id, "duelist", StringComparison.Ordinal)
            && !string.Equals(archetype.RoleFamilyTag, "striker", StringComparison.Ordinal))
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

        ValidateStableTags(issues, archetype.SupportModifierBiasTags, assetPath, "Archetype support modifier bias");
        if (!string.IsNullOrWhiteSpace(archetype.LockedAttackProfileId))
        {
            ValidateCanonicalId(archetype.LockedAttackProfileId, assetPath, "UnitArchetypeDefinition.LockedAttackProfileId", issues);
        }

        if (!string.IsNullOrWhiteSpace(archetype.LockedAttackProfileTag))
        {
            ValidateCanonicalId(archetype.LockedAttackProfileTag, assetPath, "UnitArchetypeDefinition.LockedAttackProfileTag", issues);
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

    internal static void ValidateSkill(SkillDefinitionAsset skill, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(skill.TemplateType, "Skill template type", assetPath, issues);
        ValidateDefinedEnum(skill.Kind, "Skill kind", assetPath, issues);
        ValidateDefinedEnum(skill.SlotKind, "Skill slot kind", assetPath, issues);
        ValidateDefinedEnum(skill.DamageType, "Skill damage type", assetPath, issues);
        ValidateDefinedEnum(skill.Delivery, "Skill delivery", assetPath, issues);
        ValidateDefinedEnum(skill.TargetRule, "Skill target rule", assetPath, issues);
        ValidateDefinedEnum(skill.LearnSource, "Skill learn source", assetPath, issues);
        ValidateStableTags(issues, skill.CompileTags, assetPath, "Skill compile");
        ValidateStableTags(issues, skill.RuleModifierTags, assetPath, "Skill rule modifier");
        ValidateStableTags(issues, skill.SupportAllowedTags, assetPath, "Skill support allowed");
        ValidateStableTags(issues, skill.SupportBlockedTags, assetPath, "Skill support blocked");
        ValidateStableTags(issues, skill.RequiredWeaponTags, assetPath, "Skill required weapon");
        ValidateStableTags(issues, skill.RequiredClassTags, assetPath, "Skill required class");

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

        ValidateSchemaIdOrKey(skill.AnimationHookId, "skill.animation_hook", assetPath, issues);
        ValidateSchemaIdOrKey(skill.VfxHookId, "skill.vfx_hook", assetPath, issues);
        ValidateSchemaIdOrKey(skill.SfxHookId, "skill.sfx_hook", assetPath, issues);

        foreach (var status in skill.AppliedStatuses.Where(status => status != null))
        {
            ValidateStatusApplicationRule(status, assetPath, issues);
        }

        LoopAContractValidator.ValidateSkill(skill, assetPath, issues);
    }

    internal static void ValidateAugment(AugmentDefinition augment, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(augment.OfferBucket, "Augment offer bucket", assetPath, issues);
        ValidateDefinedEnum(augment.RiskRewardClass, "Augment risk reward class", assetPath, issues);
        ValidateModifiers(issues, augment.Modifiers, assetPath, "AugmentDefinition.Modifiers");
        ValidateStableTags(issues, augment.Tags, assetPath, "Augment tags");
        ValidateStableTags(issues, augment.BuildBiasTags, assetPath, "Augment build bias");
        ValidateStableTags(issues, augment.ProtectionTags, assetPath, "Augment protection");
        ValidateStableTags(issues, augment.MutualExclusionTags, assetPath, "Augment mutual exclusion");
        ValidateStableTags(issues, augment.RequiresTags, assetPath, "Augment requires");
        ValidateStableTags(issues, augment.RuleModifierTags, assetPath, "Augment rule modifier");
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

    internal static void ValidateItem(ItemBaseDefinition item, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(item.SlotType, "Item slot type", assetPath, issues);
        ValidateDefinedEnum(item.IdentityKind, "Item identity kind", assetPath, issues);
        ValidateModifiers(issues, item.BaseModifiers, assetPath, "ItemBaseDefinition.BaseModifiers");
        ValidateStableTags(issues, item.CompileTags, assetPath, "Item compile");
        ValidateStableTags(issues, item.RuleModifierTags, assetPath, "Item rule modifier");
        ValidateStableTags(issues, item.AllowedArchetypeTags, assetPath, "Item allowed archetype");
        ValidateStableTags(issues, item.UniqueRuleTags, assetPath, "Item unique rule");
        if (item.IdentityKind == ItemIdentityValue.Unique
            && item.GrantedSkills.Count == 0
            && item.RuleModifierTags.Count == 0
            && item.UniqueRuleTags.Count == 0)
        {
            ContentValidationIssueFactory.AddError(issues, "item.unique_payload", "Unique item must define granted skill or rule payload.", assetPath);
        }
    }

    internal static void ValidateAffix(AffixDefinition affix, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(affix.Category, "Affix category", assetPath, issues);
        ValidateDefinedEnum(affix.AffixFamily, "Affix family", assetPath, issues);
        ValidateDefinedEnum(affix.EffectType, "Affix effect type", assetPath, issues);
        ValidateModifiers(issues, affix.Modifiers, assetPath, "AffixDefinition.Modifiers");
        ValidateStableTags(issues, affix.CompileTags, assetPath, "Affix compile");
        ValidateStableTags(issues, affix.RuleModifierTags, assetPath, "Affix rule modifier");
        ValidateStableTags(issues, affix.RequiredTags, assetPath, "Affix required");
        ValidateStableTags(issues, affix.ExcludedTags, assetPath, "Affix excluded");

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
            ValidateCanonicalId(affix.ExclusiveGroupId, assetPath, "AffixDefinition.ExclusiveGroupId", issues);
        }

        ValidateSchemaIdOrKey(affix.TextTemplateKey, "affix.text_template", assetPath, issues);

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

    internal static void ValidateStatusFamily(StatusFamilyDefinition statusFamily, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(statusFamily.Group, "Status group", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultStackPolicy, "Status default stack policy", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultRefreshPolicy, "Status default refresh policy", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultProcAttributionPolicy, "Status default proc attribution policy", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultOwnershipPolicy, "Status default ownership policy", assetPath, issues);
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

    internal static void ValidateRoleInstruction(RoleInstructionDefinition roleInstruction, string assetPath, ICollection<ContentValidationIssue> issues)
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

    internal static void ValidatePassiveBoard(PassiveBoardDefinition board, string assetPath, ICollection<ContentValidationIssue> issues)
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

    internal static void ValidatePassiveNode(PassiveNodeDefinition passiveNode, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(passiveNode.NodeKind, "Passive node kind", assetPath, issues);
        ValidateModifiers(issues, passiveNode.Modifiers, assetPath, "PassiveNodeDefinition.Modifiers");
        ValidateStableTags(issues, passiveNode.CompileTags, assetPath, "Passive node compile");
        ValidateStableTags(issues, passiveNode.RuleModifierTags, assetPath, "Passive node rule modifier");
        ValidateStableTags(issues, passiveNode.MutualExclusionTags, assetPath, "Passive node mutual exclusion");
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

    internal static void ValidateSynergy(SynergyDefinition synergy, string assetPath, ICollection<ContentValidationIssue> issues)
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
        if (!ContentValidationPolicyCatalog.RequiredSynergyThresholds.SetEquals(thresholds))
        {
            ContentValidationIssueFactory.AddError(issues, "synergy.thresholds", "Synergy must define exact 2/4 breakpoint tiers.", assetPath);
        }

        LoopAContractValidator.ValidateSynergy(synergy, assetPath, issues);
    }

    internal static void ValidateSynergyTier(SynergyTierDefinition tier, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateModifiers(issues, tier.Modifiers, assetPath, "SynergyTierDefinition.Modifiers");
        if (!ContentValidationPolicyCatalog.RequiredSynergyThresholds.Contains(tier.Threshold))
        {
            ContentValidationIssueFactory.AddError(issues, "synergy_tier.threshold", "Synergy tier threshold must be one of 2 or 4.", assetPath);
        }
    }
}
