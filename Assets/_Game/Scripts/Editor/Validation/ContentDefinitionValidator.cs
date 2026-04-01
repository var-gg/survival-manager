using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
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
    private const string ReportFolderName = "Logs/content-validation";
    private const string JsonReportFileName = "content-validation-report.json";
    private const string MarkdownSummaryFileName = "content-validation-summary.md";
    private const string BudgetAuditJsonFileName = "content_budget_audit.json";
    private const string BudgetAuditMarkdownFileName = "content_budget_audit.md";
    private const string CounterCoverageMatrixMarkdownFileName = "counter_coverage_matrix.md";
    private const string ForbiddenFeatureReportMarkdownFileName = "v1_forbidden_feature_report.md";

    private static readonly string[] RequiredLocaleCodes = { "ko", "en" };
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Regex CanonicalIdPattern = new(
        "^[a-z0-9]+([._][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> CanonicalClassIds = new(StringComparer.Ordinal) { "vanguard", "duelist", "ranger", "mystic" };
    private static readonly HashSet<string> AllowedWeaponFamilyIds = new(StringComparer.Ordinal) { "shield", "blade", "bow", "focus", "greatblade", "polearm" };
    private static readonly HashSet<string> AllowedCraftCurrencyIds = new(StringComparer.Ordinal) { "gold", "ember_dust", "echo_crystal", "boss_sigil" };
    private static readonly HashSet<string> RequiredLaunchStatusIds = new(StringComparer.Ordinal)
    {
        "stun", "root", "silence", "slow",
        "burn", "bleed", "wound", "sunder",
        "marked", "exposed",
        "barrier", "guarded", "unstoppable"
    };
    private static readonly HashSet<string> RequiredCleanseProfileIds = new(StringComparer.Ordinal)
    {
        "cleanse_basic",
        "cleanse_control",
        "break_and_unstoppable",
    };
    private static readonly HashSet<string> RequiredTraitTokenIds = new(StringComparer.Ordinal)
    {
        "trait_reroll_token",
        "trait_lock_token",
        "trait_purge_token",
    };
    private static readonly HashSet<string> AllowedRoleFamilyTags = new(StringComparer.Ordinal) { "vanguard", "striker", "ranger", "mystic" };
    private static readonly HashSet<string> AllowedRoleInstructionTags = new(StringComparer.Ordinal) { "anchor", "bruiser", "carry", "support", "frontline", "backline" };
    private static readonly HashSet<int> RequiredSynergyThresholds = new() { 2, 4 };
    private static readonly Dictionary<int, string> FallbackResolvedAssetPaths = new();

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
        AffixCount = 24,
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
        AffixCount = 30,
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

    internal static string ResolveAssetPath(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

#if UNITY_EDITOR
        var assetPath = AssetDatabase.GetAssetPath(asset);
        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            return assetPath;
        }
#endif

        return FallbackResolvedAssetPaths.TryGetValue(asset.GetInstanceID(), out var fallbackPath)
            ? fallbackPath
            : asset.name ?? string.Empty;
    }

    internal static void RegisterResolvedAssetPath(UnityEngine.Object asset, string assetPath)
    {
        if (asset == null || string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        FallbackResolvedAssetPaths[asset.GetInstanceID()] = assetPath;
    }

    public static LaunchScopeCountReport BuildLaunchScopeCountReport(IEnumerable<ScriptableObject> assets)
    {
        return BuildLaunchScopeCountReportInternal(assets);
    }

    public static string GetDefaultReportDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFolderName));
    }

    [MenuItem("SM/Validation/Validate Content Definitions")]
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

    [MenuItem("SM/Validation/Write Content Validation Report")]
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
#if UNITY_EDITOR
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
#endif
        return BuildValidationReport(LoadAllDefinitionAssets());
    }

    public static ContentValidationReport BuildValidationReport(IEnumerable<ScriptableObject> assets)
    {
        var allAssets = assets
            .Where(asset => asset != null)
            .ToList();
        var ids = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var localizationKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var passiveBoardsByClassId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var issues = new List<ContentValidationIssue>();

        foreach (var asset in allAssets)
        {
            var assetPath = ResolveAssetPath(asset);
            ValidateLocalizationAsset(asset, assetPath, localizationKeys, issues);

            switch (asset)
            {
                case StatDefinition stat:
                    RegisterId(ids, nameof(StatDefinition), stat.Id, assetPath);
                    ValidateCanonicalId(stat.Id, assetPath, "StatDefinition.Id", issues);
                    break;
                case RaceDefinition race:
                    RegisterId(ids, nameof(RaceDefinition), race.Id, assetPath);
                    ValidateCanonicalId(race.Id, assetPath, "RaceDefinition.Id", issues);
                    break;
                case ClassDefinition @class:
                    RegisterId(ids, nameof(ClassDefinition), @class.Id, assetPath);
                    ValidateCanonicalId(@class.Id, assetPath, "ClassDefinition.Id", issues);
                    if (string.Equals(@class.Id, "striker", StringComparison.Ordinal))
                    {
                        AddError(issues, "glossary.class_id", "Class canonical id must remain 'duelist'; 'Striker' is player-facing only.", assetPath, "ClassDefinition.Id");
                    }
                    break;
                case TraitPoolDefinition traitPool:
                    RegisterId(ids, nameof(TraitPoolDefinition), traitPool.Id, assetPath);
                    ValidateCanonicalId(traitPool.Id, assetPath, "TraitPoolDefinition.Id", issues);
                    if (traitPool.PositiveTraits.Count < 3 || traitPool.NegativeTraits.Count < 3)
                    {
                        AddError(issues, "traitpool.structure", "Trait pool must keep the 3+3 positive/negative structure.", assetPath);
                    }
                    break;
                case UnitArchetypeDefinition archetype:
                    RegisterId(ids, nameof(UnitArchetypeDefinition), archetype.Id, assetPath);
                    ValidateCanonicalId(archetype.Id, assetPath, "UnitArchetypeDefinition.Id", issues);
                    ValidateArchetype(archetype, assetPath, issues);
                    break;
                case SkillDefinitionAsset skill:
                    RegisterId(ids, nameof(SkillDefinitionAsset), skill.Id, assetPath);
                    ValidateCanonicalId(skill.Id, assetPath, "SkillDefinitionAsset.Id", issues);
                    ValidateSkill(skill, assetPath, issues);
                    break;
                case AugmentDefinition augment:
                    RegisterId(ids, nameof(AugmentDefinition), augment.Id, assetPath);
                    ValidateCanonicalId(augment.Id, assetPath, "AugmentDefinition.Id", issues);
                    ValidateAugment(augment, assetPath, issues);
                    break;
                case ItemBaseDefinition item:
                    RegisterId(ids, nameof(ItemBaseDefinition), item.Id, assetPath);
                    ValidateCanonicalId(item.Id, assetPath, "ItemBaseDefinition.Id", issues);
                    ValidateItem(item, assetPath, issues);
                    break;
                case AffixDefinition affix:
                    RegisterId(ids, nameof(AffixDefinition), affix.Id, assetPath);
                    ValidateCanonicalId(affix.Id, assetPath, "AffixDefinition.Id", issues);
                    ValidateAffix(affix, assetPath, issues);
                    break;
                case StableTagDefinition tag:
                    RegisterId(ids, nameof(StableTagDefinition), tag.Id, assetPath);
                    ValidateCanonicalId(tag.Id, assetPath, "StableTagDefinition.Id", issues);
                    break;
                case TeamTacticDefinition teamTactic:
                    RegisterId(ids, nameof(TeamTacticDefinition), teamTactic.Id, assetPath);
                    ValidateCanonicalId(teamTactic.Id, assetPath, "TeamTacticDefinition.Id", issues);
                    break;
                case RoleInstructionDefinition roleInstruction:
                    RegisterId(ids, nameof(RoleInstructionDefinition), roleInstruction.Id, assetPath);
                    ValidateCanonicalId(roleInstruction.Id, assetPath, "RoleInstructionDefinition.Id", issues);
                    ValidateRoleInstruction(roleInstruction, assetPath, issues);
                    break;
                case PassiveBoardDefinition board:
                    RegisterId(ids, nameof(PassiveBoardDefinition), board.Id, assetPath);
                    ValidateCanonicalId(board.Id, assetPath, "PassiveBoardDefinition.Id", issues);
                    RegisterId(passiveBoardsByClassId, "PassiveBoardClass", board.ClassId, assetPath);
                    ValidatePassiveBoard(board, assetPath, issues);
                    break;
                case PassiveNodeDefinition passiveNode:
                    RegisterId(ids, nameof(PassiveNodeDefinition), passiveNode.Id, assetPath);
                    ValidateCanonicalId(passiveNode.Id, assetPath, "PassiveNodeDefinition.Id", issues);
                    ValidatePassiveNode(passiveNode, assetPath, issues);
                    break;
                case SynergyDefinition synergy:
                    RegisterId(ids, nameof(SynergyDefinition), synergy.Id, assetPath);
                    ValidateCanonicalId(synergy.Id, assetPath, "SynergyDefinition.Id", issues);
                    ValidateSynergy(synergy, assetPath, issues);
                    break;
                case SynergyTierDefinition tier:
                    RegisterId(ids, nameof(SynergyTierDefinition), tier.Id, assetPath);
                    ValidateCanonicalId(tier.Id, assetPath, "SynergyTierDefinition.Id", issues);
                    ValidateSynergyTier(tier, assetPath, issues);
                    break;
                case ExpeditionDefinition expedition:
                    RegisterId(ids, nameof(ExpeditionDefinition), expedition.Id, assetPath);
                    ValidateCanonicalId(expedition.Id, assetPath, "ExpeditionDefinition.Id", issues);
                    if (expedition.Nodes.Any(node => node.RewardTable == null))
                    {
                        AddError(issues, "expedition.reward_table", "Expedition node is missing a reward table reference.", assetPath);
                    }
                    break;
                case RewardTableDefinition rewardTable:
                    RegisterId(ids, nameof(RewardTableDefinition), rewardTable.Id, assetPath);
                    ValidateCanonicalId(rewardTable.Id, assetPath, "RewardTableDefinition.Id", issues);
                    break;
                case CampaignChapterDefinition chapter:
                    RegisterId(ids, nameof(CampaignChapterDefinition), chapter.Id, assetPath);
                    ValidateCanonicalId(chapter.Id, assetPath, "CampaignChapterDefinition.Id", issues);
                    break;
                case ExpeditionSiteDefinition site:
                    RegisterId(ids, nameof(ExpeditionSiteDefinition), site.Id, assetPath);
                    ValidateCanonicalId(site.Id, assetPath, "ExpeditionSiteDefinition.Id", issues);
                    break;
                case EncounterDefinition encounter:
                    RegisterId(ids, nameof(EncounterDefinition), encounter.Id, assetPath);
                    ValidateCanonicalId(encounter.Id, assetPath, "EncounterDefinition.Id", issues);
                    break;
                case EnemySquadTemplateDefinition squad:
                    RegisterId(ids, nameof(EnemySquadTemplateDefinition), squad.Id, assetPath);
                    ValidateCanonicalId(squad.Id, assetPath, "EnemySquadTemplateDefinition.Id", issues);
                    break;
                case BossOverlayDefinition bossOverlay:
                    RegisterId(ids, nameof(BossOverlayDefinition), bossOverlay.Id, assetPath);
                    ValidateCanonicalId(bossOverlay.Id, assetPath, "BossOverlayDefinition.Id", issues);
                    break;
                case StatusFamilyDefinition statusFamily:
                    RegisterId(ids, nameof(StatusFamilyDefinition), statusFamily.Id, assetPath);
                    ValidateCanonicalId(statusFamily.Id, assetPath, "StatusFamilyDefinition.Id", issues);
                    ValidateStatusFamily(statusFamily, assetPath, issues);
                    break;
                case CleanseProfileDefinition cleanseProfile:
                    RegisterId(ids, nameof(CleanseProfileDefinition), cleanseProfile.Id, assetPath);
                    ValidateCanonicalId(cleanseProfile.Id, assetPath, "CleanseProfileDefinition.Id", issues);
                    break;
                case ControlDiminishingRuleDefinition diminishingRule:
                    RegisterId(ids, nameof(ControlDiminishingRuleDefinition), diminishingRule.Id, assetPath);
                    ValidateCanonicalId(diminishingRule.Id, assetPath, "ControlDiminishingRuleDefinition.Id", issues);
                    break;
                case RewardSourceDefinition rewardSource:
                    RegisterId(ids, nameof(RewardSourceDefinition), rewardSource.Id, assetPath);
                    ValidateCanonicalId(rewardSource.Id, assetPath, "RewardSourceDefinition.Id", issues);
                    break;
                case DropTableDefinition dropTable:
                    RegisterId(ids, nameof(DropTableDefinition), dropTable.Id, assetPath);
                    ValidateCanonicalId(dropTable.Id, assetPath, "DropTableDefinition.Id", issues);
                    break;
                case LootBundleDefinition lootBundle:
                    RegisterId(ids, nameof(LootBundleDefinition), lootBundle.Id, assetPath);
                    ValidateCanonicalId(lootBundle.Id, assetPath, "LootBundleDefinition.Id", issues);
                    break;
                case TraitTokenDefinition traitToken:
                    RegisterId(ids, nameof(TraitTokenDefinition), traitToken.Id, assetPath);
                    ValidateCanonicalId(traitToken.Id, assetPath, "TraitTokenDefinition.Id", issues);
                    break;
            }
        }

        foreach (var pair in ids.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value.Count > 1))
        {
            AddError(issues, "id.duplicate", $"Duplicate id '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        foreach (var pair in passiveBoardsByClassId.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value.Count > 1))
        {
            AddError(issues, "passive_board.duplicate_class", $"Duplicate passive board class '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        foreach (var pair in localizationKeys.Where(x => x.Value.Count > 1))
        {
            AddError(issues, "localization.duplicate_key", $"Duplicate localization key '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        var passiveBoardReports = BuildPassiveBoardReports(allAssets);
        foreach (var boardReport in passiveBoardReports)
        {
            ValidatePassiveBoardShape(boardReport, issues);
        }

        var launchScope = BuildLaunchScopeCountReportInternal(allAssets);
        var floorGaps = BuildLaunchScopeGapList(launchScope, PaidLaunchFloor);
        foreach (var gap in floorGaps)
        {
            AddError(issues, "launch_floor.gap", $"Paid launch floor requirement not met: {gap}", ReportFolderName);
        }

        var safeTargetGaps = BuildLaunchScopeGapList(launchScope, PaidLaunchSafeTarget);
        foreach (var gap in BuildLaunchScopeGapList(launchScope, CurrentMvpMinimum))
        {
            AddError(issues, "launch_scope.mvp_gap", $"Current MVP minimum requirement not met: {gap}", ReportFolderName);
        }

        ValidateLaunchFloorCatalogs(allAssets, issues);
        var loopCSummary = LoopCContentGovernanceValidator.Validate(allAssets, issues);

        return new ContentValidationReport
        {
            ValidationPhase = ContentLocalizationPolicy.CurrentPhase,
            LaunchScope = launchScope,
            FloorGaps = floorGaps,
            SafeTargetGaps = safeTargetGaps,
            PassiveBoards = passiveBoardReports,
            LoopC = loopCSummary,
            Issues = issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ToList(),
        };
    }

    public static ContentValidationReport WriteValidationReport(ContentValidationReport report)
    {
        var reportDirectory = GetDefaultReportDirectory();
        Directory.CreateDirectory(reportDirectory);
        var jsonPath = Path.Combine(reportDirectory, JsonReportFileName);
        var markdownPath = Path.Combine(reportDirectory, MarkdownSummaryFileName);
        var budgetAuditJsonPath = Path.Combine(reportDirectory, BudgetAuditJsonFileName);
        var budgetAuditMarkdownPath = Path.Combine(reportDirectory, BudgetAuditMarkdownFileName);
        var counterCoverageMatrixMarkdownPath = Path.Combine(reportDirectory, CounterCoverageMatrixMarkdownFileName);
        var forbiddenFeatureReportMarkdownPath = Path.Combine(reportDirectory, ForbiddenFeatureReportMarkdownFileName);
        var withPaths = report with
        {
            JsonReportPath = jsonPath,
            MarkdownSummaryPath = markdownPath,
            ContentBudgetAuditJsonPath = budgetAuditJsonPath,
            ContentBudgetAuditMarkdownPath = budgetAuditMarkdownPath,
            CounterCoverageMatrixMarkdownPath = counterCoverageMatrixMarkdownPath,
            ForbiddenFeatureReportMarkdownPath = forbiddenFeatureReportMarkdownPath,
        };

        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(withPaths, Formatting.Indented));
        File.WriteAllText(markdownPath, BuildMarkdownSummary(withPaths));
        File.WriteAllText(budgetAuditJsonPath, JsonConvert.SerializeObject(withPaths.LoopC.BudgetAudit, Formatting.Indented));
        File.WriteAllText(budgetAuditMarkdownPath, BuildLoopCBudgetAuditMarkdown(withPaths));
        File.WriteAllText(counterCoverageMatrixMarkdownPath, BuildCounterCoverageMatrixMarkdown(withPaths));
        File.WriteAllText(forbiddenFeatureReportMarkdownPath, BuildForbiddenFeatureReportMarkdown(withPaths));
        return withPaths;
    }

    private static void ValidateLocalizationAsset(
        ScriptableObject asset,
        string assetPath,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<ContentValidationIssue> issues)
    {
        ValidateLocalizationObject(asset, assetPath, asset.GetType().Name, localizationKeys, issues);

        switch (asset)
        {
            case TraitPoolDefinition traitPool:
                ValidateNestedObjects(traitPool.PositiveTraits, assetPath, "PositiveTraits", localizationKeys, issues);
                ValidateNestedObjects(traitPool.NegativeTraits, assetPath, "NegativeTraits", localizationKeys, issues);
                break;
            case RewardTableDefinition rewardTable:
                ValidateNestedObjects(rewardTable.Rewards, assetPath, "Rewards", localizationKeys, issues);
                break;
            case ExpeditionDefinition expedition:
                ValidateNestedObjects(expedition.Nodes, assetPath, "Nodes", localizationKeys, issues);
                break;
            case SynergyDefinition synergy:
                ValidateNestedObjects(synergy.Tiers, assetPath, "Tiers", localizationKeys, issues);
                break;
        }
    }

    private static void ValidateNestedObjects(
        IEnumerable objects,
        string assetPath,
        string scope,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<ContentValidationIssue> issues)
    {
        var index = 0;
        foreach (var item in objects)
        {
            if (item != null && item is not ScriptableObject)
            {
                ValidateLocalizationObject(item, assetPath, $"{scope}[{index}]", localizationKeys, issues);
            }

            index++;
        }
    }

    private static void ValidateLocalizationObject(
        object target,
        string assetPath,
        string scope,
        Dictionary<string, List<string>> localizationKeys,
        ICollection<ContentValidationIssue> issues)
    {
        var type = target.GetType();
        var tableName = ContentLocalizationTables.GetTableName(type);

        foreach (var fieldName in EnumerateLocalizationFieldNames(type))
        {
            var key = type.GetField(fieldName, InstanceFlags)?.GetValue(target) as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                AddError(issues, "localization.missing_key", $"Missing localization key '{fieldName}' on {scope}.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (!LocalizationKeyPattern.IsValid(key))
            {
                AddError(issues, "localization.invalid_key", $"Invalid localization key '{key}' on {scope}.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (ShouldValidateTableEntries(tableName))
            {
                RegisterLocalizationKey(localizationKeys, tableName, key, $"{assetPath} ({scope}.{fieldName})");
                ValidateTableEntries(tableName, key, assetPath, scope, fieldName, issues);
            }
        }

        ValidateLegacyText(target, assetPath, scope, issues);
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

    private static void ValidateLegacyText(object target, string assetPath, string scope, ICollection<ContentValidationIssue> issues)
    {
        foreach (var (propertyName, fieldName) in new[]
                 {
                     ("LegacyDisplayName", "legacyDisplayName"),
                     ("LegacyDescription", "legacyDescription"),
                     ("LegacyLabel", "legacyLabel"),
                 })
        {
            var property = target.GetType().GetProperty(propertyName, InstanceFlags);
            var value = property?.PropertyType == typeof(string)
                ? property.GetValue(target) as string
                : null;

            if (string.IsNullOrWhiteSpace(value))
            {
                var field = target.GetType().GetField(fieldName, InstanceFlags);
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
                AddError(issues, "localization.legacy_text", $"Legacy localized prose remains in {scope}.{propertyName}.", assetPath, $"{scope}.{propertyName}");
            }
            else
            {
                AddWarning(issues, "localization.legacy_text", $"Legacy localized prose remains in {scope}.{propertyName}.", assetPath, $"{scope}.{propertyName}");
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
        string fieldName,
        ICollection<ContentValidationIssue> issues)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null)
        {
            AddError(issues, "localization.missing_collection", $"Missing string table collection '{tableName}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
            return;
        }

        foreach (var localeCode in RequiredLocaleCodes)
        {
            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table == null)
            {
                AddError(issues, "localization.missing_locale_table", $"Missing locale table '{tableName}/{localeCode}' for key '{key}'.", assetPath, $"{scope}.{fieldName}");
                continue;
            }

            if (table.GetEntry(key) == null)
            {
                var severity = ContentLocalizationPolicy.TreatsMissingLocalizationAsError
                    ? ContentValidationSeverity.Error
                    : ContentValidationSeverity.Warning;
                AddIssue(issues, severity, "localization.missing_entry", $"Missing localized entry '{tableName}/{localeCode}:{key}'.", assetPath, $"{scope}.{fieldName}");
            }
        }
    }

    private static void RegisterId(Dictionary<string, List<string>> ids, string kind, string id, string path)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var composite = $"{kind}:{id}";
        if (!ids.TryGetValue(composite, out var list))
        {
            list = new List<string>();
            ids[composite] = list;
        }

        list.Add(path);
    }

    private static void ValidateCanonicalId(string id, string assetPath, string scope, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            AddError(issues, "id.missing", $"Missing canonical id on {scope}.", assetPath, scope);
            return;
        }

        if (!CanonicalIdPattern.IsMatch(id))
        {
            AddError(issues, "id.invalid_pattern", $"Canonical id '{id}' must stay lower-case with '.' or '_' separators.", assetPath, scope);
        }
    }

    private static void ValidateModifiers(
        ICollection<ContentValidationIssue> issues,
        IEnumerable<SerializableStatModifier> modifiers,
        string path,
        string scope)
    {
        foreach (var modifier in modifiers)
        {
            switch (GetStatIdStatus(modifier.StatId))
            {
                case StatIdValidationStatus.Unsupported:
                    AddError(issues, "stat.unsupported", $"Unsupported stat id '{modifier.StatId}'.", path, scope);
                    break;
                case StatIdValidationStatus.LegacyAlias:
                    AddWarning(issues, "stat.legacy_alias", $"Legacy stat alias '{modifier.StatId}' should migrate to canonical id.", path, scope);
                    break;
            }
        }
    }

    private static List<ScriptableObject> LoadAllDefinitionAssets()
    {
        var assetsByPath = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
        var pathScores = new Dictionary<string, int>(StringComparer.Ordinal);
        var looseAssets = new List<ScriptableObject>();
        var root = SampleSeedGenerator.ResourcesRoot.Replace('\\', '/');
        var definitionTypes = GetKnownDefinitionTypes();
        var resourcesAssets = Resources.LoadAll<ScriptableObject>("_Game/Content/Definitions")
            .Where(asset => asset != null)
            .Select(asset => (asset, ResolveAssetPath(asset)))
            .ToList();

        AddLoadedAssets(
            assetsByPath,
            pathScores,
            looseAssets,
            resourcesAssets);

#if UNITY_EDITOR
        var typedQueryCount = 0;
        var genericQueryCount = 0;
        var diskCount = 0;
        if (AssetDatabase.IsValidFolder(root))
        {
            foreach (var definitionType in definitionTypes)
            {
                var paths = AssetDatabase.FindAssets($"t:{definitionType.Name}", new[] { root })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                typedQueryCount += paths.Count;
                AddLoadedAssets(
                    assetsByPath,
                    pathScores,
                    looseAssets,
                    paths.Select(path => (LoadDefinitionAssetAtPath(path, definitionType), path)));
            }

            var genericPaths = AssetDatabase.FindAssets(string.Empty, new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                .ToList();
            genericQueryCount = genericPaths.Count;
            AddLoadedAssets(
                assetsByPath,
                pathScores,
                looseAssets,
                genericPaths.Select(path => (LoadDefinitionAssetAtPath(path), path)));
        }

        if (GetLoadedAssetCount(assetsByPath, looseAssets) == 0)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var diskRoot = Path.Combine(projectRoot, root.Replace('/', Path.DirectorySeparatorChar));
            diskCount = Directory.Exists(diskRoot)
                ? Directory.EnumerateFiles(diskRoot, "*.asset", SearchOption.AllDirectories).Count()
                : 0;
            Debug.LogWarning($"[ContentDefinitionValidator] Zero assets loaded. root={root} validFolder={AssetDatabase.IsValidFolder(root)} resources={resourcesAssets.Count} typedQueries={typedQueryCount} genericQuery={genericQueryCount} disk={diskCount}");
            if (Directory.Exists(diskRoot))
            {
                AddLoadedAssets(
                    assetsByPath,
                    pathScores,
                    looseAssets,
                    Directory.EnumerateFiles(diskRoot, "*.asset", SearchOption.AllDirectories)
                        .Select(path => path.Replace('\\', '/'))
                        .Select(path => path.StartsWith(projectRoot.Replace('\\', '/') + "/", StringComparison.Ordinal)
                            ? path.Substring(projectRoot.Replace('\\', '/').Length + 1)
                            : path)
                        .Select(path => (LoadDefinitionAssetAtPath(path), path)));
            }
        }

        var loadedCount = GetLoadedAssetCount(assetsByPath, looseAssets);
        if (loadedCount == 0 || (genericQueryCount > 0 && loadedCount < genericQueryCount) || (diskCount > 0 && loadedCount < diskCount))
        {
            var fallback = ContentDefinitionFileFallbackLoader.Load();
            AddLoadedAssets(
                assetsByPath,
                pathScores,
                looseAssets,
                fallback.Assets.Select(asset =>
                {
                    var assetPath = fallback.AssetPaths.TryGetValue(asset.GetInstanceID(), out var resolvedPath)
                        ? resolvedPath
                        : asset.name;
                    RegisterResolvedAssetPath(asset, assetPath);
                    return (asset, assetPath);
                }));
        }
#endif

        return assetsByPath.Values
            .Concat(looseAssets)
            .ToList();
    }

    private static void AddLoadedAssets(
        IDictionary<string, ScriptableObject> assetsByPath,
        IDictionary<string, int> pathScores,
        ICollection<ScriptableObject> looseAssets,
        IEnumerable<(ScriptableObject? Asset, string Path)> candidates)
    {
        foreach (var (asset, path) in candidates)
        {
            if (asset == null)
            {
                continue;
            }

            if (ShouldSkipLoadedAsset(asset, path))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                if (!looseAssets.Contains(asset))
                {
                    looseAssets.Add(asset);
                }

                continue;
            }

            var score = GetAssetCompletenessScore(asset);
            if (!assetsByPath.TryGetValue(path, out var existing)
                || !pathScores.TryGetValue(path, out var existingScore)
                || score >= existingScore)
            {
                assetsByPath[path] = asset;
                pathScores[path] = score;
            }
        }
    }

    private static int GetLoadedAssetCount(
        IReadOnlyDictionary<string, ScriptableObject> assetsByPath,
        IReadOnlyCollection<ScriptableObject> looseAssets)
    {
        return assetsByPath.Count + looseAssets.Count;
    }

    private static bool ShouldSkipLoadedAsset(ScriptableObject asset, string path)
    {
        if (asset is SynergyTierDefinition tier)
        {
            return tier.Threshold == 3
                   || path.EndsWith("_3.asset", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static int GetAssetCompletenessScore(ScriptableObject asset)
    {
        var score = 0;
        switch (asset)
        {
            case UnitArchetypeDefinition archetype:
                score += string.IsNullOrWhiteSpace(archetype.Id) ? 0 : 10;
                score += archetype.Race != null ? 5 : 0;
                score += archetype.Class != null ? 5 : 0;
                score += archetype.TraitPool != null ? 5 : 0;
                score += archetype.Skills?.Count ?? 0;
                score += archetype.TacticPreset?.Count ?? 0;
                score += archetype.BudgetCard?.Vector?.FinalScore ?? 0;
                break;
            case SkillDefinitionAsset skill:
                score += string.IsNullOrWhiteSpace(skill.Id) ? 0 : 10;
                score += skill.BudgetCard?.Vector?.FinalScore ?? 0;
                score += skill.CompileTags?.Count ?? 0;
                break;
            case AugmentDefinition augment:
                score += string.IsNullOrWhiteSpace(augment.Id) ? 0 : 10;
                score += augment.BudgetCard?.Vector?.FinalScore ?? 0;
                score += augment.Modifiers?.Count ?? 0;
                break;
            case SynergyTierDefinition tier:
                score += string.IsNullOrWhiteSpace(tier.Id) ? 0 : 10;
                score += tier.BudgetCard?.Vector?.FinalScore ?? 0;
                score += tier.Threshold;
                break;
            case StatusFamilyDefinition status:
                score += string.IsNullOrWhiteSpace(status.Id) ? 0 : 10;
                score += status.BudgetCard?.Vector?.FinalScore ?? 0;
                score += status.DefaultStackCap;
                break;
            case CleanseProfileDefinition cleanse:
                score += string.IsNullOrWhiteSpace(cleanse.Id) ? 0 : 10;
                score += cleanse.RemovesStatusIds?.Count ?? 0;
                score += cleanse.GrantsUnstoppable ? 2 : 0;
                break;
            default:
                score += string.IsNullOrWhiteSpace(GetCanonicalId(asset)) ? 0 : 10;
                break;
        }

        return score;
    }

    private static string GetCanonicalId(ScriptableObject asset)
    {
        return asset switch
        {
            StatDefinition stat => stat.Id,
            RaceDefinition race => race.Id,
            ClassDefinition @class => @class.Id,
            TraitPoolDefinition traitPool => traitPool.Id,
            UnitArchetypeDefinition archetype => archetype.Id,
            SkillDefinitionAsset skill => skill.Id,
            AugmentDefinition augment => augment.Id,
            ItemBaseDefinition item => item.Id,
            AffixDefinition affix => affix.Id,
            StableTagDefinition tag => tag.Id,
            TeamTacticDefinition tactic => tactic.Id,
            RoleInstructionDefinition role => role.Id,
            PassiveBoardDefinition board => board.Id,
            PassiveNodeDefinition node => node.Id,
            SynergyDefinition synergy => synergy.Id,
            SynergyTierDefinition tier => tier.Id,
            ExpeditionDefinition expedition => expedition.Id,
            RewardTableDefinition rewardTable => rewardTable.Id,
            CampaignChapterDefinition chapter => chapter.Id,
            ExpeditionSiteDefinition site => site.Id,
            EncounterDefinition encounter => encounter.Id,
            EnemySquadTemplateDefinition squad => squad.Id,
            BossOverlayDefinition overlay => overlay.Id,
            StatusFamilyDefinition status => status.Id,
            CleanseProfileDefinition cleanse => cleanse.Id,
            ControlDiminishingRuleDefinition controlRule => controlRule.Id,
            RewardSourceDefinition rewardSource => rewardSource.Id,
            DropTableDefinition dropTable => dropTable.Id,
            LootBundleDefinition lootBundle => lootBundle.Id,
            TraitTokenDefinition traitToken => traitToken.Id,
            _ => string.Empty,
        };
    }

#if UNITY_EDITOR
    private static ScriptableObject? LoadDefinitionAssetAtPath(string path)
    {
        return LoadDefinitionAssetAtPath(path, typeof(ScriptableObject));
    }

    private static ScriptableObject? LoadDefinitionAssetAtPath(string path, Type definitionType)
    {
        var typedAsset = AssetDatabase.LoadAssetAtPath(path, definitionType) as ScriptableObject;
        if (typedAsset != null)
        {
            return typedAsset;
        }

        var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
        if (mainAsset != null)
        {
            return mainAsset;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<ScriptableObject>()
            .FirstOrDefault(asset => definitionType == typeof(ScriptableObject) || definitionType.IsInstanceOfType(asset));
    }
#endif

    private static IReadOnlyList<Type> GetKnownDefinitionTypes()
    {
        return new[]
        {
            typeof(StatDefinition),
            typeof(RaceDefinition),
            typeof(ClassDefinition),
            typeof(TraitPoolDefinition),
            typeof(UnitArchetypeDefinition),
            typeof(SkillDefinitionAsset),
            typeof(AugmentDefinition),
            typeof(ItemBaseDefinition),
            typeof(AffixDefinition),
            typeof(StableTagDefinition),
            typeof(TeamTacticDefinition),
            typeof(RoleInstructionDefinition),
            typeof(PassiveBoardDefinition),
            typeof(PassiveNodeDefinition),
            typeof(SynergyDefinition),
            typeof(SynergyTierDefinition),
            typeof(ExpeditionDefinition),
            typeof(RewardTableDefinition),
            typeof(CampaignChapterDefinition),
            typeof(ExpeditionSiteDefinition),
            typeof(EncounterDefinition),
            typeof(EnemySquadTemplateDefinition),
            typeof(BossOverlayDefinition),
            typeof(StatusFamilyDefinition),
            typeof(CleanseProfileDefinition),
            typeof(ControlDiminishingRuleDefinition),
            typeof(RewardSourceDefinition),
            typeof(DropTableDefinition),
            typeof(LootBundleDefinition),
            typeof(TraitTokenDefinition),
        };
    }

    private static LaunchScopeCountReport BuildLaunchScopeCountReportInternal(IEnumerable<ScriptableObject> assets)
    {
        var assetList = assets.ToList();
        var archetypes = assetList.OfType<UnitArchetypeDefinition>().ToList();
        var augments = assetList.OfType<AugmentDefinition>().ToList();
        var authoredSkills = assetList.OfType<SkillDefinitionAsset>()
            .Where(skill => !skill.Id.StartsWith("support_", StringComparison.Ordinal))
            .ToList();

        return new LaunchScopeCountReport
        {
            ArchetypeCount = archetypes.Count,
            CoreArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Core),
            SpecialistArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Specialist),
            SkillCount = authoredSkills.Count,
            EquippableCount = assetList.OfType<ItemBaseDefinition>().Count(),
            AffixCount = assetList.OfType<AffixDefinition>().Count(),
            PassiveBoardCount = assetList.OfType<PassiveBoardDefinition>().Count(),
            PassiveNodeCount = assetList.OfType<PassiveNodeDefinition>().Count(),
            TemporaryAugmentCount = augments.Count(augment => !augment.IsPermanent),
            PermanentAugmentCount = augments.Count(augment => augment.IsPermanent),
            SynergyFamilyCount = assetList.OfType<SynergyDefinition>().Count(),
            TeamTacticCount = assetList.OfType<TeamTacticDefinition>().Count(),
            RoleInstructionCount = assetList.OfType<RoleInstructionDefinition>().Count(),
        };
    }

    private static List<string> BuildLaunchScopeGapList(LaunchScopeCountReport report, LaunchScopeThreshold threshold)
    {
        var gaps = new List<string>();
        AddThresholdGap(gaps, "archetypes", report.ArchetypeCount, threshold.ArchetypeCount);
        AddThresholdGap(gaps, "coreArchetypes", report.CoreArchetypeCount, threshold.CoreArchetypeCount);
        AddThresholdGap(gaps, "specialists", report.SpecialistArchetypeCount, threshold.SpecialistArchetypeCount);
        AddThresholdGap(gaps, "skills", report.SkillCount, threshold.SkillCount);
        AddThresholdGap(gaps, "equippables", report.EquippableCount, threshold.EquippableCount);
        AddThresholdGap(gaps, "affixes", report.AffixCount, threshold.AffixCount);
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

    private static IReadOnlyList<PassiveBoardShapeReport> BuildPassiveBoardReports(IEnumerable<ScriptableObject> assets)
    {
        return assets
            .OfType<PassiveBoardDefinition>()
            .OrderBy(board => board.ClassId, StringComparer.Ordinal)
            .Select(board => new PassiveBoardShapeReport
            {
                BoardId = board.Id,
                ClassId = board.ClassId,
                SmallCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Small),
                NotableCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Notable),
                KeystoneCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Keystone),
            })
            .ToList();
    }

    private static string BuildMarkdownSummary(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Content Validation Summary");
        builder.AppendLine();
        builder.AppendLine($"- generatedAtUtc: `{report.GeneratedAtUtc}`");
        builder.AppendLine($"- validationPhase: `{report.ValidationPhase}`");
        builder.AppendLine($"- errors: `{report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error)}`");
        builder.AppendLine($"- warnings: `{report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning)}`");
        builder.AppendLine();
        builder.AppendLine("## Launch Scope");
        builder.AppendLine();
        builder.AppendLine($"- archetypes: `{report.LaunchScope.ArchetypeCount}` (core `{report.LaunchScope.CoreArchetypeCount}`, specialist `{report.LaunchScope.SpecialistArchetypeCount}`)");
        builder.AppendLine($"- skills: `{report.LaunchScope.SkillCount}`");
        builder.AppendLine($"- equippables: `{report.LaunchScope.EquippableCount}`");
        builder.AppendLine($"- affixes: `{report.LaunchScope.AffixCount}`");
        builder.AppendLine($"- passiveBoards: `{report.LaunchScope.PassiveBoardCount}`");
        builder.AppendLine($"- passiveNodes: `{report.LaunchScope.PassiveNodeCount}`");
        builder.AppendLine($"- tempAugments: `{report.LaunchScope.TemporaryAugmentCount}`");
        builder.AppendLine($"- permAugments: `{report.LaunchScope.PermanentAugmentCount}`");
        builder.AppendLine($"- synergyFamilies: `{report.LaunchScope.SynergyFamilyCount}`");
        builder.AppendLine($"- teamTactics: `{report.LaunchScope.TeamTacticCount}`");
        builder.AppendLine($"- roleInstructions: `{report.LaunchScope.RoleInstructionCount}`");
        builder.AppendLine();
        builder.AppendLine("## Floor Gaps");
        builder.AppendLine();
        if (report.FloorGaps.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var gap in report.FloorGaps)
            {
                builder.AppendLine($"- {gap}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Safe Target Gaps");
        builder.AppendLine();
        if (report.SafeTargetGaps.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var gap in report.SafeTargetGaps)
            {
                builder.AppendLine($"- {gap}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Passive Boards");
        builder.AppendLine();
        foreach (var board in report.PassiveBoards)
        {
            builder.AppendLine($"- `{board.ClassId}`: small `{board.SmallCount}`, notable `{board.NotableCount}`, keystone `{board.KeystoneCount}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Issues");
        builder.AppendLine();
        if (report.Issues.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var issue in report.Issues)
            {
                builder.AppendLine($"- `{issue.Severity}` `{issue.Code}` {issue.Message} ({issue.AssetPath}{(string.IsNullOrWhiteSpace(issue.Scope) ? string.Empty : $" / {issue.Scope}")})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Loop C Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- budgetAuditJson: `{report.ContentBudgetAuditJsonPath}`");
        builder.AppendLine($"- budgetAuditMarkdown: `{report.ContentBudgetAuditMarkdownPath}`");
        builder.AppendLine($"- counterCoverageMatrix: `{report.CounterCoverageMatrixMarkdownPath}`");
        builder.AppendLine($"- forbiddenFeatureReport: `{report.ForbiddenFeatureReportMarkdownPath}`");

        return builder.ToString();
    }

    private static string BuildLoopCBudgetAuditMarkdown(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Content Budget Audit");
        builder.AppendLine();
        builder.AppendLine("| Content | Kind | Domain | Rarity | PowerBand | Role | Final | Derived | Delta | Counters |");
        builder.AppendLine("|---|---|---|---|---|---|---:|---:|---:|---|");
        foreach (var entry in report.LoopC.BudgetAudit)
        {
            builder.AppendLine($"| `{entry.ContentId}` | `{entry.ContentKind}` | `{entry.Domain}` | `{entry.Rarity}` | `{entry.PowerBand}` | `{entry.RoleProfile}` | {entry.BudgetFinalScore} | {entry.DerivedScore} | {entry.DerivedDelta} | {entry.CounterTools} |");
        }

        return builder.ToString();
    }

    private static string BuildCounterCoverageMatrixMarkdown(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Counter Coverage Matrix");
        builder.AppendLine();
        builder.AppendLine("| Content | Kind | Threats | Counters |");
        builder.AppendLine("|---|---|---|---|");
        foreach (var entry in report.LoopC.CounterCoverageMatrix)
        {
            builder.AppendLine($"| `{entry.ContentId}` | `{entry.ContentKind}` | {entry.ThreatPatterns} | {entry.CounterTools} |");
        }

        return builder.ToString();
    }

    private static string BuildForbiddenFeatureReportMarkdown(ContentValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# V1 Forbidden Feature Report");
        builder.AppendLine();
        if (report.LoopC.ForbiddenFeatureEntries.Count == 0)
        {
            builder.AppendLine("- none");
            return builder.ToString();
        }

        foreach (var entry in report.LoopC.ForbiddenFeatureEntries)
        {
            builder.AppendLine($"- `{entry.ContentId}` `{entry.FeatureFlags}` {entry.Reason} ({entry.AssetPath})");
        }

        return builder.ToString();
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

    private static void ValidateArchetype(UnitArchetypeDefinition archetype, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (archetype.Race == null || archetype.Class == null || archetype.TraitPool == null)
        {
            AddError(issues, "archetype.references", "Archetype is missing race/class/trait pool references.", assetPath);
        }

        if (archetype.TacticPreset == null || archetype.TacticPreset.Count == 0)
        {
            AddError(issues, "archetype.tactic_preset", "Archetype is missing a tactic preset.", assetPath);
        }

        ValidateDefinedEnum(archetype.ScopeKind, "Archetype scope", assetPath, issues);
        if (string.IsNullOrWhiteSpace(archetype.RoleFamilyTag))
        {
            AddError(issues, "archetype.role_family", "Archetype is missing RoleFamilyTag.", assetPath);
        }
        else if (!AllowedRoleFamilyTags.Contains(archetype.RoleFamilyTag))
        {
            AddError(issues, "archetype.role_family", $"Archetype role family tag must be one of [{string.Join(", ", AllowedRoleFamilyTags)}].", assetPath);
        }

        if (string.Equals(archetype.Class?.Id, "duelist", StringComparison.Ordinal)
            && !string.Equals(archetype.RoleFamilyTag, "striker", StringComparison.Ordinal))
        {
            AddError(issues, "glossary.duelist_role_family", "Duelist archetypes must expose the player-facing role family tag 'striker'.", assetPath);
        }

        if (string.IsNullOrWhiteSpace(archetype.PrimaryWeaponFamilyTag))
        {
            AddError(issues, "archetype.weapon_family", "Archetype is missing PrimaryWeaponFamilyTag.", assetPath);
        }

        if (archetype.Skills.Count == 0
            && (archetype.Loadout == null || !archetype.Loadout.IsComplete()))
        {
            AddError(issues, "archetype.skill_contract", "Archetype must resolve a Loop A loadout or provide legacy migration skills.", assetPath);
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
                AddError(issues, "archetype.locked_signature_active", "Locked signature active must reference a core_active skill.", assetPath);
            }

            if (!archetype.Skills.Contains(archetype.LockedSignatureActiveSkill))
            {
                AddError(issues, "archetype.locked_signature_active_ref", "Locked signature active must also exist in the 4-slot compiled skill list.", assetPath);
            }
        }

        if (archetype.LockedSignaturePassiveSkill != null)
        {
            if (archetype.LockedSignaturePassiveSkill.SlotKind != SkillSlotKindValue.Passive)
            {
                AddError(issues, "archetype.locked_signature_passive", "Locked signature passive must reference a passive skill.", assetPath);
            }

            if (!archetype.Skills.Contains(archetype.LockedSignaturePassiveSkill))
            {
                AddError(issues, "archetype.locked_signature_passive_ref", "Locked signature passive must also exist in the 4-slot compiled skill list.", assetPath);
            }
        }

        if (archetype.FlexUtilitySkillPool.Any(skill => skill == null))
        {
            AddError(issues, "archetype.flex_utility_pool", "Flex utility pool contains a missing skill reference.", assetPath);
        }
        else if (archetype.FlexUtilitySkillPool.Any(skill => skill.SlotKind != SkillSlotKindValue.UtilityActive))
        {
            AddError(issues, "archetype.flex_utility_pool", "Flex utility pool must contain only utility_active skills.", assetPath);
        }

        if (archetype.FlexSupportSkillPool.Any(skill => skill == null))
        {
            AddError(issues, "archetype.flex_support_pool", "Flex support pool contains a missing skill reference.", assetPath);
        }
        else if (archetype.FlexSupportSkillPool.Any(skill => skill.SlotKind != SkillSlotKindValue.Support))
        {
            AddError(issues, "archetype.flex_support_pool", "Flex support pool must contain only support slot skills.", assetPath);
        }

        LoopAContractValidator.ValidateArchetype(archetype, assetPath, issues);
        LoopBContractValidator.ValidateArchetype(archetype, assetPath, issues);
    }

    private static void ValidateSkill(SkillDefinitionAsset skill, string assetPath, ICollection<ContentValidationIssue> issues)
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
            AddError(issues, "skill.range_band", "Skill RangeMin must be non-negative.", assetPath);
        }

        var resolvedRangeMax = skill.RangeMax >= 0f ? skill.RangeMax : skill.Range;
        if (resolvedRangeMax < Math.Max(0f, skill.RangeMin))
        {
            AddError(issues, "skill.range_band", "Skill RangeMax must be greater than or equal to RangeMin.", assetPath);
        }

        if (skill.Radius < 0f || skill.Width < 0f || skill.ArcDegrees < 0f || skill.ArcDegrees > 360f)
        {
            AddError(issues, "skill.shape", "Skill radius/width/arc must stay within non-negative bounds and arc must not exceed 360 degrees.", assetPath);
        }

        if (skill.ResourceCost < -1f || skill.CooldownSeconds < -1f || skill.RecoverySeconds < -1f || skill.PowerBudget < 0f)
        {
            AddError(issues, "skill.schema_budget", "Skill resource/cooldown/recovery values must be non-negative or use -1 fallback, and PowerBudget must be non-negative.", assetPath);
        }

        if (skill.AiIntents.Distinct().Count() != skill.AiIntents.Count)
        {
            AddError(issues, "skill.ai_intents", "Skill AI intents contain duplicates.", assetPath);
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
                AddError(issues, "skill.ai_score_hints", "Skill AI score hints must keep health ratios within 0..1 and distance bands ordered.", assetPath);
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

    private static void ValidateAugment(AugmentDefinition augment, string assetPath, ICollection<ContentValidationIssue> issues)
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
            AddError(issues, "augment.family_id", "Augment is missing FamilyId.", assetPath);
        }

        if (augment.BudgetScore < 0f)
        {
            AddError(issues, "augment.budget_score", "Augment BudgetScore must be non-negative.", assetPath);
        }

        if (augment.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
        {
            AddError(issues, "augment.rule_tag", "Augment has an empty rule modifier tag.", assetPath);
        }

        if (augment.BuildBiasTags.Select(tag => tag?.Id ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Intersect(augment.ProtectionTags.Select(tag => tag?.Id ?? string.Empty), StringComparer.Ordinal)
            .Any())
        {
            AddError(issues, "augment.bias_tag_overlap", "Augment build bias tags and protection tags must not overlap.", assetPath);
        }

        if (augment.OfferBucket == AugmentOfferBucketValue.SynergyLinked && augment.BuildBiasTags.Count == 0)
        {
            AddError(issues, "augment.offer_metadata", "Synergy-linked augments must define at least one build bias tag.", assetPath);
        }

        if (augment.MutualExclusionTags.Select(tag => tag == null ? string.Empty : tag.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count()
            != augment.MutualExclusionTags.Count(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)))
        {
            AddError(issues, "augment.mutual_exclusion", "Augment has duplicate mutual exclusion tags.", assetPath);
        }

        LoopAContractValidator.ValidateAugment(augment, assetPath, issues);
    }

    private static void ValidateItem(ItemBaseDefinition item, string assetPath, ICollection<ContentValidationIssue> issues)
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
            AddError(issues, "item.unique_payload", "Unique item must define granted skill or rule payload.", assetPath);
        }
    }

    private static void ValidateAffix(AffixDefinition affix, string assetPath, ICollection<ContentValidationIssue> issues)
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
            AddError(issues, "affix.value_band", "Affix ValueMax must be greater than or equal to ValueMin.", assetPath);
        }

        if (affix.ItemLevelMin < 0 || affix.SpawnWeight < 0f || affix.BudgetScore < 0f)
        {
            AddError(issues, "affix.schema_budget", "Affix ItemLevelMin, SpawnWeight, and BudgetScore must be non-negative.", assetPath);
        }

        if (affix.RequiredTags.Select(tag => tag?.Id ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Intersect(affix.ExcludedTags.Select(tag => tag?.Id ?? string.Empty), StringComparer.Ordinal)
            .Any())
        {
            AddError(issues, "affix.tag_overlap", "Affix required tags and excluded tags must not overlap.", assetPath);
        }

        if (!string.IsNullOrWhiteSpace(affix.ExclusiveGroupId))
        {
            ValidateCanonicalId(affix.ExclusiveGroupId, assetPath, "AffixDefinition.ExclusiveGroupId", issues);
        }

        ValidateSchemaIdOrKey(affix.TextTemplateKey, "affix.text_template", assetPath, issues);

        if (affix.EffectType == AffixEffectTypeValue.StatModifier && affix.Modifiers.Count == 0)
        {
            AddError(issues, "affix.effect_payload", "StatModifier affixes must define at least one stat modifier.", assetPath);
        }

        if (affix.EffectType == AffixEffectTypeValue.RuleModifier && affix.RuleModifierTags.Count == 0)
        {
            AddError(issues, "affix.effect_payload", "RuleModifier affixes must define at least one rule modifier tag.", assetPath);
        }

        if (affix.EffectType == AffixEffectTypeValue.ConditionalTagged && affix.RequiredTags.Count == 0)
        {
            AddError(issues, "affix.effect_payload", "ConditionalTagged affixes must define at least one required tag.", assetPath);
        }

        LoopAContractValidator.ValidateAffix(affix, assetPath, issues);
    }

    private static void ValidateStatusFamily(StatusFamilyDefinition statusFamily, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(statusFamily.Group, "Status group", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultStackPolicy, "Status default stack policy", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultRefreshPolicy, "Status default refresh policy", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultProcAttributionPolicy, "Status default proc attribution policy", assetPath, issues);
        ValidateDefinedEnum(statusFamily.DefaultOwnershipPolicy, "Status default ownership policy", assetPath, issues);
        if (!statusFamily.IsRuleModifierOnly && statusFamily.DefaultStackCap < 1)
        {
            AddError(issues, "status.family_defaults", "Non-rule-only status families must define DefaultStackCap >= 1.", assetPath);
        }

        if (statusFamily.VisualPriority < 0)
        {
            AddError(issues, "status.visual_priority", "Status VisualPriority must be non-negative.", assetPath);
        }

        LoopAContractValidator.ValidateStatusFamily(statusFamily, assetPath, issues);
    }

    private static void ValidateStatusApplicationRule(StatusApplicationRule statusRule, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(statusRule.StatusId))
        {
            AddError(issues, "status.rule_status_id", "Status application rule is missing StatusId.", assetPath);
        }

        if (statusRule.DurationSeconds < 0f || statusRule.MaxStacks < 1 || statusRule.StackCap < 0)
        {
            AddError(issues, "status.rule_range", "Status duration must be non-negative, MaxStacks must be >= 1, and StackCap must be >= 0.", assetPath);
        }

        ValidateDefinedEnum(statusRule.StackPolicy, "Status stack policy", assetPath, issues);
        ValidateDefinedEnum(statusRule.RefreshPolicy, "Status refresh policy", assetPath, issues);
        ValidateDefinedEnum(statusRule.ProcAttributionPolicy, "Status proc attribution policy", assetPath, issues);
        ValidateDefinedEnum(statusRule.OwnershipPolicy, "Status ownership policy", assetPath, issues);

        if (statusRule.StackCap > 0 && statusRule.MaxStacks > statusRule.StackCap)
        {
            AddError(issues, "status.rule_stack_cap", "Status rule MaxStacks must not exceed StackCap when StackCap is explicitly set.", assetPath);
        }
    }

    private static void ValidateRoleInstruction(RoleInstructionDefinition roleInstruction, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(roleInstruction.LegacyDisplayName))
        {
            if (ContentLocalizationPolicy.TreatsLegacyTextAsError)
            {
                AddError(issues, "localization.legacy_text", "Legacy localized prose remains in RoleInstructionDefinition.LegacyDisplayName.", assetPath, "RoleInstructionDefinition.LegacyDisplayName");
            }
            else
            {
                AddWarning(issues, "localization.legacy_text", "Legacy localized prose remains in RoleInstructionDefinition.LegacyDisplayName.", assetPath, "RoleInstructionDefinition.LegacyDisplayName");
            }
        }

        if (string.IsNullOrWhiteSpace(roleInstruction.RoleTag))
        {
            AddError(issues, "role_instruction.role_tag", "Role instruction is missing RoleTag.", assetPath);
            return;
        }

        if (!AllowedRoleInstructionTags.Contains(roleInstruction.RoleTag))
        {
            AddError(issues, "role_instruction.role_tag", $"Role instruction tag must be one of [{string.Join(", ", AllowedRoleInstructionTags)}].", assetPath);
        }
    }

    private static void ValidatePassiveBoard(PassiveBoardDefinition board, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(board.ClassId))
        {
            AddError(issues, "passive_board.class_id", "Passive board is missing ClassId.", assetPath);
        }
        else if (!CanonicalClassIds.Contains(board.ClassId))
        {
            AddError(issues, "passive_board.class_id", $"Passive board class id must be one of [{string.Join(", ", CanonicalClassIds)}].", assetPath);
        }

        if (board.Nodes.Any(node => node == null))
        {
            AddError(issues, "passive_board.node_ref", "Passive board has a missing node reference.", assetPath);
        }
    }

    private static void ValidatePassiveNode(PassiveNodeDefinition passiveNode, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateDefinedEnum(passiveNode.NodeKind, "Passive node kind", assetPath, issues);
        ValidateModifiers(issues, passiveNode.Modifiers, assetPath, "PassiveNodeDefinition.Modifiers");
        ValidateStableTags(issues, passiveNode.CompileTags, assetPath, "Passive node compile");
        ValidateStableTags(issues, passiveNode.RuleModifierTags, assetPath, "Passive node rule modifier");
        ValidateStableTags(issues, passiveNode.MutualExclusionTags, assetPath, "Passive node mutual exclusion");
        if (string.IsNullOrWhiteSpace(passiveNode.BoardId))
        {
            AddError(issues, "passive_node.board_id", "Passive node is missing BoardId.", assetPath);
        }
        if (passiveNode.BoardDepth < 0)
        {
            AddError(issues, "passive_node.board_depth", "Passive node BoardDepth cannot be negative.", assetPath);
        }
        if (passiveNode.PrerequisiteNodeIds.Any(string.IsNullOrWhiteSpace))
        {
            AddError(issues, "passive_node.prerequisite", "Passive node has an empty prerequisite id.", assetPath);
        }
    }

    private static void ValidateSynergy(SynergyDefinition synergy, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(synergy.CountedTagId))
        {
            AddError(issues, "synergy.counted_tag", "Synergy is missing CountedTagId.", assetPath);
        }

        if (synergy.Tiers.Any(tier => tier == null))
        {
            AddError(issues, "synergy.tier_ref", "Synergy has a missing tier reference.", assetPath);
            return;
        }

        var thresholds = synergy.Tiers
            .Where(tier => tier != null)
            .Select(tier => tier.Threshold)
            .OrderBy(value => value)
            .ToList();
        if (!RequiredSynergyThresholds.SetEquals(thresholds))
        {
            AddError(issues, "synergy.thresholds", "Synergy must define exact 2/4 breakpoint tiers.", assetPath);
        }

        LoopAContractValidator.ValidateSynergy(synergy, assetPath, issues);
    }

    private static void ValidateSynergyTier(SynergyTierDefinition tier, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        ValidateModifiers(issues, tier.Modifiers, assetPath, "SynergyTierDefinition.Modifiers");
        if (!RequiredSynergyThresholds.Contains(tier.Threshold))
        {
            AddError(issues, "synergy_tier.threshold", "Synergy tier threshold must be one of 2 or 4.", assetPath);
        }
    }

    private static void ValidatePassiveBoardShape(PassiveBoardShapeReport board, ICollection<ContentValidationIssue> issues)
    {
        if (board.SmallCount != 12 || board.NotableCount != 5 || board.KeystoneCount != 1)
        {
            AddError(
                issues,
                "passive_board.shape",
                $"Passive board '{board.BoardId}' must match the paid launch floor shape 12/5/1. Found {board.SmallCount}/{board.NotableCount}/{board.KeystoneCount}.",
                board.BoardId,
                board.ClassId);
        }
    }

    private static void ValidateDefinedEnum<TEnum>(TEnum value, string label, string assetPath, ICollection<ContentValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            AddError(issues, "enum.undefined", $"{label} is not a defined enum value.", assetPath);
        }
    }

    private static void ValidateStableTags(
        ICollection<ContentValidationIssue> issues,
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
                AddError(issues, "tag.missing_id", $"{scope} tag is missing id.", assetPath);
                continue;
            }

            ids.Add(tag.Id);
        }

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            AddError(issues, "tag.duplicate", $"{scope} tags contain duplicates.", assetPath);
        }
    }

    private static void ValidateSchemaIdOrKey(string value, string code, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!CanonicalIdPattern.IsMatch(value))
        {
            AddError(issues, code, $"'{value}' must use the canonical lower-case id/key pattern.", assetPath);
        }
    }

    private static void ValidateLaunchFloorCatalogs(IReadOnlyList<ScriptableObject> assets, ICollection<ContentValidationIssue> issues)
    {
        var chapters = assets.OfType<CampaignChapterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var sites = assets.OfType<ExpeditionSiteDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var encounters = assets.OfType<EncounterDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var squads = assets.OfType<EnemySquadTemplateDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var overlays = assets.OfType<BossOverlayDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var statuses = assets.OfType<StatusFamilyDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var cleanseProfiles = assets.OfType<CleanseProfileDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var controlRules = assets.OfType<ControlDiminishingRuleDefinition>().ToList();
        var rewardSources = assets.OfType<RewardSourceDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var dropTables = assets.OfType<DropTableDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var lootBundles = assets.OfType<LootBundleDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var traitTokens = assets.OfType<TraitTokenDefinition>().ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var skills = assets.OfType<SkillDefinitionAsset>().ToList();
        var items = assets.OfType<ItemBaseDefinition>().ToList();
        var synergies = assets.OfType<SynergyDefinition>().ToList();
        var archetypeIds = assets.OfType<UnitArchetypeDefinition>().Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);

        ValidateCampaignCatalog(chapters, sites, encounters, squads, overlays, rewardSources, archetypeIds, issues);
        ValidateStatusCatalog(statuses, cleanseProfiles, controlRules, skills, issues);
        ValidateRewardCatalog(rewardSources, dropTables, lootBundles, traitTokens, issues);
        ValidateSkillCatalog(skills, statuses.Keys.ToHashSet(StringComparer.Ordinal), cleanseProfiles.Keys.ToHashSet(StringComparer.Ordinal), issues);
        ValidateItemCatalog(items, issues);
        ValidateFactionIsolation(sites.Values, encounters.Values, squads.Values, synergies, issues);
    }

    private static void ValidateCampaignCatalog(
        IReadOnlyDictionary<string, CampaignChapterDefinition> chapters,
        IReadOnlyDictionary<string, ExpeditionSiteDefinition> sites,
        IReadOnlyDictionary<string, EncounterDefinition> encounters,
        IReadOnlyDictionary<string, EnemySquadTemplateDefinition> squads,
        IReadOnlyDictionary<string, BossOverlayDefinition> overlays,
        IReadOnlyDictionary<string, RewardSourceDefinition> rewardSources,
        IReadOnlyCollection<string> archetypeIds,
        ICollection<ContentValidationIssue> issues)
    {
        if (chapters.Count != 3)
        {
            AddError(issues, "campaign.chapter_count", $"Story chapters must be locked to 3. Found {chapters.Count}.", ReportFolderName);
        }

        if (sites.Count != 6)
        {
            AddError(issues, "campaign.site_count", $"Expedition sites must be locked to 6. Found {sites.Count}.", ReportFolderName);
        }

        if (encounters.Count != 24)
        {
            AddError(issues, "campaign.encounter_count", $"Encounter catalog must be locked to 24 battle encounters. Found {encounters.Count}.", ReportFolderName);
        }

        foreach (var chapter in chapters.Values)
        {
            var assetPath = ResolveAssetPath(chapter);
            if (chapter.SiteIds.Distinct(StringComparer.Ordinal).Count() != 2)
            {
                AddError(issues, "campaign.chapter_site_count", "Campaign chapter must own exactly 2 expedition sites.", assetPath);
            }

            foreach (var siteId in chapter.SiteIds)
            {
                if (!sites.ContainsKey(siteId))
                {
                    AddError(issues, "campaign.chapter_site_ref", $"Campaign chapter references missing site '{siteId}'.", assetPath);
                }
            }
        }

        foreach (var site in sites.Values)
        {
            var assetPath = ResolveAssetPath(site);
            if (!chapters.ContainsKey(site.ChapterId))
            {
                AddError(issues, "campaign.site_chapter_ref", $"Expedition site references missing chapter '{site.ChapterId}'.", assetPath);
            }

            if (site.EncounterIds.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                AddError(issues, "campaign.site_encounter_count", "Expedition site must own exactly 4 battle encounters (2 skirmish / 1 elite / 1 boss).", assetPath);
            }

            if (string.IsNullOrWhiteSpace(site.ExtractRewardSourceId) || !rewardSources.ContainsKey(site.ExtractRewardSourceId))
            {
                AddError(issues, "campaign.site_extract_source", "Expedition site is missing a valid extract reward source.", assetPath);
            }

            foreach (var encounterId in site.EncounterIds)
            {
                if (!encounters.ContainsKey(encounterId))
                {
                    AddError(issues, "campaign.site_encounter_ref", $"Expedition site references missing encounter '{encounterId}'.", assetPath);
                }
            }
        }

        foreach (var encounter in encounters.Values)
        {
            var assetPath = ResolveAssetPath(encounter);
            if (!sites.ContainsKey(encounter.SiteId))
            {
                AddError(issues, "encounter.site_ref", $"Encounter references missing site '{encounter.SiteId}'.", assetPath);
            }

            if (!squads.ContainsKey(encounter.EnemySquadTemplateId))
            {
                AddError(issues, "encounter.squad_ref", $"Encounter references missing enemy squad '{encounter.EnemySquadTemplateId}'.", assetPath);
            }

            if (string.IsNullOrWhiteSpace(encounter.RewardSourceId) || !rewardSources.ContainsKey(encounter.RewardSourceId))
            {
                AddError(issues, "encounter.reward_source_ref", "Encounter is missing a valid reward source.", assetPath);
            }

            if (string.IsNullOrWhiteSpace(encounter.FactionId))
            {
                AddError(issues, "encounter.faction_id", "Encounter must keep a faction/allegiance id.", assetPath);
            }

            if (encounter.ThreatCost is < 1 or > 3 || encounter.ThreatSkulls is < 1 or > 3)
            {
                AddError(issues, "encounter.threat_budget", "Encounter threat cost and skulls must stay within the 1/2/3 launch-floor grammar.", assetPath);
            }

            if (string.IsNullOrWhiteSpace(encounter.DifficultyBand))
            {
                AddError(issues, "encounter.difficulty_band", "Encounter must define a difficulty band.", assetPath);
            }

            if (encounter.RewardDropTags.Count == 0)
            {
                AddError(issues, "encounter.reward_drop_tags", "Encounter must define reward/drop tags.", assetPath);
            }

            if (encounter.Kind == EncounterKindValue.Boss)
            {
                if (string.IsNullOrWhiteSpace(encounter.BossOverlayId) || !overlays.ContainsKey(encounter.BossOverlayId))
                {
                    AddError(issues, "encounter.boss_overlay_ref", "Boss encounter must reference a valid boss overlay.", assetPath);
                }
            }
            else if (!string.IsNullOrWhiteSpace(encounter.BossOverlayId))
            {
                AddError(issues, "encounter.non_boss_overlay", "Only boss encounters may reference a boss overlay.", assetPath);
            }
        }

        foreach (var squad in squads.Values)
        {
            var assetPath = ResolveAssetPath(squad);
            if (string.IsNullOrWhiteSpace(squad.FactionId))
            {
                AddError(issues, "enemy_squad.faction_id", "Enemy squad must keep a faction/allegiance id.", assetPath);
            }

            if (squad.Members.Count == 0)
            {
                AddError(issues, "enemy_squad.member_count", "Enemy squad must define at least one member.", assetPath);
                continue;
            }

            var captainCount = squad.Members.Count(member => member.Role == EnemySquadMemberRoleValue.Captain);
            var escortCount = squad.Members.Count(member => member.Role == EnemySquadMemberRoleValue.Escort);
            if (captainCount > 0 && (captainCount != 1 || escortCount < 2))
            {
                AddError(issues, "enemy_squad.boss_structure", "Boss squads must follow BossCaptain + 2~3 Escorts.", assetPath);
            }

            foreach (var member in squad.Members)
            {
                if (!archetypeIds.Contains(member.ArchetypeId))
                {
                    AddError(issues, "enemy_squad.member_archetype_ref", $"Enemy squad member references missing archetype '{member.ArchetypeId}'.", assetPath);
                }
            }
        }
    }

    private static void ValidateStatusCatalog(
        IReadOnlyDictionary<string, StatusFamilyDefinition> statuses,
        IReadOnlyDictionary<string, CleanseProfileDefinition> cleanseProfiles,
        IReadOnlyList<ControlDiminishingRuleDefinition> controlRules,
        IReadOnlyList<SkillDefinitionAsset> skills,
        ICollection<ContentValidationIssue> issues)
    {
        var statusIds = statuses.Keys.ToHashSet(StringComparer.Ordinal);
        if (!RequiredLaunchStatusIds.SetEquals(statusIds))
        {
            AddError(issues, "status.catalog_floor", $"Launch-floor status catalog must match [{string.Join(", ", RequiredLaunchStatusIds.OrderBy(id => id, StringComparer.Ordinal))}].", ReportFolderName);
        }

        if (!RequiredCleanseProfileIds.SetEquals(cleanseProfiles.Keys))
        {
            AddError(issues, "status.cleanse_floor", $"Cleanse profile catalog must match [{string.Join(", ", RequiredCleanseProfileIds.OrderBy(id => id, StringComparer.Ordinal))}].", ReportFolderName);
        }

        if (controlRules.Count != 1)
        {
            AddError(issues, "status.dr_rule_count", $"Launch-floor control diminishing rules must be locked to 1. Found {controlRules.Count}.", ReportFolderName);
        }
        else
        {
            var rule = controlRules[0];
            var assetPath = ResolveAssetPath(rule);
            if (Math.Abs(rule.WindowSeconds - 1.5f) > 0.001f || Math.Abs(rule.ControlResistMultiplier - 0.5f) > 0.001f)
            {
                AddError(issues, "status.dr_values", "Launch-floor DR must stay at 1.5s window and 50% control resist.", assetPath);
            }
        }

        foreach (var profile in cleanseProfiles.Values)
        {
            var assetPath = ResolveAssetPath(profile);
            foreach (var statusId in profile.RemovesStatusIds)
            {
                if (!statusIds.Contains(statusId))
                {
                    AddError(issues, "status.cleanse_target_ref", $"Cleanse profile references missing status '{statusId}'.", assetPath);
                }
            }
        }

        foreach (var skill in skills)
        {
            var assetPath = ResolveAssetPath(skill);
            foreach (var status in skill.AppliedStatuses.Where(status => status != null))
            {
                if (!statusIds.Contains(status.StatusId))
                {
                    AddError(issues, "status.skill_status_ref", $"Skill '{skill.Id}' references missing status '{status.StatusId}'.", assetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId) && !cleanseProfiles.ContainsKey(skill.CleanseProfileId))
            {
                AddError(issues, "status.skill_cleanse_ref", $"Skill '{skill.Id}' references missing cleanse profile '{skill.CleanseProfileId}'.", assetPath);
            }
        }
    }

    private static void ValidateRewardCatalog(
        IReadOnlyDictionary<string, RewardSourceDefinition> rewardSources,
        IReadOnlyDictionary<string, DropTableDefinition> dropTables,
        IReadOnlyDictionary<string, LootBundleDefinition> lootBundles,
        IReadOnlyDictionary<string, TraitTokenDefinition> traitTokens,
        ICollection<ContentValidationIssue> issues)
    {
        if (rewardSources.Count != 6)
        {
            AddError(issues, "reward.source_count", $"Launch-floor reward sources must be locked to 6. Found {rewardSources.Count}.", ReportFolderName);
        }

        if (dropTables.Count != 6)
        {
            AddError(issues, "reward.drop_table_count", $"Launch-floor drop tables must be locked to 6. Found {dropTables.Count}.", ReportFolderName);
        }

        if (!RequiredTraitTokenIds.SetEquals(traitTokens.Keys))
        {
            AddError(issues, "reward.trait_token_floor", $"Trait token catalog must match [{string.Join(", ", RequiredTraitTokenIds.OrderBy(id => id, StringComparer.Ordinal))}].", ReportFolderName);
        }

        var mappedSourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rewardSource in rewardSources.Values)
        {
            var assetPath = ResolveAssetPath(rewardSource);
            if (string.IsNullOrWhiteSpace(rewardSource.DropTableId) || !dropTables.ContainsKey(rewardSource.DropTableId))
            {
                AddError(issues, "reward.source_drop_table_ref", $"Reward source '{rewardSource.Id}' references missing drop table '{rewardSource.DropTableId}'.", assetPath);
                continue;
            }

            if (!mappedSourceIds.Add(rewardSource.DropTableId))
            {
                AddError(issues, "reward.duplicate_source_tag_mapping", $"Drop table '{rewardSource.DropTableId}' is mapped by more than one reward source.", assetPath);
            }

            var dropTable = dropTables[rewardSource.DropTableId];
            var unreachableBands = rewardSource.AllowedRarityBrackets
                .Where(bracket => dropTable.Entries.All(entry => entry.RarityBracket != bracket))
                .ToList();
            foreach (var bracket in unreachableBands)
            {
                AddWarning(issues, "reward.unreachable_rarity_band", $"Reward source '{rewardSource.Id}' exposes rarity bracket '{bracket}' but the drop table never rolls it.", assetPath);
            }

            foreach (var entry in dropTable.Entries)
            {
                if (!rewardSource.AllowedRarityBrackets.Contains(entry.RarityBracket))
                {
                    AddError(issues, "reward.rarity_band_out_of_source", $"Drop table entry '{entry.Id}' uses rarity '{entry.RarityBracket}' outside reward source '{rewardSource.Id}'.", assetPath);
                }
            }
        }

        foreach (var lootBundle in lootBundles.Values)
        {
            var assetPath = ResolveAssetPath(lootBundle);
            if (string.IsNullOrWhiteSpace(lootBundle.RewardSourceId) || !rewardSources.ContainsKey(lootBundle.RewardSourceId))
            {
                AddError(issues, "reward.loot_bundle_source_ref", $"Loot bundle '{lootBundle.Id}' references missing reward source '{lootBundle.RewardSourceId}'.", assetPath);
            }
        }
    }

    private static void ValidateSkillCatalog(
        IReadOnlyList<SkillDefinitionAsset> skills,
        IReadOnlyCollection<string> statusIds,
        IReadOnlyCollection<string> cleanseProfileIds,
        ICollection<ContentValidationIssue> issues)
    {
        var supportModifierSkills = skills.Where(skill => skill.Id.StartsWith("support_", StringComparison.Ordinal)).ToList();
        if (supportModifierSkills.Count != 12)
        {
            AddError(issues, "skill.support_modifier_floor", $"Support modifier floor must stay at 12. Found {supportModifierSkills.Count}.", ReportFolderName);
        }

        foreach (var skill in skills)
        {
            var assetPath = ResolveAssetPath(skill);
            var compileTagIds = skill.CompileTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var allowedTagIds = skill.SupportAllowedTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var blockedTagIds = skill.SupportBlockedTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var requiredWeaponIds = skill.RequiredWeaponTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);
            var requiredClassIds = skill.RequiredClassTags.Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)).Select(tag => tag.Id).ToHashSet(StringComparer.Ordinal);

            if (allowedTagIds.Overlaps(blockedTagIds))
            {
                AddError(issues, "skill.support_conflict", $"Skill '{skill.Id}' contains support tags in both include and exclude lists.", assetPath);
            }

            foreach (var weaponId in requiredWeaponIds)
            {
                if (!AllowedWeaponFamilyIds.Contains(weaponId))
                {
                    AddError(issues, "skill.weapon_family_ref", $"Skill '{skill.Id}' references unsupported weapon family '{weaponId}'.", assetPath);
                }
            }

            foreach (var classId in requiredClassIds)
            {
                if (!CanonicalClassIds.Contains(classId))
                {
                    AddError(issues, "skill.class_tag_ref", $"Skill '{skill.Id}' references unsupported class tag '{classId}'.", assetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId))
            {
                if (!cleanseProfileIds.Contains(skill.CleanseProfileId))
                {
                    AddError(issues, "skill.cleanse_profile_ref", $"Skill '{skill.Id}' references missing cleanse profile '{skill.CleanseProfileId}'.", assetPath);
                }

                if (!compileTagIds.Contains("cleanse"))
                {
                    AddError(issues, "skill.cleanse_tag", $"Skill '{skill.Id}' uses cleanse without the canonical 'cleanse' compile tag.", assetPath);
                }
            }

            var duplicateStatuses = skill.AppliedStatuses
                .Where(status => status != null)
                .GroupBy(status => status.StatusId, StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            foreach (var statusId in duplicateStatuses)
            {
                AddError(issues, "skill.duplicate_status_application", $"Skill '{skill.Id}' applies status '{statusId}' more than once.", assetPath);
            }

            foreach (var status in skill.AppliedStatuses.Where(status => status != null))
            {
                if (!statusIds.Contains(status.StatusId))
                {
                    AddError(issues, "skill.status_ref", $"Skill '{skill.Id}' references missing status '{status.StatusId}'.", assetPath);
                }
            }

            if (skill.Id.StartsWith("support_", StringComparison.Ordinal))
            {
                if (skill.SlotKind != SkillSlotKindValue.Support)
                {
                    AddError(issues, "skill.support_slot", $"Support modifier '{skill.Id}' must use the canonical support slot.", assetPath);
                }

                if (allowedTagIds.Count == 0)
                {
                    AddError(issues, "skill.support_allowed_tags", $"Support modifier '{skill.Id}' must define at least one include tag.", assetPath);
                }
            }
        }
    }

    private static void ValidateItemCatalog(IReadOnlyList<ItemBaseDefinition> items, ICollection<ContentValidationIssue> issues)
    {
        foreach (var item in items)
        {
            var assetPath = ResolveAssetPath(item);
            var weaponFamilyId = NormalizeWeaponFamilyId(item);
            if (item.SlotType == ItemSlotType.Weapon && !AllowedWeaponFamilyIds.Contains(weaponFamilyId))
            {
                AddError(issues, "item.weapon_family_ref", $"Weapon item '{item.Id}' must use one of [{string.Join(", ", AllowedWeaponFamilyIds)}].", assetPath);
            }

            var craftCurrencyId = NormalizeCraftCurrencyId(item);
            if (!AllowedCraftCurrencyIds.Contains(craftCurrencyId))
            {
                AddError(issues, "item.craft_currency_ref", $"Item '{item.Id}' references unsupported craft currency '{craftCurrencyId}'.", assetPath);
            }

            if (item.IdentityKind == ItemIdentityValue.Unique && !string.Equals(craftCurrencyId, "boss_sigil", StringComparison.Ordinal))
            {
                AddError(issues, "item.unique_craft_currency", "Unique/boss items must use 'boss_sigil' as their imprint currency.", assetPath);
            }

            var operations = NormalizeCraftOperations(item);
            if (operations.Count > 5)
            {
                AddError(issues, "item.affix_slot_overfill", $"Item '{item.Id}' exposes more than 5 launch-floor craft operations.", assetPath);
            }

            if (!operations.Contains(CraftOperationKindValue.Salvage))
            {
                AddError(issues, "item.salvage_missing", $"Item '{item.Id}' must support salvage in the launch-floor crafting contract.", assetPath);
            }
        }
    }

    private static void ValidateFactionIsolation(
        IEnumerable<ExpeditionSiteDefinition> sites,
        IEnumerable<EncounterDefinition> encounters,
        IEnumerable<EnemySquadTemplateDefinition> squads,
        IEnumerable<SynergyDefinition> synergies,
        ICollection<ContentValidationIssue> issues)
    {
        var factionIds = sites.Select(site => site.FactionId)
            .Concat(encounters.Select(encounter => encounter.FactionId))
            .Concat(squads.Select(squad => squad.FactionId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var synergy in synergies)
        {
            var assetPath = ResolveAssetPath(synergy);
            if (factionIds.Contains(synergy.CountedTagId))
            {
                AddError(issues, "faction.synergy_leak", $"Faction id '{synergy.CountedTagId}' must not leak into synergy counted tags.", assetPath);
            }
        }
    }

    private static string NormalizeWeaponFamilyId(ItemBaseDefinition item)
    {
        if (item.SlotType != ItemSlotType.Weapon)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.WeaponFamilyTag))
        {
            return item.WeaponFamilyTag;
        }

        if (!string.IsNullOrWhiteSpace(item.ItemFamilyTag))
        {
            return item.ItemFamilyTag;
        }

        if (item.Id.Contains("shield", StringComparison.Ordinal))
        {
            return "shield";
        }

        if (item.Id.Contains("bow", StringComparison.Ordinal))
        {
            return "bow";
        }

        if (item.Id.Contains("focus", StringComparison.Ordinal) || item.Id.Contains("bead", StringComparison.Ordinal))
        {
            return "focus";
        }

        return "blade";
    }

    private static string NormalizeCraftCurrencyId(ItemBaseDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(item.CraftCurrencyTag))
        {
            return item.CraftCurrencyTag;
        }

        return item.IdentityKind == ItemIdentityValue.Unique ? "boss_sigil" : "ember_dust";
    }

    private static IReadOnlyList<CraftOperationKindValue> NormalizeCraftOperations(ItemBaseDefinition item)
    {
        if (item.AllowedCraftOperations.Count > 0)
        {
            return item.AllowedCraftOperations;
        }

        var operations = new List<CraftOperationKindValue>
        {
            CraftOperationKindValue.Temper,
            CraftOperationKindValue.Reforge,
            CraftOperationKindValue.Seal,
            CraftOperationKindValue.Salvage,
        };

        if (item.IdentityKind == ItemIdentityValue.Unique)
        {
            operations.Insert(3, CraftOperationKindValue.Imprint);
        }

        return operations;
    }

    private static void AddError(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath, string scope = "")
    {
        AddIssue(issues, ContentValidationSeverity.Error, code, message, assetPath, scope);
    }

    private static void AddWarning(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath, string scope = "")
    {
        AddIssue(issues, ContentValidationSeverity.Warning, code, message, assetPath, scope);
    }

    private static void AddIssue(
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
