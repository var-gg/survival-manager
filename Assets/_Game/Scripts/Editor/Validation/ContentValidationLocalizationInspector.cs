using System;
using System.Collections;
using System.Collections.Generic;
using SM.Content.Definitions;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace SM.Editor.Validation;

internal interface ILocalizationInspector
{
    void InspectAsset(ValidationAssetDescriptor descriptor, ValidationReportBuilder builder);
}

internal sealed class ReflectionLocalizationInspector : ILocalizationInspector
{
    public void InspectAsset(ValidationAssetDescriptor descriptor, ValidationReportBuilder builder)
    {
        ValidateLocalizationObject(descriptor.Asset, descriptor.AssetPath, descriptor.AssetType.Name, builder);

        switch (descriptor.Asset)
        {
            case TraitPoolDefinition traitPool:
                ValidateNestedObjects(traitPool.PositiveTraits, descriptor.AssetPath, "PositiveTraits", builder);
                ValidateNestedObjects(traitPool.NegativeTraits, descriptor.AssetPath, "NegativeTraits", builder);
                break;
            case RewardTableDefinition rewardTable:
                ValidateNestedObjects(rewardTable.Rewards, descriptor.AssetPath, "Rewards", builder);
                break;
            case ExpeditionDefinition expedition:
                ValidateNestedObjects(expedition.Nodes, descriptor.AssetPath, "Nodes", builder);
                break;
            case SynergyDefinition synergy:
                ValidateNestedObjects(synergy.Tiers, descriptor.AssetPath, "Tiers", builder);
                break;
        }
    }

    private static void ValidateNestedObjects(IEnumerable objects, string assetPath, string scope, ValidationReportBuilder builder)
    {
        var index = 0;
        foreach (var item in objects)
        {
            if (item != null && item is not UnityEngine.Object)
            {
                ValidateLocalizationObject(item, assetPath, $"{scope}[{index}]", builder);
            }

            index++;
        }
    }

    private static void ValidateLocalizationObject(object target, string assetPath, string scope, ValidationReportBuilder builder)
    {
        var type = target.GetType();
        var tableName = ContentLocalizationTables.GetTableName(type);

        foreach (var fieldName in EnumerateLocalizationFieldNames(type))
        {
            var key = type.GetField(fieldName, ContentValidationPolicyCatalog.InstanceFlags)?.GetValue(target) as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                ContentValidationIssueFactory.AddError(builder.Issues, "localization.missing_key", $"Missing localization key '{fieldName}' on {scope}.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (!LocalizationKeyPattern.IsValid(key))
            {
                ContentValidationIssueFactory.AddError(builder.Issues, "localization.invalid_key", $"Invalid localization key '{key}' on {scope}.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (ShouldValidateTableEntries(tableName))
            {
                builder.RegisterLocalizationKey(tableName, key, $"{assetPath} ({scope}.{fieldName})");
                ValidateTableEntries(tableName, key, assetPath, scope, fieldName, builder.Issues);
            }
        }

        ValidateLegacyText(target, assetPath, scope, builder.Issues);
    }

    private static IEnumerable<string> EnumerateLocalizationFieldNames(Type type)
    {
        if (type.GetField("NameKey", ContentValidationPolicyCatalog.InstanceFlags) != null)
        {
            yield return "NameKey";
        }

        if (type.GetField("DescriptionKey", ContentValidationPolicyCatalog.InstanceFlags) != null)
        {
            yield return "DescriptionKey";
        }

        if (type.GetField("LabelKey", ContentValidationPolicyCatalog.InstanceFlags) != null)
        {
            yield return "LabelKey";
        }

        if (type.GetField("RewardSummaryKey", ContentValidationPolicyCatalog.InstanceFlags) != null)
        {
            yield return "RewardSummaryKey";
        }
    }

    private static void ValidateLegacyText(object target, string assetPath, string scope, ICollection<ContentValidationIssue> issues)
    {
        foreach (var (propertyName, fieldName) in new[]
                 {
                     ("LegacyDisplayName", "legacyDisplayName"),
                     ("LegacyDescription", "legacyDescription"),
                     ("LegacyLabel", "legacyLabel"),
                 })
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

    private static void ValidateTableEntries(
        string tableName,
        string key,
        string assetPath,
        string scope,
        string fieldName,
        ICollection<ContentValidationIssue> issues)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null)
        {
            ContentValidationIssueFactory.AddError(issues, "localization.missing_collection", $"Missing string table collection '{tableName}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
            return;
        }

        foreach (var localeCode in ContentValidationPolicyCatalog.RequiredLocaleCodes)
        {
            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table == null)
            {
                ContentValidationIssueFactory.AddError(issues, "localization.missing_locale_table", $"Missing locale table '{tableName}/{localeCode}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (table.GetEntry(key) == null)
            {
                var severity = ContentLocalizationPolicy.TreatsMissingLocalizationAsError
                    ? ContentValidationSeverity.Error
                    : ContentValidationSeverity.Warning;
                ContentValidationIssueFactory.AddIssue(issues, severity, "localization.missing_entry", $"Missing localized entry '{tableName}/{localeCode}:{key}'.", assetPath, $"{scope}.{fieldName}");
            }
        }
    }
}
