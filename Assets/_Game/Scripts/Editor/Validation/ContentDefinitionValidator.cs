using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SM.Content.Definitions;
using SM.Core.Stats;
using SM.Editor.SeedData;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Validation;

public enum StatIdValidationStatus
{
    Unsupported = 0,
    LegacyAlias = 1,
    Canonical = 2,
}

public sealed record LaunchScopeThreshold
{
    public string Label { get; init; } = string.Empty;
    public int? ArchetypeCount { get; init; }
    public int? CoreArchetypeCount { get; init; }
    public int? SpecialistArchetypeCount { get; init; }
    public int? SkillCount { get; init; }
    public int? EquippableCount { get; init; }
    public int? PassiveBoardCount { get; init; }
    public int? PassiveNodeCount { get; init; }
    public int? TemporaryAugmentCount { get; init; }
    public int? PermanentAugmentCount { get; init; }
    public int? SynergyFamilyCount { get; init; }
}

public sealed record LaunchScopeCountReport
{
    public int ArchetypeCount { get; init; }
    public int CoreArchetypeCount { get; init; }
    public int SpecialistArchetypeCount { get; init; }
    public int SkillCount { get; init; }
    public int EquippableCount { get; init; }
    public int AffixCount { get; init; }
    public int PassiveBoardCount { get; init; }
    public int PassiveNodeCount { get; init; }
    public int TemporaryAugmentCount { get; init; }
    public int PermanentAugmentCount { get; init; }
    public int SynergyFamilyCount { get; init; }
}

public static class ContentDefinitionValidator
{
    private static readonly string[] RequiredLocaleCodes = { "ko", "en" };
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly HashSet<string> CanonicalClassIds = new(StringComparer.Ordinal) { "vanguard", "duelist", "ranger", "mystic" };
    private static readonly HashSet<string> AllowedRoleFamilyTags = new(StringComparer.Ordinal) { "vanguard", "striker", "ranger", "mystic" };

    public static LaunchScopeThreshold CurrentMvpMinimum { get; } = new()
    {
        Label = "current MVP minimum",
        ArchetypeCount = 8,
        TemporaryAugmentCount = 9,
        SynergyFamilyCount = 7,
    };

    public static LaunchScopeThreshold PaidLaunchFloor { get; } = new()
    {
        Label = "paid launch floor",
        ArchetypeCount = 12,
        CoreArchetypeCount = 12,
        SkillCount = 40,
        EquippableCount = 36,
        PassiveBoardCount = 4,
        PassiveNodeCount = 72,
        TemporaryAugmentCount = 18,
        PermanentAugmentCount = 9,
        SynergyFamilyCount = 7,
    };

    public static LaunchScopeThreshold PaidLaunchSafeTarget { get; } = new()
    {
        Label = "paid launch safe target",
        ArchetypeCount = 16,
        CoreArchetypeCount = 12,
        SpecialistArchetypeCount = 4,
        SkillCount = 40,
        EquippableCount = 42,
        PassiveBoardCount = 4,
        PassiveNodeCount = 96,
        TemporaryAugmentCount = 24,
        PermanentAugmentCount = 12,
        SynergyFamilyCount = 7,
    };

    public static StatIdValidationStatus GetStatIdStatus(string statId)
    {
        return StatKey.TryResolve(statId, out _, out var isLegacyAlias)
            ? isLegacyAlias ? StatIdValidationStatus.LegacyAlias : StatIdValidationStatus.Canonical
            : StatIdValidationStatus.Unsupported;
    }

    public static LaunchScopeCountReport BuildLaunchScopeCountReport()
    {
        return BuildLaunchScopeCountReport(LoadAllDefinitionAssets());
    }

    [MenuItem("SM/Validation/Validate Content Definitions")]
    public static void Validate()
    {
        var allAssets = LoadAllDefinitionAssets();

        var ids = new Dictionary<string, List<string>>();
        var localizationKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var passiveBoardsByClassId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var asset in allAssets)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            ValidateLocalizationAsset(asset, assetPath, localizationKeys, errors);

            switch (asset)
            {
                case StatDefinition stat:
                    RegisterId(ids, stat.Id, assetPath);
                    break;
                case RaceDefinition race:
                    RegisterId(ids, race.Id, assetPath);
                    break;
                case ClassDefinition @class:
                    RegisterId(ids, @class.Id, assetPath);
                    break;
                case TraitPoolDefinition traitPool:
                    RegisterId(ids, traitPool.Id, assetPath);
                    if (traitPool.PositiveTraits.Count < 3 || traitPool.NegativeTraits.Count < 3)
                    {
                        errors.Add($"Trait pool missing 3+3 structure: {assetPath}");
                    }
                    break;
                case UnitArchetypeDefinition archetype:
                    RegisterId(ids, archetype.Id, assetPath);
                    if (archetype.Race == null || archetype.Class == null || archetype.TraitPool == null)
                    {
                        errors.Add($"Archetype missing references: {assetPath}");
                    }
                    if (archetype.TacticPreset == null || archetype.TacticPreset.Count == 0)
                    {
                        errors.Add($"Archetype missing tactic preset: {assetPath}");
                    }
                    ValidateDefinedEnum(archetype.ScopeKind, "Archetype scope", assetPath, errors);
                    if (string.IsNullOrWhiteSpace(archetype.RoleFamilyTag))
                    {
                        errors.Add($"Archetype missing role family tag: {assetPath}");
                    }
                    else if (!AllowedRoleFamilyTags.Contains(archetype.RoleFamilyTag))
                    {
                        errors.Add($"Archetype role family tag must be one of [{string.Join(", ", AllowedRoleFamilyTags)}]: {assetPath}");
                    }
                    if (string.IsNullOrWhiteSpace(archetype.PrimaryWeaponFamilyTag))
                    {
                        errors.Add($"Archetype missing primary weapon family tag: {assetPath}");
                    }
                    ValidateStableTags(errors, archetype.SupportModifierBiasTags, assetPath, "Archetype support modifier bias");
                    break;
                case SkillDefinitionAsset skill:
                    RegisterId(ids, skill.Id, assetPath);
                    ValidateDefinedEnum(skill.Kind, "Skill kind", assetPath, errors);
                    ValidateDefinedEnum(skill.SlotKind, "Skill slot kind", assetPath, errors);
                    ValidateDefinedEnum(skill.DamageType, "Skill damage type", assetPath, errors);
                    ValidateDefinedEnum(skill.Delivery, "Skill delivery", assetPath, errors);
                    ValidateDefinedEnum(skill.TargetRule, "Skill target rule", assetPath, errors);
                    ValidateStableTags(errors, skill.CompileTags, assetPath, "Skill compile");
                    ValidateStableTags(errors, skill.RuleModifierTags, assetPath, "Skill rule modifier");
                    ValidateStableTags(errors, skill.SupportAllowedTags, assetPath, "Skill support allowed");
                    ValidateStableTags(errors, skill.RequiredWeaponTags, assetPath, "Skill required weapon");
                    ValidateStableTags(errors, skill.RequiredClassTags, assetPath, "Skill required class");
                    break;
                case AugmentDefinition augment:
                    RegisterId(ids, augment.Id, assetPath);
                    ValidateModifiers(errors, warnings, augment.Modifiers, assetPath);
                    if (string.IsNullOrWhiteSpace(augment.FamilyId))
                    {
                        errors.Add($"Augment missing family id: {assetPath}");
                    }
                    if (augment.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Augment has empty rule modifier tag: {assetPath}");
                    }
                    if (augment.MutualExclusionTags.Select(tag => tag == null ? string.Empty : tag.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count()
                        != augment.MutualExclusionTags.Count(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Augment has duplicate mutual exclusion tags: {assetPath}");
                    }
                    break;
                case ItemBaseDefinition item:
                    RegisterId(ids, item.Id, assetPath);
                    ValidateDefinedEnum(item.SlotType, "Item slot type", assetPath, errors);
                    ValidateDefinedEnum(item.IdentityKind, "Item identity kind", assetPath, errors);
                    ValidateModifiers(errors, warnings, item.BaseModifiers, assetPath);
                    ValidateStableTags(errors, item.CompileTags, assetPath, "Item compile");
                    ValidateStableTags(errors, item.RuleModifierTags, assetPath, "Item rule modifier");
                    ValidateStableTags(errors, item.AllowedClassTags, assetPath, "Item allowed class");
                    ValidateStableTags(errors, item.AllowedArchetypeTags, assetPath, "Item allowed archetype");
                    ValidateStableTags(errors, item.UniqueRuleTags, assetPath, "Item unique rule");
                    if (item.IdentityKind == ItemIdentityValue.Unique
                        && item.GrantedSkills.Count == 0
                        && item.RuleModifierTags.Count == 0
                        && item.UniqueRuleTags.Count == 0)
                    {
                        errors.Add($"Unique item must define granted skill or rule tags: {assetPath}");
                    }
                    break;
                case AffixDefinition affix:
                    RegisterId(ids, affix.Id, assetPath);
                    ValidateDefinedEnum(affix.Category, "Affix category", assetPath, errors);
                    ValidateModifiers(errors, warnings, affix.Modifiers, assetPath);
                    ValidateStableTags(errors, affix.CompileTags, assetPath, "Affix compile");
                    ValidateStableTags(errors, affix.RuleModifierTags, assetPath, "Affix rule modifier");
                    break;
                case StableTagDefinition tag:
                    RegisterId(ids, tag.Id, assetPath);
                    break;
                case TeamTacticDefinition teamTactic:
                    RegisterId(ids, teamTactic.Id, assetPath);
                    break;
                case RoleInstructionDefinition roleInstruction:
                    RegisterId(ids, roleInstruction.Id, assetPath);
                    if (string.IsNullOrWhiteSpace(roleInstruction.RoleTag))
                    {
                        errors.Add($"Role instruction missing role tag: {assetPath}");
                    }
                    break;
                case PassiveBoardDefinition board:
                    RegisterId(ids, board.Id, assetPath);
                    RegisterId(passiveBoardsByClassId, board.ClassId, assetPath);
                    if (string.IsNullOrWhiteSpace(board.ClassId))
                    {
                        errors.Add($"Passive board missing class id: {assetPath}");
                    }
                    else if (!CanonicalClassIds.Contains(board.ClassId))
                    {
                        errors.Add($"Passive board class id must be one of [{string.Join(", ", CanonicalClassIds)}]: {assetPath}");
                    }
                    if (board.Nodes.Any(node => node == null))
                    {
                        errors.Add($"Passive board has missing node reference: {assetPath}");
                    }
                    break;
                case PassiveNodeDefinition passiveNode:
                    RegisterId(ids, passiveNode.Id, assetPath);
                    ValidateDefinedEnum(passiveNode.NodeKind, "Passive node kind", assetPath, errors);
                    ValidateModifiers(errors, warnings, passiveNode.Modifiers, assetPath);
                    ValidateStableTags(errors, passiveNode.CompileTags, assetPath, "Passive node compile");
                    ValidateStableTags(errors, passiveNode.RuleModifierTags, assetPath, "Passive node rule modifier");
                    ValidateStableTags(errors, passiveNode.MutualExclusionTags, assetPath, "Passive node mutual exclusion");
                    if (passiveNode.BoardDepth < 0)
                    {
                        errors.Add($"Passive node board depth cannot be negative: {assetPath}");
                    }
                    if (passiveNode.PrerequisiteNodeIds.Any(string.IsNullOrWhiteSpace))
                    {
                        errors.Add($"Passive node has empty prerequisite id: {assetPath}");
                    }
                    break;
                case SynergyDefinition synergy:
                    RegisterId(ids, synergy.Id, assetPath);
                    if (string.IsNullOrWhiteSpace(synergy.CountedTagId))
                    {
                        errors.Add($"Synergy missing counted tag id: {assetPath}");
                    }
                    if (synergy.Tiers.Any(tier => tier == null))
                    {
                        errors.Add($"Synergy has missing tier reference: {assetPath}");
                    }
                    break;
                case SynergyTierDefinition tier:
                    RegisterId(ids, tier.Id, assetPath);
                    ValidateModifiers(errors, warnings, tier.Modifiers, assetPath);
                    break;
                case ExpeditionDefinition expedition:
                    RegisterId(ids, expedition.Id, assetPath);
                    if (expedition.Nodes.Any(n => n.RewardTable == null))
                    {
                        errors.Add($"Expedition has node with missing reward table: {assetPath}");
                    }
                    break;
                case RewardTableDefinition rewardTable:
                    RegisterId(ids, rewardTable.Id, assetPath);
                    break;
            }
        }

        foreach (var pair in ids.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value.Count > 1))
        {
            errors.Add($"Duplicate ID '{pair.Key}': {string.Join(", ", pair.Value)}");
        }

        foreach (var pair in passiveBoardsByClassId.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value.Count > 1))
        {
            errors.Add($"Duplicate passive board class '{pair.Key}': {string.Join(", ", pair.Value)}");
        }

        foreach (var pair in localizationKeys.Where(x => x.Value.Count > 1))
        {
            errors.Add($"Duplicate localization key '{pair.Key}': {string.Join(", ", pair.Value)}");
        }

        var report = BuildLaunchScopeCountReport(allAssets);
        warnings.AddRange(BuildLaunchScopeWarnings(report));

        if (errors.Count == 0)
        {
            foreach (var warning in warnings)
            {
                Debug.LogWarning(warning);
            }

            Debug.Log("SM content validation passed.");
            return;
        }

        foreach (var error in errors)
        {
            Debug.LogError(error);
        }

        throw new Exception($"SM content validation failed with {errors.Count} issue(s).");
    }

    private static void ValidateLocalizationAsset(
        ScriptableObject asset,
        string assetPath,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<string> errors)
    {
        ValidateLocalizationObject(asset, assetPath, asset.GetType().Name, localizationKeys, errors);

        switch (asset)
        {
            case TraitPoolDefinition traitPool:
                ValidateNestedObjects(traitPool.PositiveTraits, assetPath, "PositiveTraits", localizationKeys, errors);
                ValidateNestedObjects(traitPool.NegativeTraits, assetPath, "NegativeTraits", localizationKeys, errors);
                break;
            case RewardTableDefinition rewardTable:
                ValidateNestedObjects(rewardTable.Rewards, assetPath, "Rewards", localizationKeys, errors);
                break;
            case ExpeditionDefinition expedition:
                ValidateNestedObjects(expedition.Nodes, assetPath, "Nodes", localizationKeys, errors);
                break;
            case SynergyDefinition synergy:
                ValidateNestedObjects(synergy.Tiers, assetPath, "Tiers", localizationKeys, errors);
                break;
        }
    }

    private static void ValidateNestedObjects(
        IEnumerable objects,
        string assetPath,
        string scope,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<string> errors)
    {
        var index = 0;
        foreach (var item in objects)
        {
            if (item != null)
            {
                ValidateLocalizationObject(item, assetPath, $"{scope}[{index}]", localizationKeys, errors);
            }

            index++;
        }
    }

    private static void ValidateLocalizationObject(
        object target,
        string assetPath,
        string scope,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<string> errors)
    {
        var type = target.GetType();
        var tableName = ContentLocalizationTables.GetTableName(type);

        foreach (var fieldName in EnumerateLocalizationFieldNames(type))
        {
            var key = type.GetField(fieldName, InstanceFlags)?.GetValue(target) as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"Missing localization key '{fieldName}' on {scope}: {assetPath}");
                continue;
            }

            if (!LocalizationKeyPattern.IsValid(key))
            {
                errors.Add($"Invalid localization key '{key}' on {scope}: {assetPath}");
                continue;
            }

            if (ShouldValidateTableEntries(tableName))
            {
                RegisterLocalizationKey(localizationKeys, tableName, key, $"{assetPath} ({scope}.{fieldName})");
                ValidateTableEntries(tableName, key, assetPath, scope, errors);
            }
        }

        ValidateLegacyText(target, assetPath, scope, errors);
    }

    private static IEnumerable<string> EnumerateLocalizationFieldNames(Type type)
    {
        if (type.GetField("NameKey", InstanceFlags) != null)
        {
            yield return "NameKey";
        }

        if (type.GetField("DescriptionKey", InstanceFlags) != null)
        {
            yield return "DescriptionKey";
        }

        if (type.GetField("LabelKey", InstanceFlags) != null)
        {
            yield return "LabelKey";
        }

        if (type.GetField("RewardSummaryKey", InstanceFlags) != null)
        {
            yield return "RewardSummaryKey";
        }
    }

    private static void ValidateLegacyText(object target, string assetPath, string scope, ICollection<string> errors)
    {
        foreach (var propertyName in new[] { "LegacyDisplayName", "LegacyDescription", "LegacyLabel" })
        {
            var property = target.GetType().GetProperty(propertyName, InstanceFlags);
            if (property?.PropertyType != typeof(string))
            {
                continue;
            }

            var value = property.GetValue(target) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Legacy localized prose remains in {scope}.{propertyName}: {assetPath}");
            }
        }
    }

    private static bool ShouldValidateTableEntries(string tableName)
    {
        return !string.IsNullOrWhiteSpace(tableName) && !string.Equals(tableName, ContentLocalizationTables.SystemMessages, StringComparison.Ordinal);
    }

    private static void RegisterLocalizationKey(Dictionary<string, List<string>> keys, string tableName, string key, string owner)
    {
        var composite = $"{tableName}:{key}";
        if (!keys.TryGetValue(composite, out var owners))
        {
            owners = new List<string>();
            keys[composite] = owners;
        }

        owners.Add(owner);
    }

    private static void ValidateTableEntries(
        string tableName,
        string key,
        string assetPath,
        string scope,
        ICollection<string> errors)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null)
        {
            errors.Add($"Missing string table collection '{tableName}' for key '{key}' on {scope}: {assetPath}");
            return;
        }

        foreach (var localeCode in RequiredLocaleCodes)
        {
            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table == null)
            {
                errors.Add($"Missing locale table '{tableName}/{localeCode}' for key '{key}' on {scope}: {assetPath}");
                continue;
            }

            if (table.GetEntry(key) == null)
            {
                errors.Add($"Missing localized entry '{tableName}/{localeCode}:{key}' on {scope}: {assetPath}");
            }
        }
    }

    private static void RegisterId(Dictionary<string, List<string>> ids, string id, string path)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!ids.TryGetValue(id, out var list))
        {
            list = new List<string>();
            ids[id] = list;
        }

        list.Add(path);
    }

    private static void ValidateModifiers(
        ICollection<string> errors,
        ICollection<string> warnings,
        IEnumerable<SerializableStatModifier> modifiers,
        string path)
    {
        foreach (var modifier in modifiers)
        {
            switch (GetStatIdStatus(modifier.StatId))
            {
                case StatIdValidationStatus.Unsupported:
                    errors.Add($"Unsupported stat id '{modifier.StatId}': {path}");
                    break;
                case StatIdValidationStatus.LegacyAlias:
                    warnings.Add($"Legacy stat alias '{modifier.StatId}' should migrate to canonical id: {path}");
                    break;
            }
        }
    }

    private static List<ScriptableObject> LoadAllDefinitionAssets()
    {
        return AssetDatabase.FindAssets("t:ScriptableObject", new[] { SampleSeedGenerator.ResourcesRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadMainAssetAtPath(path))
            .OfType<ScriptableObject>()
            .ToList();
    }

    private static LaunchScopeCountReport BuildLaunchScopeCountReport(IEnumerable<ScriptableObject> assets)
    {
        var assetList = assets.ToList();
        var archetypes = assetList.OfType<UnitArchetypeDefinition>().ToList();
        var augments = assetList.OfType<AugmentDefinition>().ToList();

        return new LaunchScopeCountReport
        {
            ArchetypeCount = archetypes.Count,
            CoreArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Core),
            SpecialistArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Specialist),
            SkillCount = assetList.OfType<SkillDefinitionAsset>().Count(),
            EquippableCount = assetList.OfType<ItemBaseDefinition>().Count(),
            AffixCount = assetList.OfType<AffixDefinition>().Count(),
            PassiveBoardCount = assetList.OfType<PassiveBoardDefinition>().Count(),
            PassiveNodeCount = assetList.OfType<PassiveNodeDefinition>().Count(),
            TemporaryAugmentCount = augments.Count(augment => !augment.IsPermanent),
            PermanentAugmentCount = augments.Count(augment => augment.IsPermanent),
            SynergyFamilyCount = assetList.OfType<SynergyDefinition>().Count(),
        };
    }

    private static IEnumerable<string> BuildLaunchScopeWarnings(LaunchScopeCountReport report)
    {
        yield return FormatLaunchScopeSummary(report);

        foreach (var threshold in new[] { CurrentMvpMinimum, PaidLaunchFloor, PaidLaunchSafeTarget })
        {
            var gaps = BuildLaunchScopeGapList(report, threshold);
            if (gaps.Count == 0)
            {
                continue;
            }

            yield return $"Launch scope below '{threshold.Label}': {string.Join(", ", gaps)}";
        }
    }

    private static string FormatLaunchScopeSummary(LaunchScopeCountReport report)
    {
        return "Launch scope authoring counts: "
               + $"archetypes={report.ArchetypeCount} (core={report.CoreArchetypeCount}, specialist={report.SpecialistArchetypeCount}), "
               + $"skills={report.SkillCount}, equippables={report.EquippableCount}, affixes={report.AffixCount}, "
               + $"passiveBoards={report.PassiveBoardCount}, passiveNodes={report.PassiveNodeCount}, "
               + $"tempAugments={report.TemporaryAugmentCount}, permAugments={report.PermanentAugmentCount}, "
               + $"synergyFamilies={report.SynergyFamilyCount}";
    }

    private static List<string> BuildLaunchScopeGapList(LaunchScopeCountReport report, LaunchScopeThreshold threshold)
    {
        var gaps = new List<string>();
        AddThresholdGap(gaps, "archetypes", report.ArchetypeCount, threshold.ArchetypeCount);
        AddThresholdGap(gaps, "coreArchetypes", report.CoreArchetypeCount, threshold.CoreArchetypeCount);
        AddThresholdGap(gaps, "specialists", report.SpecialistArchetypeCount, threshold.SpecialistArchetypeCount);
        AddThresholdGap(gaps, "skills", report.SkillCount, threshold.SkillCount);
        AddThresholdGap(gaps, "equippables", report.EquippableCount, threshold.EquippableCount);
        AddThresholdGap(gaps, "passiveBoards", report.PassiveBoardCount, threshold.PassiveBoardCount);
        AddThresholdGap(gaps, "passiveNodes", report.PassiveNodeCount, threshold.PassiveNodeCount);
        AddThresholdGap(gaps, "tempAugments", report.TemporaryAugmentCount, threshold.TemporaryAugmentCount);
        AddThresholdGap(gaps, "permAugments", report.PermanentAugmentCount, threshold.PermanentAugmentCount);
        AddThresholdGap(gaps, "synergyFamilies", report.SynergyFamilyCount, threshold.SynergyFamilyCount);
        return gaps;
    }

    private static void AddThresholdGap(ICollection<string> gaps, string label, int current, int? target)
    {
        if (!target.HasValue || current >= target.Value)
        {
            return;
        }

        gaps.Add($"{label} {current}/{target.Value}");
    }

    private static void ValidateDefinedEnum<TEnum>(TEnum value, string label, string assetPath, ICollection<string> errors)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            errors.Add($"{label} is not a defined enum value: {assetPath}");
        }
    }

    private static void ValidateStableTags(
        ICollection<string> errors,
        IEnumerable<StableTagDefinition> tags,
        string assetPath,
        string scope)
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
                errors.Add($"{scope} tag is missing id: {assetPath}");
                continue;
            }

            ids.Add(tag.Id);
        }

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            errors.Add($"{scope} tags contain duplicates: {assetPath}");
        }
    }
}
