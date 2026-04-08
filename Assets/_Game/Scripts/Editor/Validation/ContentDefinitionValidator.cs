using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
    public int? AffixCount { get; init; }
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
    public int TeamTacticCount { get; init; }
    public int RoleInstructionCount { get; init; }
}

public static class ContentDefinitionValidator
{
    public static LaunchScopeThreshold CurrentMvpMinimum => ContentValidationPolicyCatalog.CurrentMvpMinimum;
    public static LaunchScopeThreshold PaidLaunchFloor => ContentValidationPolicyCatalog.PaidLaunchFloor;
    public static LaunchScopeThreshold PaidLaunchSafeTarget => ContentValidationPolicyCatalog.PaidLaunchSafeTarget;

    public static StatIdValidationStatus GetStatIdStatus(string statId)
    {
        return ContentValidationPolicyCatalog.GetStatIdStatus(statId);
    }

    public static LaunchScopeCountReport BuildLaunchScopeCountReport()
    {
        return ContentValidationCompositionRoot.Validator.BuildLaunchScopeCountReport();
    }

    internal static string ResolveAssetPath(UnityEngine.Object asset)
    {
        return ContentValidationCompositionRoot.UnityGateway.ResolveAssetPath(asset);
    }

    internal static void RegisterResolvedAssetPath(UnityEngine.Object asset, string assetPath)
    {
        ContentValidationCompositionRoot.UnityGateway.RegisterResolvedAssetPath(asset, assetPath);
    }

    public static LaunchScopeCountReport BuildLaunchScopeCountReport(IEnumerable<ScriptableObject> assets)
    {
        return ContentValidationCompositionRoot.Validator.BuildLaunchScopeCountReport(assets);
    }

    public static string GetDefaultReportDirectory()
    {
        return ContentValidationCompositionRoot.ReportPaths.GetDefaultReportDirectory();
    }

    [MenuItem("SM/Internal/Validation/Validate Content Definitions")]
    public static void Validate()
    {
        var report = ValidateAndWriteReport();
        LogReport(report);
        if (report.Issues.Any(issue => issue.Severity == ContentValidationSeverity.Error))
        {
            throw new Exception($"SM content validation failed with {report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error)} issue(s).");
        }

        Debug.Log($"SM content validation passed. Report: {report.JsonReportPath}");
    }

    [MenuItem("SM/Internal/Validation/Write Content Validation Report")]
    public static void WriteReportMenu()
    {
        var report = WriteValidationReport(BuildValidationReport());
        LogReport(report);
        Debug.Log($"SM content validation report written. Json={report.JsonReportPath} Markdown={report.MarkdownSummaryPath}");
    }

    public static ContentValidationReport ValidateAndWriteReport()
    {
        return WriteValidationReport(BuildValidationReport());
    }

    public static ContentValidationReport BuildValidationReport()
    {
        return ContentValidationCompositionRoot.Validator.BuildReport();
    }

    public static ContentValidationReport BuildValidationReport(IEnumerable<ScriptableObject> assets)
    {
        return ContentValidationCompositionRoot.Validator.BuildReport(assets);
    }

    public static ContentValidationReport WriteValidationReport(ContentValidationReport report)
    {
        return ContentValidationCompositionRoot.ReportWriter.Write(report);
    }

    private static void LogReport(ContentValidationReport report)
    {
        foreach (var issue in report.Issues)
        {
            if (issue.Severity == ContentValidationSeverity.Error)
            {
                Debug.LogError($"{issue.Code}: {issue.Message} [{issue.AssetPath}]");
                continue;
            }

            if (issue.Severity == ContentValidationSeverity.Warning)
            {
                Debug.LogWarning($"{issue.Code}: {issue.Message} [{issue.AssetPath}]");
            }
        }
    }
}
