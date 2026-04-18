using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Validation;

internal sealed record LocalizedFieldDescriptor(string FieldName);

internal sealed record LocalizedCollectionDescriptor(string ScopeName, Func<object, IEnumerable?> Selector);

internal sealed class LocalizationShape
{
    public LocalizationShape(
        string tableName,
        IReadOnlyList<LocalizedFieldDescriptor> fields,
        IReadOnlyList<LocalizedCollectionDescriptor>? nestedCollections = null,
        bool inspectLegacyText = true)
    {
        TableName = tableName;
        Fields = fields;
        NestedCollections = nestedCollections ?? Array.Empty<LocalizedCollectionDescriptor>();
        InspectLegacyText = inspectLegacyText;
    }

    internal string TableName { get; }
    internal IReadOnlyList<LocalizedFieldDescriptor> Fields { get; }
    internal IReadOnlyList<LocalizedCollectionDescriptor> NestedCollections { get; }
    internal bool InspectLegacyText { get; }
}

internal interface ILocalizationShapeProvider
{
    bool TryGetShape(Type type, out LocalizationShape shape);
}

internal interface ILocalizationTableCollection
{
    bool HasLocaleTable(string localeCode);
    bool HasEntry(string localeCode, string key);
}

internal interface ILocalizationEntryLookup
{
    ILocalizationTableCollection? GetCollection(string tableName);
}

internal interface ILocalizationInspector
{
    void InspectAsset(ValidationAssetDescriptor descriptor, ValidationReportBuilder builder);
}

internal sealed class CompositeLocalizationShapeProvider : ILocalizationShapeProvider
{
    private readonly IReadOnlyList<ILocalizationShapeProvider> _providers;

    public CompositeLocalizationShapeProvider(params ILocalizationShapeProvider[] providers)
    {
        _providers = providers;
    }

    public bool TryGetShape(Type type, out LocalizationShape shape)
    {
        foreach (var provider in _providers)
        {
            if (provider.TryGetShape(type, out shape!))
            {
                return true;
            }
        }

        shape = null!;
        return false;
    }
}

internal sealed class DefaultLocalizationShapeProvider : ILocalizationShapeProvider
{
    private readonly IReadOnlyDictionary<Type, LocalizationShape> _shapes;

    public DefaultLocalizationShapeProvider()
    {
        _shapes = new Dictionary<Type, LocalizationShape>
        {
            [typeof(StatDefinition)] = CreateShape<StatDefinition>("NameKey", "DescriptionKey"),
            [typeof(RaceDefinition)] = CreateShape<RaceDefinition>("NameKey", "DescriptionKey"),
            [typeof(ClassDefinition)] = CreateShape<ClassDefinition>("NameKey", "DescriptionKey"),
            [typeof(CharacterDefinition)] = CreateShape<CharacterDefinition>("NameKey", "DescriptionKey"),
            [typeof(TraitPoolDefinition)] = CreateShape<TraitPoolDefinition>(
                Array.Empty<string>(),
                CreateCollectionDescriptor<TraitPoolDefinition>("PositiveTraits", asset => asset.PositiveTraits),
                CreateCollectionDescriptor<TraitPoolDefinition>("NegativeTraits", asset => asset.NegativeTraits)),
            [typeof(UnitArchetypeDefinition)] = CreateShape<UnitArchetypeDefinition>("NameKey", "DescriptionKey"),
            [typeof(SkillDefinitionAsset)] = CreateShape<SkillDefinitionAsset>("NameKey", "DescriptionKey"),
            [typeof(AugmentDefinition)] = CreateShape<AugmentDefinition>("NameKey", "DescriptionKey"),
            [typeof(ItemBaseDefinition)] = CreateShape<ItemBaseDefinition>("NameKey", "DescriptionKey"),
            [typeof(AffixDefinition)] = CreateShape<AffixDefinition>("NameKey", "DescriptionKey"),
            [typeof(StableTagDefinition)] = CreateShape<StableTagDefinition>("NameKey", "DescriptionKey"),
            [typeof(TeamTacticDefinition)] = CreateShape<TeamTacticDefinition>("NameKey", "DescriptionKey"),
            [typeof(RoleInstructionDefinition)] = CreateShape<RoleInstructionDefinition>("NameKey"),
            [typeof(PassiveBoardDefinition)] = CreateShape<PassiveBoardDefinition>("NameKey", "DescriptionKey"),
            [typeof(PassiveNodeDefinition)] = CreateShape<PassiveNodeDefinition>("NameKey", "DescriptionKey"),
            [typeof(SynergyDefinition)] = CreateShape<SynergyDefinition>(
                new[] { "NameKey", "DescriptionKey" },
                CreateCollectionDescriptor<SynergyDefinition>("Tiers", asset => asset.Tiers)),
            [typeof(SynergyTierDefinition)] = CreateShape<SynergyTierDefinition>("NameKey", "DescriptionKey"),
            [typeof(ExpeditionDefinition)] = CreateShape<ExpeditionDefinition>(
                new[] { "NameKey", "DescriptionKey" },
                CreateCollectionDescriptor<ExpeditionDefinition>("Nodes", asset => asset.Nodes)),
            [typeof(RewardTableDefinition)] = CreateShape<RewardTableDefinition>(
                new[] { "NameKey" },
                CreateCollectionDescriptor<RewardTableDefinition>("Rewards", asset => asset.Rewards)),
            [typeof(CampaignChapterDefinition)] = CreateShape<CampaignChapterDefinition>("NameKey", "DescriptionKey"),
            [typeof(ExpeditionSiteDefinition)] = CreateShape<ExpeditionSiteDefinition>("NameKey", "DescriptionKey"),
            [typeof(EncounterDefinition)] = CreateShape<EncounterDefinition>("NameKey", "DescriptionKey"),
            [typeof(EnemySquadTemplateDefinition)] = CreateShape<EnemySquadTemplateDefinition>("NameKey", "DescriptionKey"),
            [typeof(BossOverlayDefinition)] = CreateShape<BossOverlayDefinition>("NameKey", "DescriptionKey"),
            [typeof(StatusFamilyDefinition)] = CreateShape<StatusFamilyDefinition>("NameKey", "DescriptionKey"),
            [typeof(CleanseProfileDefinition)] = CreateShape<CleanseProfileDefinition>("NameKey", "DescriptionKey"),
            [typeof(ControlDiminishingRuleDefinition)] = CreateShape<ControlDiminishingRuleDefinition>("NameKey", "DescriptionKey"),
            [typeof(RewardSourceDefinition)] = CreateShape<RewardSourceDefinition>("NameKey", "DescriptionKey"),
            [typeof(DropTableDefinition)] = CreateShape<DropTableDefinition>("NameKey", "DescriptionKey"),
            [typeof(LootBundleDefinition)] = CreateShape<LootBundleDefinition>("NameKey", "DescriptionKey"),
            [typeof(TraitTokenDefinition)] = CreateShape<TraitTokenDefinition>("NameKey", "DescriptionKey"),
            [typeof(TraitEntry)] = CreateShape<TraitEntry>("NameKey", "DescriptionKey"),
            [typeof(RewardEntry)] = CreateShape<RewardEntry>("LabelKey", "RewardSummaryKey"),
            [typeof(ExpeditionNodeDefinition)] = CreateShape<ExpeditionNodeDefinition>("LabelKey", "DescriptionKey", "RewardSummaryKey"),
        };
    }

    public bool TryGetShape(Type type, out LocalizationShape shape)
    {
        return _shapes.TryGetValue(type, out shape!);
    }

    private static LocalizationShape CreateShape<T>(params string[] fieldNames)
    {
        return CreateShape<T>(fieldNames, Array.Empty<LocalizedCollectionDescriptor>());
    }

    private static LocalizationShape CreateShape<T>(IReadOnlyList<string> fieldNames, params LocalizedCollectionDescriptor[] nestedCollections)
    {
        var fields = fieldNames
            .Where(fieldName => typeof(T).GetField(fieldName, ContentValidationPolicyCatalog.InstanceFlags) != null)
            .Select(fieldName => new LocalizedFieldDescriptor(fieldName))
            .ToList();

        return new LocalizationShape(
            ContentLocalizationTables.GetTableName(typeof(T)),
            fields,
            nestedCollections);
    }

    private static LocalizedCollectionDescriptor CreateCollectionDescriptor<T>(
        string scopeName,
        Func<T, IEnumerable?> selector)
    {
        return new LocalizedCollectionDescriptor(scopeName, target => selector((T)target));
    }
}

internal sealed class ReflectionFallbackLocalizationShapeProvider : ILocalizationShapeProvider
{
    private static readonly string[] KnownFieldNames = { "NameKey", "DescriptionKey", "LabelKey", "RewardSummaryKey" };
    private static readonly (string PropertyName, string FieldName)[] LegacyTextMembers =
    {
        ("LegacyDisplayName", "legacyDisplayName"),
        ("LegacyDescription", "legacyDescription"),
        ("LegacyLabel", "legacyLabel"),
    };

    public bool TryGetShape(Type type, out LocalizationShape shape)
    {
        var fields = KnownFieldNames
            .Where(fieldName => type.GetField(fieldName, ContentValidationPolicyCatalog.InstanceFlags) != null)
            .Select(fieldName => new LocalizedFieldDescriptor(fieldName))
            .ToList();

        var hasLegacyMembers = LegacyTextMembers.Any(member =>
            type.GetProperty(member.PropertyName, ContentValidationPolicyCatalog.InstanceFlags) != null
            || type.GetField(member.FieldName, ContentValidationPolicyCatalog.InstanceFlags) != null);

        if (fields.Count == 0 && !hasLegacyMembers)
        {
            shape = null!;
            return false;
        }

        shape = new LocalizationShape(ContentLocalizationTables.GetTableName(type), fields);
        return true;
    }
}

internal sealed class UnityLocalizationEntryLookup : ILocalizationEntryLookup
{
    public ILocalizationTableCollection? GetCollection(string tableName)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        return collection == null ? null : new UnityLocalizationTableCollection(collection);
    }

    private sealed class UnityLocalizationTableCollection : ILocalizationTableCollection
    {
        private readonly StringTableCollection _collection;

        public UnityLocalizationTableCollection(StringTableCollection collection)
        {
            _collection = collection;
        }

        public bool HasLocaleTable(string localeCode)
        {
            return _collection.GetTable(new LocaleIdentifier(localeCode)) is StringTable;
        }

        public bool HasEntry(string localeCode, string key)
        {
            return _collection.GetTable(new LocaleIdentifier(localeCode)) is StringTable table
                   && table.GetEntry(key) != null;
        }
    }
}

internal sealed class DescriptorDrivenLocalizationInspector : ILocalizationInspector
{
    private static readonly (string PropertyName, string FieldName)[] LegacyTextMembers =
    {
        ("LegacyDisplayName", "legacyDisplayName"),
        ("LegacyDescription", "legacyDescription"),
        ("LegacyLabel", "legacyLabel"),
    };

    private readonly ILocalizationShapeProvider _shapeProvider;
    private readonly ILocalizationEntryLookup _entryLookup;

    public DescriptorDrivenLocalizationInspector(
        ILocalizationShapeProvider shapeProvider,
        ILocalizationEntryLookup entryLookup)
    {
        _shapeProvider = shapeProvider;
        _entryLookup = entryLookup;
    }

    public void InspectAsset(ValidationAssetDescriptor descriptor, ValidationReportBuilder builder)
    {
        InspectTarget(descriptor.Asset, descriptor.AssetPath, descriptor.AssetType.Name, builder);
    }

    private void InspectTarget(object target, string assetPath, string scope, ValidationReportBuilder builder)
    {
        if (target == null || !_shapeProvider.TryGetShape(target.GetType(), out var shape))
        {
            return;
        }

        foreach (var field in shape.Fields)
        {
            ValidateField(field, target, shape.TableName, assetPath, scope, builder);
        }

        if (shape.InspectLegacyText)
        {
            ValidateLegacyText(target, assetPath, scope, builder.Issues);
        }

        foreach (var nestedCollection in shape.NestedCollections)
        {
            InspectNestedCollection(target, nestedCollection, assetPath, scope, builder);
        }
    }

    private void InspectNestedCollection(
        object target,
        LocalizedCollectionDescriptor nestedCollection,
        string assetPath,
        string scope,
        ValidationReportBuilder builder)
    {
        var items = nestedCollection.Selector(target);
        if (items == null)
        {
            return;
        }

        var index = 0;
        foreach (var item in items)
        {
            if (item != null && item is not UnityEngine.Object)
            {
                var nestedScope = string.Equals(scope, target.GetType().Name, StringComparison.Ordinal)
                    ? $"{nestedCollection.ScopeName}[{index}]"
                    : $"{scope}.{nestedCollection.ScopeName}[{index}]";
                InspectTarget(item, assetPath, nestedScope, builder);
            }

            index++;
        }
    }

    private void ValidateField(
        LocalizedFieldDescriptor field,
        object target,
        string tableName,
        string assetPath,
        string scope,
        ValidationReportBuilder builder)
    {
        var value = target.GetType()
            .GetField(field.FieldName, ContentValidationPolicyCatalog.InstanceFlags)?
            .GetValue(target) as string ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            ContentValidationIssueFactory.AddError(builder.Issues, "localization.missing_key", $"Missing localization key '{field.FieldName}' on {scope}.", assetPath, $"{scope}.{field.FieldName}");
            return;
        }

        if (!LocalizationKeyPattern.IsValid(value))
        {
            ContentValidationIssueFactory.AddError(builder.Issues, "localization.invalid_key", $"Invalid localization key '{value}' on {scope}.", assetPath, $"{scope}.{field.FieldName}");
            return;
        }

        if (!ShouldValidateTableEntries(tableName))
        {
            return;
        }

        builder.RegisterLocalizationKey(tableName, value, $"{assetPath} ({scope}.{field.FieldName})");
        ValidateTableEntries(tableName, value, assetPath, scope, field.FieldName, builder.Issues);
    }

    private void ValidateLegacyText(object target, string assetPath, string scope, ICollection<ContentValidationIssue> issues)
    {
        foreach (var (propertyName, fieldName) in LegacyTextMembers)
        {
            var property = target.GetType().GetProperty(propertyName, ContentValidationPolicyCatalog.InstanceFlags);
            var value = property?.PropertyType == typeof(string)
                ? property.GetValue(target) as string
                : null;

            if (string.IsNullOrWhiteSpace(value))
            {
                var field = target.GetType().GetField(fieldName, ContentValidationPolicyCatalog.InstanceFlags);
                if (field?.FieldType == typeof(string))
                {
                    value = field.GetValue(target) as string;
                }
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (ContentLocalizationPolicy.TreatsLegacyTextAsError)
            {
                ContentValidationIssueFactory.AddError(issues, "localization.legacy_text", $"Legacy localized prose remains in {scope}.{propertyName}.", assetPath, $"{scope}.{propertyName}");
            }
            else
            {
                ContentValidationIssueFactory.AddWarning(issues, "localization.legacy_text", $"Legacy localized prose remains in {scope}.{propertyName}.", assetPath, $"{scope}.{propertyName}");
            }
        }
    }

    private static bool ShouldValidateTableEntries(string tableName)
    {
        return !string.IsNullOrWhiteSpace(tableName)
               && !string.Equals(tableName, ContentLocalizationTables.SystemMessages, StringComparison.Ordinal);
    }

    private void ValidateTableEntries(
        string tableName,
        string key,
        string assetPath,
        string scope,
        string fieldName,
        ICollection<ContentValidationIssue> issues)
    {
        var collection = _entryLookup.GetCollection(tableName);
        if (collection == null)
        {
            ContentValidationIssueFactory.AddError(issues, "localization.missing_collection", $"Missing string table collection '{tableName}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
            return;
        }

        foreach (var localeCode in ContentValidationPolicyCatalog.RequiredLocaleCodes)
        {
            if (!collection.HasLocaleTable(localeCode))
            {
                ContentValidationIssueFactory.AddError(issues, "localization.missing_locale_table", $"Missing locale table '{tableName}/{localeCode}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (!collection.HasEntry(localeCode, key))
            {
                var severity = ContentLocalizationPolicy.TreatsMissingLocalizationAsError
                    ? ContentValidationSeverity.Error
                    : ContentValidationSeverity.Warning;
                ContentValidationIssueFactory.AddIssue(issues, severity, "localization.missing_entry", $"Missing localized entry '{tableName}/{localeCode}:{key}'.", assetPath, $"{scope}.{fieldName}");
            }
        }
    }
}
